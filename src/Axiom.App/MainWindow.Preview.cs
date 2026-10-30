using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using Axiom.Core;
using Axiom.Infrastructure;

namespace Axiom.App;

public sealed partial class MainWindow
{
    private void RenderPreview()
    {
        if (!_ready || Current is not { } tab)
        {
            return;
        }

        try
        {
            tab.Token     = Guid.NewGuid().ToString("N");
            tab.Structure = new MarkdownStructureService().Parse(tab.Session.Text);
            RefreshOutline();
            tab.Report               = _renderer.RenderWithReport(tab.Session.Text
                                                                , tab.Session.Path
                                                                , _appearance
                                                                , _folder
                                                                , tab.Token
                                                                , FeatureAccess.Evaluate(OptionalFeature.RemoteContent).Allowed ? tab.RemoteImages : null);
            _notice.Text             = tab.Report.Changes.Count > 0 ? $"Preview sanitized: {tab.Report.Changes.Count} type(s) of content removed or changed. Markdown source is unchanged." : "";
            _noticeDetails.IsVisible = tab.Report.Changes.Count > 0;
            var blocked = tab.Report.RemoteImages.Count(url => !tab.RemoteImages.ContainsKey(url) || !FeatureAccess.Evaluate(OptionalFeature.RemoteContent).Allowed);
            if (blocked > 0)
            {
                _notice.Text += $" {blocked} external image(s) blocked.";
            }

            _loadRemote.IsVisible   = blocked > 0 && FeatureAccess.Evaluate(OptionalFeature.RemoteContent).Allowed;
            _revokeRemote.IsVisible = tab.RemoteImages.Count > 0;
            _noticeBar.IsVisible    = !string.IsNullOrWhiteSpace(_notice.Text) || _loadRemote.IsVisible || _revokeRemote.IsVisible;
            _html.Load(_preview, tab.Report.Html);
        }
        catch (Exception error)
        {
            _notice.Text         = "Preview could not be rendered: " + error.Message + ". Source remains available.";
            _noticeBar.IsVisible = true;
        }
    }

    private void HardenPreview()
    {
        PreviewBrowserPolicy.Attach(_preview, _html);
    }

    private async Task ObserveDiagramChangesAsync(EditorTab tab
                                                , string    token)
    {
        for (var attempt = 0; attempt < 45 && ReferenceEquals(tab, Current) && tab.Token == token && !_lifetime.IsCancellationRequested; attempt++)
        {
            await Task.Delay(250);
            var value = ReviewProbe.DecodeScriptResult(await _preview.InvokeScript("JSON.stringify(window.axiomMermaidStatus)"));
            if (string.IsNullOrWhiteSpace(value) || value == "null" || value == "undefined")
            {
                continue;
            }

            using var result = System.Text.Json.JsonDocument.Parse(value);
            if (result.RootElement.GetProperty("token").GetString() != token)
            {
                continue;
            }

            var removed = result.RootElement.GetProperty("sanitized").GetInt32();
            if (removed > 0 && tab.Report is { } report)
            {
                var message = $"Mermaid sanitization removed {removed} unsafe diagram attribute/style item(s).";
                tab.Report               = report with

                {
                    Changes = [..report.Changes.Where(change => !change.StartsWith("Mermaid sanitization")), message]
                };
                _notice.Text             = "Preview sanitized. Markdown source is unchanged. " + message;
                _noticeDetails.IsVisible = true;
                _noticeBar.IsVisible     = true;
            }

            if (result.RootElement.GetProperty("completed").GetInt32() >= result.RootElement.GetProperty("total").GetInt32())
            {
                return;
            }
        }
    }

    private async Task TrustRemoteAsync()
    {
        FeatureAccess.Require(OptionalFeature.RemoteContent);
        if (Current is not { Report: { } report } tab)
        {
            return;
        }

        var targets = report.RemoteImages.Where(url => !tab.RemoteImages.ContainsKey(url)).ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        if (targets.Length > 16)
        {
            throw new IOException("This document references more than 16 external images. Reduce them before loading.");
        }

        var answer = await ChoiceDialog.ShowAsync(this
                                                , "Load external content?"
                                                , "This document references external content. Loading it may contact other servers, disclose information about your connection, or retrieve harmful content. Only load external content if you trust the document and its source.\n\n" + string.Join("\n"
                                                                                                                                                                                                                                                                                                   , targets) + "\n\nApproval covers only these images in this document session. Redirects and active content remain blocked."
                                                , "Load for this session"
                                                , "Keep blocked");
        if (answer != "Load for this session")
        {
            return;
        }

        FeatureAccess.Require(OptionalFeature.RemoteContent);
        var source = tab.Session.Text;
        var loader = new RemoteImageLoader();
        foreach (var target in targets)
        {
            var data = await loader.LoadAsync(target, _lifetime.Token);
            if (!ReferenceEquals(tab, Current) || tab.Session.Text != source)
            {
                throw new IOException("The document changed; review its external content again.");
            }

            tab.RemoteImages[target] = data;
        }

        RenderPreview();
    }

    private void WirePreviewEvents()
    {
        _preview.EnvironmentRequested += (_, eventArgs2) =>
        {
            if (eventArgs2 is WindowsWebView2EnvironmentRequestedEventArgs windows)
            {
                windows.UserDataFolder = Path.Combine(_paths.Root, "PreviewCache");
            }
        };
        _preview.AdapterCreated += (_, _) =>
        {
            HardenPreview();
            _ready = true;
            RenderPreview();
        };
        _preview.NavigationStarted += (_, eventArgs3) =>
        {
            if (eventArgs3.Request is not { } uri)
            {
                return;
            }

            if (uri.Scheme == "axiom-destination" || uri.Scheme == "axiom-link" || uri.Scheme == "axiom-source" || uri.Scheme == "axiom-task" || uri.Scheme is "http" or "https" or "mailto" || uri.Fragment.Length > 0)
            {
                eventArgs3.Cancel = true;
                Dispatcher.UIThread.Post(async () => await RunAsync(() => FollowLinkAsync(uri)));
                return;
            }

            if (uri.Scheme is not "about" and not "data" && !_html.Allows(uri))
            {
                eventArgs3.Cancel = true;
            }
        };
        _preview.NewWindowRequested += (_, eventArgs4) => eventArgs4.Handled = true;
        _preview.NavigationCompleted += async (_, eventArgs5) =>
        {
            if (!eventArgs5.IsSuccess || Current is not { } tab)
            {
                return;
            }

            try
            {
                var token = tab.Token;
                await _preview.InvokeScript(MermaidPreviewRenderer.BuildScript(token
                                                                             , _appearance == AppearancePreference.Dark));
                await ObserveDiagramChangesAsync(tab, token);
            }
            catch (Exception error)
            {
                _status.Text = "Diagram rendering unavailable: " + error.Message;
            }
        };
    }
}

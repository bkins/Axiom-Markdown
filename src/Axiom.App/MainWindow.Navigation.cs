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
    internal void QueueExternalOpen(string? path) => _externalRequests.Enqueue(path);
    private async Task DrainExternalOpensAsync()
    {
        if (!_startupReady || _busy || _closing || _openingExternal)
        {
            return;
        }

        _openingExternal = true;
        try
        {
            while (!_busy && !_closing && _externalRequests.TryDequeue(out var path))
            {
                await RunAsync(async () =>
                {
                    if (path is not null)
                    {
                        await OpenAsync(path);
                    }

                    if (WindowState == WindowState.Minimized)
                    {
                        WindowState = WindowState.Normal;
                    }

                    Activate();
                });
            }
        }
        finally
        {
            _openingExternal = false;
        }
    }

    private async Task FollowLinkAsync(Uri uri)
    {
        if (Current is not { Structure: { } structure } tab)
        {
            return;
        }

        if (uri.Scheme.StartsWith("axiom-", StringComparison.Ordinal))
        {
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts.Length < 2 || parts[0] != tab.Token || structure.SourceText != tab.Session.Text)
            {
                return;
            }

            if (uri.Scheme == "axiom-destination")
            {
                await NavigateDestinationAsync(Uri.UnescapeDataString(parts[1]), tab);
                return;
            }

            if (!int.TryParse(parts[1], out var number))
            {
                return;
            }

            if (uri.Scheme == "axiom-source")
            {
                tab.Editor.CaretOffset = Math.Clamp(number, 0, tab.Editor.Text.Length);
                tab.Editor.ScrollToLine(tab.Editor.Document.GetLineByOffset(tab.Editor.CaretOffset).LineNumber);
                if (_viewMode.SelectedIndex == 2)
                {
                    _viewMode.SelectedIndex = 0;
                }

                tab.Editor.Focus();
                return;
            }

            if (uri.Scheme == "axiom-task" && parts.Length == 3 && PreviewTaskToggle.TryToggle(structure
                                                                                             , tab.Session.Text
                                                                                             , number
                                                                                             , parts[2] == "1"
                                                                                             , out var replacement))
            {
                tab.Editor.Document.Replace(number, 1, replacement.ToString());
                return;
            }

            if (uri.Scheme != "axiom-link" || number < 0 || number >= structure.Links.Count)
            {
                return;
            }

            await NavigateDestinationAsync(structure.Links[number].Destination, tab);
            return;
        }

        if (uri.Fragment.Length > 0 && (uri.Scheme is "about" or "data" || _html.CurrentNavigation is { } current && uri.GetLeftPart(UriPartial.Path) == current.GetLeftPart(UriPartial.Path)))
        {
            await NavigateDestinationAsync(uri.Fragment, tab);
            return;
        }

        await NavigateDestinationAsync(uri.AbsoluteUri, tab);
    }

    private async Task NavigateDestinationAsync(string    destination
                                              , EditorTab tab)
    {
        if (destination.StartsWith('#'))
        {
            await _preview.InvokeScript("document.getElementById(" + System.Text.Json.JsonSerializer.Serialize(Uri.UnescapeDataString(destination[1..])) + ")?.scrollIntoView()");
            return;
        }

        if (destination.StartsWith("axiom-wiki:"))
        {
            destination = Uri.UnescapeDataString(destination[11..]);
        }

        if (Uri.TryCreate(destination, UriKind.Absolute, out var external))
        {
            if (external.Scheme is not "http" and not "https" and not "mailto")
            {
                throw new IOException("Unsupported external link.");
            }

            if (await ChoiceDialog.ShowAsync(this
                                           , "Open external link?"
                                           , "Only open this link if you trust its source. Your browser may contact an external server.\n\n" + external.AbsoluteUri
                                           , "Open link"
                                           , "Cancel") == "Open link")
            {
                Process.Start(new ProcessStartInfo(external.AbsoluteUri) { UseShellExecute = true });
            }

            return;
        }

        if (destination.StartsWith("//") || destination.Contains('\\'))
        {
            throw new IOException("Network-share links are blocked.");
        }

        var baseFolder = tab.Session.Path is { } path ? Path.GetDirectoryName(path) : _folder;
        if (baseFolder is null)
        {
            throw new IOException("Save the document or open a folder before following relative links.");
        }

        var target = Path.GetFullPath(Path.Combine(baseFolder, Uri.UnescapeDataString(destination.Split('#')[0])));
        if (Path.GetExtension(target).Length == 0)
        {
            target += ".md";
        }

        await OpenAsync(target);
    }
}

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
    private async Task InitializeAsync()
    {
        await _state.InitializeAsync();
        _sessionId = await _state.BeginSessionAsync();
        try
        {
            _preferences = new EditorPreferencesStore(_paths.Root).Load();
        }
        catch (Exception error)
        {
            _status.Text = "Default editor preferences used: " + error.Message;
        }

        ApplyPreferences(save: false);
        await Dispatcher.UIThread.InvokeAsync(FitInitialWindowToScreen, DispatcherPriority.Loaded);
        ActualThemeVariantChanged += (_, _) => ApplyPreferences(save: false);
        foreach (var draft in await _state.GetRecoverableDraftsAsync())
        {
            var answer = await ChoiceDialog.ShowAsync(this
                                                    , "Recover unsaved Markdown"
                                                    , $"Recover changes from {draft.FilePath ?? "an unsaved document"}? Your saved file will not be changed."
                                                    , "Recover"
                                                    , "Discard draft"
                                                    , "Keep for later");
            if (answer == "Discard draft")
            {
                await _state.DeleteDraftAsync(draft.DraftKey);
                continue;
            }

            if (answer != "Recover")
            {
                continue;
            }

            var saved = draft.FilePath is { } path && File.Exists(path) ? (await _files.ReadAsync(path)).Text : "";
            AddTab(DocumentSession.Recover(draft, saved));
        }

        if (Program.StartupDocument is { } startup)
        {
            await OpenAsync(startup);
        }

        if (_items.Count == 0)
        {
            AddTab(_documents.NewDocument());
        }

        _protectTimer.Start();
        RefreshFeatureMenus();
        _startupReady = true;
        if (Program.ProbeRoot is { } probe)
        {
            Dispatcher.UIThread.Post(async () => await ReviewProbe.RunAsync(this, probe));
            return;
        }

        if (FeatureAccess.Evaluate(OptionalFeature.Updates).Allowed)
        {
            _ = CheckUpdatesAsync(false);
        }

        _updateTimer.Start();
    }

    private async void OnClosing(object?                sender
                               , WindowClosingEventArgs args)
    {
        if (_approvedClose)
        {
            return;
        }

        args.Cancel = true;
        if (_closing || _busy)
        {
            return;
        }

        _closing = true;
        try
        {
            foreach (var tab in Documents.ToArray())
            {
                if (!await ResolveDirtyAsync(tab))
                {
                    return;
                }
            }

            _protectTimer.Stop();
            while (_protecting)
            {
                await Task.Delay(20);
            }

            foreach (var tab in Documents)
            {
                await _state.DeleteDraftAsync(tab.Session.DraftKey);
            }

            if (_sessionId is { } id)
            {
                await _state.EndSessionAsync(id, true);
            }

            _approvedClose = true;
            Close();
        }
        catch (Exception error)
        {
            _status.Text = "Close cancelled: " + error.Message;
        }
        finally
        {
            _closing = false;
            if (!_approvedClose)
            {
                _protectTimer.Start();
            }
        }
    }

    private async Task ProtectAndCheckPolicyAsync()
    {
        if (_protecting || _closing)
        {
            return;
        }

        _protecting = true;
        try
        {
            RefreshFeatureMenus();
            foreach (var tab in Documents.ToArray())
            {
                await _documents.ProtectAsync(tab.Session);
            }

            _recoveryStatus.Text = "Recovery active";
            if (!FeatureAccess.Evaluate(OptionalFeature.Updates).Allowed)
            {
                _updateCancellation?.Cancel();
            }

            if (!FeatureAccess.Evaluate(OptionalFeature.RemoteContent).Allowed && Documents.Any(tab => tab.RemoteImages.Count > 0))
            {
                foreach (var tab in Documents)
                {
                    tab.RemoteImages.Clear();
                }

                RenderPreview();
            }

            if (!FeatureAccess.Evaluate(OptionalFeature.Terminal).Allowed)
            {
                foreach (var terminal in _terminals.ToArray())
                {
                    await terminal.View.DisposeAsync();
                    terminal.Window.Close();
                }
            }

            if (!FeatureAccess.Evaluate(OptionalFeature.Git).Allowed)
            {
                foreach (var git in _gitWindows.ToArray())
                {
                    git.View.CancelOperation();
                    git.Window.Close();
                }
            }
        }
        catch (Exception error)
        {
            _status.Text         = "Recovery/policy check failed: " + error.Message;
            _recoveryStatus.Text = "Recovery/policy needs attention";
        }
        finally
        {
            _protecting = false;
        }
    }

    private void WireLifecycleEvents()
    {
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            RenderPreview();
        };
        _protectTimer.Tick += async (_, _) =>
        {
            if (!_busy)
            {
                await ProtectAndCheckPolicyAsync();
                await DrainExternalOpensAsync();
            }
        };
        _updateTimer.Tick += async (_, _) =>
        {
            if (!_busy && !_closing)
            {
                await CheckUpdatesAsync(false);
            }
        };
        _monitor.Changed += (_, path) => Dispatcher.UIThread.Post(async () => await RunAsync(() => CheckExternalAsync(path)));
        _monitor.Renamed += (_, pair) => Dispatcher.UIThread.Post(async () => await RunAsync(() => HandleRenameAsync(pair.OldPath
                                                                                                                   , pair.NewPath)));
        Activated += async (_, _) =>
        {
            if (_sessionId is not null)
            {
                foreach (var path in Documents.Select(tab => tab.Session.Path).OfType<string>().ToArray())
                {
                    await RunAsync(() => CheckExternalAsync(path));
                }
            }
        };
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnContextMenuKeyUp, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Deactivated += (_, _) => _keyboardContextTarget = null;
        Closing += OnClosing;
        Closed += async (_, _) =>
        {
            _lifetime.Cancel();
            _updateCancellation?.Cancel();
            _protectTimer.Stop();
            _previewTimer.Stop();
            _updateTimer.Stop();
            _monitor.Dispose();
            _html.Dispose();
            foreach (var terminal in _terminals.ToArray())
            {
                await terminal.View.DisposeAsync();
                terminal.Window.Close();
            }

            foreach (var git in _gitWindows.ToArray())
            {
                git.Window.Close();
            }
        };
        Opened += async (_, _) => await RunAsync(InitializeAsync);
    }
}

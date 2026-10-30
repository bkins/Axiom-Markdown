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
    private Menu BuildMenu()
    {
        var menu = new Menu();
        MenuItem Group(string label)
        {
            var group = new MenuItem { Header = label };
            menu.Items.Add(group);
            return group;
        }

        void Item(MenuItem         group
                , string           label
                , Func<Task>       action
                , OptionalFeature? feature = null)
        {
            var item = new MenuItem { Header = label };
            item.Click += async (_, _) => await RunAsync(action);
            group.Items.Add(item);
            if (feature is { } value)
            {
                _featureMenuItems.Add((item, value));
            }
        }

        var file = Group("_File");
        Item(file, "New     Ctrl+N", () => NewAsync());
        Item(file, "Open…     Ctrl+O", OpenPickerAsync);
        Item(file, "Open folder…", OpenFolderAsync);
        Item(file, "Save     Ctrl+S", () => SaveAsync(Current, false));
        Item(file, "Save As…", () => SaveAsync(Current, true));
        Item(file, "Save all     Ctrl+Shift+S", SaveAllAsync);
        Item(file
           , "Import HTML or Word…"
           , ImportAsync
           , OptionalFeature.Import);
        Item(file
           , "Export…"
           , ExportAsync
           , OptionalFeature.Export);
        Item(file, "Close document     Ctrl+W", () => CloseTabAsync(Current));
        Item(file
           , "Exit"
           , () =>
        {
            Close();
            return Task.CompletedTask;
        });
        var edit = Group("_Edit");
        Item(edit
           , "Undo     Ctrl+Z"
           , () =>
        {
            Current?.Editor.Undo();
            return Task.CompletedTask;
        });
        Item(edit
           , "Redo     Ctrl+Y"
           , () =>
        {
            Current?.Editor.Redo();
            return Task.CompletedTask;
        });
        Item(edit, "Find and replace…     Ctrl+F", FindAsync);
        var format = Group("F_ormat");
        foreach (var(label, kind)in new[]
        {
            ("Bold", MarkdownEditKind.Bold)
          , ("Italic", MarkdownEditKind.Italic)
          , ("Heading 1", MarkdownEditKind.Heading1)
          , ("Heading 2", MarkdownEditKind.Heading2)
          , ("Bulleted list", MarkdownEditKind.UnorderedList)
          , ("Numbered list", MarkdownEditKind.OrderedList)
          , ("Task list", MarkdownEditKind.TaskList)
          , ("Link", MarkdownEditKind.Link)
          , ("Code block", MarkdownEditKind.FencedCode)
          , ("Table", MarkdownEditKind.Table)
          , ("Hard line break", MarkdownEditKind.HardBreak)
        }

        )
            Item(format
               , label
               , () =>
            {
                Format(kind);
                return Task.CompletedTask;
            });
        var tools = Group("_Tools");
        Item(tools
           , "Git changes…"
           , OpenGitAsync
           , OptionalFeature.Git);
        Item(tools
           , "Document history…"
           , OpenHistoryAsync
           , OptionalFeature.Git);
        Item(tools
           , "Set up local Git history…"
           , InitializeGitAsync
           , OptionalFeature.Git);
        Item(tools
           , "Checkpoint saved document…"
           , CheckpointAsync
           , OptionalFeature.Git);
        Item(tools
           , "Embedded terminal…"
           , OpenTerminalAsync
           , OptionalFeature.Terminal);
        Item(tools, "Feature controls and policy…", FeatureSettingsAsync);
        Item(tools
           , "Check for updates…"
           , () => CheckUpdatesAsync(true)
           , OptionalFeature.Updates);
        Item(tools
           , "Restart to apply downloaded update…"
           , ApplyUpdateAsync
           , OptionalFeature.Updates);
        var view = Group("_View");
        Item(view, "Editor and appearance settings…", ShowEditorSettingsAsync);
        Item(view, "Reading mode     Ctrl+Shift+R", ToggleReadingModeAsync);
        Item(view, "Document outline", ToggleOutlineAsync);
        Item(view
           , "Show or hide file navigator"
           , () =>
        {
            _preferences = _preferences with

            {
                ShowFileNavigator = !_preferences.ShowFileNavigator
            };
            ApplyPreferences();
            return Task.CompletedTask;
        });
        Item(view
           , "Split editor and preview"
           , () =>
        {
            _viewMode.SelectedIndex = 0;
            return Task.CompletedTask;
        });
        Item(view
           , "Editor only"
           , () =>
        {
            _viewMode.SelectedIndex = 1;
            return Task.CompletedTask;
        });
        Item(view
           , "Toggle word wrap"
           , () =>
        {
            _preferences = _preferences with

            {
                WordWrap = !_preferences.WordWrap
            };
            ApplyPreferences();
            return Task.CompletedTask;
        });
        Item(view
           , "Larger editor font"
           , () =>
        {
            _preferences = _preferences with

            {
                FontSize = Math.Min(32
                                  , _preferences.FontSize + 1)
            };
            ApplyPreferences();
            return Task.CompletedTask;
        });
        Item(view
           , "Smaller editor font"
           , () =>
        {
            _preferences = _preferences with

            {
                FontSize = Math.Max(10
                                  , _preferences.FontSize - 1)
            };
            ApplyPreferences();
            return Task.CompletedTask;
        });
        foreach (var appearance in Enum.GetValues<AppearancePreference>())
        {
            Item(view
               , appearance + " theme"
               , () =>
            {
                _preferences = _preferences with

                {
                    Appearance = appearance
                };
                ApplyPreferences();
                return Task.CompletedTask;
            });
        }

        var help = Group("_Help");
        Item(help
           , "About and local data"
           , async () => await ChoiceDialog.ShowAsync(this
                                                    , "Axiom Markdown"
                                                    , $"Axiom Markdown {ApplicationBuildIdentity.FromAssembly(typeof(MainWindow).Assembly).InformationalVersion}\nMarkdown editing with optional organization-controlled tools.\nLocal drafts, preferences and webview cache: {_paths.Root}\nThis edition has its own state and update feed.\nSee the repository User Guide and security review packet for policy and limitations."
                                                    , "OK"));
        return menu;
    }

    private Button ActionButton(string     label
                              , Func<Task> action)
    {
        var button = new Button
                     {
                         Content = label
                       , Margin  = new Thickness(0
                                               , 0
                                               , 6
                                               , 3)
                     };
        button.Click += async (_, _) => await RunAsync(action);
        return button;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy || _closing)
        {
            return;
        }

        _busy = true;
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Cancelled; current work retained.";
        }
        catch (Exception error)
        {
            _status.Text = error.Message;
            await ChoiceDialog.ShowAsync(this
                                       , "Operation not completed"
                                       , error.Message
                                       , "OK");
        }
        finally
        {
            _busy = false;
        }
    }

    private void RefreshFeatureMenus()
    {
        foreach (var(item, feature)in _featureMenuItems)
        {
            var decision = FeatureAccess.Evaluate(feature);
            item.IsEnabled = decision.Allowed;
            ToolTip.SetTip(item, decision.Reason);
        }
    }
}

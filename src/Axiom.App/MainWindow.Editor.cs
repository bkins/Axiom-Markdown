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
    private void Format(MarkdownEditKind kind)
    {
        if (Current is not { } tab)
        {
            return;
        }

        var editor = tab.Editor;
        var result = new MarkdownEditService().Apply(editor.Text
                                                   , editor.SelectionStart
                                                   , editor.SelectionLength
                                                   , kind);
        if (!result.Changed)
        {
            return;
        }

        editor.Document.Replace(0, editor.Document.TextLength, result.Text);
        editor.Select(result.SelectionStart, result.SelectionLength);
        editor.Focus();
    }

    private async Task FindAsync()
    {
        if (Current is not { } tab)
        {
            return;
        }

        var find = new TextBox { PlaceholderText = "Find text" };
        var replace = new TextBox { PlaceholderText = "Replace with" };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var next = new Button { Content = "Find next" };
        var one = new Button { Content = "Replace" };
        var all = new Button { Content = "Replace all" };
        var window = new Window
                     {
                         Title                 = "Find and replace"
                       , Width                 = 480
                       , Height                = 255
                       , WindowStartupLocation = WindowStartupLocation.CenterOwner
                     };
        void Next()
        {
            if (string.IsNullOrEmpty(find.Text))
            {
                return;
            }

            var text = tab.Editor.Text;
            var start = Math.Min(text.Length, tab.Editor.SelectionStart + tab.Editor.SelectionLength);
            var at = text.IndexOf(find.Text, start, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                at = text.IndexOf(find.Text, StringComparison.OrdinalIgnoreCase);
            }

            if (at < 0)
            {
                status.Text = "No match.";
                return;
            }

            tab.Editor.Select(at, find.Text.Length);
            tab.Editor.ScrollToLine(tab.Editor.Document.GetLineByOffset(at).LineNumber);
            status.Text = "Match selected in editor.";
        }

        next.Click += (_, _) => Next();
        one.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(find.Text) && tab.Editor.SelectedText.Equals(find.Text
                                                                                 , StringComparison.OrdinalIgnoreCase))
            {
                tab.Editor.Document.Replace(tab.Editor.SelectionStart
                                          , tab.Editor.SelectionLength
                                          , replace.Text ?? "");
            }

            Next();
        };
        all.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(find.Text))
            {
                return;
            }

            tab.Editor.Document.Replace(0
                                      , tab.Editor.Document.TextLength
                                      , tab.Editor.Text.Replace(find.Text
                                                              , replace.Text ?? ""
                                                              , StringComparison.OrdinalIgnoreCase));
            status.Text = "All matches replaced; use Undo to restore.";
        };
        window.Content = new StackPanel
                         {
                             Margin   = new Thickness(15)
                           , Spacing  = 10
                           , Children =
                             {
                                 find
                               , replace
                               , new WrapPanel
                                 {
                                     Children =
                                     {
                                         next
                                       , one
                                       , all
                                     }
                                 }
                               , status
                             }
                         };
        await window.ShowDialog(this);
    }

    private async void OnKeyDown(object?      sender
                               , KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Apps || eventArgs.Key == Key.F10 && eventArgs.KeyModifiers == KeyModifiers.Shift)
        {
            if (_folderFiles.IsKeyboardFocusWithin)
            {
                BuildExplorerContextMenu(_folderFiles.SelectedItem as WorkspaceTreeNode).Open(_folderFiles);
                eventArgs.Handled = true;
                return;
            }

            if (Current?.Editor is { IsKeyboardFocusWithin: true } editor)
            {
                BuildEditorContextMenu(editor).Open(editor);
                eventArgs.Handled = true;
                return;
            }
        }

        if (eventArgs.KeyModifiers == KeyModifiers.Alt || eventArgs.Key == Key.F10 && eventArgs.KeyModifiers == KeyModifiers.None)
        {
            var menuIndex = eventArgs.Key switch
            {
                Key.F => 0,
                Key.E => 1,
                Key.M => 2,
                Key.T => 3,
                Key.V => 4,
                Key.H => 5,
                Key.F10 => 6,
                _ => -1
            };
            if (menuIndex >= 0)
            {
                eventArgs.Handled = true;
                OpenMainMenu(menuIndex == 6 ? -1 : menuIndex);
                return;
            }
        }

        if (eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Func<Task>? action = eventArgs.Key switch
            {
                Key.R                when eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift) => ToggleReadingModeAsync,
                Key.N => () => NewAsync(),
                Key.O => OpenPickerAsync,
                Key.S => eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift) ? SaveAllAsync : () => SaveAsync(Current
                                                                                                           , false),
                Key.W => () => CloseTabAsync(Current),
                Key.F => FindAsync,
                Key.B => () =>
                {
                    Format(MarkdownEditKind.Bold);
                    return Task.CompletedTask;
                },
                Key.I => () =>
                {
                    Format(MarkdownEditKind.Italic);
                    return Task.CompletedTask;
                },
                _ => null
            };
            if (action is not null)
            {
                eventArgs.Handled = true;
                await RunAsync(action);
            }
        }
    }
}

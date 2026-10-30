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
    private Task NewAsync()
    {
        AddTab(_documents.NewDocument());
        return Task.CompletedTask;
    }

    private EditorTab AddTab(DocumentSession session)
    {
        var editor = new TextEditor
                     {
                         Text            = session.Text
                       , ShowLineNumbers = true
                       , WordWrap        = _preferences.WordWrap
                       , FontFamily      = new FontFamily("Cascadia Mono,Consolas,monospace")
                       , FontSize        = _preferences.FontSize
                       , Padding         = new Thickness(0
                                                       , 8)
                     };
        editor.Options.EnableHyperlinks      = false;
        editor.Options.EnableEmailHyperlinks = false;
        ApplyEditorColors(editor);
        editor.Options.HighlightCurrentLine            = true;
        editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.Parse("#187B9BD1"));
        editor.TextArea.TextView.CurrentLineBorder     = null;
        editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            if (ReferenceEquals(Current?.Editor, editor))
            {
                RefreshDocumentChrome();
            }
        };
        AutomationProperties.SetName(editor, "Markdown source editor");
        var tab = new EditorTab(session, editor);
        var close = new Button
                    {
                        Content = "×"
                      , Padding = new Thickness(5
                                              , 0)
                      , Margin  = new Thickness(7
                                              , 0
                                              , 0
                                              , 0)
                    };
        AutomationProperties.SetName(close, "Close document");
        close.MinWidth   = close.MinHeight = 28;
        close.Background = new SolidColorBrush(Color.Parse("#B91C1C"));
        close.Foreground = Brushes.White;
        ToolTip.SetTip(close, "Close document; unsaved changes offer Save, Discard or Cancel");
        close.Click += async (_, eventArgs6) =>
        {
            eventArgs6.Handled = true;
            await RunAsync(() => CloseTabAsync(tab));
        };
        var label = new TextBlock
                    {
                        VerticalAlignment = VerticalAlignment.Center
                      , FontSize          = 14
                    };
        tab.Header = label;
        var item = new TabItem
                   {
                       Header = new StackPanel
                                {
                                    Orientation = Orientation.Horizontal
                                  , Children    =
                                    {
                                        label
                                      , close
                                    }
                                }
                     , Tag    = tab
                   };
        editor.TextChanged += (_, _) =>
        {
            session.UpdateText(editor.Text);
            RefreshHeader(tab);
            RefreshDocumentChrome();
            if (ReferenceEquals(Current, tab))
            {
                _previewTimer.Stop();
                _previewTimer.Start();
            }
        };
        tab.Item = item;
        WireEditorContextMenu(tab);
        _items.Add(item);
        _tabs.SelectedItem = item;
        RefreshHeader(tab);
        WatchDocuments();
        return tab;
    }

    private void ActivateCurrent()
    {
        _editorHost.Content     = Current?.Editor;
        _viewMode.SelectedIndex = Current?.ViewMode ?? 0;
        ApplyView();
        RenderPreview();
        if (Current is { } tab)
        {
            RefreshHeader(tab);
            tab.Editor.Focus();
        }
    }

    private void RefreshHeader(EditorTab tab)
    {
        ToolTip.SetTip(tab.Item, tab.Session.Path ?? "Unsaved Markdown document");
        tab.Header.Text = (tab.Session.Path is { } path ? Path.GetFileName(path) : "Untitled") + (tab.Session.IsDirty ? " *" : "") + (tab.Session.Conflict is null ? "" : " ⚠");
        if (ReferenceEquals(Current, tab))
        {
            Title             = tab.Header.Text + " — Axiom Markdown";
            _windowTitle.Text = Title;
        }
    }

    private async Task CloseTabAsync(EditorTab? tab)
    {
        if (tab is null || !await ResolveDirtyAsync(tab))
        {
            return;
        }

        _items.Remove(tab.Item);
        await _state.DeleteDraftAsync(tab.Session.DraftKey);
        if (_items.Count == 0)
        {
            AddTab(_documents.NewDocument());
        }
        else if (_tabs.SelectedIndex < 0)
        {
            _tabs.SelectedIndex = 0;
        }

        WatchDocuments();
    }
}

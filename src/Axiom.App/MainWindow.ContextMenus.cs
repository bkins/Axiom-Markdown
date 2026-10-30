using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Axiom.Core;

namespace Axiom.App;

public sealed partial class MainWindow
{
    private MenuItem ContextAction(string     label
                                 , Func<Task> action
                                 , bool       enabled = true)
    {
        var item = new MenuItem
                   {
                       Header    = label
                     , IsEnabled = enabled
                   };
        item.Click += async (_, _) => await RunAsync(action);
        return item;
    }

    private void WireEditorContextMenu(EditorTab tab)
    {
        tab.Editor.ContextRequested += (_, eventArgs) =>
        {
            var editor = tab.Editor;
            if (eventArgs.TryGetPosition(editor.TextArea.TextView, out var point) && editor.TextArea.TextView.GetPosition(point + editor.TextArea.TextView.ScrollOffset) is { } position)
            {
                var offset = editor.Document.GetOffset(position.Line, position.Column);
                if (offset < editor.SelectionStart || offset > editor.SelectionStart + editor.SelectionLength)
                {
                    editor.Select(offset, 0);
                }
            }

            _tabs.SelectedItem = tab.Item;
            BuildEditorContextMenu(editor).Open(editor);
            eventArgs.Handled = true;
        };
    }

    private ContextMenu BuildEditorContextMenu(TextEditor editor)
    {
        var menu = new ContextMenu();
        void Add(string label
               , Action action
               , bool   enabled = true)
        {
            menu.Items.Add(ContextAction(label
                                       , () =>
            {
                action();
                editor.Focus();
                return Task.CompletedTask;
            }
                                       , enabled));
        }

        Add("Undo", () => editor.Undo(), editor.Document.UndoStack.CanUndo);
        Add("Redo", () => editor.Redo(), editor.Document.UndoStack.CanRedo);
        menu.Items.Add(new Separator());
        Add("Cut", editor.Cut, editor.SelectionLength > 0);
        Add("Copy", editor.Copy, editor.SelectionLength > 0);
        Add("Paste", editor.Paste);
        Add("Select all", editor.SelectAll);
        menu.Items.Add(new Separator());
        menu.Items.Add(ContextAction("Find and replace…", FindAsync));
        var formatting = new MenuItem { Header = "Format" };
        foreach (var entry in new[]
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
        }

        )
        {
            formatting.Items.Add(ContextAction(entry.Item1
                                             , () =>
            {
                Format(entry.Item2);
                return Task.CompletedTask;
            }));
        }

        menu.Items.Add(formatting);
        return menu;
    }

    private static TreeViewItem? ExplorerItem(object? source)
    {
        return source is Control control ? control as TreeViewItem ?? control.GetVisualAncestors().OfType<TreeViewItem>().FirstOrDefault() : null;
    }

    private void ShowExplorerContextMenu(object?                   sender
                                       , ContextRequestedEventArgs eventArgs)
    {
        var item = ExplorerItem(eventArgs.Source);
        var node = item?.DataContext as WorkspaceTreeNode;
        if (node is not null)
        {
            _folderFiles.SelectedItem = node;
        }

        BuildExplorerContextMenu(node).Open(item ?? (Control)_folderFiles);
        eventArgs.Handled = true;
    }

    private ContextMenu BuildExplorerContextMenu(WorkspaceTreeNode? node)
    {
        var menu = new ContextMenu();
        void Add(string     title
               , Func<Task> action)
        {
            menu.Items.Add(ContextAction(title, action, _folder is not null));
        }

        if (node is { IsDocument: true })
        {
            Add("Open", () => OpenExplorerNodeAsync(node));
        }

        Add("New file…", () => CreateExplorerItemAsync(node, false));
        Add("New folder…", () => CreateExplorerItemAsync(node, true));
        if (node is not null)
        {
            menu.Items.Add(new Separator());
            Add("Rename…", () => RenameExplorerItemAsync(node));
            Add("Move…", () => ChooseExplorerMoveAsync(node));
            if (node.IsDocument)
            {
                Add("Duplicate…", () => DuplicateExplorerDocumentAsync(node));
            }

            Add("Recycle…", () => RecycleExplorerItemAsync(node));
            menu.Items.Add(new Separator());
            Add("Copy path"
              , async () =>
            {
                if (Clipboard is { } clipboard)
                {
                    await clipboard.SetTextAsync(Path.Combine(_folder!, node.RelativePath));
                }
            });
            Add("Show in File Explorer", () => RevealExplorerItemAsync(node));
        }

        Add("Refresh"
          , async () =>
        {
            if (_folder is { } root)
            {
                await SetFolderAsync(root);
            }
        });
        return menu;
    }
}

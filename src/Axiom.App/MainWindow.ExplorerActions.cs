using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Axiom.Core;
using Axiom.Infrastructure;

namespace Axiom.App;

public sealed partial class MainWindow
{
    private readonly MarkdownWorkspaceOperations _workspaceOperations = new();
    private Task OpenExplorerNodeAsync(WorkspaceTreeNode node)
    {
        return OpenAsync(_workspaceOperations.Resolve(_folder!, node.RelativePath));
    }

    private static string ContainerRelativePath(WorkspaceTreeNode? node)
    {
        return node is null ? "" : node.IsDocument ? Path.GetDirectoryName(node.RelativePath) ?? "" : node.RelativePath;
    }

    private async Task CreateExplorerItemAsync(WorkspaceTreeNode? node
                                             , bool               folder)
    {
        if (_folder is not { } root)
        {
            return;
        }

        var name = await TextEntryDialog.ShowAsync(this
                                                 , folder ? "New folder" : "New Markdown file"
                                                 , "Enter a name inside the selected folder."
                                                 , folder ? "New folder" : "Untitled.md");
        if (name is null)
        {
            return;
        }

        RequireItemName(name);
        var relative = Path.Combine(ContainerRelativePath(node), name);
        var path = folder ? _workspaceOperations.CreateFolder(root, relative) : _workspaceOperations.CreateDocument(root
                                                                                                                  , relative);
        await SetFolderAsync(root);
        if (!folder)
        {
            await OpenAsync(path);
        }
    }

    private async Task RenameExplorerItemAsync(WorkspaceTreeNode node)
    {
        var name = await TextEntryDialog.ShowAsync(this
                                                 , "Rename"
                                                 , "Enter the new name in the same folder."
                                                 , node.Name);
        if (name is null)
        {
            return;
        }

        RequireItemName(name);
        await MoveExplorerItemAsync(node, Path.Combine(Path.GetDirectoryName(node.RelativePath) ?? "", name));
    }

    private async Task ChooseExplorerMoveAsync(WorkspaceTreeNode node)
    {
        if (_folder is not { } root)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                                                                  {
                                                                      Title                  = "Move into a folder inside the open workspace"
                                                                    , AllowMultiple          = false
                                                                    , SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(root)
                                                                  });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } target)
        {
            return;
        }

        await MoveExplorerItemAsync(node, Path.Combine(Path.GetRelativePath(root, target), node.Name));
    }

    private async Task MoveExplorerItemAsync(WorkspaceTreeNode node
                                           , string            destinationRelative
                                           , bool              confirm = true)
    {
        if (_folder is not { } root)
        {
            return;
        }

        var source = _workspaceOperations.Resolve(root, node.RelativePath);
        var destination = _workspaceOperations.Resolve(root, destinationRelative);
        var affected = Documents.Where(tab => tab.Session.Path is { } path && WorkspacePathBoundary.Contains(source
                                                                                                           , path
                                                                                                           , includeRoot: true)).ToArray();
        RequireCleanExplorerDocuments(affected);
        if (confirm && await ChoiceDialog.ShowAsync(this
                                                  , "Move workspace item?"
                                                  , $"Move {node.RelativePath}\nto {destinationRelative}?\n\nExisting items will not be overwritten. Relative links are not rewritten and may need adjustment after a move. Open clean tabs will follow the new paths."
                                                  , "Move"
                                                  , "Cancel") != "Move")
        {
            return;
        }

        RequireCleanExplorerDocuments(affected);
        foreach (var tab in affected)
        {
            tab.Editor.IsEnabled = false;
        }

        _monitor.WatchMany([]);
        var moved = false;
        try
        {
            await Task.Run(() => _workspaceOperations.Move(root, node.RelativePath, destinationRelative));
            moved = true;
            foreach (var tab in affected)
            {
                var relative = Path.GetRelativePath(source, tab.Session.Path!);
                var path = relative == "." ? destination : Path.Combine(destination, relative);
                tab.Session.UpdatePath(path);
                tab.RemoteImages.Clear();
                RefreshHeader(tab);
            }

            foreach (var tab in affected)
            {
                var caret = tab.Editor.CaretOffset;
                tab.Session.Reload(await _files.ReadAsync(tab.Session.Path!));
                tab.Editor.Text        = tab.Session.Text;
                tab.Editor.CaretOffset = Math.Min(caret, tab.Editor.Document.TextLength);
                if (tab.Session.DocumentId is { } identity)
                {
                    await _state.RecordIdentityAsync(identity, tab.Session.Path!);
                }
            }

            await SetFolderAsync(root);
            _status.Text = "Moved to " + destinationRelative + "; relative links were retained.";
        }
        catch (Exception error)when (moved)
        {
            throw new IOException("The item moved to " + destinationRelative + ", but refreshing its state failed. Open buffers were retained. " + error.Message
                                , error);
        }
        finally
        {
            foreach (var tab in affected)
            {
                tab.Editor.IsEnabled = true;
            }

            WatchDocuments();
        }
    }

    private static void RequireCleanExplorerDocuments(IEnumerable<EditorTab> tabs)
    {
        if (tabs.Any(tab => tab.Session.IsDirty || tab.Session.Conflict is not null))
        {
            throw new IOException("Save affected documents and resolve their conflicts before moving, duplicating or recycling. Nothing was moved, saved or discarded.");
        }
    }

    private async Task DuplicateExplorerDocumentAsync(WorkspaceTreeNode node)
    {
        if (_folder is not { } root)
        {
            return;
        }

        var source = _workspaceOperations.Resolve(root, node.RelativePath);
        RequireCleanExplorerDocuments(Documents.Where(tab => SamePath(tab.Session.Path, source)));
        var name = await TextEntryDialog.ShowAsync(this
                                                 , "Duplicate Markdown file"
                                                 , "Enter a new filename. The saved text is copied as UTF-8; an existing document identity gets a new ID."
                                                 , Path.GetFileNameWithoutExtension(node.Name) + " copy" + Path.GetExtension(node.Name));
        if (name is null)
        {
            return;
        }

        RequireItemName(name);
        var destination = await _workspaceOperations.DuplicateDocumentAsync(root
                                                                          , node.RelativePath
                                                                          , Path.Combine(Path.GetDirectoryName(node.RelativePath) ?? ""
                                                                                       , name));
        await SetFolderAsync(root);
        await OpenAsync(destination);
    }

    private async Task RecycleExplorerItemAsync(WorkspaceTreeNode node)
    {
        if (_folder is not { } root)
        {
            return;
        }

        var source = _workspaceOperations.Resolve(root, node.RelativePath);
        var affected = Documents.Where(tab => tab.Session.Path is { } path && WorkspacePathBoundary.Contains(source
                                                                                                           , path
                                                                                                           , includeRoot: true)).ToArray();
        RequireCleanExplorerDocuments(affected);
        if (await ChoiceDialog.ShowAsync(this
                                       , "Send to Recycle Bin?"
                                       , $"Recycle {node.RelativePath}? Folder contents are included. Open clean tabs for the item will close. Restore deleted items using the Windows Recycle Bin."
                                       , "Recycle"
                                       , "Cancel") != "Recycle")
        {
            return;
        }

        RequireCleanExplorerDocuments(affected);
        _workspaceOperations.Recycle(root, node.RelativePath);
        foreach (var tab in affected)
        {
            _items.Remove(tab.Item);
        }

        if (_items.Count == 0)
        {
            AddTab(_documents.NewDocument());
        }

        WatchDocuments();
        await SetFolderAsync(root);
    }

    private Task RevealExplorerItemAsync(WorkspaceTreeNode node)
    {
        var path = _workspaceOperations.Resolve(_folder!, node.RelativePath);
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        var start = new System.Diagnostics.ProcessStartInfo(explorer) { UseShellExecute = false };
        start.ArgumentList.Add("/select," + path);
        System.Diagnostics.Process.Start(start)?.Dispose();
        return Task.CompletedTask;
    }

    private static void RequireItemName(string name)
    {
        if (name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new IOException("Enter a filename or folder name only.");
        }
    }
}

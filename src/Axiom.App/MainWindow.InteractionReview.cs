using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Axiom.Core;

namespace Axiom.App;

public sealed partial class MainWindow
{
    internal async Task<IReadOnlyDictionary<string, bool>> VerifyInteractionsForReviewAsync(string root)
    {
        var checks = new Dictionary<string, bool>();
        var originalTab = Current!;
        var text = originalTab.Session.Text;
        checks["HamburgerContainsSixSections"] = _commandGroups.Select(group => group.Header?.ToString()?.Replace("_"
                                                                                                                , "")).SequenceEqual(new[]
                                                                                                                {
                                                                                                                    "File"
                                                                                                                  , "Edit"
                                                                                                                  , "Format"
                                                                                                                  , "Tools"
                                                                                                                  , "View"
                                                                                                                  , "Help"
                                                                                                                }) && _menuButtons.Count == 1;
        UpdateLayout();
        checks["CaptionIconsCentered"] = _captionButtons.Children.OfType<Button>().All(button =>
        {
            if (button.Content is not WorkbenchIcon icon)
            {
                return false;
            }

            var center = icon.TranslatePoint(new Point(icon.Bounds.Width / 2, icon.Bounds.Height / 2), button);
            return button.HorizontalContentAlignment == HorizontalAlignment.Center && button.VerticalContentAlignment == VerticalAlignment.Center && center is { } point && Math.Abs(point.X - button.Bounds.Width / 2) < 1 && Math.Abs(point.Y - button.Bounds.Height / 2) < 1;
        });
        using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(_captionButtons.Bounds.Width * RenderScaling)
                                                               , (int)Math.Ceiling(_captionButtons.Bounds.Height * RenderScaling))
                                                 , new Vector(96 * RenderScaling
                                                            , 96 * RenderScaling)))
        {
            bitmap.Render(_captionButtons);
            bitmap.Save(Path.Combine(root, "caption-controls.png"), new PngBitmapEncoderOptions());
        }

        originalTab.Editor.Select(2, 5);
        var context = BuildEditorContextMenu(originalTab.Editor);
        var format = context.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Format"));
        format.Items.OfType<MenuItem>()
                    .Single(item => Equals(item.Header, "Bold"))
                    .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await Task.Delay(100);
        checks["EditorContextFormatsSelection"] = originalTab.Session.Text.Contains("**Axiom**");
        BuildEditorContextMenu(originalTab.Editor).Items.OfType<MenuItem>()
                                                        .Single(item => Equals(item.Header, "Undo"))
                                                        .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await Task.Delay(100);
        checks["EditorContextUndoRestoresDirtyBuffer"] = originalTab.Session.Text == text && originalTab.Session.IsDirty;
        originalTab.Editor.Focus();
        originalTab.Editor.RaiseEvent(new KeyEventArgs
                                      {
                                          RoutedEvent  = KeyDownEvent
                                        , Key          = Key.F10
                                        , KeyModifiers = KeyModifiers.Shift
                                      });
        checks["KeyboardContextWaitsForKeyRelease"] = ReferenceEquals(_keyboardContextTarget, originalTab.Editor) && _keyboardContextMenu is null;
        originalTab.Editor.RaiseEvent(new KeyEventArgs
                                      {
                                          RoutedEvent  = KeyUpEvent
                                        , Key          = Key.F10
                                        , KeyModifiers = KeyModifiers.Shift
                                      });
        await Task.Delay(100);
        checks["KeyboardContextRemainsOpenAfterKeyRelease"] = _keyboardContextMenu?.IsOpen == true;
        _keyboardContextMenu?.Close();
        var workspace = Path.Combine(root, "interaction-workspace");
        Directory.CreateDirectory(Path.Combine(workspace, "Target"));
        Directory.CreateDirectory(Path.Combine(workspace, "Bundle"));
        await File.WriteAllTextAsync(Path.Combine(workspace, "MoveSource.md"), "# Move me");
        await File.WriteAllTextAsync(Path.Combine(workspace, "Bundle", "Inside.md"), "# Inside");
        await File.WriteAllBytesAsync(Path.Combine(workspace, "Bundle", "image.png"), [1, 2, 3]);
        await SetFolderAsync(workspace);
        var node = new WorkspaceTreeNode("MoveSource.md"
                                       , "MoveSource.md"
                                       , true
                                       , []);
        var folder = new WorkspaceTreeNode("Bundle"
                                         , "Bundle"
                                         , false
                                         , []);
        checks["FileAndFolderContextActions"] = BuildExplorerContextMenu(node).Items.OfType<MenuItem>().Any(item => Equals(item.Header
                                                                                                                         , "Rename…")) && BuildExplorerContextMenu(folder).Items.OfType<MenuItem>().Any(item => Equals(item.Header
                                                                                                                                                                                                                     , "Move…"));
        await OpenExplorerNodeAsync(node);
        var movingTab = Current!;
        movingTab.Editor.AppendText(" unsaved");
        try
        {
            await MoveExplorerItemAsync(node, Path.Combine("Target", node.Name), confirm: false);
            checks["DirtyMoveRejectedWithoutSaving"] = false;
        }
        catch (IOException)
        {
            checks["DirtyMoveRejectedWithoutSaving"] = movingTab.Session.IsDirty && File.ReadAllText(Path.Combine(workspace
                                                                                                                , node.Name)) == "# Move me" && !File.Exists(Path.Combine(workspace
                                                                                                                                                                        , "Target"
                                                                                                                                                                        , node.Name));
        }

        movingTab.Editor.Undo();
        await MoveExplorerItemAsync(node, Path.Combine("Target", node.Name), confirm: false);
        checks["MoveRetargetsOpenCleanTab"] = ReferenceEquals(Current, movingTab) && movingTab.Session.Path == Path.Combine(workspace
                                                                                                                          , "Target"
                                                                                                                          , node.Name) && movingTab.Editor.Text == "# Move me" && !File.Exists(Path.Combine(workspace
                                                                                                                                                                                                          , node.Name));
        movingTab.Editor.AppendText(" retained");
        await _documents.ProtectAsync(movingTab.Session);
        checks["MovedDraftUsesNewPath"] = (await _state.GetRecoverableDraftsAsync()).Any(draft => draft.DraftKey == movingTab.Session.DraftKey && draft.FilePath == movingTab.Session.Path && draft.Text.EndsWith(" retained"));
        await _documents.SaveAsync(movingTab.Session);
        _items.Remove(movingTab.Item);
        _tabs.SelectedItem = originalTab.Item;
        await MoveExplorerItemAsync(folder, Path.Combine("Target", "Bundle"), confirm: false);
        checks["FolderMovePreservesDocumentsAndAssets"] = File.ReadAllText(Path.Combine(workspace
                                                                                      , "Target"
                                                                                      , "Bundle"
                                                                                      , "Inside.md")) == "# Inside" && File.ReadAllBytes(Path.Combine(workspace
                                                                                                                                                    , "Target"
                                                                                                                                                    , "Bundle"
                                                                                                                                                    , "image.png")).SequenceEqual(new byte[]
                                                                                                                                                                                   {
                                                                                                                                                                                       1
                                                                                                                                                                                     , 2
                                                                                                                                                                                     , 3
                                                                                                                                                                                   });
        _explorerDragToken                              = Guid.NewGuid().ToString("N");
        _explorerDragRoot                               = workspace;
        _explorerDragNode                               = new WorkspaceTreeNode("Inside.md"
                                                                              , Path.Combine("Target"
                                                                                           , "Bundle"
                                                                                           , "Inside.md")
                                                                              , true
                                                                              , []);
        using var transfer = new DataTransfer();
        var payload = new DataTransferItem();
        payload.Set(ExplorerMoveFormat, _explorerDragToken);
        transfer.Add(payload);
        var over = new DragEventArgs(DragDrop.DragOverEvent
                                   , transfer
                                   , _folderFiles
                                   , new Point(2
                                             , 2)
                                   , KeyModifiers.None);
        _folderFiles.RaiseEvent(over);
        checks["InternalDragAdvertisesMove"] = over.DragEffects == DragDropEffects.Move;
        _explorerDragToken                   = null;
        var foreign = new DragEventArgs(DragDrop.DragOverEvent
                                      , transfer
                                      , _folderFiles
                                      , new Point(2
                                                , 2)
                                      , KeyModifiers.None);
        _folderFiles.RaiseEvent(foreign);
        checks["ForeignDragRejected"] = foreign.DragEffects == DragDropEffects.None;
        _explorerDragNode             = null;
        ClearExplorerDropTarget();
        checks["ExplorerActionsRetainOriginalDirtyDocument"] = ReferenceEquals(Current, originalTab) && originalTab.Session.Text == text && originalTab.Session.IsDirty;
        return checks;
    }
}

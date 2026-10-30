using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Axiom.Core;

namespace Axiom.App;

public sealed partial class MainWindow
{
    private static readonly DataFormat<string> ExplorerMoveFormat = DataFormat.CreateStringApplicationFormat("AxiomMarkdown.WorkspaceMove");
    private PointerPressedEventArgs? _explorerDragPress;
    private WorkspaceTreeNode? _explorerDragNode;
    private Point _explorerDragOrigin;
    private string? _explorerDragToken;
    private string? _explorerDragRoot;
    private TreeViewItem? _explorerDropTarget;
    private bool _explorerDragging;
    private bool _suppressExplorerOpen;
    private readonly DispatcherTimer _explorerScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private ScrollViewer? _explorerScrollViewer;
    private int _explorerScrollDirection;
    private void InitializeExplorerDrag()
    {
        _folderFiles.AddHandler(PointerPressedEvent
                              , ExplorerPointerPressed
                              , RoutingStrategies.Tunnel
                              , handledEventsToo: true);
        _folderFiles.AddHandler(PointerMovedEvent
                              , ExplorerPointerMoved
                              , RoutingStrategies.Tunnel
                              , handledEventsToo: true);
        _folderFiles.AddHandler(PointerReleasedEvent
                              , (_, _) => _explorerDragPress = null
                              , RoutingStrategies.Tunnel
                              , handledEventsToo: true);
        DragDrop.SetAllowDrop(_folderFiles, true);
        _folderFiles.AddHandler(DragDrop.DragOverEvent, ExplorerDragOver);
        _folderFiles.AddHandler(DragDrop.DropEvent, ExplorerDrop);
        _folderFiles.AddHandler(DragDrop.DragLeaveEvent, (_, _) => ClearExplorerDropTarget());
        _explorerScrollTimer.Tick += (_, _) =>
        {
            if (_explorerDragToken is null || _folder != _explorerDragRoot || _explorerScrollViewer is null)
            {
                _explorerScrollTimer.Stop();
                return;
            }

            var maximum = Math.Max(0, _explorerScrollViewer.Extent.Height - _explorerScrollViewer.Viewport.Height);
            _explorerScrollViewer.Offset = new Vector(_explorerScrollViewer.Offset.X
                                                    , Math.Clamp(_explorerScrollViewer.Offset.Y + _explorerScrollDirection * 16
                                                               , 0
                                                               , maximum));
        };
        Closed += (_, _) => ClearExplorerDropTarget();
    }

    private void ExplorerPointerPressed(object?                 sender
                                      , PointerPressedEventArgs eventArgs)
    {
        _explorerDragPress    = null;
        _suppressExplorerOpen = false;
        if (_busy || _closing || _folder is null || eventArgs.KeyModifiers != KeyModifiers.None || !eventArgs.GetCurrentPoint(_folderFiles).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (eventArgs.Source is Control control && (control is ToggleButton || control.GetVisualAncestors().OfType<ToggleButton>().Any()))
        {
            return;
        }

        if (ExplorerItem(eventArgs.Source)?.DataContext is not WorkspaceTreeNode node)
        {
            return;
        }

        _explorerDragNode   = node;
        _explorerDragRoot   = _folder;
        _explorerDragOrigin = eventArgs.GetPosition(_folderFiles);
        _explorerDragPress  = eventArgs;
    }

    private async void ExplorerPointerMoved(object?          sender
                                          , PointerEventArgs eventArgs)
    {
        if (_explorerDragging || _explorerDragPress is not { } press || _explorerDragNode is null || !eventArgs.GetCurrentPoint(_folderFiles).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var distance = eventArgs.GetPosition(_folderFiles) - _explorerDragOrigin;
        if (Math.Abs(distance.X) < 6 && Math.Abs(distance.Y) < 6)
        {
            return;
        }

        _explorerDragging     = true;
        _suppressExplorerOpen = true;
        _explorerDragToken    = Guid.NewGuid().ToString("N");
        using var transfer = new DataTransfer();
        var item = new DataTransferItem();
        item.Set(ExplorerMoveFormat, _explorerDragToken);
        transfer.Add(item);
        try
        {
            await DragDrop.DoDragDropAsync(press, transfer, DragDropEffects.Move);
        }
        catch (Exception error)
        {
            _status.Text = "Drag did not complete: " + error.Message;
        }
        finally
        {
            _explorerDragging  = false;
            _explorerDragToken = null;
            _explorerDragPress = null;
            _explorerDragNode  = null;
            ClearExplorerDropTarget();
        }
    }

    private string? ExplorerDropFolder(DragEventArgs eventArgs)
    {
        if (_busy || _closing || _explorerDragToken is null || _folder != _explorerDragRoot || eventArgs.DataTransfer.TryGetValue(ExplorerMoveFormat) != _explorerDragToken)
        {
            return null;
        }

        var item = ExplorerItem(eventArgs.Source);
        return item is null ? "" : item.DataContext is WorkspaceTreeNode { IsDocument: false } folder ? folder.RelativePath : null;
    }

    private bool CanDropExplorerItem(string folderRelative)
    {
        if (_folder is not { } root || _explorerDragNode is not { } node)
        {
            return false;
        }

        try
        {
            var source = _workspaceOperations.Resolve(root, node.RelativePath);
            var destination = _workspaceOperations.Resolve(root, Path.Combine(folderRelative, node.Name));
            return !WorkspacePathBoundary.Contains(source, destination, includeRoot: true) && !File.Exists(destination) && !Directory.Exists(destination);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void ExplorerDragOver(object?       sender
                                , DragEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        ClearExplorerDropTarget();
        var folder = ExplorerDropFolder(eventArgs);
        eventArgs.DragEffects = folder is not null && CanDropExplorerItem(folder) ? DragDropEffects.Move : DragDropEffects.None;
        if (eventArgs.DragEffects != DragDropEffects.Move)
        {
            return;
        }

        _explorerDropTarget = ExplorerItem(eventArgs.Source);
        _explorerDropTarget?.Classes.Add("move-target");
        if (_explorerDropTarget is not null)
        {
            _explorerDropTarget.IsExpanded = true;
        }

        _status.Text          = "Move into " + (folder!.Length == 0 ? "workspace root" : folder) + " — confirmation follows on drop.";
        _explorerScrollViewer = _folderFiles.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (_explorerScrollViewer is not { } scroll)
        {
            return;
        }

        var point = eventArgs.GetPosition(scroll);
        _explorerScrollDirection = point.Y < 32 ? -1 : point.Y > scroll.Bounds.Height - 32 ? 1 : 0;
        if (_explorerScrollDirection != 0)
        {
            _explorerScrollTimer.Start();
        }
    }

    private async void ExplorerDrop(object?       sender
                                  , DragEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        var folder = ExplorerDropFolder(eventArgs);
        var node = _explorerDragNode;
        var valid = folder is not null && CanDropExplorerItem(folder);
        ClearExplorerDropTarget();
        if (!valid || node is null)
        {
            return;
        }

        await RunAsync(() => MoveExplorerItemAsync(node, Path.Combine(folder!, node.Name)));
    }

    private void ClearExplorerDropTarget()
    {
        _explorerScrollTimer.Stop();
        _explorerDropTarget?.Classes.Remove("move-target");
        _explorerDropTarget = null;
    }
}

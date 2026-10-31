using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Avalonia.Interactivity;
using Osynt.Desktop.Services;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Views;

public partial class SidebarView : UserControl
{
    private Button? _pressedRequest;
    private Point _pressPoint;
    private LibraryRequest? _draggedRequest;
    private Control? _dropTarget;
    private Guid? _sourceFolder;
    private DateTimeOffset _hoverStarted;
    private double _scrollY = double.NaN;

    public SidebarView()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(LibraryScroll, true);
        AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            if (!e.Handled)
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
            }
            var point = e.GetPosition(LibraryScroll);
            _scrollY = point.X >= 0 && point.X <= LibraryScroll.Bounds.Width && point.Y >= 0 && point.Y <= LibraryScroll.Bounds.Height ? point.Y : double.NaN;
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (!e.Handled)
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DragLeaveEvent, (_, e) =>
        {
            var point = e.GetPosition(LibraryScroll);
            if (!new Rect(LibraryScroll.Bounds.Size).Contains(point))
                _scrollY = double.NaN;
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerPressedEvent, RequestPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, RequestMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, _) => _pressedRequest = null, RoutingStrategies.Tunnel);
    }

    private void RequestPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressedRequest = null;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var control = e.Source as Control;
        _pressedRequest = control as Button ?? control?.GetVisualAncestors().OfType<Button>().FirstOrDefault();
        if (_pressedRequest?.DataContext is not LibraryRequest)
            _pressedRequest = null;
        _pressPoint = e.GetPosition(this);
    }

    private async void RequestMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedRequest is not { DataContext: LibraryRequest request } row || _draggedRequest is not null ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            (Math.Abs(e.GetPosition(this).X - _pressPoint.X) < 6 && Math.Abs(e.GetPosition(this).Y - _pressPoint.Y) < 6))
            return;
        _pressedRequest = null;
        _draggedRequest = request;
        _sourceFolder = (DataContext as WorkspaceViewModel)?.Library.GetAllCollections()
            .FirstOrDefault(c => c.Requests.Any(r => r.Id == request.Document.Id))?.Id;
        row.Classes.Add("dragging");
        e.Handled = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += (_, _) =>
        {
            if (_dropTarget?.DataContext is LibraryCollection folder &&
                DateTimeOffset.UtcNow - _hoverStarted > TimeSpan.FromMilliseconds(550))
                folder.Expanded = true;
            var delta = _scrollY < 32 ? -10 : _scrollY > LibraryScroll.Bounds.Height - 32 ? 10 : 0;
            if (delta != 0)
                LibraryScroll.Offset = new Vector(0, LibraryScroll.Offset.Y + delta);
        };
        timer.Start();
        try
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText(request.Name));
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }
        finally
        {
            timer.Stop();
            ClearDropTarget();
            row.Classes.Remove("dragging");
            _draggedRequest = null;
            _sourceFolder = null;
            _scrollY = double.NaN;
        }
    }

    private bool CanDrop(Control? target) => _draggedRequest is not null && _sourceFolder is { } source &&
        target?.DataContext is LibraryCollection folder && folder.Id != source;

    private void FolderDragOver(object? sender, DragEventArgs e)
    {
        var target = sender as Control;
        e.DragEffects = CanDrop(target) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
        _scrollY = e.GetPosition(LibraryScroll).Y;
        if (!CanDrop(target))
        {
            ClearDropTarget();
            return;
        }
        if (_dropTarget == target)
        {
            if (DateTimeOffset.UtcNow - _hoverStarted > TimeSpan.FromMilliseconds(550) && target?.DataContext is LibraryCollection folder)
                folder.Expanded = true;
            return;
        }
        ClearDropTarget();
        _dropTarget = target;
        _hoverStarted = DateTimeOffset.UtcNow;
        target!.Classes.Add("drop-target");
    }

    private void FolderDragLeave(object? sender, DragEventArgs e)
    {
        if (_dropTarget == sender)
            ClearDropTarget();
    }

    private void FolderDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        e.DragEffects = DragDropEffects.None;
        if (sender is Control target && CanDrop(target) && target.DataContext is LibraryCollection folder &&
            DataContext is WorkspaceViewModel workspace && _draggedRequest is { } request)
        {
            try
            {
                if (workspace.Library.MoveRequest(request.Document.Id, folder.Id))
                {
                    e.DragEffects = DragDropEffects.Move;
                    var destination = workspace.Library.Collections.FirstOrDefault(c => c.Id == folder.Id);
                    if (destination is not null)
                        destination.Expanded = true;
                    workspace.Status.Notice = $"Moved {request.Name} to {folder.Name}.";
                }
            }
            catch (InvalidOperationException error) { workspace.Status.Notice = error.Message; }
        }
        ClearDropTarget();
    }

    private void ClearDropTarget()
    {
        _dropTarget?.Classes.Remove("drop-target");
        _dropTarget = null;
    }

    private void ClearSearchClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel workspace)
            workspace.Library.Search = "";
        SearchBox.Focus();
    }
    private async void CreateFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window owner && DataContext is WorkspaceViewModel workspace)
            await new RenameItemWindow("New folder", "", workspace.Library.CreateCollection).ShowDialog(owner);
    }
    private void DuplicateRequestClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: LibraryRequest request } || DataContext is not WorkspaceViewModel workspace)
            return;
        try
        {
            if (workspace.Library.DuplicateRequest(request.Document.Id) is { } duplicate)
                workspace.Open(duplicate);
        }
        catch (InvalidOperationException error) { workspace.Status.Notice = error.Message; }
    }
    private async void MoveRequestClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: LibraryRequest request } &&
            TopLevel.GetTopLevel(this) is Window owner && DataContext is WorkspaceViewModel workspace)
            await new MoveRequestWindow(workspace.Library, request).ShowDialog(owner);
    }
    private async void RenameClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: LibraryCollection collection } &&
            TopLevel.GetTopLevel(this) is Window owner && DataContext is WorkspaceViewModel workspace)
            await new RenameItemWindow("Rename folder", collection.Name,
                name => workspace.Library.RenameCollection(collection.Id, name)).ShowDialog(owner);
    }
    private async void RenameRequestClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: LibraryRequest request } &&
            TopLevel.GetTopLevel(this) is Window owner && DataContext is WorkspaceViewModel workspace)
            await new RenameItemWindow("Rename request", request.Name,
                name => workspace.Library.RenameRequest(request.Document.Id, name)).ShowDialog(owner);
    }
    private async void DeleteCollectionClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: LibraryCollection collection } &&
            TopLevel.GetTopLevel(this) is Window owner && DataContext is WorkspaceViewModel workspace)
            await new RequestDeleteDialogs(owner, workspace.Library).DeleteCollectionAsync(collection);
    }
    private async void DeleteRequestClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: LibraryRequest request } &&
            TopLevel.GetTopLevel(this) is Window owner && DataContext is WorkspaceViewModel workspace)
            await new RequestDeleteDialogs(owner, workspace.Library).DeleteRequestAsync(request);
    }
    private async void ImportClicked(object? sender, RoutedEventArgs e)
    {
        if (WorkspaceFileDialogs.For(this) is { } dialogs)
            await dialogs.ImportAsync();
    }
    private async void ExportClicked(object? sender, RoutedEventArgs e)
    {
        if (WorkspaceFileDialogs.For(this) is { } dialogs)
            await dialogs.ExportAsync();
    }
}

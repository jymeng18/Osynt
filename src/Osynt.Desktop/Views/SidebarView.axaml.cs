using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Desktop.Services;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Views;

public partial class SidebarView : UserControl
{
    public SidebarView() => InitializeComponent();
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

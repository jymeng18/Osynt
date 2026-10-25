using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Desktop.Services;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Views;

public partial class SidebarView : UserControl
{
    public SidebarView() => InitializeComponent();
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

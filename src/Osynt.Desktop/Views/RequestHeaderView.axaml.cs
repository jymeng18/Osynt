using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Desktop.ViewModels;
using Osynt.Desktop.Services;

namespace Osynt.Desktop.Views;

public partial class RequestHeaderView : UserControl
{
    public RequestHeaderView() => InitializeComponent();
    private async void ImportCurlClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel workspace && TopLevel.GetTopLevel(this) is Window owner)
            await new ImportCurlWindow(workspace.Commands).ShowDialog(owner);
    }
    private async void CopyCurlClicked(object? sender, RoutedEventArgs e)
    {
        if (WorkspaceFileDialogs.For(this) is { } dialogs)
            await dialogs.CopyCurlAsync();
    }
    private async void EnvironmentClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel workspace && TopLevel.GetTopLevel(this) is Window owner)
            await new EnvironmentWindow(workspace).ShowDialog(owner);
    }
}

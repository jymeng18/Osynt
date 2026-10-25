using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Desktop.Services;

namespace Osynt.Desktop.Views;

public partial class SidebarView : UserControl
{
    public SidebarView() => InitializeComponent();
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

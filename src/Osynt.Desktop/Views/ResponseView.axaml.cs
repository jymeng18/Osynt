using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Desktop.Services;

namespace Osynt.Desktop.Views;

public partial class ResponseView : UserControl
{
    public ResponseView() => InitializeComponent();
    private async void CopyResponseClicked(object? sender, RoutedEventArgs e)
    {
        if (WorkspaceFileDialogs.For(this) is { } dialogs)
            await dialogs.CopyResponseAsync();
    }
    private async void SaveResponseClicked(object? sender, RoutedEventArgs e)
    {
        if (WorkspaceFileDialogs.For(this) is { } dialogs)
            await dialogs.SaveResponseAsync();
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Desktop.Services;

namespace Osynt.Desktop.Views;

public partial class ResponseView : UserControl
{
    public ResponseView() => InitializeComponent();
    private void FindClicked(object? sender, RoutedEventArgs e) => ResponseCode.ShowFind();
    private async void CopyHeadersClicked(object? sender, RoutedEventArgs e)
    {
        if (WorkspaceFileDialogs.For(this) is { } dialogs)
            await dialogs.CopyResponseHeadersAsync();
    }
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

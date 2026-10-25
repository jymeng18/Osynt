using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Desktop.Services;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Views;

public partial class RequestTabsView : UserControl
{
    public RequestTabsView() => InitializeComponent();
    private async void CloseTabClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RequestTab tab } && TopLevel.GetTopLevel(this) is Window owner && DataContext is WorkspaceViewModel workspace)
            await new RequestCloseDialogs(owner, workspace).CloseRequestAsync(tab);
    }
    private void DiscardTabClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: RequestTab tab } && DataContext is WorkspaceViewModel workspace)
            workspace.Requests.Discard(tab);
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Views;

public partial class RequestHeaderView : UserControl
{
    public RequestHeaderView() => InitializeComponent();
    private async void EnvironmentClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel workspace && TopLevel.GetTopLevel(this) is Window owner)
            await new EnvironmentWindow(workspace).ShowDialog(owner);
    }
}

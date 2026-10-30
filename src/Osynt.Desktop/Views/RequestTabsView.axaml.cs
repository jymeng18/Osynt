using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Osynt.Desktop.Services;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Views;

public partial class RequestTabsView : UserControl
{
    private RequestTab? _lastRevealedTab;
    private double _lastWidth;
    public RequestTabsView()
    {
        InitializeComponent();
        LayoutUpdated += (_, _) => RevealSelectedTab();
        TabScroll.AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (TabScroll.Extent.Width <= TabScroll.Viewport.Width)
                return;
            TabScroll.Offset = new Vector(TabScroll.Offset.X - (e.Delta.X + e.Delta.Y) * 80, 0);
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
    }
    private void RevealSelectedTab()
    {
        if (!IsEffectivelyVisible || DataContext is not WorkspaceViewModel workspace ||
            (_lastRevealedTab == workspace.Requests.Selected && _lastWidth == TabScroll.Viewport.Width))
            return;
        var button = this.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.CommandParameter == workspace.Requests.Selected);
        if (button is null || button.Bounds.Width <= 0)
            return;
        _lastRevealedTab = workspace.Requests.Selected;
        _lastWidth = TabScroll.Viewport.Width;
        // Include the close button when revealing a tab selected by keyboard or the sidebar.
        (button.Parent as Control)?.BringIntoView();
    }
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

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Views;

public partial class RequestEditorView : UserControl
{
    public RequestEditorView() => InitializeComponent();
    private void AddRowClicked(object? sender, RoutedEventArgs e)
    {
        // The button command adds the row after Click. Wait for its controls to be laid out.
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is not WorkspaceViewModel workspace)
                return;
            var document = workspace.Requests.Selected.Document;
            var rows = workspace.Editor.BodyActive ? document.FormFields :
                workspace.Editor.HeadersActive ? document.Headers : document.Parameters;
            var input = this.GetVisualDescendants().OfType<TextBox>()
                .FirstOrDefault(c => c.IsEffectivelyVisible && c.DataContext == rows.LastOrDefault() && Grid.GetColumn(c) == 1);
            input?.BringIntoView();
            input?.Focus();
        }, DispatcherPriority.Loaded);
    }
}

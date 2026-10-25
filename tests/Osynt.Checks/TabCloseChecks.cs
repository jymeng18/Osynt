using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Desktop;
using Osynt.Desktop.ViewModels;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunTabCloseChecks(string directory, RequestExecutor executor, DemoServer demo)
    {
        using var workspace = CreateWorkspace(Path.Combine(directory, "close-dialogs"), executor, demo.BaseUrl);
        var window = new MainWindow { DataContext = workspace };
        window.Show();
        Check("Discard in the close dialog removes an unsaved request without saving", () =>
        {
            workspace.NewRequest();
            var tab = workspace.Requests.Selected;
            tab.Document.Name = "Discard this request";
            Click(window, CloseButton(window, tab));
            var dialog = window.OwnedWindows.Single();
            Click(dialog, dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Discard"));
            PumpUntil(() => !workspace.Requests.Tabs.Contains(tab));
            True(workspace.Library.Collections.SelectMany(c => c.Requests).All(r => r.Document.Id != tab.Document.Id));
        });
        Check("The tab context menu discards an unsaved request without saving", () =>
        {
            workspace.NewRequest();
            var tab = workspace.Requests.Selected;
            tab.Document.Name = "Discard from menu";
            var close = CloseButton(window, tab);
            var menu = close.ContextMenu!;
            menu.Open(close);
            Dispatcher.UIThread.RunJobs();
            var item = menu.Items.OfType<MenuItem>().Single();
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.Close();
            True(!workspace.Requests.Tabs.Contains(tab));
        });
        Check("Discarding the last request leaves a clean empty tab", () =>
        {
            foreach (var other in workspace.Requests.Tabs.Skip(1).ToArray())
                workspace.Requests.Discard(other);
            var tab = workspace.Requests.Selected;
            tab.Document.Name = "Last unsaved request";
            Click(window, CloseButton(window, tab));
            var dialog = window.OwnedWindows.Single();
            Click(dialog, dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Discard"));
            PumpUntil(() => !workspace.Requests.Tabs.Contains(tab));
            Equal(1, workspace.Requests.Tabs.Count);
            True(!workspace.Requests.Selected.IsDirty);
        });
        foreach (var dialog in window.OwnedWindows.ToArray())
            dialog.Close();
        foreach (var tab in workspace.Requests.Tabs)
            tab.IsDirty = false;
        window.Close();
    }

    private static Button CloseButton(Window window, RequestTab tab)
    {
        Dispatcher.UIThread.RunJobs();
        return window.GetVisualDescendants().OfType<Button>().Single(b => ReferenceEquals(b.Tag, tab));
    }
}

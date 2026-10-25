using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Checks.Fakes;
using Osynt.Desktop;
using Osynt.Desktop.Composition;
using Osynt.Desktop.ViewModels;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunDeletionDesktopChecks(string directory, RequestExecutor executor, DemoServer demo)
    {
        using var workspace = CreateWorkspace(Path.Combine(directory, "delete-ui"), executor, demo.BaseUrl);
        var window = new MainWindow { DataContext = workspace };
        window.Show();
        Check("Cancelling request deletion keeps the saved request and unsaved tab edits", () =>
        {
            var request = workspace.Library.Collections[0].Requests[0];
            workspace.Open(request.Document);
            var tab = workspace.Requests.Selected;
            tab.Document.Name = "Unsaved changes";
            var row = RequestRow(window, request.Document.Id);
            var dialog = OpenDeleteMenu(window, row);
            Click(dialog, DialogButton(dialog, "Cancel"));
            PumpUntil(() => !window.OwnedWindows.Contains(dialog));
            True(workspace.Library.Collections.SelectMany(c => c.Requests).Any(r => r.Document.Id == request.Document.Id));
            True(workspace.Requests.Tabs.Contains(tab));
            Equal("Unsaved changes", tab.Document.Name);
        });
        Check("Deleting a request from its menu removes it and closes its edited tab", () =>
        {
            var request = workspace.Library.Collections[0].Requests[0];
            var tab = workspace.Requests.Tabs.Single(t => t.Document.Id == request.Document.Id);
            var dialog = OpenDeleteMenu(window, RequestRow(window, request.Document.Id));
            Click(dialog, DialogButton(dialog, "Delete"));
            PumpUntil(() => !workspace.Requests.Tabs.Contains(tab));
            True(workspace.Library.Collections.SelectMany(c => c.Requests).All(r => r.Document.Id != request.Document.Id));
            Equal("Request deleted.", workspace.Status.Notice);
        });
        Check("Folder deletion includes filtered-out requests and closes only that folder's tabs", () =>
        {
            var folder = workspace.Library.Collections[0];
            workspace.Open(folder.Requests[0].Document);
            var first = workspace.Requests.Selected;
            workspace.Open(folder.Requests[1].Document);
            var second = workspace.Requests.Selected;
            workspace.NewRequest();
            var unrelated = workspace.Requests.Selected;
            unrelated.Document.Name = "Keep this edit";
            workspace.Library.Search = folder.Requests[0].Name;
            Equal(1, workspace.Library.Collections.Single().Requests.Count);
            Dispatcher.UIThread.RunJobs();
            var row = window.GetVisualDescendants().OfType<Button>()
                .Single(b => b.Classes.Contains("collection") && b.DataContext is LibraryCollection c && c.Id == folder.Id);
            var dialog = OpenDeleteMenu(window, row);
            Click(dialog, DialogButton(dialog, "Delete"));
            PumpUntil(() => !workspace.Requests.Tabs.Contains(first));
            True(!workspace.Requests.Tabs.Contains(second));
            True(workspace.Requests.Tabs.Contains(unrelated));
            True(unrelated.IsDirty);
            Equal("Keep this edit", unrelated.Document.Name);
            workspace.Library.Search = "";
            True(workspace.Library.Collections.All(c => c.Id != folder.Id));
            Equal("Folder deleted.", workspace.Status.Notice);
        });
        foreach (var tab in workspace.Requests.Tabs)
            tab.IsDirty = false;
        window.Close();
        Check("A failed deletion keeps library entries and open tabs", CheckDesktopDeletionFailure);
    }

    private static void CheckDesktopDeletionFailure()
    {
        var store = new InMemoryWorkspaceStore { FailWrites = true };
        var executor = new StubRequestExecutor((_, _, _) => Task.FromResult(SuccessResponse()));
        using var workspace = WorkspaceComposition.Create(store, new JsonCollectionSerializer(), executor, "");
        var folder = workspace.Library.Collections[0];
        var request = folder.Requests[0];
        workspace.Open(request.Document);
        var tab = workspace.Requests.Selected;
        tab.Document.Name = "Keep unsaved changes";
        True(!workspace.Library.DeleteRequest(request.Document.Id));
        True(!workspace.Library.DeleteCollection(folder.Id));
        True(workspace.Requests.Tabs.Contains(tab));
        True(tab.IsDirty);
        Equal(folder.Requests.Count, workspace.Library.Collections.Single(c => c.Id == folder.Id).Requests.Count);
        True(workspace.Status.Notice.Contains("Could not save your workspace"));
    }

    private static Button RequestRow(Window window, Guid requestId)
    {
        Dispatcher.UIThread.RunJobs();
        return window.GetVisualDescendants().OfType<Button>().Single(b =>
            b.CommandParameter is LibraryRequest r && r.Document.Id == requestId);
    }

    private static DeleteConfirmationWindow OpenDeleteMenu(Window window, Button row)
    {
        var menu = row.ContextMenu!;
        menu.Open(row);
        Dispatcher.UIThread.RunJobs();
        menu.Items.OfType<MenuItem>().Single(i => i.Header as string == "Delete")
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.Close();
        return window.OwnedWindows.OfType<DeleteConfirmationWindow>().Single();
    }

    private static Button DialogButton(Window dialog, string label) =>
        dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == label);
}

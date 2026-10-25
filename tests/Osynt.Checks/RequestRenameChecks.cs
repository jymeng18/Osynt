using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Checks.Fakes;
using Osynt.Checks.Fixtures;
using Osynt.Desktop;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunRequestRenameChecks(string directory)
    {
        Check("Renaming a saved request changes only its name and survives restart", () =>
        {
            var store = new WorkspaceStore(Path.Combine(directory, "rename-request"));
            var workspace = new ApplicationWorkspace(store);
            var folder = workspace.Collections.GetCollections()[0];
            var original = folder.Requests[0];
            workspace.Collections.RenameRequest(original.Id, "  Renamed request  ");
            var restarted = new ApplicationWorkspace(store);
            var renamed = restarted.Collections.GetCollections().Single(c => c.Id == folder.Id).Requests.Single(r => r.Id == original.Id);
            Equal("Renamed request", renamed.Name);
            Equal(original.Method, renamed.Method);
            Equal(original.Url, renamed.Url);
            Equal(original.Body, renamed.Body);
            Equal(original.Parameters.Count, renamed.Parameters.Count);
            Equal(original.Headers.Count, renamed.Headers.Count);
        });
        Check("Invalid and failed request renames preserve the saved request", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store);
            var request = workspace.Collections.GetCollections()[0].Requests[0];
            Throws<ArgumentException>(() => workspace.Collections.RenameRequest(request.Id, "  "));
            Throws<InvalidOperationException>(() => workspace.Collections.RenameRequest(Guid.NewGuid(), "Missing"));
            store.FailWrites = true;
            Throws<IOException>(() => workspace.Collections.RenameRequest(request.Id, "Failed rename"));
            Equal(request.Name, workspace.Collections.GetCollections()[0].Requests[0].Name);
        });
    }

    private static void RunRequestRenameDesktopChecks(string directory, RequestExecutor executor, DemoServer demo)
    {
        var path = Path.Combine(directory, "request-rename-ui");
        using var workspace = CreateWorkspace(path, executor, demo.BaseUrl);
        var window = new MainWindow { DataContext = workspace };
        window.Show();
        Check("Right-click Rename updates the saved request and tab without losing unsaved edits", () =>
        {
            var original = workspace.Library.Collections[0].Requests[0];
            workspace.Open(original.Document);
            var tab = workspace.Requests.Selected;
            tab.Document.Body = "unsaved body";
            var dialog = OpenRequestRenameMenu(window, original.Document.Id);
            dialog.FindControl<TextBox>("NameInput")!.Text = "  Renamed from menu  ";
            Click(dialog, DialogButton(dialog, "Save"));
            PumpUntil(() => !window.OwnedWindows.Contains(dialog));
            Equal("Renamed from menu", tab.Document.Name);
            Equal("Renamed from menu", tab.TabTitle);
            Equal("unsaved body", tab.Document.Body);
            True(tab.IsDirty);
            var saved = workspace.Library.Collections[0].Requests.Single(r => r.Document.Id == original.Document.Id);
            Equal("Renamed from menu", saved.Name);
            Equal(original.Document.Body, saved.Document.Body);
            using var restarted = CreateWorkspace(path, executor, demo.BaseUrl);
            Equal("Renamed from menu", restarted.Library.Collections[0].Requests.Single(r => r.Document.Id == original.Document.Id).Name);
        });
        Check("Renaming a clean request keeps its tab clean", () =>
        {
            var request = workspace.Library.Collections[0].Requests[1];
            workspace.Open(request.Document);
            var tab = workspace.Requests.Selected;
            True(!tab.IsDirty);
            var dialog = OpenRequestRenameMenu(window, request.Document.Id);
            dialog.FindControl<TextBox>("NameInput")!.Text = "Clean rename";
            Click(dialog, DialogButton(dialog, "Save"));
            PumpUntil(() => !window.OwnedWindows.Contains(dialog));
            Equal("Clean rename", tab.Document.Name);
            True(!tab.IsDirty);
        });
        Check("A blank request name shows an error and Cancel leaves the name unchanged", () =>
        {
            var request = workspace.Library.Collections[0].Requests[1];
            var dialog = OpenRequestRenameMenu(window, request.Document.Id);
            dialog.FindControl<TextBox>("NameInput")!.Text = " ";
            Click(dialog, DialogButton(dialog, "Save"));
            True(dialog.FindControl<TextBlock>("ErrorLabel")!.IsEffectivelyVisible);
            Click(dialog, DialogButton(dialog, "Cancel"));
            PumpUntil(() => !window.OwnedWindows.Contains(dialog));
            Equal(request.Name, workspace.Library.Collections[0].Requests[1].Name);
        });
        foreach (var tab in workspace.Requests.Tabs)
            tab.IsDirty = false;
        window.Close();
    }

    private static RenameItemWindow OpenRequestRenameMenu(Window window, Guid requestId)
    {
        var menu = RequestRow(window, requestId).ContextMenu!;
        menu.Open(RequestRow(window, requestId));
        Dispatcher.UIThread.RunJobs();
        menu.Items.OfType<MenuItem>().Single(i => i.Header as string == "Rename")
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.Close();
        return window.OwnedWindows.OfType<RenameItemWindow>().Single();
    }
}

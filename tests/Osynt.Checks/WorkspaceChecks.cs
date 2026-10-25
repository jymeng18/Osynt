using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Application.Workspaces;
using Osynt.Application.Requests;
using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;
using Osynt.Infrastructure.Demo;
using Osynt.Infrastructure.Http;
using Osynt.Infrastructure.Persistence;
using Osynt.Desktop;
using Osynt.Desktop.ViewModels;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunWorkspaceChecks(string directory, RequestExecutor executor, DemoServer demo)
    {
        Check("Workspace requests and history survive a restart", () =>
        {
            var store = new WorkspaceStore(Path.Combine(directory, "persistence"));
            var state = StarterWorkspace.Create();
            state.Collections[0].Requests[0].Name = "Saved crop";
            state.History.Add(new()
            {
                Request = state.Collections[0].Requests[0].Clone(),
                StatusCode = 200
            });
            store.Save(state);
            var loaded = store.Load()!;
            Equal("Saved crop", loaded.Collections[0].Requests[0].Name);
            Equal(1, loaded.History.Count);
        });
        Check("Unreadable workspaces are preserved before the next save", () =>
        {
            var path = Path.Combine(directory, "corrupt");
            Directory.CreateDirectory(path);
            var store = new WorkspaceStore(path);
            File.WriteAllText(store.FilePath, "{not json");
            var state = store.Load() ?? StarterWorkspace.Create();
            True(store.LoadNotice is not null);
            store.Save(state);
            True(Directory.GetFiles(path, "workspace-unreadable-*.json").Length == 1);
        });
        Check("Editing a request leaves its saved collection unchanged until Save", () =>
        {
            using var workspace = CreateWorkspace(Path.Combine(directory, "editing"), executor, demo.BaseUrl);
            workspace.Open(workspace.Library.Collections[0].Requests[0].Document);
            var original = workspace.Library.Collections[0].Requests[0].Name;
            workspace.Requests.Selected.Document.Name = "My new crop";
            True(workspace.Requests.Selected.IsDirty);
            Equal(original, workspace.Library.Collections[0].Requests[0].Name);
            workspace.Requests.SaveSelected();
            True(!workspace.Requests.Selected.IsDirty);
            Equal("My new crop", workspace.Library.Collections[0].Requests[0].Name);
        });
        Check("Unsaved tabs are protected and saved tabs close", () =>
        {
            using var workspace = CreateWorkspace(Path.Combine(directory, "tabs"), executor, demo.BaseUrl);
            workspace.NewRequest();
            var tab = workspace.Requests.Selected;
            workspace.Requests.Close(tab);
            True(workspace.Requests.Tabs.Contains(tab));
            workspace.Requests.SaveSelected();
            workspace.Requests.Close(tab);
            True(!workspace.Requests.Tabs.Contains(tab));
        });
        Check("Import/export round-trips collections and assigns fresh request IDs", () =>
        {
            using var workspace = CreateWorkspace(Path.Combine(directory, "import"), executor, demo.BaseUrl);
            var original = workspace.Library.Collections[0].Requests[0].Document.Id;
            workspace.Library.ImportCollections(workspace.Library.ExportCollections());
            Equal(4, workspace.Library.Collections.Count);
            True(original != workspace.Library.Collections[2].Requests[0].Document.Id);
            Throws<JsonException>(() => workspace.Library.ImportCollections("[{\"Name\":\"broken\",\"Requests\":null}]"));
            Equal(4, workspace.Library.Collections.Count);
        });
        Check("Duplicate environment variable names do not overwrite the environment", () =>
        {
            using var workspace = CreateWorkspace(Path.Combine(directory, "environment"), executor, demo.BaseUrl);
            Throws<ArgumentException>(() => workspace.Environment.UpdateVariables([new() { Key = "base_url", Value = "one" }, new() { Key = "base_url", Value = "two" }]));
            Equal(demo.BaseUrl, workspace.Environment.Variables["base_url"]);
        });

    }
}

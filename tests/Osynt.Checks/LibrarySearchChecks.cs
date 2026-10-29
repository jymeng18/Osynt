using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Osynt.Application.Workspaces;
using Osynt.Checks.Fakes;
using Osynt.Desktop.ViewModels;
using Osynt.Domain.Workspaces;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunLibrarySearchChecks()
    {
        var state = new WorkspaceState
        {
            Collections = [new() { Name = "Billing API", Requests = [new() { Name = "Create invoice", Method = "POST", Url = "https://example.com/invoices" },
                new() { Name = "List invoices", Url = "https://example.com/invoices" }] }, new() { Name = "Empty folder" }]
        };
        var session = new WorkspaceSession(new InMemoryWorkspaceStore(state), "");
        var history = new HistoryService(session, TimeProvider.System);
        var library = new LibraryViewModel(new CollectionService(session, new JsonCollectionSerializer()), history, new WorkspaceStatus());
        Check("Library search combines folder, method, name, and URL terms without case sensitivity", () =>
        {
            library.Search = " billing POST example.com ";
            Equal(1, library.Collections.Count);
            Equal("Create invoice", library.Collections[0].Requests.Single().Name);
            library.Search = "empty";
            Equal("Empty folder", library.Collections.Single().Name);
            library.Search = " \t ";
            Equal(2, library.Collections.Count);
            True(!library.HasSearch);
        });
        Check("Search expands matching folders temporarily and preserves collapsed state across excluded results", () =>
        {
            library.Search = "";
            var billing = library.Collections[0];
            billing.Expanded = false;
            library.Search = "invoice";
            True(library.Collections[0].Expanded);
            library.Search = "no-such-request";
            True(library.EmptyLibrary);
            Equal("No matching requests", library.EmptyLibraryTitle);
            library.Search = "";
            True(!library.Collections.Single(c => c.Id == billing.Id).Expanded);
        });
        Check("History search matches method and URL and reports filtered empty results", () =>
        {
            history.Record(state.Collections[0].Requests[0], 201, 5);
            library.RefreshHistory();
            library.ShowHistory = true;
            library.Search = "POST invoices";
            Equal(1, library.VisibleHistory.Count());
            True(!library.EmptyLibrary);
            library.Search = "DELETE";
            True(library.EmptyLibrary && library.HasHistory);
            library.Search = "";
            library.ClearHistoryCommand.Execute(null);
            True(library.EmptyLibrary);
            Equal("No request history yet", library.EmptyLibraryTitle);
        });
    }
}

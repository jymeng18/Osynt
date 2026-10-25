using Osynt.Application.Abstractions;
using Osynt.Application.Garden;
using Osynt.Application.Requests;
using Osynt.Application.Workspaces;
using Osynt.Desktop.ViewModels;

namespace Osynt.Desktop.Composition;

/// <summary>Wires one workspace lifetime. Views and view models never construct infrastructure.</summary>
public static class WorkspaceComposition
{
    public static WorkspaceViewModel Create(IWorkspaceStore store, ICollectionSerializer serializer,
        IRequestExecutor executor, string demoBaseUrl)
    {
        var session = new WorkspaceSession(store, demoBaseUrl);
        var collections = new CollectionService(session, serializer);
        var history = new HistoryService(session, TimeProvider.System);
        var environment = new EnvironmentService(session);
        var status = new WorkspaceStatus { Notice = session.LoadNotice ?? "Ready" };
        var requests = new RequestTabsViewModel(collections, status);
        var sending = new RequestSendingViewModel(new SendRequest(executor, environment, history), requests, status);
        return new WorkspaceViewModel(requests, sending, new RequestPaneViewModel(requests, status),
            new LibraryViewModel(collections, history, status), new EnvironmentViewModel(environment, status),
            new GardenViewModel(new GardenService(session), status), status);
    }
}

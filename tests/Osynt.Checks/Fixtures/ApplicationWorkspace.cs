using Osynt.Application.Abstractions;
using Osynt.Application.Garden;
using Osynt.Application.Requests;
using Osynt.Application.Workspaces;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks.Fixtures;

internal sealed class ApplicationWorkspace
{
    public ApplicationWorkspace(IWorkspaceStore store, string demoBaseUrl = "")
    {
        var session = new WorkspaceSession(store, demoBaseUrl);
        Collections = new CollectionService(session, new JsonCollectionSerializer());
        History = new HistoryService(session, TimeProvider.System);
        Environment = new EnvironmentService(session);
        Garden = new GardenService(session);
    }

    public CollectionService Collections
    {
        get;
    }
    public HistoryService History
    {
        get;
    }
    public EnvironmentService Environment
    {
        get;
    }
    public GardenService Garden
    {
        get;
    }
    public SendRequest Sender(IRequestExecutor executor) => new(executor, Environment, History);
}

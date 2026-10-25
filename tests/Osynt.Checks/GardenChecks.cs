using Osynt.Application.Garden;
using Osynt.Application.Workspaces;
using Osynt.Checks.Fakes;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunGardenChecks()
    {
        Check("Every 2xx response grows the garden exactly once; errors and redirects do not", () =>
        {
            var workspace = new WorkspaceService(new InMemoryWorkspaceStore(), new JsonCollectionSerializer(), "");
            foreach (var status in new[] { 200, 201, 204, 299, 300, 304, 400, 404, 500, 0 })
                workspace.RecordHistory(new RequestDocument(), status, 1);
            Equal(4L, workspace.Garden.SuccessfulRequests);
            True(workspace.Garden.LastGrownAt is not null);
        });
        Check("Garden progress survives history limits, clearing history, and restarting", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new WorkspaceService(store, new JsonCollectionSerializer(), "");
            for (var i = 0; i < 125; i++)
                workspace.RecordHistory(new RequestDocument(), 200, 1);
            Equal(100, workspace.History.Count);
            Equal(125L, workspace.Garden.SuccessfulRequests);
            workspace.ClearHistory();
            var restarted = new WorkspaceService(store, new JsonCollectionSerializer(), "");
            Equal(0, restarted.History.Count);
            Equal(125L, restarted.Garden.SuccessfulRequests);
        });
        Check("Older workspaces migrate retained successes once", () =>
        {
            var state = StarterWorkspace.Create();
            foreach (var status in new[] { 200, 201, 204, 302, 404 })
                state.History.Add(new()
                {
                    StatusCode = status
                });
            var store = new InMemoryWorkspaceStore(state);
            var workspace = new WorkspaceService(store, new JsonCollectionSerializer(), "");
            Equal(3L, workspace.Garden.SuccessfulRequests);
            workspace.RecordHistory(new RequestDocument(), 200, 1);
            var restarted = new WorkspaceService(store, new JsonCollectionSerializer(), "");
            Equal(4L, restarted.Garden.SuccessfulRequests);
        });
        Check("A failed save does not partially grant garden growth", () =>
        {
            var store = new InMemoryWorkspaceStore { FailWrites = true };
            var workspace = new WorkspaceService(store, new JsonCollectionSerializer(), "");
            Throws<IOException>(() => workspace.RecordHistory(new RequestDocument(), 200, 1));
            Equal(0L, workspace.Garden.SuccessfulRequests);
            Equal(0, workspace.History.Count);
            store.FailWrites = false;
            workspace.RecordHistory(new RequestDocument(), 200, 1);
            Equal(1L, workspace.Garden.SuccessfulRequests);
        });
        Check("Garden preferences persist without granting growth", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new WorkspaceService(store, new JsonCollectionSerializer(), "");
            workspace.UpdateGardenPreferences("dusk", true);
            var restarted = new WorkspaceService(store, new JsonCollectionSerializer(), "");
            Equal("dusk", restarted.Garden.Atmosphere);
            True(restarted.Garden.ReducedMotion);
            Equal(0L, restarted.Garden.SuccessfulRequests);
            Throws<ArgumentException>(() => restarted.UpdateGardenPreferences("unknown", false));
        });
        Check("Milestones, levels, and three-step plant growth remain consistent", () =>
        {
            Equal(1L, GardenProgress.Plants(1));
            Equal(1L, GardenProgress.Plants(3));
            Equal(2L, GardenProgress.Plants(4));
            Equal(1, GardenProgress.Level(0));
            Equal(2, GardenProgress.Level(5));
            Equal(3, GardenProgress.Level(20));
            Equal(5L, GardenProgress.NextMilestone(1)!.RequiredSuccesses);
            Equal("Home, grown", GardenProgress.Stage(40));
            True(GardenProgress.LevelProgress(21) is > 0 and < 100);
            Equal(1_000_001L, GardenProgress.Plants(3_000_001));
        });
    }
}

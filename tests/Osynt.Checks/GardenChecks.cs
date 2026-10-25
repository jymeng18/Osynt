using Osynt.Application.Garden;
using Osynt.Application.Workspaces;
using Osynt.Checks.Fakes;
using Osynt.Checks.Fixtures;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunGardenChecks()
    {
        Check("Every 2xx response grows the garden exactly once; errors and redirects do not", () =>
        {
            var workspace = new ApplicationWorkspace(new InMemoryWorkspaceStore(), "");
            foreach (var status in new[] { 200, 201, 204, 299, 300, 304, 400, 404, 500, 0 })
                workspace.History.Record(new RequestDocument(), status, 1);
            Equal(4L, workspace.Garden.GetState().SuccessfulRequests);
            True(workspace.Garden.GetState().LastGrownAt is not null);
        });
        Check("Garden progress survives history limits, clearing history, and restarting", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store, "");
            for (var i = 0; i < 125; i++)
                workspace.History.Record(new RequestDocument(), 200, 1);
            Equal(100, workspace.History.GetHistory().Count);
            Equal(125L, workspace.Garden.GetState().SuccessfulRequests);
            workspace.History.Clear();
            var restarted = new ApplicationWorkspace(store, "");
            Equal(0, restarted.History.GetHistory().Count);
            Equal(125L, restarted.Garden.GetState().SuccessfulRequests);
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
            var workspace = new ApplicationWorkspace(store, "");
            Equal(3L, workspace.Garden.GetState().SuccessfulRequests);
            workspace.History.Record(new RequestDocument(), 200, 1);
            var restarted = new ApplicationWorkspace(store, "");
            Equal(4L, restarted.Garden.GetState().SuccessfulRequests);
        });
        Check("A failed save does not partially grant garden growth", () =>
        {
            var store = new InMemoryWorkspaceStore { FailWrites = true };
            var workspace = new ApplicationWorkspace(store, "");
            Throws<IOException>(() => workspace.History.Record(new RequestDocument(), 200, 1));
            Equal(0L, workspace.Garden.GetState().SuccessfulRequests);
            Equal(0, workspace.History.GetHistory().Count);
            store.FailWrites = false;
            workspace.History.Record(new RequestDocument(), 200, 1);
            Equal(1L, workspace.Garden.GetState().SuccessfulRequests);
        });
        Check("Garden preferences persist without granting growth", () =>
        {
            var store = new InMemoryWorkspaceStore();
            var workspace = new ApplicationWorkspace(store, "");
            workspace.Garden.UpdatePreferences("dusk", true);
            var restarted = new ApplicationWorkspace(store, "");
            Equal("dusk", restarted.Garden.GetState().Atmosphere);
            True(restarted.Garden.GetState().ReducedMotion);
            Equal(0L, restarted.Garden.GetState().SuccessfulRequests);
            Throws<ArgumentException>(() => restarted.Garden.UpdatePreferences("unknown", false));
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
            Equal("Cottage", GardenProgress.Stage(40));
            True(GardenProgress.LevelProgress(21) is > 0 and < 100);
            Equal(1_000_001L, GardenProgress.Plants(3_000_001));
        });
    }
}

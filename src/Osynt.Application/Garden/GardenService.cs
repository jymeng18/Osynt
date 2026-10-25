using Osynt.Application.Workspaces;
using Osynt.Domain.Garden;

namespace Osynt.Application.Garden;

public sealed class GardenService(WorkspaceSession session)
{
    public GardenState GetState() => session.Read(state => state.Garden!.Clone());

    public void UpdatePreferences(string atmosphere, bool reducedMotion) =>
        session.Commit(state => state.Garden!.UpdatePreferences(atmosphere, reducedMotion));
}

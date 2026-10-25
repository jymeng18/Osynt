using Osynt.Application.Abstractions;
using Osynt.Domain.Workspaces;

namespace Osynt.Checks.Fakes;

internal sealed class InMemoryWorkspaceStore(WorkspaceState? initial = null) : IWorkspaceStore
{
    public string FilePath => "memory://workspace";
    public string? LoadNotice => null;
    public WorkspaceState? State { get; private set; } = initial?.Clone();
    public bool FailWrites
    {
        get; set;
    }
    public WorkspaceState? Load() => State?.Clone();
    public void Save(WorkspaceState state)
    {
        if (FailWrites)
            throw new IOException("Simulated disk failure");
        State = state.Clone();
    }
}

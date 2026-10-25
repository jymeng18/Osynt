using Osynt.Domain.Workspaces;

namespace Osynt.Application.Abstractions;

public interface IWorkspaceStore
{
    string FilePath
    {
        get;
    }
    string? LoadNotice
    {
        get;
    }
    WorkspaceState? Load();
    void Save(WorkspaceState state);
}

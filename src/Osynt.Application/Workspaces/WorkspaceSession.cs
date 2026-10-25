using Osynt.Application.Abstractions;
using Osynt.Domain.Garden;
using Osynt.Domain.Workspaces;

namespace Osynt.Application.Workspaces;

/// <summary>Owns the loaded workspace and serializes atomic, copy-on-write commits.</summary>
public sealed class WorkspaceSession
{
    private readonly IWorkspaceStore _store;
    private readonly object _gate = new();
    private WorkspaceState _state;

    public WorkspaceSession(IWorkspaceStore store, string demoBaseUrl)
    {
        _store = store;
        _state = (store.Load() ?? StarterWorkspace.Create()).Clone();
        _state.Garden ??= new GardenState
        {
            SuccessfulRequests = _state.History.LongCount(h => h.StatusCode is >= 200 and < 300)
        };
        DemoBaseUrl = demoBaseUrl;
        if (_state.UseLocalDemo || string.IsNullOrEmpty(_state.Variables.GetValueOrDefault("base_url")))
            _state.Variables["base_url"] = demoBaseUrl;
    }

    public string DemoBaseUrl
    {
        get;
    }
    public string StoragePath => _store.FilePath;
    public string? LoadNotice => _store.LoadNotice;

    // Queries stay inside the application layer and must return detached values.
    internal T Read<T>(Func<WorkspaceState, T> query)
    {
        lock (_gate)
            return query(_state);
    }

    internal void Commit(Action<WorkspaceState> change)
    {
        lock (_gate)
        {
            var candidate = _state.Clone();
            change(candidate);
            _store.Save(candidate);
            _state = candidate;
        }
    }
}

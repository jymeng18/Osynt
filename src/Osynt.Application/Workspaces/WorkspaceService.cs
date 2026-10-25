using Osynt.Application.Abstractions;
using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;
using Osynt.Domain.Garden;

namespace Osynt.Application.Workspaces;

/// <summary>Workspace use cases, independent of the desktop UI, HTTP stack, and file format.</summary>
public sealed class WorkspaceService
{
    public const int HistoryLimit = 100;
    private readonly IWorkspaceStore _store;
    private readonly ICollectionSerializer _serializer;
    private WorkspaceState _state;
    public WorkspaceService(IWorkspaceStore store, ICollectionSerializer serializer, string demoBaseUrl)
    {
        _store = store;
        _serializer = serializer;
        _state = store.Load() ?? StarterWorkspace.Create();
        // Migrate older workspaces once, from the successes still available in their history.
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
    public IReadOnlyList<RequestCollection> Collections => _state.Collections;
    public IReadOnlyList<HistoryEntry> History => _state.History;
    public IReadOnlyDictionary<string, string> Variables => _state.Variables;
    public GardenState Garden => _state.Garden!.Clone();

    public void SaveRequest(RequestDocument request)
    {
        Commit(state =>
        {
            var collection = state.Collections.FirstOrDefault(c => c.Requests.Any(r => r.Id == request.Id));
            if (collection is null)
            {
                collection = state.Collections.FirstOrDefault(c => c.Name == "My requests");
                if (collection is null)
                {
                    collection = new()
                    {
                        Name = "My requests"
                    };
                    state.Collections.Add(collection);
                }
            }
            var index = collection.Requests.FindIndex(r => r.Id == request.Id);
            if (index < 0)
                collection.Requests.Add(request.Clone());
            else
                collection.Requests[index] = request.Clone();
        });
    }
    public void RecordHistory(RequestDocument request, int statusCode, long elapsedMilliseconds)
    {
        Commit(state =>
        {
            state.History.Insert(0, new()
            {
                Request = request.Clone(),
                StatusCode = statusCode,
                ElapsedMilliseconds = elapsedMilliseconds
            });
            if (statusCode is >= 200 and < 300)
                state.Garden!.Grow(DateTimeOffset.Now);
            if (state.History.Count > HistoryLimit)
                state.History.RemoveRange(HistoryLimit, state.History.Count - HistoryLimit);
        });
    }
    public void ClearHistory()
    {
        Commit(state => state.History.Clear());
    }
    public void UpdateGardenPreferences(string atmosphere, bool reducedMotion)
    {
        if (atmosphere is not ("day" or "dusk"))
            throw new ArgumentException("Choose day or dusk.");
        Commit(state =>
        {
            state.Garden!.Atmosphere = atmosphere;
            state.Garden.ReducedMotion = reducedMotion;
        });
    }
    public void UpdateVariables(IEnumerable<RequestPair> pairs)
    {
        var values = pairs.Where(p => !string.IsNullOrWhiteSpace(p.Key)).ToList();
        if (values.GroupBy(p => p.Key.Trim(), StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new ArgumentException("Each environment variable needs a unique name.");
        Commit(state =>
        {
            state.Variables = values.ToDictionary(p => p.Key.Trim(), p => p.Value);
            state.UseLocalDemo = DemoBaseUrl.Length > 0 && state.Variables.GetValueOrDefault("base_url") == DemoBaseUrl;
        });
    }
    public string ExportCollections() => _serializer.Serialize(_state.Collections);
    public int ImportCollections(string text)
    {
        var collections = _serializer.Deserialize(text);
        Commit(state =>
        {
            foreach (var collection in collections)
            {
                foreach (var request in collection.Requests)
                    request.Id = Guid.NewGuid();
                state.Collections.Add(collection);
            }
        });
        return collections.Count;
    }

    private void Commit(Action<WorkspaceState> change)
    {
        var candidate = _state.Clone();
        change(candidate);
        _store.Save(candidate);
        _state = candidate;
    }
}

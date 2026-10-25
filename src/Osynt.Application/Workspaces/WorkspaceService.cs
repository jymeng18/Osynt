using Osynt.Application.Abstractions;
using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;

namespace Osynt.Application.Workspaces;

/// <summary>Workspace use cases, independent of the desktop UI, HTTP stack, and file format.</summary>
public sealed class WorkspaceService
{
    public const int HistoryLimit = 100;
    private readonly IWorkspaceStore _store;
    private readonly ICollectionSerializer _serializer;
    private readonly WorkspaceState _state;
    public WorkspaceService(IWorkspaceStore store, ICollectionSerializer serializer, string demoBaseUrl)
    {
        _store = store;
        _serializer = serializer;
        _state = store.Load() ?? StarterWorkspace.Create();
        DemoBaseUrl = demoBaseUrl;
        if (_state.UseLocalDemo || string.IsNullOrEmpty(_state.Variables.GetValueOrDefault("base_url")))
            _state.Variables["base_url"] = demoBaseUrl;
    }
    public string DemoBaseUrl { get; }
    public string StoragePath => _store.FilePath;
    public string? LoadNotice => _store.LoadNotice;
    public IReadOnlyList<RequestCollection> Collections => _state.Collections;
    public IReadOnlyList<HistoryEntry> History => _state.History;
    public IReadOnlyDictionary<string, string> Variables => _state.Variables;

    public void SaveRequest(RequestDocument request)
    {
        var collection = _state.Collections.FirstOrDefault(c => c.Requests.Any(r => r.Id == request.Id));
        if (collection is null)
        {
            collection = _state.Collections.FirstOrDefault(c => c.Name == "My requests");
            if (collection is null) { collection = new() { Name = "My requests" }; _state.Collections.Add(collection); }
        }
        var index = collection.Requests.FindIndex(r => r.Id == request.Id);
        if (index < 0) collection.Requests.Add(request.Clone());
        else collection.Requests[index] = request.Clone();
        _store.Save(_state);
    }
    public void RecordHistory(RequestDocument request, int statusCode, long elapsedMilliseconds)
    {
        _state.History.Insert(0, new() { Request = request.Clone(), StatusCode = statusCode, ElapsedMilliseconds = elapsedMilliseconds });
        if (_state.History.Count > HistoryLimit) _state.History.RemoveRange(HistoryLimit, _state.History.Count - HistoryLimit);
        _store.Save(_state);
    }
    public void ClearHistory() { _state.History.Clear(); _store.Save(_state); }
    public void UpdateVariables(IEnumerable<RequestPair> pairs)
    {
        var values = pairs.Where(p => !string.IsNullOrWhiteSpace(p.Key)).ToList();
        if (values.GroupBy(p => p.Key.Trim(), StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new ArgumentException("Each environment variable needs a unique name.");
        _state.Variables = values.ToDictionary(p => p.Key.Trim(), p => p.Value);
        _state.UseLocalDemo = DemoBaseUrl.Length > 0 && _state.Variables.GetValueOrDefault("base_url") == DemoBaseUrl;
        _store.Save(_state);
    }
    public string ExportCollections() => _serializer.Serialize(_state.Collections);
    public int ImportCollections(string text)
    {
        var collections = _serializer.Deserialize(text);
        foreach (var collection in collections)
        {
            foreach (var request in collection.Requests) request.Id = Guid.NewGuid();
            _state.Collections.Add(collection);
        }
        _store.Save(_state);
        return collections.Count;
    }
}

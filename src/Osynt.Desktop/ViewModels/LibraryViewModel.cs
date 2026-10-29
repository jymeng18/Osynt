using System.Collections.ObjectModel;
using System.Windows.Input;
using Osynt.Application.Workspaces;
using Osynt.Domain.Requests;

namespace Osynt.Desktop.ViewModels;

public sealed class LibraryViewModel : ObservableModel
{
    private readonly CollectionService _collections;
    private readonly HistoryService _history;
    private readonly WorkspaceStatus _status;
    private Guid _selectedId;
    private string _search = "";
    private bool _showHistory;
    private readonly Dictionary<Guid, bool> _expansion = [];
    private bool _collectionsFiltered;
    private string[] _searchTerms = [];

    public LibraryViewModel(CollectionService collections, HistoryService history, WorkspaceStatus status)
    {
        _collections = collections;
        _history = history;
        _status = status;
        NavigationCommand = new ActionCommand(p => ShowHistory = p?.ToString() == "History");
        ClearHistoryCommand = new ActionCommand(_ => ClearHistory());
        RefreshCollections();
        RefreshHistory();
    }

    public ObservableCollection<LibraryCollection> Collections { get; } = [];
    public event Action<IReadOnlySet<Guid>>? RequestsDeleted;
    public event Action<Guid, string>? RequestRenamed;
    public bool ConfirmDeletion => _collections.ConfirmDeletion;
    public ObservableCollection<HistoryItemViewModel> History { get; } = [];
    public ICommand NavigationCommand
    {
        get;
    }
    public ICommand ClearHistoryCommand
    {
        get;
    }
    public bool HasHistory => History.Count > 0;
    public bool HasSearch => _searchTerms.Length > 0;
    public bool EmptyLibrary => ShowHistory ? !VisibleHistory.Any() : Collections.Count == 0;
    public string EmptyLibraryTitle => HasSearch ? "No matching requests" : ShowHistory ? "No request history yet" : "No collections yet";
    public string EmptyLibraryHint => HasSearch ? "Try a folder name, method, or part of a URL." :
        ShowHistory ? "Send a request to see it here." : "Save a request or import a collection to get started.";
    public IEnumerable<HistoryItemViewModel> VisibleHistory => History.Where(h => Matches(h.Request));
    public string Search
    {
        get => _search;
        set
        {
            if (_search == value)
                return;
            _searchTerms = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Set(ref _search, value);
            RefreshCollections();
            Raise(nameof(VisibleHistory));
            RaiseEmptyState();
        }
    }
    public bool ShowHistory
    {
        get => _showHistory;
        set
        {
            Set(ref _showHistory, value);
            Raise(nameof(ShowCollections));
            RaiseEmptyState();
        }
    }
    public bool ShowCollections => !ShowHistory;

    public void Select(Guid requestId)
    {
        _selectedId = requestId;
        foreach (var request in Collections.SelectMany(c => c.Requests))
            request.IsSelected = request.Document.Id == requestId;
    }

    public void RefreshCollections()
    {
        if (!_collectionsFiltered)
            foreach (var collection in Collections)
                _expansion[collection.Id] = collection.Expanded;
        Collections.Clear();
        foreach (var collection in _collections.GetCollections())
        {
            var requests = collection.Requests.Where(r => Matches(r, collection.Name))
                .Select(r => new LibraryRequest(r) { IsSelected = _selectedId == r.Id }).ToList();
            if (requests.Count > 0 || !HasSearch || MatchesText(collection.Name))
                Collections.Add(new(collection.Id, collection.Name, requests)
                {
                    Expanded = HasSearch || _expansion.GetValueOrDefault(collection.Id, true)
                });
        }
        _collectionsFiltered = HasSearch;
        RaiseEmptyState();
    }

    public void RefreshHistory()
    {
        History.Clear();
        foreach (var entry in _history.GetHistory())
            History.Add(new(entry));
        Raise(nameof(VisibleHistory));
        Raise(nameof(HasHistory));
        RaiseEmptyState();
    }

    private bool Matches(RequestDocument request) => Matches(request, "");
    private bool Matches(RequestDocument request, string folder) => MatchesText($"{folder} {request.Method} {request.Name} {request.Url}");
    private bool MatchesText(string text) => _searchTerms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    private void RaiseEmptyState()
    {
        Raise(nameof(HasSearch));
        Raise(nameof(EmptyLibrary));
        Raise(nameof(EmptyLibraryTitle));
        Raise(nameof(EmptyLibraryHint));
    }

    private void ClearHistory()
    {
        if (_status.TrySave(_history.Clear, "History cleared."))
            RefreshHistory();
    }

    public string ExportCollections() => _collections.Export();

    public bool DeleteRequest(Guid requestId, bool dontAskAgain = false)
    {
        if (!_status.TrySave(() => _collections.DeleteRequest(requestId, dontAskAgain)))
            return false;
        FinishDeletion(new HashSet<Guid> { requestId }, "Request deleted.");
        return true;
    }

    public bool DeleteCollection(Guid collectionId, bool dontAskAgain = false)
    {
        IReadOnlyList<Guid> deletedRequests = [];
        if (!_status.TrySave(() => deletedRequests = _collections.DeleteCollection(collectionId, dontAskAgain)))
            return false;
        FinishDeletion(deletedRequests.ToHashSet(), "Folder deleted.");
        return true;
    }

    private void FinishDeletion(IReadOnlySet<Guid> requestIds, string notice)
    {
        RefreshCollections();
        RequestsDeleted?.Invoke(requestIds);
        _status.Notice = notice;
    }

    public bool RenameCollection(Guid collectionId, string name)
    {
        if (!_status.TrySave(() => _collections.Rename(collectionId, name), "Folder renamed."))
            return false;
        RefreshCollections();
        return true;
    }

    public bool RenameRequest(Guid requestId, string name)
    {
        if (!_status.TrySave(() => _collections.RenameRequest(requestId, name), "Request renamed."))
            return false;
        RequestRenamed?.Invoke(requestId, name.Trim());
        RefreshCollections();
        return true;
    }

    public void ImportCollections(string json)
    {
        var count = 0;
        if (!_status.TrySave(() => count = _collections.Import(json)))
            return;
        _status.Notice = $"Imported {count} collection(s).";
        RefreshCollections();
    }
}

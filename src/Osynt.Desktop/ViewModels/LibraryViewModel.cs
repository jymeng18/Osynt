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
    public IEnumerable<HistoryItemViewModel> VisibleHistory => History.Where(h => Matches(h.Request));
    public string Search
    {
        get => _search;
        set
        {
            if (!Set(ref _search, value))
                return;
            RefreshCollections();
            Raise(nameof(VisibleHistory));
        }
    }
    public bool ShowHistory
    {
        get => _showHistory;
        set
        {
            Set(ref _showHistory, value);
            Raise(nameof(ShowCollections));
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
        var collapsed = Collections.Where(c => !c.Expanded).Select(c => c.Name).ToHashSet();
        Collections.Clear();
        foreach (var collection in _collections.GetCollections())
        {
            var requests = collection.Requests.Where(Matches)
                .Select(r => new LibraryRequest(r) { IsSelected = _selectedId == r.Id }).ToList();
            if (requests.Count > 0 || string.IsNullOrEmpty(Search))
                Collections.Add(new(collection.Name, requests)
                {
                    Expanded = !collapsed.Contains(collection.Name)
                });
        }
    }

    public void RefreshHistory()
    {
        History.Clear();
        foreach (var entry in _history.GetHistory())
            History.Add(new(entry));
        Raise(nameof(VisibleHistory));
        Raise(nameof(HasHistory));
    }

    private bool Matches(RequestDocument request) =>
        request.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
        request.Url.Contains(Search, StringComparison.OrdinalIgnoreCase);

    private void ClearHistory()
    {
        if (_status.TrySave(_history.Clear, "History cleared. A fresh start."))
            RefreshHistory();
    }

    public string ExportCollections() => _collections.Export();

    public void ImportCollections(string json)
    {
        var count = 0;
        if (!_status.TrySave(() => count = _collections.Import(json)))
            return;
        _status.Notice = $"Imported {count} collection(s). Welcome to the garden.";
        RefreshCollections();
    }
}

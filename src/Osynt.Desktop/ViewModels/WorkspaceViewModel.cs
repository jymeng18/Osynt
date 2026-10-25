using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using Osynt.Application.Abstractions;
using Osynt.Application.Workspaces;
using Osynt.Domain.Requests;
using Osynt.Desktop.Presentation;

namespace Osynt.Desktop.ViewModels;

public sealed class WorkspaceViewModel : ObservableModel, IDisposable
{
    private readonly WorkspaceService _workspace;
    private readonly IRequestExecutor _executor;
    private RequestTab _selected = null!;
    private string _search = "";
    private string _notice = "Your little workspace is ready. Make yourself at home.";
    private string _editor = "Params";
    private string _responsePane = "Body";
    private bool _history;
    public ObservableCollection<LibraryCollection> Collections { get; } = [];
    public ObservableCollection<RequestTab> Tabs { get; } = [];
    public ObservableCollection<HistoryItemViewModel> History { get; } = [];
    public IReadOnlyList<string> Methods => RequestMethods.All;
    public IReadOnlyDictionary<string, string> Variables => _workspace.Variables;
    public string DemoBaseUrl => _workspace.DemoBaseUrl;
    public bool DemoAvailable => DemoBaseUrl.Length > 0;
    public string StoragePath => _workspace.StoragePath;
    public string Search
    {
        get => _search; set
        {
            Set(ref _search, value);
            RefreshLibrary();
            Raise(nameof(VisibleHistory));
        }
    }
    public string Notice
    {
        get => _notice; set => Set(ref _notice, value);
    }
    public bool ShowHistory
    {
        get => _history; set
        {
            Set(ref _history, value);
            Raise(nameof(ShowCollections));
        }
    }
    public bool ShowCollections => !ShowHistory;
    public string EnvironmentLabel => Variables.GetValueOrDefault("base_url") == DemoBaseUrl && DemoAvailable ? "Little farm · local" : "Custom environment";
    public string CollectionLabel => _workspace.Collections.FirstOrDefault(c => c.Requests.Any(r => r.Id == Selected.Document.Id))?.Name ?? "My requests";
    public IEnumerable<HistoryItemViewModel> VisibleHistory => History.Where(h => h.Request.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || h.Request.Url.Contains(Search, StringComparison.OrdinalIgnoreCase));
    public bool HasHistory => History.Count > 0;
    public bool ParamsActive => _editor == "Params";
    public bool HeadersActive => _editor == "Headers";
    public bool BodyActive => _editor == "Body";
    public bool NotesActive => _editor == "About";
    public bool ResponseBodyActive => _responsePane == "Body";
    public bool ResponseHeadersActive => _responsePane == "Headers";
    public RequestTab Selected
    {
        get => _selected;
        set
        {
            if (_selected is not null)
                _selected.IsSelected = false;
            _selected = value;
            Raise(nameof(Selected));
            value.IsSelected = true;
            Raise(nameof(CollectionLabel));
            foreach (var request in Collections.SelectMany(c => c.Requests))
                request.IsSelected = request.Document.Id == value.Document.Id;
        }
    }

    public ICommand OpenRequestCommand
    {
        get;
    }
    public ICommand SelectTabCommand
    {
        get;
    }
    public ICommand CloseTabCommand
    {
        get;
    }
    public ICommand NewRequestCommand
    {
        get;
    }
    public ICommand SaveCommand
    {
        get;
    }
    public ICommand SendCommand
    {
        get;
    }
    public ICommand EditorCommand
    {
        get;
    }
    public ICommand ResponsePaneCommand
    {
        get;
    }
    public ICommand AddPairCommand
    {
        get;
    }
    public ICommand RemovePairCommand
    {
        get;
    }
    public ICommand NavigationCommand
    {
        get;
    }
    public ICommand OpenHistoryCommand
    {
        get;
    }
    public ICommand ClearHistoryCommand
    {
        get;
    }
    public ICommand PrettyCommand
    {
        get;
    }
    public ICommand FormatBodyCommand
    {
        get;
    }

    public WorkspaceViewModel(WorkspaceService workspace, IRequestExecutor executor)
    {
        _workspace = workspace;
        _executor = executor;
        foreach (var history in workspace.History)
            History.Add(new(history));
        OpenRequestCommand = new ActionCommand(p => { if (p is LibraryRequest request) Open(request.Document); });
        SelectTabCommand = new ActionCommand(p => { if (p is RequestTab tab) Selected = tab; });
        CloseTabCommand = new ActionCommand(p => { if (p is RequestTab tab) Close(tab); });
        NewRequestCommand = new ActionCommand(_ => NewRequest());
        SaveCommand = new ActionCommand(_ => SaveSelected());
        SendCommand = new ActionCommand(_ => _ = SendSelectedAsync());
        EditorCommand = new ActionCommand(p => { _editor = p?.ToString() ?? "Params"; foreach (var name in new[] { nameof(ParamsActive), nameof(HeadersActive), nameof(BodyActive), nameof(NotesActive) }) Raise(name); });
        ResponsePaneCommand = new ActionCommand(p => { _responsePane = p?.ToString() ?? "Body"; Raise(nameof(ResponseBodyActive)); Raise(nameof(ResponseHeadersActive)); });
        AddPairCommand = new ActionCommand(_ => (HeadersActive ? Selected.Document.Headers : Selected.Document.Parameters).Add(new()));
        RemovePairCommand = new ActionCommand(p => { if (p is RequestPairViewModel pair) { Selected.Document.Parameters.Remove(pair); Selected.Document.Headers.Remove(pair); } });
        NavigationCommand = new ActionCommand(p => ShowHistory = p?.ToString() == "History");
        OpenHistoryCommand = new ActionCommand(p => { if (p is HistoryItemViewModel entry) { var tab = new RequestTab(entry.Request.Clone(true), true); Tabs.Add(tab); Selected = tab; } });
        ClearHistoryCommand = new ActionCommand(_ =>
        {
            if (TryPersist(_workspace.ClearHistory, "History cleared. A fresh start."))
                RefreshHistory();
        });
        PrettyCommand = new ActionCommand(p => Selected.Raw = p?.ToString() == "Raw");
        FormatBodyCommand = new ActionCommand(_ => FormatBody());
        var first = _workspace.Collections.SelectMany(c => c.Requests).FirstOrDefault() ?? new RequestDocument();
        Tabs.Add(new RequestTab(first.Clone()));
        Selected = Tabs[0];
        RefreshLibrary();
        if (workspace.LoadNotice is not null)
            Notice = workspace.LoadNotice;
    }

    public void NewRequest()
    {
        var tab = new RequestTab(new RequestDocument(), true);
        Tabs.Add(tab);
        Selected = tab;
        Notice = "A blank page, a new possibility. Save your request when it feels right.";
    }
    public void Open(RequestDocument document)
    {
        var tab = Tabs.FirstOrDefault(t => t.Document.Id == document.Id);
        if (tab is null)
        {
            tab = new RequestTab(document.Clone());
            Tabs.Add(tab);
        }
        Selected = tab;
    }
    public void Close(RequestTab tab)
    {
        if (tab.IsDirty)
        {
            Notice = "This request has unsaved changes. Save it before closing, or use Discard from the tab menu.";
            return;
        }
        DiscardTab(tab);
    }
    public void DiscardTab(RequestTab tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0)
            return;
        var wasSelected = Selected == tab;
        Tabs.Remove(tab);
        tab.Dispose();
        if (Tabs.Count == 0)
            NewRequest();
        else if (wasSelected)
            Selected = Tabs[Math.Clamp(index - 1, 0, Tabs.Count - 1)];
    }
    public void SaveSelected()
    {
        if (string.IsNullOrWhiteSpace(Selected.Document.Name))
            Selected.Document.Name = "Untitled request";
        if (TryPersist(() => _workspace.SaveRequest(Selected.Document.Clone()),
            $"Saved “{Selected.Document.Name}”. Tucked away for next time."))
            Selected.IsDirty = false;
        RefreshLibrary();
        Raise(nameof(CollectionLabel));
    }

    public async Task SendSelectedAsync()
    {
        var tab = Selected;
        if (tab.IsSending)
        {
            tab.Cancellation?.Cancel();
            return;
        }
        tab.Cancellation = new CancellationTokenSource();
        tab.IsSending = true;
        tab.Error = "";
        tab.Response = null;
        var snapshot = tab.Document.Clone();
        var variables = new Dictionary<string, string>(Variables);
        try
        {
            tab.Response = await _executor.SendAsync(snapshot, variables, tab.Cancellation.Token);
            AddHistory(snapshot, tab.Response.StatusCode, tab.Response.ElapsedMilliseconds);
            Notice = tab.Response.IsSuccess ? "Request delivered. A little progress, beautifully grown." : "Your API sent a response. There might be a little tending to do.";
        }
        catch (OperationCanceledException) { tab.Error = "Request cancelled. Take your time; your API will be here."; Notice = "Request cancelled."; }
        catch (Exception ex) when (ex is HttpRequestException or ArgumentException or TimeoutException or InvalidOperationException or IOException)
        {
            tab.Error = ex.Message;
            AddHistory(snapshot, 0, 0);
            Notice = "A little bump in the path. Check the request details and try again.";
        }
        finally { tab.IsSending = false; tab.Cancellation.Dispose(); tab.Cancellation = null; }
    }

    private void AddHistory(RequestDocument request, int code, long elapsed)
    {
        TryPersist(() => _workspace.RecordHistory(request, code, elapsed), null);
        RefreshHistory();
    }
    private void RefreshHistory()
    {
        History.Clear();
        foreach (var entry in _workspace.History)
            History.Add(new(entry));
        Raise(nameof(VisibleHistory));
        Raise(nameof(HasHistory));
    }
    public bool UpdateVariables(IEnumerable<RequestPair> pairs)
    {
        if (!TryPersist(() => _workspace.UpdateVariables(pairs), "Environment saved. A little change of scenery."))
            return false;
        Raise(nameof(Variables));
        Raise(nameof(EnvironmentLabel));
        return true;
    }
    public string ExportCollections() => _workspace.ExportCollections();
    public void ImportCollections(string json)
    {
        var count = 0;
        if (TryPersist(() => count = _workspace.ImportCollections(json), null))
            Notice = $"Imported {count} collection(s). Welcome to the garden.";
        RefreshLibrary();
    }
    private bool TryPersist(Action action, string? successMessage)
    {
        try
        {
            action();
            if (successMessage is not null)
                Notice = successMessage;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notice = $"Could not save your workspace: {ex.Message}";
            return false;
        }
    }
    private void FormatBody()
    {
        try
        {
            Selected.Document.Body = JsonPresentation.Format(Selected.Document.Body);
            Notice = "JSON, neatly arranged.";
        }
        catch (JsonException ex) { Notice = $"A little JSON typo: {ex.Message}"; }
    }
    private void RefreshLibrary()
    {
        var collapsed = Collections.Where(c => !c.Expanded).Select(c => c.Name).ToHashSet();
        Collections.Clear();
        foreach (var collection in _workspace.Collections)
        {
            var requests = collection.Requests.Where(r => r.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || r.Url.Contains(Search, StringComparison.OrdinalIgnoreCase))
                .Select(r => new LibraryRequest(r) { IsSelected = _selected?.Document.Id == r.Id }).ToList();
            if (requests.Count > 0 || string.IsNullOrEmpty(Search))
                Collections.Add(new(collection.Name, requests)
                {
                    Expanded = !collapsed.Contains(collection.Name)
                });
        }
    }
    public void Dispose()
    {
        foreach (var tab in Tabs)
            tab.Dispose();
    }
}

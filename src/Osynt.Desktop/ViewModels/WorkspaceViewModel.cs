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
    private string _notice = "Ready";
    private string _editor = "Params";
    private string _responsePane = "Body";
    private bool _history;
    public ObservableCollection<LibraryCollection> Collections { get; } = [];
    public GardenViewModel Garden
    {
        get;
    }
    public ICommand OpenGardenCommand
    {
        get;
    }
    public ICommand CloseGardenCommand
    {
        get;
    }
    public ICommand SendGardenDemoCommand
    {
        get;
    }
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
        Garden = new GardenViewModel(workspace.Garden);
        OpenGardenCommand = new ActionCommand(_ => Garden.IsOpen = true);
        CloseGardenCommand = new ActionCommand(_ => Garden.IsOpen = false);
        SendGardenDemoCommand = new ActionCommand(_ => _ = SendGardenDemoAsync());
        foreach (var history in workspace.History)
            History.Add(new(history));
        OpenRequestCommand = new ActionCommand(p => { if (p is LibraryRequest request) { Open(request.Document); Garden.IsOpen = false; } });
        SelectTabCommand = new ActionCommand(p => { if (p is RequestTab tab) Selected = tab; });
        CloseTabCommand = new ActionCommand(p => { if (p is RequestTab tab) Close(tab); });
        NewRequestCommand = new ActionCommand(_ => NewRequest());
        SaveCommand = new ActionCommand(_ => SaveSelected());
        SendCommand = new ActionCommand(_ => _ = SendSelectedAsync());
        EditorCommand = new ActionCommand(p => { _editor = p?.ToString() ?? "Params"; foreach (var name in new[] { nameof(ParamsActive), nameof(HeadersActive), nameof(BodyActive) }) Raise(name); });
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
        Tabs.Add(new RequestTab(new RequestDocument()));
        Selected = Tabs[0];
        RefreshLibrary();
        if (workspace.LoadNotice is not null)
            Notice = workspace.LoadNotice;
    }

    public void NewRequest()
    {
        Garden.IsOpen = false;
        var tab = new RequestTab(new RequestDocument(), true);
        Tabs.Add(tab);
        Selected = tab;
        Notice = "New request";
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
            $"Saved {Selected.Document.Name}"))
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
            if (AddHistory(snapshot, tab.Response.StatusCode, tab.Response.ElapsedMilliseconds))
                Notice = $"{snapshot.Method} {snapshot.Url} · {tab.Response.StatusCode} {tab.Response.Reason}";
        }
        catch (OperationCanceledException) { tab.Error = "Request cancelled."; Notice = "Request cancelled"; }
        catch (Exception ex) when (ex is HttpRequestException or ArgumentException or TimeoutException or InvalidOperationException or IOException)
        {
            tab.Error = ex.Message;
            AddHistory(snapshot, 0, 0);
            Notice = "Request failed — see the response pane for details";
        }
        finally { tab.IsSending = false; tab.Cancellation.Dispose(); tab.Cancellation = null; }
    }

    private bool AddHistory(RequestDocument request, int code, long elapsed)
    {
        var saved = TryPersist(() => _workspace.RecordHistory(request, code, elapsed), null);
        Garden.Refresh(_workspace.Garden);
        RefreshHistory();
        return saved;
    }
    public bool UpdateGardenPreferences(string atmosphere, bool reducedMotion)
    {
        if (!TryPersist(() => _workspace.UpdateGardenPreferences(atmosphere, reducedMotion), null))
            return false;
        Garden.Refresh(_workspace.Garden);
        return true;
    }
    private async Task SendGardenDemoAsync()
    {
        if (!DemoAvailable)
        {
            Notice = "The local demo is unavailable. You can send a request to your own API instead.";
            return;
        }
        if (Selected.IsSending)
            return;
        var existing = Tabs.FirstOrDefault(t => t.Document.Name == "Garden health check" && t.Document.Url == DemoBaseUrl + "/v1/health");
        if (existing is null)
        {
            existing = new RequestTab(new RequestDocument { Name = "Garden health check", Url = DemoBaseUrl + "/v1/health" });
            Tabs.Add(existing);
        }
        Selected = existing;
        await SendSelectedAsync();
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

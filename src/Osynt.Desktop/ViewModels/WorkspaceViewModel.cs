using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Input;
using Osynt.Core;

namespace Osynt.Desktop.ViewModels;

public sealed class ActionCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter);
}

public sealed class LibraryRequest(RequestDocument document) : ObservableModel
{
    private bool _selected;
    public RequestDocument Document { get; } = document;
    public string Name => Document.Name;
    public string Method => Document.Method;
    public string MethodColor => Method switch { "GET" => "#6E8C50", "POST" => "#BD9652", "DELETE" => "#B87966", _ => "#9A85AA" };
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }
}

public sealed class LibraryCollection(string name, IEnumerable<LibraryRequest> requests) : ObservableModel
{
    private bool _expanded = true;
    public string Name { get; } = name;
    public ObservableCollection<LibraryRequest> Requests { get; } = new(requests);
    public int Count => Requests.Count;
    public bool Expanded { get => _expanded; set { Set(ref _expanded, value); Raise(nameof(Chevron)); } }
    public string Chevron => Expanded ? "⌄" : "›";
    public ICommand ToggleCommand => new ActionCommand(_ => Expanded = !Expanded);
}

public sealed class RequestTab : ObservableModel, IDisposable
{
    private bool _dirty;
    private bool _selected;
    private bool _sending;
    private bool _raw;
    private string _error = "";
    private ResponseData? _response;
    private readonly HashSet<RequestPair> _observedPairs = [];
    public RequestDocument Document { get; }
    public CancellationTokenSource? Cancellation { get; set; }
    public RequestTab(RequestDocument document, bool dirty = false)
    {
        Document = document;
        _dirty = dirty;
        document.PropertyChanged += DocumentChanged;
        document.Parameters.CollectionChanged += PairsChanged;
        document.Headers.CollectionChanged += PairsChanged;
        ObservePairs();
    }
    public bool IsDirty { get => _dirty; set { Set(ref _dirty, value); Raise(nameof(TabTitle)); } }
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }
    public bool IsSending { get => _sending; set { Set(ref _sending, value); RaiseResponseProperties(); } }
    public bool Raw { get => _raw; set { Set(ref _raw, value); Raise(nameof(ResponseDisplay)); Raise(nameof(Pretty)); } }
    public bool Pretty => !Raw;
    public string TabTitle => Document.Name;
    public string MethodColor => Document.Method switch { "GET" => "#6E8C50", "POST" => "#BD9652", "DELETE" => "#B87966", _ => "#9A85AA" };
    public bool HasBody => !string.IsNullOrEmpty(Document.Body);
    public ResponseData? Response { get => _response; set { Set(ref _response, value); RaiseResponseProperties(); } }
    public string Error { get => _error; set { Set(ref _error, value); RaiseResponseProperties(); } }
    public bool HasError => Error.Length > 0;
    public bool HasResponse => Response is not null && !HasError;
    public bool ShowEmpty => !HasResponse && !HasError;
    public bool IsSuccess => Response?.IsSuccess == true;
    public bool IsFailure => Response is not null && !Response.IsSuccess;
    public string Status => Response is null ? "" : $"{Response.StatusCode} {Response.Reason}";
    public string Duration => Response is null ? "" : $"{Response.ElapsedMilliseconds} ms";
    public string Size => Response is null ? "" : Response.SizeBytes < 1024 ? $"{Response.SizeBytes} B" : $"{Response.SizeBytes / 1024d:0.0} KB";
    public string SendLabel => IsSending ? "Cancel" : "Send request";
    public string EmptyTitle => IsSending ? "Off on a little adventure…" : "Good things start with a request.";
    public string EmptyHint => IsSending ? "Waiting for your API to send something back." : "Hit Send request and see what grows.  Ctrl + Enter works, too.";
    public string ResponseDisplay
    {
        get
        {
            if (Response is null) return "";
            if (Raw) return Response.Body;
            try { using var json = JsonDocument.Parse(Response.Body); return JsonSerializer.Serialize(json.RootElement, WorkspaceStore.JsonOptions); }
            catch (JsonException) { return Response.Body; }
        }
    }
    public IReadOnlyList<KeyValuePair<string, string>> ResponseHeaders => Response?.Headers ?? [];
    public bool Truncated => Response?.Truncated == true;

    private void RaiseResponseProperties()
    {
        foreach (var name in new[] { nameof(HasResponse), nameof(ShowEmpty), nameof(HasError), nameof(IsSuccess), nameof(IsFailure), nameof(Status),
            nameof(Duration), nameof(Size), nameof(SendLabel), nameof(EmptyTitle), nameof(EmptyHint), nameof(ResponseDisplay), nameof(ResponseHeaders), nameof(Truncated) }) Raise(name);
    }
    private void DocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDirty = true;
        Raise(nameof(TabTitle)); Raise(nameof(MethodColor)); Raise(nameof(HasBody));
    }
    private void PairsChanged(object? sender, NotifyCollectionChangedEventArgs e) { ObservePairs(); IsDirty = true; }
    private void PairChanged(object? sender, PropertyChangedEventArgs e) => IsDirty = true;
    private void ObservePairs()
    {
        var current = Document.Parameters.Concat(Document.Headers).ToHashSet();
        foreach (var old in _observedPairs.Except(current).ToArray()) { old.PropertyChanged -= PairChanged; _observedPairs.Remove(old); }
        foreach (var pair in current.Except(_observedPairs).ToArray()) { pair.PropertyChanged += PairChanged; _observedPairs.Add(pair); }
    }
    public void Dispose()
    {
        Cancellation?.Cancel();
        Document.PropertyChanged -= DocumentChanged;
        Document.Parameters.CollectionChanged -= PairsChanged;
        Document.Headers.CollectionChanged -= PairsChanged;
        foreach (var pair in _observedPairs) pair.PropertyChanged -= PairChanged;
    }
}

public sealed class WorkspaceViewModel : ObservableModel, IDisposable
{
    private readonly WorkspaceStore _store;
    private readonly RequestExecutor _executor;
    private readonly WorkspaceState _state;
    private RequestTab _selected = null!;
    private string _search = "";
    private string _notice = "Your little workspace is ready. Make yourself at home.";
    private string _editor = "Params";
    private string _responsePane = "Body";
    private bool _history;
    public ObservableCollection<LibraryCollection> Collections { get; } = [];
    public ObservableCollection<RequestTab> Tabs { get; } = [];
    public ObservableCollection<HistoryEntry> History { get; } = [];
    public static string[] Methods { get; } = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];
    public Dictionary<string, string> Variables => _state.Variables;
    public string DemoBaseUrl { get; }
    public bool DemoAvailable => DemoBaseUrl.Length > 0;
    public string StoragePath => _store.FilePath;
    public string Search { get => _search; set { Set(ref _search, value); RefreshLibrary(); Raise(nameof(VisibleHistory)); } }
    public string Notice { get => _notice; set => Set(ref _notice, value); }
    public bool ShowHistory { get => _history; set { Set(ref _history, value); Raise(nameof(ShowCollections)); } }
    public bool ShowCollections => !ShowHistory;
    public string EnvironmentLabel => Variables.GetValueOrDefault("base_url") == DemoBaseUrl && DemoAvailable ? "Little farm · local" : "Custom environment";
    public string CollectionLabel => _state.Collections.FirstOrDefault(c => c.Requests.Any(r => r.Id == Selected.Document.Id))?.Name ?? "My requests";
    public IEnumerable<HistoryEntry> VisibleHistory => History.Where(h => h.Request.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || h.Request.Url.Contains(Search, StringComparison.OrdinalIgnoreCase));
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
            if (_selected is not null) _selected.IsSelected = false;
            _selected = value;
            Raise(nameof(Selected));
            value.IsSelected = true;
            Raise(nameof(CollectionLabel));
            foreach (var request in Collections.SelectMany(c => c.Requests)) request.IsSelected = request.Document.Id == value.Document.Id;
        }
    }

    public ICommand OpenRequestCommand { get; }
    public ICommand SelectTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand NewRequestCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SendCommand { get; }
    public ICommand EditorCommand { get; }
    public ICommand ResponsePaneCommand { get; }
    public ICommand AddPairCommand { get; }
    public ICommand RemovePairCommand { get; }
    public ICommand NavigationCommand { get; }
    public ICommand OpenHistoryCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand PrettyCommand { get; }
    public ICommand FormatBodyCommand { get; }

    public WorkspaceViewModel(WorkspaceStore store, RequestExecutor executor, string demoBaseUrl)
    {
        _store = store;
        _executor = executor;
        _state = store.Load();
        DemoBaseUrl = demoBaseUrl;
        // A loopback demo gets a fresh port on each launch; never persist that ephemeral address.
        if (_state.UseLocalDemo || string.IsNullOrEmpty(_state.Variables.GetValueOrDefault("base_url")))
            _state.Variables["base_url"] = demoBaseUrl;
        foreach (var history in _state.History.Take(100)) History.Add(history);
        OpenRequestCommand = new ActionCommand(p => { if (p is LibraryRequest request) Open(request.Document); });
        SelectTabCommand = new ActionCommand(p => { if (p is RequestTab tab) Selected = tab; });
        CloseTabCommand = new ActionCommand(p => { if (p is RequestTab tab) Close(tab); });
        NewRequestCommand = new ActionCommand(_ => NewRequest());
        SaveCommand = new ActionCommand(_ => SaveSelected());
        SendCommand = new ActionCommand(_ => _ = SendSelectedAsync());
        EditorCommand = new ActionCommand(p => { _editor = p?.ToString() ?? "Params"; foreach (var name in new[] { nameof(ParamsActive), nameof(HeadersActive), nameof(BodyActive), nameof(NotesActive) }) Raise(name); });
        ResponsePaneCommand = new ActionCommand(p => { _responsePane = p?.ToString() ?? "Body"; Raise(nameof(ResponseBodyActive)); Raise(nameof(ResponseHeadersActive)); });
        AddPairCommand = new ActionCommand(_ => (HeadersActive ? Selected.Document.Headers : Selected.Document.Parameters).Add(new()));
        RemovePairCommand = new ActionCommand(p => { if (p is RequestPair pair) { Selected.Document.Parameters.Remove(pair); Selected.Document.Headers.Remove(pair); } });
        NavigationCommand = new ActionCommand(p => ShowHistory = p?.ToString() == "History");
        OpenHistoryCommand = new ActionCommand(p => { if (p is HistoryEntry entry) { var tab = new RequestTab(entry.Request.Clone(true), true); Tabs.Add(tab); Selected = tab; } });
        ClearHistoryCommand = new ActionCommand(_ => { History.Clear(); _state.History.Clear(); Persist("History cleared. A fresh start."); Raise(nameof(VisibleHistory)); Raise(nameof(HasHistory)); });
        PrettyCommand = new ActionCommand(p => Selected.Raw = p?.ToString() == "Raw");
        FormatBodyCommand = new ActionCommand(_ => FormatBody());
        var first = _state.Collections.SelectMany(c => c.Requests).FirstOrDefault() ?? new RequestDocument();
        Tabs.Add(new RequestTab(first.Clone()));
        Selected = Tabs[0];
        RefreshLibrary();
        if (store.LoadNotice is not null) Notice = store.LoadNotice;
    }

    public void NewRequest()
    {
        var tab = new RequestTab(new RequestDocument(), true);
        Tabs.Add(tab); Selected = tab;
        Notice = "A blank page, a new possibility. Save your request when it feels right.";
    }
    public void Open(RequestDocument document)
    {
        var tab = Tabs.FirstOrDefault(t => t.Document.Id == document.Id);
        if (tab is null) { tab = new RequestTab(document.Clone()); Tabs.Add(tab); }
        Selected = tab;
    }
    public void Close(RequestTab tab)
    {
        if (tab.IsDirty) { Notice = "This request has unsaved changes. Save it before closing, or use Discard from the tab menu."; return; }
        DiscardTab(tab);
    }
    public void DiscardTab(RequestTab tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0) return;
        var wasSelected = Selected == tab;
        Tabs.Remove(tab); tab.Dispose();
        if (Tabs.Count == 0) NewRequest();
        else if (wasSelected) Selected = Tabs[Math.Clamp(index - 1, 0, Tabs.Count - 1)];
    }
    public void SaveSelected()
    {
        if (string.IsNullOrWhiteSpace(Selected.Document.Name)) Selected.Document.Name = "Untitled request";
        var collection = _state.Collections.FirstOrDefault(c => c.Requests.Any(r => r.Id == Selected.Document.Id));
        if (collection is null)
        {
            collection = _state.Collections.FirstOrDefault(c => c.Name == "My requests");
            if (collection is null) { collection = new() { Name = "My requests" }; _state.Collections.Add(collection); }
        }
        var index = collection.Requests.FindIndex(r => r.Id == Selected.Document.Id);
        if (index < 0) collection.Requests.Add(Selected.Document.Clone());
        else collection.Requests[index] = Selected.Document.Clone();
        if (Persist($"Saved “{Selected.Document.Name}”. Tucked away for next time.")) Selected.IsDirty = false;
        RefreshLibrary(); Raise(nameof(CollectionLabel));
    }

    public async Task SendSelectedAsync()
    {
        var tab = Selected;
        if (tab.IsSending) { tab.Cancellation?.Cancel(); return; }
        tab.Cancellation = new CancellationTokenSource();
        tab.IsSending = true; tab.Error = ""; tab.Response = null;
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
        History.Insert(0, new() { Request = request, StatusCode = code, ElapsedMilliseconds = elapsed });
        while (History.Count > 100) History.RemoveAt(History.Count - 1);
        _state.History = History.ToList();
        Persist(null); Raise(nameof(VisibleHistory)); Raise(nameof(HasHistory));
    }
    public void UpdateVariables(IEnumerable<RequestPair> pairs)
    {
        var values = pairs.Where(p => !string.IsNullOrWhiteSpace(p.Key)).ToList();
        if (values.GroupBy(p => p.Key.Trim(), StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new ArgumentException("Each environment variable needs a unique name.");
        Variables.Clear();
        foreach (var pair in values) Variables[pair.Key.Trim()] = pair.Value;
        _state.UseLocalDemo = DemoAvailable && Variables.GetValueOrDefault("base_url") == DemoBaseUrl;
        Persist("Environment saved. A little change of scenery.");
        Raise(nameof(EnvironmentLabel));
    }
    public string ExportCollections() => JsonSerializer.Serialize(_state.Collections, WorkspaceStore.JsonOptions);
    public void ImportCollections(string json)
    {
        var collections = JsonSerializer.Deserialize<List<RequestCollection>>(json, WorkspaceStore.JsonOptions)
            ?? throw new JsonException("No collections were found.");
        if (collections.Any(c => c is null || string.IsNullOrWhiteSpace(c.Name) || c.Requests is null || c.Requests.Any(r =>
            r is null || r.Name is null || r.Url is null || r.Body is null || r.Parameters is null || r.Headers is null || !Methods.Contains(r.Method))))
            throw new JsonException("This file contains an invalid collection or HTTP method.");
        foreach (var collection in collections)
        {
            foreach (var request in collection.Requests) request.Id = Guid.NewGuid();
            _state.Collections.Add(collection);
        }
        Persist($"Imported {collections.Count} collection(s). Welcome to the garden."); RefreshLibrary();
    }
    private bool Persist(string? successMessage)
    {
        try { _store.Save(_state); if (successMessage is not null) Notice = successMessage; return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Notice = $"Could not save your workspace: {ex.Message}"; return false; }
    }
    private void FormatBody()
    {
        try { using var json = JsonDocument.Parse(Selected.Document.Body); Selected.Document.Body = JsonSerializer.Serialize(json.RootElement, WorkspaceStore.JsonOptions); Notice = "JSON, neatly arranged."; }
        catch (JsonException ex) { Notice = $"A little JSON typo: {ex.Message}"; }
    }
    private void RefreshLibrary()
    {
        var collapsed = Collections.Where(c => !c.Expanded).Select(c => c.Name).ToHashSet();
        Collections.Clear();
        foreach (var collection in _state.Collections)
        {
            var requests = collection.Requests.Where(r => r.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || r.Url.Contains(Search, StringComparison.OrdinalIgnoreCase))
                .Select(r => new LibraryRequest(r) { IsSelected = _selected?.Document.Id == r.Id }).ToList();
            if (requests.Count > 0 || string.IsNullOrEmpty(Search)) Collections.Add(new(collection.Name, requests) { Expanded = !collapsed.Contains(collection.Name) });
        }
    }
    public void Dispose() { foreach (var tab in Tabs) tab.Dispose(); }
}

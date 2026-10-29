using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using Osynt.Domain.Requests;
using Osynt.Desktop.Presentation;

namespace Osynt.Desktop.ViewModels;

public sealed class RequestTab : ObservableModel, IDisposable
{
    private bool _dirty;
    private bool _selected;
    private bool _sending;
    private bool _raw;
    private string _error = "";
    private ResponseData? _response;
    private readonly HashSet<RequestPairViewModel> _observedPairs = [];
    public RequestEditorViewModel Document
    {
        get;
    }
    private CancellationTokenSource? _cancellation;

    internal CancellationTokenSource BeginSend()
    {
        _cancellation = new CancellationTokenSource();
        IsSending = true;
        Error = "";
        Response = null;
        return _cancellation;
    }

    internal void CancelSend() => _cancellation?.Cancel();

    internal void EndSend()
    {
        _cancellation = null;
        IsSending = false;
    }
    public RequestTab(RequestDocument document, bool dirty = false)
    {
        Document = new RequestEditorViewModel(document);
        _dirty = dirty;
        Document.PropertyChanged += DocumentChanged;
        Document.Parameters.CollectionChanged += PairsChanged;
        Document.Headers.CollectionChanged += PairsChanged;
        Document.FormFields.CollectionChanged += PairsChanged;
        ObservePairs();
    }
    public bool IsDirty
    {
        get => _dirty; set
        {
            Set(ref _dirty, value);
            Raise(nameof(TabTitle));
        }
    }
    public bool IsSelected
    {
        get => _selected; set => Set(ref _selected, value);
    }
    public bool IsSending
    {
        get => _sending; set
        {
            Set(ref _sending, value);
            RaiseResponseProperties();
        }
    }
    public bool Raw
    {
        get => _raw; set
        {
            Set(ref _raw, value);
            Raise(nameof(ResponseDisplay));
            Raise(nameof(Pretty));
        }
    }
    public bool Pretty => !Raw;
    public string TabTitle => Document.Name;
    public string MethodColor => Document.Method switch { "GET" => "#6E8C50", "POST" => "#BD9652", "DELETE" => "#B87966", _ => "#9A85AA" };
    public bool HasBody => Document.FormBody
        ? Document.FormFields.Any(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key))
        : !Document.NoBody && !string.IsNullOrEmpty(Document.Body);
    public ResponseData? Response
    {
        get => _response; set
        {
            Set(ref _response, value);
            RaiseResponseProperties();
        }
    }
    public string Error
    {
        get => _error; set
        {
            Set(ref _error, value);
            RaiseResponseProperties();
        }
    }
    public bool HasError => Error.Length > 0;
    public bool HasResponse => Response is not null && !HasError;
    public bool ShowEmpty => !HasResponse && !HasError;
    public bool IsSuccess => Response?.IsSuccess == true;
    public bool IsFailure => Response is not null && !Response.IsSuccess;
    public string Status => Response is null ? "" : $"{Response.StatusCode} {Response.Reason}";
    public string Duration => Response is null ? "" : $"{Response.ElapsedMilliseconds} ms";
    public string Size => Response is null ? "" : Response.SizeBytes < 1024 ? $"{Response.SizeBytes} B" : $"{Response.SizeBytes / 1024d:0.0} KB";
    public string SendLabel => IsSending ? "Cancel" : "Send";
    public string EmptyTitle => IsSending ? "Sending request…" : "Ready for your next request";
    public string EmptyHint => IsSending ? "Waiting for a response." : "Enter a URL and press Send.  Ctrl + Enter";
    public bool EmptyResponseBody => HasResponse && Response!.Body.Length == 0 && Response.BinaryBody is not { Length: > 0 };
    public bool BinaryResponse => HasResponse && Response!.IsBinary && !EmptyResponseBody;
    public bool ShowResponseBody => HasResponse && !EmptyResponseBody && !BinaryResponse;
    public bool CanCopyResponse => HasResponse && !BinaryResponse;
    public string ContentType => HasResponse ? Response!.ContentType is { Length: > 0 } type ? type : "Unknown content type" : "";
    public string ResponseDisplay
    {
        get
        {
            if (Response is null)
                return "";
            if (Raw)
                return Response.Body;
            try
            {
                return JsonPresentation.Format(Response.Body);
            }
            catch (JsonException) { return Response.Body; }
        }
    }
    public IReadOnlyList<KeyValuePair<string, string>> ResponseHeaders => Response?.Headers ?? [];
    public bool Truncated => Response?.Truncated == true;

    private void RaiseResponseProperties()
    {
        foreach (var name in new[] { nameof(HasResponse), nameof(ShowEmpty), nameof(HasError), nameof(IsSuccess), nameof(IsFailure), nameof(Status),
            nameof(Duration), nameof(Size), nameof(SendLabel), nameof(EmptyTitle), nameof(EmptyHint), nameof(ResponseDisplay), nameof(ResponseHeaders), nameof(Truncated), nameof(EmptyResponseBody), nameof(ShowResponseBody),
            nameof(BinaryResponse), nameof(CanCopyResponse), nameof(ContentType) })
            Raise(name);
    }
    private void DocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDirty = true;
        Raise(nameof(TabTitle));
        Raise(nameof(MethodColor));
        Raise(nameof(HasBody));
    }
    private void PairsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ObservePairs();
        IsDirty = true;
        Raise(nameof(HasBody));
    }
    private void PairChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDirty = true;
        Raise(nameof(HasBody));
    }
    private void ObservePairs()
    {
        var current = Document.Parameters.Concat(Document.Headers).Concat(Document.FormFields).ToHashSet();
        foreach (var old in _observedPairs.Except(current).ToArray())
        {
            old.PropertyChanged -= PairChanged;
            _observedPairs.Remove(old);
        }
        foreach (var pair in current.Except(_observedPairs).ToArray())
        {
            pair.PropertyChanged += PairChanged;
            _observedPairs.Add(pair);
        }
    }
    public void Dispose()
    {
        CancelSend();
        Document.PropertyChanged -= DocumentChanged;
        Document.Parameters.CollectionChanged -= PairsChanged;
        Document.Headers.CollectionChanged -= PairsChanged;
        Document.FormFields.CollectionChanged -= PairsChanged;
        foreach (var pair in _observedPairs)
            pair.PropertyChanged -= PairChanged;
        Document.Dispose();
    }
}


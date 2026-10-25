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
    public CancellationTokenSource? Cancellation
    {
        get; set;
    }
    public RequestTab(RequestDocument document, bool dirty = false)
    {
        Document = new RequestEditorViewModel(document);
        _dirty = dirty;
        Document.PropertyChanged += DocumentChanged;
        Document.Parameters.CollectionChanged += PairsChanged;
        Document.Headers.CollectionChanged += PairsChanged;
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
    public bool HasBody => !string.IsNullOrEmpty(Document.Body);
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
    public string SendLabel => IsSending ? "Cancel" : "Send request";
    public string EmptyTitle => IsSending ? "Off on a little adventure…" : "Good things start with a request.";
    public string EmptyHint => IsSending ? "Waiting for your API to send something back." : "Hit Send request and see what grows.  Ctrl + Enter works, too.";
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
            nameof(Duration), nameof(Size), nameof(SendLabel), nameof(EmptyTitle), nameof(EmptyHint), nameof(ResponseDisplay), nameof(ResponseHeaders), nameof(Truncated) })
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
    }
    private void PairChanged(object? sender, PropertyChangedEventArgs e) => IsDirty = true;
    private void ObservePairs()
    {
        var current = Document.Parameters.Concat(Document.Headers).ToHashSet();
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
        Cancellation?.Cancel();
        Document.PropertyChanged -= DocumentChanged;
        Document.Parameters.CollectionChanged -= PairsChanged;
        Document.Headers.CollectionChanged -= PairsChanged;
        foreach (var pair in _observedPairs)
            pair.PropertyChanged -= PairChanged;
    }
}


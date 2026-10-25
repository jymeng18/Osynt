using System.Text.Json;
using System.Windows.Input;
using Osynt.Desktop.Presentation;
using Osynt.Domain.Requests;

namespace Osynt.Desktop.ViewModels;

public sealed class RequestPaneViewModel : ObservableModel
{
    private readonly RequestTabsViewModel _requests;
    private readonly WorkspaceStatus _status;
    private string _editor = "Params";
    private string _responsePane = "Body";

    public RequestPaneViewModel(RequestTabsViewModel requests, WorkspaceStatus status)
    {
        _requests = requests;
        _status = status;
        EditorCommand = new ActionCommand(p => SelectEditor(p?.ToString() ?? "Params"));
        ResponsePaneCommand = new ActionCommand(p => SelectResponsePane(p?.ToString() ?? "Body"));
        AddPairCommand = new ActionCommand(_ => AddPair());
        RemovePairCommand = new ActionCommand(p => { if (p is RequestPairViewModel pair) RemovePair(pair); });
        PrettyCommand = new ActionCommand(p => _requests.Selected.Raw = p?.ToString() == "Raw");
        FormatBodyCommand = new ActionCommand(_ => FormatBody());
    }

    public IReadOnlyList<string> Methods => RequestMethods.All;
    public bool ParamsActive => _editor == "Params";
    public bool HeadersActive => _editor == "Headers";
    public bool BodyActive => _editor == "Body";
    public bool ResponseBodyActive => _responsePane == "Body";
    public bool ResponseHeadersActive => _responsePane == "Headers";
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
    public ICommand PrettyCommand
    {
        get;
    }
    public ICommand FormatBodyCommand
    {
        get;
    }

    private void SelectEditor(string pane)
    {
        _editor = pane;
        Raise(nameof(ParamsActive));
        Raise(nameof(HeadersActive));
        Raise(nameof(BodyActive));
    }

    private void SelectResponsePane(string pane)
    {
        _responsePane = pane;
        Raise(nameof(ResponseBodyActive));
        Raise(nameof(ResponseHeadersActive));
    }

    private void AddPair()
    {
        var document = _requests.Selected.Document;
        (HeadersActive ? document.Headers : document.Parameters).Add(new());
    }

    private void RemovePair(RequestPairViewModel pair)
    {
        var document = _requests.Selected.Document;
        document.Parameters.Remove(pair);
        document.Headers.Remove(pair);
    }

    private void FormatBody()
    {
        try
        {
            var document = _requests.Selected.Document;
            document.Body = JsonPresentation.Format(document.Body);
            _status.Notice = "JSON formatted.";
        }
        catch (JsonException ex) { _status.Notice = $"Invalid JSON: {ex.Message}"; }
    }
}

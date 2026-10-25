using System.Windows.Input;
using Osynt.Application.Requests;

namespace Osynt.Desktop.ViewModels;

/// <summary>Owns the presentation lifecycle of a send; application policy lives in SendRequest.</summary>
public sealed class RequestSendingViewModel
{
    private readonly SendRequest _send;
    private readonly RequestTabsViewModel _requests;
    private readonly WorkspaceStatus _status;

    public RequestSendingViewModel(SendRequest send, RequestTabsViewModel requests, WorkspaceStatus status)
    {
        _send = send;
        _requests = requests;
        _status = status;
        SendCommand = new ActionCommand(_ => _ = SendSelectedAsync());
    }

    public event Action? Completed;
    public ICommand SendCommand
    {
        get;
    }

    public async Task SendSelectedAsync()
    {
        var tab = _requests.Selected;
        if (tab.IsSending)
        {
            tab.CancelSend();
            return;
        }
        using var cancellation = tab.BeginSend();
        try
        {
            var result = await _send.ExecuteAsync(tab.Document.Clone(), cancellation.Token);
            tab.Response = result.Response;
            tab.Error = result.Error ?? "";
            ShowOutcome(result);
            Completed?.Invoke();
        }
        catch (OperationCanceledException)
        {
            tab.Error = "Request cancelled.";
            _status.Notice = "Request cancelled";
        }
        finally { tab.EndSend(); }
    }

    private void ShowOutcome(SendRequestResult result)
    {
        if (result.PersistenceError is not null)
            _status.ReportSaveFailure(result.PersistenceError);
        else if (result.Response is { } response)
            _status.Notice = $"{result.Request.Method} {result.Request.Url} · {response.StatusCode} {response.Reason}";
        else
            _status.Notice = "Request failed — see the response pane for details";
    }
}

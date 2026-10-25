using Osynt.Application.Abstractions;
using Osynt.Application.Workspaces;
using Osynt.Domain.Requests;

namespace Osynt.Application.Requests;

/// <summary>Executes a snapshot and records its outcome. Cancellation never enters history.</summary>
public sealed class SendRequest(IRequestExecutor executor, EnvironmentService environment, HistoryService history)
{
    public async Task<SendRequestResult> ExecuteAsync(RequestDocument request, CancellationToken cancellationToken = default)
    {
        var snapshot = request.Clone();
        var variables = environment.GetVariables();
        ResponseData? response = null;
        string? error = null;
        try
        {
            response = await executor.SendAsync(snapshot, variables, cancellationToken);
        }
        catch (RequestExecutionException ex)
        {
            error = ex.Message;
        }

        var persistenceError = RecordOutcome(snapshot, response);
        return new SendRequestResult(snapshot, response, error, persistenceError);
    }

    private string? RecordOutcome(RequestDocument request, ResponseData? response)
    {
        try
        {
            history.Record(request, response?.StatusCode ?? 0, response?.ElapsedMilliseconds ?? 0);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }
}

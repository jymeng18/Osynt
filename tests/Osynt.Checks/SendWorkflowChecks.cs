using Osynt.Application.Requests;
using Osynt.Checks.Fakes;
using Osynt.Checks.Fixtures;
using Osynt.Desktop.Composition;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunSendWorkflowChecks()
    {
        Check("Sending captures the document and environment before awaiting transport", CheckSendSnapshots);
        Check("Concurrent sends each record their own result and grow the garden once", CheckConcurrentSends);
        Check("Cancelled sends leave history and garden unchanged", CheckCancelledSend);
        Check("Execution failures enter history without granting growth", CheckFailedSend);
        Check("HTTP error responses remain responses and do not grant growth", CheckHttpFailure);
        Check("Failed history persistence preserves the response and does not retry transport", CheckFailedHistorySave);
        Check("Closing an in-flight tab cancels its request without recording history", CheckCloseDuringSend);
        Check("Failed desktop saves preserve dirty tabs and report persistence failures", CheckDesktopSaveFailure);
    }

    private static void CheckSendSnapshots()
    {
        var workspace = new ApplicationWorkspace(new InMemoryWorkspaceStore());
        workspace.Environment.Update([new() { Key = "season", Value = "spring" }]);
        var completion = new TaskCompletionSource<ResponseData>();
        RequestDocument? sent = null;
        IReadOnlyDictionary<string, string>? variables = null;
        var executor = new StubRequestExecutor((document, environment, _) =>
        {
            sent = document;
            variables = environment;
            return completion.Task;
        });
        var request = new RequestDocument { Name = "Original", Body = "original" };
        var operation = workspace.Sender(executor).ExecuteAsync(request);
        request.Name = "Edited while sending";
        request.Body = "edited";
        workspace.Environment.Update([new() { Key = "season", Value = "summer" }]);
        completion.SetResult(SuccessResponse());
        var result = operation.GetAwaiter().GetResult();
        Equal("Original", sent!.Name);
        Equal("original", sent.Body);
        Equal("spring", variables!["season"]);
        Equal("Original", result.Request.Name);
        Equal("Original", workspace.History.GetHistory().Single().Request.Name);
        Equal(1L, workspace.Garden.GetState().SuccessfulRequests);
    }

    private static void CheckConcurrentSends()
    {
        var workspace = new ApplicationWorkspace(new InMemoryWorkspaceStore());
        var first = new TaskCompletionSource<ResponseData>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<ResponseData>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new StubRequestExecutor((request, _, _) => request.Name == "first" ? first.Task : second.Task);
        var sender = workspace.Sender(executor);
        var sends = new[] { sender.ExecuteAsync(new() { Name = "first" }), sender.ExecuteAsync(new() { Name = "second" }) };
        second.SetResult(SuccessResponse());
        first.SetResult(SuccessResponse());
        Task.WhenAll(sends).GetAwaiter().GetResult();
        Equal(2, workspace.History.GetHistory().Count);
        Equal(2, workspace.History.GetHistory().Select(h => h.Request.Name).Distinct().Count());
        Equal(2L, workspace.Garden.GetState().SuccessfulRequests);
    }

    private static void CheckCancelledSend()
    {
        var workspace = new ApplicationWorkspace(new InMemoryWorkspaceStore());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var executor = new StubRequestExecutor((_, _, token) => Task.FromCanceled<ResponseData>(token));
        Throws<OperationCanceledException>(() => workspace.Sender(executor)
            .ExecuteAsync(new(), cancellation.Token).GetAwaiter().GetResult());
        Equal(0, workspace.History.GetHistory().Count);
        Equal(0L, workspace.Garden.GetState().SuccessfulRequests);
    }

    private static void CheckFailedSend()
    {
        var workspace = new ApplicationWorkspace(new InMemoryWorkspaceStore());
        var executor = new StubRequestExecutor((_, _, _) =>
            Task.FromException<ResponseData>(new RequestExecutionException("Connection failed")));
        var result = workspace.Sender(executor).ExecuteAsync(new()).GetAwaiter().GetResult();
        True(result.Response is null);
        Equal("Connection failed", result.Error);
        Equal(0, workspace.History.GetHistory().Single().StatusCode);
        Equal(0L, workspace.Garden.GetState().SuccessfulRequests);
    }

    private static void CheckHttpFailure()
    {
        var workspace = new ApplicationWorkspace(new InMemoryWorkspaceStore());
        var executor = new StubRequestExecutor((_, _, _) => Task.FromResult(new ResponseData(500, "Server Error", 10, 0, "", [])));
        var result = workspace.Sender(executor).ExecuteAsync(new()).GetAwaiter().GetResult();
        Equal(500, result.Response!.StatusCode);
        True(result.Error is null);
        Equal(500, workspace.History.GetHistory().Single().StatusCode);
        Equal(0L, workspace.Garden.GetState().SuccessfulRequests);
    }

    private static void CheckFailedHistorySave()
    {
        var store = new InMemoryWorkspaceStore { FailWrites = true };
        var workspace = new ApplicationWorkspace(store);
        var calls = 0;
        var executor = new StubRequestExecutor((_, _, _) => { calls++; return Task.FromResult(SuccessResponse()); });
        var result = workspace.Sender(executor).ExecuteAsync(new()).GetAwaiter().GetResult();
        Equal(200, result.Response!.StatusCode);
        True(result.Error is null);
        Equal("Simulated disk failure", result.PersistenceError);
        Equal(1, calls);
        Equal(0, workspace.History.GetHistory().Count);
        Equal(0L, workspace.Garden.GetState().SuccessfulRequests);
    }

    private static void CheckCloseDuringSend()
    {
        CancellationToken observed = default;
        var executor = new StubRequestExecutor(async (_, _, token) =>
        {
            observed = token;
            await Task.Delay(Timeout.Infinite, token);
            return SuccessResponse();
        });
        using var workspace = WorkspaceComposition.Create(new InMemoryWorkspaceStore(), new JsonCollectionSerializer(), executor, "");
        var tab = workspace.Requests.Selected;
        var sending = workspace.Sending.SendSelectedAsync();
        True(tab.IsSending);
        workspace.Requests.Discard(tab);
        sending.GetAwaiter().GetResult();
        True(observed.IsCancellationRequested);
        True(!tab.IsSending);
        True(!workspace.Requests.Tabs.Contains(tab));
        Equal(1, workspace.Requests.Tabs.Count);
        Equal(0, workspace.Library.History.Count);
        Equal(0L, workspace.Garden.Successes);
    }

    private static void CheckDesktopSaveFailure()
    {
        var store = new InMemoryWorkspaceStore { FailWrites = true };
        var executor = new StubRequestExecutor((_, _, _) => Task.FromResult(SuccessResponse()));
        using var workspace = WorkspaceComposition.Create(store, new JsonCollectionSerializer(), executor, "");
        workspace.Requests.Selected.Document.Name = "Unsaved request";
        workspace.Requests.SaveSelected();
        True(workspace.Requests.Selected.IsDirty);
        True(workspace.Library.Collections.SelectMany(c => c.Requests).All(r => r.Name != "Unsaved request"));
        workspace.Sending.SendSelectedAsync().GetAwaiter().GetResult();
        Equal(200, workspace.Requests.Selected.Response!.StatusCode);
        True(!workspace.Requests.Selected.HasError);
        True(workspace.Status.Notice.Contains("Could not save your workspace"));
        Equal(0, workspace.Library.History.Count);
        Equal(0L, workspace.Garden.Successes);
        store.FailWrites = false;
        workspace.Requests.SaveSelected();
        True(!workspace.Requests.Selected.IsDirty);
    }

    private static ResponseData SuccessResponse() => new(200, "OK", 10, 2, "{}", []);
}

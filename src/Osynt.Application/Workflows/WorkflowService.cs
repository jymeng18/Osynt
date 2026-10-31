using Osynt.Application.Requests;
using Osynt.Application.Workspaces;
using Osynt.Domain.Workflows;
using Osynt.Domain.Requests;

namespace Osynt.Application.Workflows;

public sealed class WorkflowService(WorkspaceSession session, SendRequest sender, EnvironmentService environment)
{
    private int running;
    public IReadOnlyList<Workflow> List() => session.Read(s => s.Workflows.Select(w => w.Clone()).ToList());
    public void Save(Workflow workflow)
    {
        Validate(workflow, false);
        session.Commit(s =>
        {
            var index = s.Workflows.FindIndex(w => w.Id == workflow.Id);
            if (index < 0)
                s.Workflows.Add(workflow.Clone());
            else
                s.Workflows[index] = workflow.Clone();
        });
    }
    public void Delete(Guid id) => session.Commit(s => s.Workflows.RemoveAll(w => w.Id == id));
    private static void Validate(Workflow workflow, bool forRun = true)
    {
        if (forRun && string.IsNullOrWhiteSpace(workflow.Name))
            throw new ArgumentException("Enter a workflow name.");
        if (forRun && !workflow.Steps.Any(s => s.Enabled))
            throw new ArgumentException("Add at least one request.");
        foreach (var step in workflow.Steps)
        {
            foreach (var capture in step.Captures)
                ResponseFields.Validate(capture);
            if (step.Captures.Select(c => c.Variable).Distinct(StringComparer.Ordinal).Count() != step.Captures.Count)
                throw new ArgumentException("Capture variable names must be unique within a step.");
        }
    }
    public async Task<SendRequestResult> SendStepAsync(WorkflowStep step, CancellationToken token = default, Action<SendRequestResult>? received = null)
    {
        var result = await sender.ExecuteAsync(step.Request, token);
        received?.Invoke(result);
        if (result.PersistenceError is not null)
            throw new IOException("Could not save request history.");
        if (result.Response is { StatusCode: >= 200 and < 300 } response && step.Captures.Count > 0)
            environment.Capture(ResponseFields.Extract(response, step.Captures));
        return result;
    }

    public void SaveCapture(Workflow workflow, int stepIndex, ResponseData response, ResponseCapture capture)
    {
        var values = ResponseFields.Extract(response, [capture]);
        var candidate = workflow.Clone();
        var step = candidate.Steps[stepIndex];
        step.Captures.RemoveAll(c => c.Variable == capture.Variable);
        step.Captures.Add(capture);
        Validate(candidate, false);
        session.Commit(state =>
        {
            var index = state.Workflows.FindIndex(w => w.Id == candidate.Id);
            if (index < 0)
                state.Workflows.Add(candidate.Clone());
            else
                state.Workflows[index] = candidate.Clone();
            foreach (var (name, value) in values)
                state.Variables[name] = value;
            state.UseLocalDemo = session.DemoBaseUrl.Length > 0 && state.Variables.GetValueOrDefault("base_url") == session.DemoBaseUrl;
        });
        workflow.Steps[stepIndex].Captures = step.Captures;
    }

    public async Task RunAsync(Workflow workflow, Action<int, SendRequestResult> completed, CancellationToken token = default)
    {
        if (Interlocked.Exchange(ref running, 1) != 0)
            throw new InvalidOperationException("A workflow is already running.");
        try
        {
            var snapshot = workflow.Clone();
            Validate(snapshot);
            for (var i = 0; i < snapshot.Steps.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var step = snapshot.Steps[i];
                if (!step.Enabled)
                    continue;
                var result = await sender.ExecuteAsync(step.Request, token);
                completed(i, result);
                token.ThrowIfCancellationRequested();
                if (result.Error is not null || result.Response is not { StatusCode: >= 200 and < 300 })
                    throw new ArgumentException($"Step {i + 1} failed ({result.Response?.StatusCode.ToString() ?? "request error"}). Workflow stopped.");
                if (result.PersistenceError is not null)
                    throw new IOException("Could not save request history. Workflow stopped.");
                if (step.Captures.Count > 0)
                    environment.Capture(ResponseFields.Extract(result.Response, step.Captures));
            }
        }
        finally { Volatile.Write(ref running, 0); }
    }
}

using Osynt.Application.Requests;
using Osynt.Application.Workspaces;
using Osynt.Domain.Workflows;

namespace Osynt.Application.Workflows;

public sealed class WorkflowService(WorkspaceSession session, SendRequest sender, EnvironmentService environment)
{
    private int running;
    public IReadOnlyList<Workflow> List() => session.Read(s => s.Workflows.Select(w => w.Clone()).ToList());
    public void Save(Workflow workflow)
    {
        Validate(workflow);
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
    private static void Validate(Workflow workflow)
    {
        if (string.IsNullOrWhiteSpace(workflow.Name))
            throw new ArgumentException("Enter a workflow name.");
        if (workflow.Steps.Count == 0)
            throw new ArgumentException("Add at least one request.");
        foreach (var step in workflow.Steps)
        {
            foreach (var capture in step.Captures)
                ResponseFields.Validate(capture);
            if (step.Captures.Select(c => c.Variable).Distinct(StringComparer.Ordinal).Count() != step.Captures.Count)
                throw new ArgumentException("Capture variable names must be unique within a step.");
        }
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

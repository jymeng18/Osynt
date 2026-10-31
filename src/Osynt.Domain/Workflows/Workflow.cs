using Osynt.Domain.Requests;

namespace Osynt.Domain.Workflows;

public sealed class Workflow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New workflow";
    public List<WorkflowStep> Steps { get; set; } = [];
    public Workflow Clone() => new() { Id = Id, Name = Name, Steps = Steps.Select(s => s.Clone()).ToList() };
}

public sealed class WorkflowStep
{
    public bool Enabled { get; set; } = true;
    public RequestDocument Request { get; set; } = new();
    public List<ResponseCapture> Captures { get; set; } = [];
    public WorkflowStep Clone() => new() { Enabled = Enabled, Request = Request.Clone(), Captures = Captures.Select(c => c with { }).ToList() };
}

// JSON Pointer identifies fields unambiguously, including keys containing dots or slashes.
public sealed record ResponseCapture(string Pointer, string Variable);

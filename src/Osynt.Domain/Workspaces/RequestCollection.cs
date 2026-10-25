using Osynt.Domain.Requests;

namespace Osynt.Domain.Workspaces;

public sealed class RequestCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "My collection";
    public List<RequestDocument> Requests { get; set; } = [];

    public RequestCollection Clone() => new()
    {
        Id = Id,
        Name = Name,
        Requests = Requests.Select(r => r.Clone()).ToList()
    };
}

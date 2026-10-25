using Osynt.Domain.Requests;
using Osynt.Domain.Workspaces;

namespace Osynt.Application.Workspaces;

public static class StarterWorkspace
{
    public static WorkspaceState Create() => new()
    {
        Collections =
        [
            new()
            {
                Name = "The little farm",
                Requests =
                [
                    new()
                    {
                        Name = "Get seasonal crops", Url = "{{base_url}}/v1/harvest",
                        Parameters = [new() { Key = "season", Value = "{{season}}" }],
                        Headers = [new() { Key = "Accept", Value = "application/json" }]
                    },
                    new() { Name = "Meet the villagers", Url = "{{base_url}}/v1/villagers" },
                    new()
                    {
                        Name = "Plant something new", Method = "POST", Url = "{{base_url}}/v1/plant",
                        Body = "{\n  \"crop\": \"strawberry\",\n  \"quantity\": 12\n}",
                        Headers = [new() { Key = "Content-Type", Value = "application/json" }]
                    },
                    new() { Name = "Check on the farm", Url = "{{base_url}}/v1/health" }
                ]
            },
            new()
            {
                Name = "Out in the world",
                Requests = [new() { Name = "Hello, JSONPlaceholder", Url = "https://jsonplaceholder.typicode.com/posts/1" }]
            }
        ]
    };
}

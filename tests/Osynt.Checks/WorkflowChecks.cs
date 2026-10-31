using Osynt.Application.Workflows;
using Osynt.Application.Workspaces;
using Osynt.Application.Requests;
using Osynt.Checks.Fakes;
using Osynt.Domain.Workflows;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunWorkflowChecks(string directory)
    {
        Check("Workflow captures a token before authenticated requests and persists definitions and variables", () =>
        {
            var store = new WorkspaceStore(Path.Combine(directory, "workflow.json"));
            var session = new WorkspaceSession(store, "");
            var environment = new EnvironmentService(session);
            environment.Update([new() { Key = "unrelated", Value = "keep" }]);
            var sent = new List<string>();
            var executor = new StubRequestExecutor((request, variables, _) =>
            {
                sent.Add(request.Name);
                if (request.Name != "Register")
                    Equal("new-token", TemplateResolver.Resolve(request.Authentication.Token, variables));
                return Task.FromResult(new ResponseData(200, "OK", 1, 0, "{\"data\":{\"access_token\":\"new-token\"}}", []));
            });
            var service = new WorkflowService(session, new SendRequest(executor, environment, new HistoryService(session, TimeProvider.System)), environment);
            var workflow = new Workflow
            {
                Name = "Authentication",
                Steps =
            [
                new() { Request = new() { Name = "Register" }, Captures = [new("/data/access_token", "access_token")] },
                new() { Request = new() { Name = "Gateway", Authentication = new() { Mode = "Bearer", Token = "{{access_token}}" } } },
                new() { Request = new() { Name = "Logout", Authentication = new() { Mode = "Bearer", Token = "{{access_token}}" } } }
            ]
            };
            service.Save(workflow);
            service.RunAsync(workflow, (_, _) => { }).GetAwaiter().GetResult();
            Equal("Register,Gateway,Logout", string.Join(',', sent));
            var reloaded = store.Load()!;
            Equal("new-token", reloaded.Variables["access_token"]);
            Equal("keep", reloaded.Variables["unrelated"]);
            Equal("/data/access_token", reloaded.Workflows.Single().Steps[0].Captures.Single().Pointer);
            workflow.Steps[0].Request.Name = "Edited";
            Equal("Register", service.List()[0].Steps[0].Request.Name);
        });
        Check("JSON field selection handles nested arrays and escaped property names", () =>
        {
            var response = new ResponseData(200, "OK", 1, 0, "{\"a/b\":{\"~key\":[{\"id\":42,\"active\":true}]}}", []);
            var fields = ResponseFields.List(response);
            True(fields.Contains("/a~1b/~0key/0/id"));
            var values = ResponseFields.Extract(response, [new("/a~1b/~0key/0/id", "id"), new("/a~1b/~0key/0/active", "active")]);
            Equal("42", values["id"]);
            Equal("true", values["active"]);
            Throws<ArgumentException>(() => ResponseFields.Extract(response, [new("/missing", "id")]));
            Throws<ArgumentException>(() => ResponseFields.Extract(response with { Truncated = true }, [new("/a~1b/~0key/0/id", "id")]));
        });
        Check("Workflow failures stop downstream requests and capture writes are atomic", () =>
        {
            foreach (var body in new[] { "{}", "not json", "{\"token\":null}", "{\"token\":\"\"}", "{\"token\":\"new\"}" })
            {
                var session = new WorkspaceSession(new InMemoryWorkspaceStore(), "");
                var environment = new EnvironmentService(session);
                environment.Update([new() { Key = "token", Value = "old" }]);
                var calls = 0;
                var executor = new StubRequestExecutor((_, _, _) => { calls++; return Task.FromResult(new ResponseData(200, "OK", 1, 0, body, [])); });
                var service = new WorkflowService(session, new SendRequest(executor, environment, new HistoryService(session, TimeProvider.System)), environment);
                var workflow = new Workflow { Steps = [new() { Captures = [new("/token", "token"), new("/missing", "second")] }, new()] };
                Throws<ArgumentException>(() => service.RunAsync(workflow, (_, _) => { }).GetAwaiter().GetResult());
                Equal(1, calls);
                Equal("old", environment.GetVariables()["token"]);
            }
        });
        Check("HTTP failure and cancellation stop workflow execution", () =>
        {
            var session = new WorkspaceSession(new InMemoryWorkspaceStore(), "");
            var environment = new EnvironmentService(session);
            var calls = 0;
            var executor = new StubRequestExecutor((_, _, _) => { calls++; return Task.FromResult(new ResponseData(401, "Unauthorized", 1, 0, "{}", [])); });
            var service = new WorkflowService(session, new SendRequest(executor, environment, new HistoryService(session, TimeProvider.System)), environment);
            var workflow = new Workflow { Steps = [new(), new()] };
            Throws<ArgumentException>(() => service.RunAsync(workflow, (_, _) => { }).GetAwaiter().GetResult());
            Equal(1, calls);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Throws<OperationCanceledException>(() => service.RunAsync(workflow, (_, _) => { }, cancellation.Token).GetAwaiter().GetResult());
            Equal(1, calls);
        });
    }
}

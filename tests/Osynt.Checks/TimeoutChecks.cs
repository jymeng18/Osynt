using System.Net;
using Osynt.Application.Requests;
using Osynt.Desktop.ViewModels;
using Osynt.Domain.Requests;
using Osynt.Infrastructure.Http;
using Osynt.Infrastructure.Persistence;

namespace Osynt.Checks;

internal static partial class Program
{
    private static void RunTimeoutChecks(string directory)
    {
        Check("Timeout defaults preserve older collections and survive editor and storage copies", () =>
        {
            var serializer = new JsonCollectionSerializer();
            var old = serializer.Deserialize("[{\"Name\":\"Old\",\"Requests\":[{\"Url\":\"https://example.com\"}]}]");
            Equal(30, old[0].Requests[0].TimeoutSeconds);
            using var tab = new RequestTab(new RequestDocument());
            tab.Document.TimeoutSeconds = 90;
            True(tab.IsDirty);
            var state = new Osynt.Domain.Workspaces.WorkspaceState
            {
                Collections = [new() { Name = "Timeouts", Requests = [tab.Document.Clone()] }]
            };
            var store = new WorkspaceStore(Path.Combine(directory, "timeouts"));
            store.Save(state);
            Equal(90, store.Load()!.Collections[0].Requests[0].Clone().TimeoutSeconds);
            Equal(90, serializer.Deserialize(serializer.Serialize(state.Collections))[0].Requests[0].TimeoutSeconds);
        });
        Check("Out-of-range timeouts are rejected before transport and during import", () =>
        {
            foreach (var seconds in new[] { -1, 0, 3601, int.MaxValue })
            {
                Throws<ArgumentException>(() => HttpRequestFactory.Create(new() { Url = "https://example.com", TimeoutSeconds = seconds }, new Dictionary<string, string>()));
                Throws<System.Text.Json.JsonException>(() => new JsonCollectionSerializer().Deserialize(
                    $"[{{\"Name\":\"Invalid\",\"Requests\":[{{\"TimeoutSeconds\":{seconds}}}]}}]"));
            }
        });
        Check("Configured request timeout cancels transport with an actionable error", () =>
        {
            var cancelled = false;
            using var client = new HttpClient(new StubHandler(async token =>
            {
                try { await Task.Delay(Timeout.Infinite, token); }
                catch (OperationCanceledException) { cancelled = true; throw; }
                return new HttpResponseMessage(HttpStatusCode.OK);
            })) { Timeout = Timeout.InfiniteTimeSpan };
            try
            {
                new RequestExecutor(client).SendAsync(new() { Url = "https://example.com", TimeoutSeconds = 1 },
                    new Dictionary<string, string>()).GetAwaiter().GetResult();
                throw new InvalidOperationException("Expected a timeout.");
            }
            catch (RequestExecutionException error)
            {
                True(error.Message.Contains("1-second timeout"));
                True(cancelled);
            }
        });
    }
}

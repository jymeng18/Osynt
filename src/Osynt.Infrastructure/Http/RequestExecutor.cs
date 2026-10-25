using System.Diagnostics;
using Osynt.Application.Abstractions;
using Osynt.Application.Requests;
using Osynt.Domain.Requests;

namespace Osynt.Infrastructure.Http;

public sealed class RequestExecutor(HttpClient client) : IRequestExecutor
{
    public const int MaxResponseBytes = 5 * 1024 * 1024;

    public async Task<ResponseData> SendAsync(RequestDocument document,
        IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteAsync(document, variables, cancellationToken);
        }
        catch (Exception error) when (error is ArgumentException or TimeoutException or InvalidOperationException or IOException)
        {
            throw new RequestExecutionException(error.Message, error);
        }
    }

    private async Task<ResponseData> ExecuteAsync(RequestDocument document,
        IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        using var request = HttpRequestFactory.Create(document, variables);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var timer = Stopwatch.StartNew();
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var body = await ResponseBodyReader.ReadAsync(response.Content, MaxResponseBytes, timeout.Token);
            var headers = response.Headers.Concat(response.Content.Headers)
                .Select(h => new KeyValuePair<string, string>(h.Key, string.Join(", ", h.Value))).ToList();
            return new((int)response.StatusCode, response.ReasonPhrase ?? "", timer.ElapsedMilliseconds,
                body.SizeBytes, body.Text, headers, body.Truncated);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("This request took longer than 30 seconds. Check the address and try again.");
        }
        catch (HttpRequestException error)
        {
            throw new RequestExecutionException(RequestFailureMessages.Explain(error, request.RequestUri!), error);
        }
    }
}

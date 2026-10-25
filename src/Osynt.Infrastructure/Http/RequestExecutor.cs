using Osynt.Application.Abstractions;
using Osynt.Domain.Requests;

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace Osynt.Infrastructure.Http;

public sealed class RequestExecutor(HttpClient client) : IRequestExecutor
{
    public const int MaxResponseBytes = 5 * 1024 * 1024;

    public async Task<ResponseData> SendAsync(RequestDocument document,
        IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default)
    {
        using var request = HttpRequestFactory.Create(document, variables);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var timer = Stopwatch.StartNew();
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var body = new MemoryStream();
            var buffer = new byte[16 * 1024];
            while (body.Length <= MaxResponseBytes)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, MaxResponseBytes + 1 - body.Length)), timeout.Token);
                if (count == 0)
                    break;
                body.Write(buffer, 0, count);
            }
            var truncated = body.Length > MaxResponseBytes;
            var bytes = body.ToArray().AsSpan(0, (int)Math.Min(body.Length, MaxResponseBytes)).ToArray();
            var encoding = GetEncoding(response.Content.Headers.ContentType);
            var text = encoding.GetString(bytes);
            var headers = response.Headers.Concat(response.Content.Headers)
                .Select(h => new KeyValuePair<string, string>(h.Key, string.Join(", ", h.Value))).ToList();
            return new((int)response.StatusCode, response.ReasonPhrase ?? "", timer.ElapsedMilliseconds,
                bytes.Length, text, headers, truncated);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("This request took longer than 30 seconds. Check the address and try again.");
        }
        catch (HttpRequestException error)
        {
            throw RequestFailureMessages.Explain(error, request.RequestUri!);
        }
    }

    private static Encoding GetEncoding(MediaTypeHeaderValue? type)
    {
        try
        {
            return string.IsNullOrEmpty(type?.CharSet) ? Encoding.UTF8 : Encoding.GetEncoding(type.CharSet.Trim('"'));
        }
        catch (ArgumentException) { return Encoding.UTF8; }
    }

}

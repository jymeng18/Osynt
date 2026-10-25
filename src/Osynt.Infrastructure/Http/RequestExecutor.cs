using Osynt.Application.Abstractions;
using Osynt.Domain.Requests;

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace Osynt.Infrastructure.Http;

public sealed partial class RequestExecutor(HttpClient client) : IRequestExecutor
{
    public const int MaxResponseBytes = 5 * 1024 * 1024;

    public static string Resolve(string input, IReadOnlyDictionary<string, string> variables) =>
        VariablePattern().Replace(input, match =>
            variables.TryGetValue(match.Groups[1].Value, out var value) && !string.IsNullOrEmpty(value)
                ? value : throw new ArgumentException($"Set a value for {{{{{match.Groups[1].Value}}}}} in Environment first."));

    public static HttpRequestMessage BuildRequest(RequestDocument document, IReadOnlyDictionary<string, string> variables)
    {
        var resolved = Resolve(document.Url.Trim(), variables);
        if (!Uri.TryCreate(resolved, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Your URL needs to start with http:// or https://.");

        var builder = new UriBuilder(uri);
        var pairs = document.Parameters.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key))
            .Select(p => $"{Uri.EscapeDataString(Resolve(p.Key, variables))}={Uri.EscapeDataString(Resolve(p.Value, variables))}");
        builder.Query = string.Join("&", new[] { builder.Query.TrimStart('?') }.Concat(pairs).Where(q => q.Length > 0));

        var request = new HttpRequestMessage(new HttpMethod(document.Method), builder.Uri);
        try
        {
            if (!string.IsNullOrEmpty(document.Body))
                request.Content = new StringContent(Resolve(document.Body, variables), Encoding.UTF8, "application/json");

            foreach (var pair in document.Headers.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key)))
            {
                var key = Resolve(pair.Key.Trim(), variables);
                var value = Resolve(pair.Value, variables);
                if (key.Contains('\r') || key.Contains('\n') || value.Contains('\r') || value.Contains('\n'))
                    throw new ArgumentException("Header names and values cannot contain line breaks.");
                if (!request.Headers.TryAddWithoutValidation(key, value))
                {
                    request.Content ??= new ByteArrayContent([]);
                    request.Content.Headers.Remove(key);
                    if (!request.Content.Headers.TryAddWithoutValidation(key, value))
                        throw new ArgumentException($"The header '{key}' could not be added.");
                }
            }
            return request;
        }
        catch { request.Dispose(); throw; }
    }

    public async Task<ResponseData> SendAsync(RequestDocument document,
        IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(document, variables);
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
                if (count == 0) break;
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
    }

    private static Encoding GetEncoding(MediaTypeHeaderValue? type)
    {
        try { return string.IsNullOrEmpty(type?.CharSet) ? Encoding.UTF8 : Encoding.GetEncoding(type.CharSet.Trim('"')); }
        catch (ArgumentException) { return Encoding.UTF8; }
    }

    [GeneratedRegex(@"\{\{\s*([^{}\s]+)\s*\}\}")]
    private static partial Regex VariablePattern();
}

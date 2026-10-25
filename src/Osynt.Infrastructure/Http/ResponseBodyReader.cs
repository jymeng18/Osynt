using System.Net.Http.Headers;
using System.Text;

namespace Osynt.Infrastructure.Http;

internal static class ResponseBodyReader
{
    internal sealed record Body(string Text, int SizeBytes, bool Truncated);

    public static async Task<Body> ReadAsync(HttpContent content, int limit, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var body = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (body.Length <= limit)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, limit + 1 - body.Length)), cancellationToken);
            if (count == 0)
                break;
            body.Write(buffer, 0, count);
        }
        var size = (int)Math.Min(body.Length, limit);
        var text = GetEncoding(content.Headers.ContentType).GetString(body.GetBuffer(), 0, size);
        return new Body(text, size, body.Length > limit);
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

using System.Net.Http.Headers;
using System.Text;

namespace Osynt.Infrastructure.Http;

internal static class ResponseBodyReader
{
    internal sealed record Body(string Text, int SizeBytes, bool Truncated, byte[]? BinaryBytes = null);

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
        var bytes = body.GetBuffer().AsSpan(0, size);
        if (size > 0 && IsBinary(content.Headers.ContentType?.MediaType, bytes))
            return new Body("", size, body.Length > limit, bytes.ToArray());
        var encoding = GetEncoding(content.Headers.ContentType, bytes);
        var preamble = encoding.GetPreamble();
        var offset = preamble.Length > 0 && bytes.StartsWith(preamble) ? preamble.Length : 0;
        var text = encoding.GetString(bytes[offset..]);
        return new Body(text, size, body.Length > limit);
    }

    private static bool IsBinary(string? mediaType, ReadOnlySpan<byte> bytes)
    {
        if (mediaType is not null)
        {
            var type = mediaType.ToLowerInvariant();
            return !(type.StartsWith("text/", StringComparison.Ordinal) || type.EndsWith("+json", StringComparison.Ordinal) ||
                type.EndsWith("+xml", StringComparison.Ordinal) || type is "application/json" or "application/xml" or
                "application/javascript" or "application/ecmascript" or "application/x-www-form-urlencoded" or
                "application/graphql" or "application/x-ndjson" or "application/ndjson" or "application/yaml");
        }
        if (BomEncoding(bytes) is not null)
            return false;
        foreach (var value in bytes[..Math.Min(bytes.Length, 4096)])
            if (value < 9 || value is 11 or 12 or >= 14 and < 32)
                return true;
        return false;
    }

    private static Encoding? BomEncoding(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE, 0, 0 }))
            return Encoding.UTF32;
        if (bytes.StartsWith(new byte[] { 0, 0, 0xFE, 0xFF }))
            return new UTF32Encoding(bigEndian: true, byteOrderMark: true);
        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE }))
            return Encoding.Unicode;
        if (bytes.StartsWith(new byte[] { 0xFE, 0xFF }))
            return Encoding.BigEndianUnicode;
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
            return Encoding.UTF8;
        return null;
    }

    private static Encoding GetEncoding(MediaTypeHeaderValue? type, ReadOnlySpan<byte> bytes)
    {
        try
        {
            return string.IsNullOrEmpty(type?.CharSet) ? BomEncoding(bytes) ?? Encoding.UTF8 : Encoding.GetEncoding(type.CharSet.Trim('"'));
        }
        catch (ArgumentException) { return Encoding.UTF8; }
    }
}

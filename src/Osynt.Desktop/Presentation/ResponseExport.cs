using System.Text;
using Osynt.Domain.Requests;

namespace Osynt.Desktop.Presentation;

public static class ResponseExport
{
    public static string FileName(ResponseData response)
    {
        var extension = response.IsBinary ? response.ContentType.Split(';')[0].Trim().ToLowerInvariant() switch
        {
            "application/pdf" => "pdf",
            "application/zip" => "zip",
            "image/png" => "png",
            "image/jpeg" => "jpg",
            "image/gif" => "gif",
            "image/webp" => "webp",
            "audio/mpeg" => "mp3",
            "video/mp4" => "mp4",
            _ => "bin"
        } : response.Body.TrimStart().StartsWith('{') || response.Body.TrimStart().StartsWith('[') ? "json" : "txt";
        return $"response{(response.Truncated ? "-preview" : "")}.{extension}";
    }

    public static ReadOnlyMemory<byte> Bytes(ResponseData response, string textDisplay) =>
        response.BinaryBody is { } bytes ? bytes : Encoding.UTF8.GetBytes(textDisplay);
}

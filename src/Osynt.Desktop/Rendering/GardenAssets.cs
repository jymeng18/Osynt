using System.Security.Cryptography;
using Avalonia.Platform;

namespace Osynt.Desktop.Rendering;

internal static class GardenAssets
{
    private const string Root = "avares://Osynt.Desktop/Assets/Garden/";
    public static string Extract()
    {
        var assets = AssetLoader.GetAssets(new Uri(Root), null).OrderBy(u => u.AbsoluteUri).ToArray();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var files = new List<(string Name, byte[] Content)>();
        foreach (var uri in assets)
        {
            using var stream = AssetLoader.Open(uri);
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            var content = bytes.ToArray();
            hash.AppendData(content);
            files.Add((Path.GetFileName(uri.AbsolutePath), content));
        }
        if (!files.Any(f => f.Name == "index.html") || !files.Any(f => f.Name == "garden.js"))
            throw new IOException("The bundled garden renderer is missing. Run npm run build in src/Osynt.Garden.");
        var version = Convert.ToHexString(hash.GetHashAndReset())[..16].ToLowerInvariant();
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Osynt", "renderer", version);
        Directory.CreateDirectory(directory);
        foreach (var (name, content) in files)
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path))
            {
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(temporary, content);
                File.Move(temporary, path, true);
            }
        }
        return directory;
    }
}

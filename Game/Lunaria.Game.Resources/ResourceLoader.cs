using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Lunaria.Game.Resources;

public interface IResourceLoader
{
    string[] ListFiles(string path, string searchPattern = "*", bool recursive = false);

    byte[] ReadRaw(string path);
}

internal static class ResourceJson
{
    public static readonly JsonSerializerOptions Options = new() {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}

internal static class ResourceLoaderExtensions
{
    public static T? ReadJson<T>(this IResourceLoader loader, string path)
    {
        try
        {
            var data = Encoding.UTF8.GetString(loader.ReadRaw(path));
            return JsonSerializer.Deserialize<T>(data, ResourceJson.Options);
        }
        catch (Exception)
        {
            return default;
        }
    }

    public static object? ReadJson(this IResourceLoader loader, string path, Type type)
    {
        try
        {
            var data = Encoding.UTF8.GetString(loader.ReadRaw(path));
            return JsonSerializer.Deserialize(data, type, ResourceJson.Options);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public class FolderLoader(DirectoryInfo resources) : IResourceLoader
{
    public string[] ListFiles(string path, string searchPattern = "*", bool recursive = false) =>
        Directory.GetFiles(Path.Combine(resources.FullName, path), searchPattern,
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

    public byte[] ReadRaw(string path) => File.ReadAllBytes(Path.Combine(resources.FullName, path));
}

public class ZipLoader(ZipArchive archive) : IResourceLoader
{
    /// <summary>Both loaders share this lock because ZipArchive cannot read entries concurrently.</summary>
    private readonly Lock _gate = new();

    public string[] ListFiles(string path, string searchPattern = "*", bool recursive = false)
    {
        var regexPattern = "^" + Regex.Escape(searchPattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";
        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

        // ZIP entry paths always use forward slashes.
        var prefix = path.Replace(oldChar: '\\', newChar: '/').Trim('/');
        if (prefix.Length > 0) prefix += "/";

        lock (_gate)
        {
            return archive.Entries
                .Where(e => {
                    if (!e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

                    if (e.Name.Length == 0) return false;
                    if (!recursive && e.FullName.IndexOf(value: '/', prefix.Length) >= 0) return false;

                    return regex.IsMatch(e.Name);
                })
                .Select(e => e.FullName)
                .ToArray();
        }
    }

    public byte[] ReadRaw(string path)
    {
        lock (_gate)
        {
            var entry = archive.GetEntry(path);
            if (entry == null) throw new FileNotFoundException($"Resource not found in archive: {path}", path);

            using var stream = entry.Open();
            using var reader = new BinaryReader(stream);
            return reader.ReadBytes((int)entry.Length);
        }
    }
}

using Microsoft.Extensions.Options;

namespace Lunaria.SdkServer.Services;


public sealed class HotupdateStore
{
    private readonly List<HotupdateFile> _files = [];
    private readonly Lock _gate = new();
    private readonly ILogger<HotupdateStore> _logger;

    public HotupdateStore(IOptions<SdkServerOptions> options, ILogger<HotupdateStore> logger)
    {
        _logger = logger;
        Root = ResolveRoot(options.Value.HotupdateDir);
    }

    public string Root { get; }

    public IReadOnlyList<HotupdateFile> Files
    {
        get
        {
            lock (_gate)
            {
                return [.. _files];
            }
        }
    }

    public int FileCount
    {
        get
        {
            lock (_gate)
            {
                return _files.Count;
            }
        }
    }

    public long TotalBytes
    {
        get
        {
            lock (_gate)
            {
                return _files.Sum(f => f.Size);
            }
        }
    }

    public static string ResolveRoot(string configured)
    {
        if (Path.IsPathRooted(configured))
            return Path.GetFullPath(configured);

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
    }

    public void Scan()
    {
        List<HotupdateFile> found = [];

        if (Directory.Exists(Root))
        {
            foreach (var full in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(Root, full).Replace(Path.DirectorySeparatorChar, newChar: '/');
                found.Add(new HotupdateFile(relative, new FileInfo(full).Length));
            }
        } else
        {
            Directory.CreateDirectory(Root);
        }

        lock (_gate)
        {
            _files.Clear();
            _files.AddRange(found);
        }

        _logger.LogInformation(
            "hotupdate scan: {Count} files, {Bytes} bytes in {Root}",
            found.Count, found.Sum(f => f.Size), Root);
    }

    public string? Resolve(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var sanitized = relativePath.Replace(oldChar: '/', Path.DirectorySeparatorChar);
        string full;

        try
        {
            full = Path.GetFullPath(Path.Combine(Root, sanitized));
        }
        catch (Exception)
        {
            return null;
        }

        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(full, Root, StringComparison.OrdinalIgnoreCase))
            return null;

        return File.Exists(full) ? full : null;
    }

    public bool TryReadManifest(out byte[] bytes, out string source)
    {
        var inside = Path.Combine(Root, "hotupdate_manifest.json");

        if (File.Exists(inside))
        {
            bytes = File.ReadAllBytes(inside);
            source = inside;
            return true;
        }

        bytes = [];
        source = "";
        return false;
    }

    public sealed record HotupdateFile(string Path, long Size);
}

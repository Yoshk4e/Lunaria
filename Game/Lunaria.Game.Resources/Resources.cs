using System.IO.Compression;

namespace Lunaria.Game.Resources;

public static class Resources
{
    public static IResourceLoader Loader { get; private set; } = null!;

    public static void Initialize(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            throw new ResourceException(path,
                $"resource path does not exist: {path}. The game data dump (assets/tables) is required to run; " +
                "set the AssetsDir option or LUNARIA_GameServer__AssetsDir to the folder containing it.");

        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var archive = ZipFile.OpenRead(path);
            Loader = new ZipLoader(archive);
        } else
        {
            var resources = new DirectoryInfo(path);
            Loader = new FolderLoader(resources);
        }
    }
}

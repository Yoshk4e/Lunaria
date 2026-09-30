namespace Lunaria.Game.Resources;

public sealed class ResourceException : Exception
{
    public ResourceException(string path, string message, Exception? inner = null)
        : base(message, inner)
    {
        Path = path;
    }

    public string Path { get; }
}

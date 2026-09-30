namespace Lunaria.Game.Resources;

public abstract record TableRow;

/// <summary>Higher-priority tables load first.</summary>
internal enum LoadPriority
{
    Highest = 4,
    High = 3,
    Normal = 2,
    Low = 1,
    Lowest = 0
}

[AttributeUsage(AttributeTargets.Class)]
internal sealed class GameTable(string file) : Attribute
{
    public string File { get; } = file;
    public string Root { get; set; } = "";
    public LoadPriority Priority { get; set; } = LoadPriority.Normal;
}

namespace Lunaria.Game.Inventory;

/// <summary>Mail or retain any overflow. An error can still follow a partial grant.</summary>
public readonly record struct ItemAddResult(int Code, uint Stored, uint Overflow)
{
    public bool Ok => Code == 0;

    public bool AnyStored => Stored > 0;
    public static ItemAddResult Rejected(int code) => new(code, Stored: 0, Overflow: 0);
}

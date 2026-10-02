namespace Lunaria.Game.Resources.Tables;

/// <summary>Collection objects placed in the open world. Id matches the level placement the client binds to.</summary>
[GameTable("P_WorldCollectObjTable.json", Root = "P_WorldCollectObjTable")]
public record PWorldCollectObjTable : TableRow
{
    public ulong Id { get; init; }
    public ulong BlockId { get; init; }
    public uint TemplateId { get; init; }
    public int CollectType { get; init; }
    public float PosX { get; init; }
    public float PosY { get; init; }
    public float PosZ { get; init; }
    public float Direction { get; init; }
    public float Pitch { get; init; }
    public float Roll { get; init; }
    public int CollectUnlockType { get; init; }
}

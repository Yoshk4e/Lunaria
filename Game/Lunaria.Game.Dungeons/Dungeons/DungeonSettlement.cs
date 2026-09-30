using Lunaria.Game.Resources;

namespace Lunaria.Game.Dungeons;

public sealed record DungeonSettlement(
    int Result, bool Settled, bool Victory, IReadOnlyList<ItemGrant> Rewards, HordeState? Horde)
{
    public static DungeonSettlement Rejected(int code) => new(code, false, false, [], null);
}

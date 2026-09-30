using Lunaria.Game.Player.Gameplay;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public (int Result, SilverCreatureChange? Change) CombineSilverCreatures(IReadOnlyList<uint> ids)
    {
        var result = SilverCreatures.Combine(ids);
        if (result.Result == 0 && result.Change is {} change)
            foreach (var added in change.AddList)
                Gameplay.Publish(new CreatureAcquired(added.ItemId, 1));
        return result;
    }
}

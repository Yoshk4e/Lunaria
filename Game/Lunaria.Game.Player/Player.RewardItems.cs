using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    /// <summary>Reward quantities with the same item markers as inventory sync, after delivery.</summary>
    public IEnumerable<CmdItem> RewardItems(IEnumerable<ItemGrant> grants) => grants.Select(grant => new CmdItem {
        ItemId = grant.ItemId,
        ItemNum = grant.Count,
        IsNew = Bag.IsNew(grant.ItemId),
        BindId = Bag.CountOf(grant.ItemId) > 0 ? grant.ItemId : 0,
        MotiveData = null
    });
}

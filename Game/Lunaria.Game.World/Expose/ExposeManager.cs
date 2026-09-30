using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.World;

public sealed class ExposeManager(ExposeAssets assets)
{
    public SCExposeGetMonsterList MonsterList(ulong subregion)
    {
        var response = new SCExposeGetMonsterList {
            Result = 0,
            Subregion = subregion
        };

        if (assets.ForSubregion(subregion) is not {} groups)
            return response;

        foreach (var group in groups)
        {
            var data = new ExposeNpcGroupData { NpcGroupId = group.NpcGroupId };

            data.BattleList.Add(group.Battles.Select(battle => new ExposeBattle {
                BattleId = battle.BattleId,
                Weight = battle.Weight
            }));
            response.NpcgroupData.Add(data);
        }

        return response;
    }
}

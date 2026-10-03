using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Characters;

public sealed partial class SkillManager : TrackedObject
{
    public bool KnowsGroup(uint group) => _groups.ContainsKey(group);

    public uint? GroupLevel(uint group) => _groups.TryGetValue(group, out var level) ? level : null;

    public uint MaxLevel(uint group) => assets.Skills.MaxLevel(group);

    public SkillCost? NextCost(uint group) =>
        _groups.TryGetValue(group, out var level) ? assets.Skills.CostOf(group, level + 1) : null;

    public int CheckRaise(uint group)
    {
        if (!_groups.TryGetValue(group, out var level))
            return (int)EnmTextCode.EnmTextSkillNotExist;

        if (level >= assets.Skills.MaxLevel(group))
            return (int)EnmTextCode.EnmTextSkillLevelLimit;

        if (assets.Skills.CostOf(group, level + 1) is null)
            return (int)EnmTextCode.EnmTextSkillLevelLimit;

        return 0;
    }

    public int ApplyRaise(uint group)
    {
        var code = CheckRaise(group);

        if (code != 0)
            return code;

        _groups[group] += 1;

        Log.Stage("skill group {GroupId} raised to level {Level}", group, _groups[group]);
        return 0;
    }

    public int SetGroupLevel(uint group, uint level)
    {
        if (!_groups.ContainsKey(group))
            return (int)EnmTextCode.EnmTextSkillNotExist;

        var clamped = Math.Clamp(level, assets.Skills.InitLevel(group), assets.Skills.MaxLevel(group));

        if (clamped != level)
            return (int)EnmTextCode.EnmTextSkillLevelLimit;

        if (clamped == _groups[group])
            return 0;

        _groups[group] = clamped;

        return 0;
    }

    public bool GroupBelongsTo(uint group, uint characterId) =>
        assets.Characters.StartingSkillGroups(characterId).Any(pair => pair.Group == group);
}

using Msg;

namespace Lunaria.Game.Characters;

public sealed partial class SkillManager
{
    public TalentMasks TalentsOf(ulong instId) =>
        _talents.TryGetValue(instId, out var masks) ? masks : new TalentMasks();

    public bool IsTalentUnlocked(ulong instId, uint nodeId) => TalentsOf(instId).IsUnlocked(nodeId);

    public IReadOnlyList<uint> AvailableTalents(ulong instId, CharacterManager characters) =>
        assets.Talents.All.Where(node => CheckTalent(instId, node, characters) == 0).ToList();

    public int CheckTalent(ulong instId, uint nodeId, CharacterManager characters)
    {
        if (characters.Get(instId) is not {} character)
            return (int)EnmTextCode.EnmTextCharacterNotExist;

        if (!assets.Talents.NodeExists(nodeId))
            return (int)EnmTextCode.EnmTextTalentNodeInvalid;

        var masks = TalentsOf(instId);

        if (masks.IsUnlocked(nodeId))
            return (int)EnmTextCode.EnmTextTalentFull;

        if (character.Level < assets.Talents.UnlockLevel(nodeId))
            return (int)EnmTextCode.EnmTextCharacterLevelLimit;

        if (assets.Talents.Prerequisites(nodeId).Any(parent => !masks.IsUnlocked(parent)))
            return (int)EnmTextCode.EnmTextTalentContentInvalid;

        return 0;
    }

    public int UnlockTalent(ulong instId, uint nodeId, CharacterManager characters)
    {
        var code = CheckTalent(instId, nodeId, characters);

        if (code != 0)
            return code;

        _talents[instId] = TalentsOf(instId).WithUnlock(nodeId);
        IsDirty = true;
        return 0;
    }
}

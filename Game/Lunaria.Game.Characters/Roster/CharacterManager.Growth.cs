using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Characters;

public sealed partial class CharacterManager
{
    public uint LevelCap(ulong instId) =>
        Get(instId) is {} character ? assets.Characters.LevelCap(character.CharacterId, character.BreakLevel) : 0;

    public uint MaxLevel(ulong instId) =>
        Get(instId) is {} character ? assets.Characters.MaxLevel(character.CharacterId) : 0;

    public ExpGrant GrantExp(ulong instId, uint exp)
    {
        if (Get(instId) is not {} character)
            return ExpGrant.Rejected((int)EnmTextCode.EnmTextCharacterNotExist);

        if (exp == 0)
            return new ExpGrant(Code: 0, character.Level, character.Exp, LevelsGained: 0, Dropped: 0);

        var cap = assets.Characters.LevelCap(character.CharacterId, character.BreakLevel);

        if (character.Level >= cap)
            return new ExpGrant(
                (int)EnmTextCode.EnmTextCharacterExpFull, character.Level, character.Exp, LevelsGained: 0, exp);

        var pool = (ulong)character.Exp + exp;
        var level = character.Level;
        uint gained = 0;

        while (level < cap
               && assets.Progression.CharacterExpToAdvance(level) is {} need
               && pool >= need
               && assets.Progression.CharacterStarRequired(level + 1) <= character.BreakLevel)
        {
            pool -= need;
            level++;
            gained++;
        }

        var capped = level >= cap;
        var kept = capped ? 0 : (uint)pool;
        var dropped = capped ? (uint)pool : 0;

        Replace(character with { Level = level, Exp = kept });

        return new ExpGrant(
            Code: 0,
            level,
            kept,
            gained,
            dropped);
    }

    public BreakStep? NextBreak(ulong instId) =>
        Get(instId) is {} character ? assets.Characters.NextBreak(character.CharacterId, character.BreakLevel) : null;

    public int CheckBreak(ulong instId, uint worldLevel)
    {
        if (Get(instId) is not {} character)
            return (int)EnmTextCode.EnmTextCharacterNotExist;

        if (assets.Characters.NextBreak(character.CharacterId, character.BreakLevel) is not {} step)
            return (int)EnmTextCode.EnmTextCharacterStarFull;

        if (character.Level < assets.Characters.LevelCap(character.CharacterId, character.BreakLevel))
            return (int)EnmTextCode.EnmTextCharacterLevelLimit;

        if (worldLevel < step.NeedWorldLevel)
            return (int)EnmTextCode.EnmTextWorldLevelNotEnough;

        return 0;
    }

    public int ApplyBreak(ulong instId, uint worldLevel)
    {
        var code = CheckBreak(instId, worldLevel);

        if (code != 0)
            return code;

        var character = Get(instId)!;
        var step = assets.Characters.NextBreak(character.CharacterId, character.BreakLevel)!;
        Replace(character with { BreakLevel = step.BreakLevel });
        return 0;
    }

    public bool IsFullyBroken(ulong instId) =>
        Get(instId) is {} character && assets.Characters.IsFullyBroken(character.CharacterId, character.BreakLevel);

    private void Replace(CharacterState character)
    {
        var index = _roster.FindIndex(c => c.InstId == character.InstId);

        if (index < 0)
            return;

        _roster[index] = character;
        IsDirty = true;
    }
}

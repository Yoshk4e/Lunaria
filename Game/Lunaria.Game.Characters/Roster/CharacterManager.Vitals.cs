using Lunaria.Common.Tracking;
using Msg;

namespace Lunaria.Game.Characters;

public sealed partial class CharacterManager : TrackedObject
{
    /// <summary>MAXHP in table units, with the break, Motive and talent bonuses the client shows.</summary>
    public int MaxHp(ulong instId)
    {
        if (Get(instId) is not {} character)
            return 0;

        var maxHp = assets.Inside.Attr.Maxhp;
        var unit = assets.Inside.Scale(maxHp, 1);
        var baseMaxHp = assets.Attribs.MaxHp(
            character.CharacterId,
            assets.Characters.DevelopAttributeId(character.CharacterId, character.Level),
            assets.Characters.BreakDevelopAttributeId(character.CharacterId, character.BreakLevel));
        var (_, final) = WithBonus(maxHp, baseMaxHp * unit, Bonuses(instId).GetValueOrDefault(maxHp));
        return Math.Max(final / unit, 0);
    }

    public int PermanentLiquidMax(ulong instId) =>
        Get(instId) is {} character ? assets.Attribs.PermanentLiquidMax(assets.Characters.FixedAttributeId(character.CharacterId)) : 0;

    public int Hp(ulong instId) =>
        Get(instId) is {} character ? character.Hp ?? MaxHp(instId) : 0;

    public int PermanentLiquid(ulong instId) =>
        Get(instId) is {} character ? character.PermanentLiquid ?? PermanentLiquidMax(instId) : 0;

    public int SetHp(ulong instId, int value)
    {
        if (Get(instId) is not {} character)
            return (int)EnmTextCode.EnmTextCharacterNotExist;

        var max = MaxHp(instId);
        var current = character.Hp ?? max;
        var next = Math.Clamp(value, min: 0, max);

        if (next == current)
            return 0;

        Replace(character with { Hp = next });
        return 0;
    }

    public int SetPermanentLiquid(ulong instId, int value)
    {
        if (Get(instId) is not {} character)
            return (int)EnmTextCode.EnmTextCharacterNotExist;

        var max = PermanentLiquidMax(instId);
        var current = character.PermanentLiquid ?? max;
        var next = Math.Clamp(value, min: 0, max);

        if (next == current)
            return 0;

        Replace(character with { PermanentLiquid = next });
        return 0;
    }

    public IReadOnlyList<ulong> HealAll()
    {
        var changed = new List<ulong>();

        foreach (var character in _roster.Where(c => c.Hp is not null && c.Hp != MaxHp(c.InstId)).ToArray())
        {
            Replace(character with { Hp = MaxHp(character.InstId) });
            changed.Add(character.InstId);
        }

        return changed;
    }

    public void LoadVitals(IEnumerable<(ulong InstId, int? Hp, int? PermanentLiquid)> persisted)
    {
        foreach (var (instId, hp, permanentLiquid) in persisted)
        {
            if (Get(instId) is not {} character)
                continue;

            var maxHp = MaxHp(instId);
            var maxLiquid = PermanentLiquidMax(instId);
            var clampedHp = Math.Clamp(hp ?? maxHp, min: 0, maxHp);
            var clampedLiquid = Math.Clamp(permanentLiquid ?? maxLiquid, min: 0, maxLiquid);
            int? nextHp = clampedHp == maxHp ? null : clampedHp;
            int? nextLiquid = clampedLiquid == maxLiquid ? null : clampedLiquid;

            if (nextHp == character.Hp && nextLiquid == character.PermanentLiquid)
                continue;

            Replace(character with { Hp = nextHp, PermanentLiquid = nextLiquid });
        }
    }

    public IEnumerable<(ulong InstId, int? Hp, int? PermanentLiquid)> Vitals() =>
        _roster.Select(c => (c.InstId, c.Hp, c.PermanentLiquid));
}

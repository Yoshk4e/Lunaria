using Lunaria.Game.Logging;
using Lunaria.Game.Player.Gameplay;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    /// <summary>Attribute bonuses of the equipped Motive, of the unlocked talent nodes and of the active team buffs.</summary>
    private IEnumerable<AttributeModifier> BonusModifiers(ulong instId)
    {
        if (Characters.Get(instId) is not {} character)
            return [];

        var modifiers = assets.Bonuses.TalentModifiers(character.CharacterId, UnlockedTalents(instId));

        if (character.MotiveUniqId != 0 && Motives.Get(character.MotiveUniqId) is {} motive)
            modifiers = modifiers.Concat(assets.Bonuses.MotiveModifiers(motive.MotiveId, motive.Level, motive.BreakLevel));

        return modifiers.Concat(BuffModifiers());
    }

    /// <summary>
    /// TempAttribute columns ([attribute, value, ratio]) of the active whole-team buffs. The client only hands the
    /// battle the buff icon ids (SP_PlayerBuffEffectInitData has BuffEffectIDs only), so the bonus reaches combat
    /// through the attributes the server sends.
    /// </summary>
    private IEnumerable<AttributeModifier> BuffModifiers() =>
        Buffs.Buffs.Keys
            .Select(assets.ItemEffects.Buff)
            .Where(buff => buff is { TargetType: 1 })
            .SelectMany(buff => new[] { buff!.TempAttribute1, buff.TempAttribute2 })
            .Where(column => column.Count >= 3)
            .Select(column => new AttributeModifier(column[0], column[1], column[2]));

    private IEnumerable<uint> UnlockedTalents(ulong instId)
    {
        var masks = Skills.TalentsOf(instId);
        return assets.Talents.All.Where(masks.IsUnlocked);
    }

    public int UnlockTalent(ulong instanceId, uint node)
    {
        using var operationTime = BeginOperation();
        var code = Skills.CheckTalent(instanceId, node, Characters);

        if (code != 0)
            return code;

        var characterId = Characters.Get(instanceId)!.CharacterId;
        var content = assets.Bonuses.Talent(characterId, node);
        code = Purchase(content?.Cost ?? [], content?.Coin ?? 0, () => Skills.UnlockTalent(instanceId, node, Characters));

        if (code != 0)
            return code;

        var groups = ApplyTalentSkill(characterId, content);

        if (groups.Count > 0)
            Gameplay.Publish(new SkillGroupsChanged(instanceId, groups));

        // VitalsChanged also resends the character, so the client reads the new attributes and the talent masks.
        Gameplay.Publish(new VitalsChanged([instanceId]));
        return 0;
    }

    private List<uint> ApplyTalentSkill(uint characterId, TalentContent? content)
    {
        if (content is not { Type: CharacterBonusAssets.SkillLevelContent or CharacterBonusAssets.SkillUnlockContent } talent)
            return [];

        if (!Skills.GroupBelongsTo(talent.Param1, characterId) || talent.Param2 <= 0)
        {
            Log.Flag("talent of character {CharacterId} names skill group {GroupId} it does not own", characterId, talent.Param1);
            return [];
        }

        return Skills.RaiseFromTalent(talent.Param1, (uint)talent.Param2) ? [talent.Param1] : [];
    }

    /// <summary>Nodes unlocked before talent effects existed get their skill levels back at login.</summary>
    private void ApplyUnlockedTalentSkills()
    {
        foreach (var character in Characters.All)
        {
            foreach (var node in UnlockedTalents(character.InstId))
                ApplyTalentSkill(character.CharacterId, assets.Bonuses.Talent(character.CharacterId, node));
        }
    }

    /// <summary>Equip, level and break change the Motive bonus, so the attributes are sent again.</summary>
    private void RefreshMotiveHolder(ulong motiveUniq)
    {
        if (Motives.Get(motiveUniq) is { EquipedTarget: not 0 } motive)
            Gameplay.Publish(new VitalsChanged([motive.EquipedTarget]));
    }
}

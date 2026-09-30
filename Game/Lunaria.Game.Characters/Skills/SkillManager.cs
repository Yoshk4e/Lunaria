using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Characters;

public sealed partial class SkillManager(GameData assets)
{
    private readonly SortedDictionary<uint, uint> _groups = [];
    private readonly SortedDictionary<ulong, TalentMasks> _talents = [];

    public bool IsDirty { get; private set; }

    public bool GrantStarter(CharacterManager characters)
    {
        var added = false;

        foreach (var character in characters.All)
        {
            if (_talents.TryAdd(character.InstId, new TalentMasks()))
                added = true;

            foreach (var (group, level) in assets.Characters.StartingSkillGroups(character.CharacterId))
            {
                if (_groups.TryAdd(group, level))
                    added = true;
            }
        }

        if (added)
            IsDirty = true;
        return added;
    }

    public void Load(
        IEnumerable<(uint Group, uint Level)> groups,
        IEnumerable<(ulong InstId, TalentMasks Masks)> talents,
        CharacterManager characters
    )
    {
        _groups.Clear();
        var ownedGroups = characters.All.SelectMany(character => assets.Characters.StartingSkillGroups(character.CharacterId))
            .Select(pair => pair.Group).ToHashSet();

        foreach (var (group, level) in groups)
        {
            if (ownedGroups.Contains(group) && assets.Skills.GroupExists(group))
                _groups[group] = Math.Clamp(level, assets.Skills.InitLevel(group), assets.Skills.MaxLevel(group));
        }

        _talents.Clear();

        foreach (var (instId, masks) in talents)
        {
            if (characters.Owns(instId))
                _talents[instId] = masks;
        }

        IsDirty = false;
    }

    public IReadOnlyList<(uint Group, uint Level)> SkillGroups() =>
        _groups.Select(kv => (kv.Key, kv.Value)).ToList();

    public IReadOnlyList<(ulong InstId, TalentMasks Masks)> Talents() =>
        _talents.Select(kv => (kv.Key, kv.Value)).ToList();

    public void ClearDirty() => IsDirty = false;

    public IReadOnlyList<SkillGrowthInfo> GrowthInfos() =>
        _groups.Select(kv => new SkillGrowthInfo { Id = kv.Key, Level = kv.Value }).ToList();

    public IReadOnlyList<TalentInfo> TalentInfos() =>
        _talents.Select(kv => new TalentInfo {
            InstId = kv.Key,
            UnlockMask0 = kv.Value.Mask0,
            UnlockMask1 = kv.Value.Mask1
        }).ToList();
}

using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Characters;

public sealed partial class SkillManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Characters.Skills");

    private readonly TrackedSortedDictionary<uint, uint> __tracked_groups = [];
    [Tracked]
    private partial TrackedSortedDictionary<uint, uint> _groups { get; }
    private readonly TrackedSortedDictionary<ulong, TalentMasks> __tracked_talents = [];
    [Tracked]
    private partial TrackedSortedDictionary<ulong, TalentMasks> _talents { get; }

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

        AcceptLoadedState();
    }

    public IReadOnlyList<(uint Group, uint Level)> SkillGroups() =>
        _groups.Select(kv => (kv.Key, kv.Value)).ToList();

    public IReadOnlyList<(ulong InstId, TalentMasks Masks)> Talents() =>
        _talents.Select(kv => (kv.Key, kv.Value)).ToList();

    public IReadOnlyList<SkillGrowthInfo> GrowthInfos() =>
        _groups.Select(kv => new SkillGrowthInfo { Id = kv.Key, Level = kv.Value }).ToList();

    public IReadOnlyList<TalentInfo> TalentInfos() =>
        _talents.Select(kv => new TalentInfo {
            InstId = kv.Key,
            UnlockMask0 = kv.Value.Mask0,
            UnlockMask1 = kv.Value.Mask1
        }).ToList();
}

using Lunaria.Common.Tracking;
using Lunaria.Common;
using Lunaria.Game.Logging;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.Game.Characters;

/// <summary>Teams use character IDs, so each character can have only one roster instance.</summary>
public sealed partial class CharacterManager(GameData assets) : TrackedObject
{
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Characters");

    public const int MaxCharacters = (int)EnmSizeLimit.MaxCharacterNums;

    private readonly TrackedList<CharacterState> __tracked_roster = new(c => c.InstId);
    [Tracked]
    private partial TrackedList<CharacterState> _roster { get; }

    public IReadOnlyList<CharacterState> All => _roster;
    public IReadOnlyList<ulong> ChangedIds => _roster.Changes.ChangedKeys.Cast<ulong>().ToArray();
    public bool IsEmpty => _roster.Count == 0;
    public int Count => _roster.Count;

    public int GrantStarter(GuidManager guid)
    {
        if (_roster.Count > 0)
            return 0;

        var granted = 0;

        foreach (var characterId in assets.Starter.Characters)
        {
            if (Add(guid, characterId).Ok)
                granted++;
        }

        return granted;
    }

    /// <summary>Skip unknown characters because missing attributes stall client loading.</summary>
    public void Load(IEnumerable<CharacterState> persisted)
    {
        _roster.Clear();

        _roster.AddRange(persisted
            .Where(c => assets.Characters.Exists(c.CharacterId))
            .DistinctBy(c => c.CharacterId)
            .OrderBy(c => c.InstId)
            .Take(MaxCharacters)
            .Select(Clamp));
        AcceptLoadedState();
    }

    public CharacterGrant Add(GuidManager guid, uint characterId)
    {
        if (!assets.Characters.Exists(characterId))
            return CharacterGrant.Rejected((int)EnmTextCode.EnmTextCharacterNotExist);

        if (InstanceOf(characterId) is not null)
            return CharacterGrant.Rejected((int)EnmTextCode.EnmTextCharacterAlreadyExist);

        if (_roster.Count >= MaxCharacters)
            return CharacterGrant.Rejected((int)EnmTextCode.EnmTextCountGroupLimit);

        var instId = guid.Next();

        _roster.Add(new CharacterState {
            InstId = instId,
            CharacterId = characterId,
            Level = Starter.CharacterLevel,
            Exp = 0,
            BreakLevel = Starter.CharacterBreakLevel,
            MotiveUniqId = 0
        });

        Log.Stage("character {CharacterId} added as instance {InstId}", characterId, instId);
        return new CharacterGrant(Code: 0, instId);
    }

    public CharacterState? Get(ulong instId) => _roster.FirstOrDefault(c => c.InstId == instId);

    public bool Owns(ulong instId) => _roster.Any(c => c.InstId == instId);

    public CharacterState? InstanceOf(uint characterId) =>
        _roster.FirstOrDefault(c => c.CharacterId == characterId);

    private CharacterState Clamp(CharacterState character)
    {
        var cap = assets.Characters.LevelCap(character.CharacterId, character.BreakLevel);
        var level = Math.Clamp(character.Level, Starter.CharacterLevel, cap);
        return level == character.Level ? character : character with { Level = level };
    }

    public IReadOnlyList<CharacterData> ListData() => _roster.Select(ToCharacterData).ToList();

    public CharacterData ToCharacterData(CharacterState character) => new() {
        InstId = character.InstId,
        CharacterId = character.CharacterId,
        Level = character.Level,
        Exp = character.Exp,
        BreakLevel = character.BreakLevel,
        MotiveUniqId = character.MotiveUniqId
    };

    /// <summary>Attributes of an owned character, optionally with run-scoped vitals instead of its own.</summary>
    public IReadOnlyList<(int Id, int Value)> Attribs(ulong instId, int? hp = null, int? liquid = null) =>
        Get(instId) is {} character ?
            assets.Attribs.ForCharacter(
                character.CharacterId,
                assets.Characters.DevelopAttributeId(character.CharacterId, character.Level),
                hp ?? character.Hp,
                liquid ?? character.PermanentLiquid,
                assets.Characters.FixedAttributeId(character.CharacterId),
                MaxHp(instId),
                assets.Characters.BreakDevelopAttributeId(character.CharacterId, character.BreakLevel)) :
            [];

    /// <summary>Equipped Motive and talent bonuses of a character, supplied by the player.</summary>
    [Untracked]
    public Func<ulong, IEnumerable<AttributeModifier>>? Modifiers { get; set; }

    /// <summary>
    /// The client displays the extra attribute column as final minus base, and fights with the final value. Its
    /// formula counts Motive and talent flat bonuses in the base, then adds the Motive rates on top as extra.
    /// </summary>
    public PBCharacterAttribData AttribData(ulong instId, int? hp = null, int? liquid = null)
    {
        var data = new PBCharacterAttribData { InstId = instId };
        var bonuses = Bonuses(instId);

        foreach (var (id, value) in Attribs(instId, hp, liquid))
        {
            var bonus = bonuses.GetValueOrDefault(id);
            bonuses.Remove(id);
            var (baseValue, final) = WithBonus(id, value, bonus);
            data.AttribData.Add(new PBAttribDataElem { AttribType = id, BaseValue = baseValue, FinalValue = final });
        }

        if (Get(instId) is not null)
            foreach (var (id, bonus) in bonuses)
            {
                var (baseValue, final) = WithBonus(id, 0, bonus);
                data.AttribData.Add(new PBAttribDataElem { AttribType = id, BaseValue = baseValue, FinalValue = final });
            }

        return data;
    }

    private Dictionary<int, (long Add, long Multi)> Bonuses(ulong instId) =>
        (Modifiers?.Invoke(instId) ?? [])
            .GroupBy(m => m.AttrId)
            .ToDictionary(g => g.Key, g => (Add: g.Sum(m => (long)m.Add), Multi: g.Sum(m => (long)m.Multi)));

    /// <summary>OutsideAttributeData: base = level + flat bonuses, extra = floor(base * Motive rate).</summary>
    private (int Base, int Final) WithBonus(int id, int value, (long Add, long Multi) bonus)
    {
        var baseValue = value + (long)assets.Inside.Scale(id, 1) * bonus.Add;
        var final = baseValue + baseValue * bonus.Multi / 10_000;
        return ((int)Math.Clamp(baseValue, int.MinValue, int.MaxValue), (int)Math.Clamp(final, int.MinValue, int.MaxValue));
    }
}

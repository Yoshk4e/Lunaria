using Lunaria.Common;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Characters;

/// <summary>Teams use character IDs, so each character can have only one roster instance.</summary>
public sealed partial class CharacterManager(GameData assets)
{
    public const int MaxCharacters = (int)EnmSizeLimit.MaxCharacterNums;

    private readonly List<CharacterState> _roster = [];

    public bool IsDirty { get; private set; }

    public IReadOnlyList<CharacterState> All => _roster;
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
        IsDirty = false;
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
        IsDirty = true;
        return new CharacterGrant(Code: 0, instId);
    }

    public CharacterState? Get(ulong instId) => _roster.FirstOrDefault(c => c.InstId == instId);

    public bool Owns(ulong instId) => _roster.Any(c => c.InstId == instId);

    public CharacterState? InstanceOf(uint characterId) =>
        _roster.FirstOrDefault(c => c.CharacterId == characterId);

    public void ClearDirty() => IsDirty = false;

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

    public IReadOnlyList<(int Id, int Value)> Attribs(ulong instId) =>
        Get(instId) is {} character ?
            assets.Attribs.ForCharacter(
                character.CharacterId,
                assets.Characters.DevelopAttributeId(character.CharacterId, character.Level),
                character.Hp,
                character.PermanentLiquid,
                assets.Characters.FixedAttributeId(character.CharacterId)) :
            [];

    /// <summary>The client displays the extra attribute column as final minus base.</summary>
    public PBCharacterAttribData AttribData(ulong instId) => new() {
        InstId = instId,
        AttribData = {
            Attribs(instId).Select(pair => new PBAttribDataElem {
                AttribType = pair.Id,
                BaseValue = pair.Value,
                FinalValue = pair.Value
            })
        }
    };
}

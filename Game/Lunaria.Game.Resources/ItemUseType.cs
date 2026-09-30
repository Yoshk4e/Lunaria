namespace Lunaria.Game.Resources;

/// <summary>
/// Matches the client's UseEffectType. Auto-use items apply on grant and manual items use CS_ITEM_USE_COUNT.
/// </summary>
public enum ItemUseType
{
    None = 0,

    AddBuffEffect = 1,

    AwakeCharacter = 2,

    /// <summary>Param[0] is stamina restored per item.</summary>
    AddStamina = 3,

    /// <summary>Param[0] names a p_fixeddroptable bundle.</summary>
    AddDrop = 4,

    /// <summary>Duplicate character cards go to the bag.</summary>
    AddCharacter = 6,

    AddMotive = 7,

    AddSilverCreature = 8,

    /// <summary>Currency: <c>Param[0]</c> is the money type the item credits.</summary>
    AddCoin = 9,

    /// <summary>Param[0] is team XP per item.</summary>
    AddTeamExp = 10,

    /// <summary>Param[0] names the battle pass. Each item gives one XP.</summary>
    AddBpExp = 11
}

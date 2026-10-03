using Lunaria.Common.Tracking;
using Msg;

namespace Lunaria.Game.Characters;

/// <summary>Update the character slot and motive owner together.</summary>
public sealed partial class CharacterManager : TrackedObject
{
    public int EquipMotive(ulong instId, ulong motiveUniqId)
    {
        if (Get(instId) is not {} character)
            return (int)EnmTextCode.EnmTextCharacterNotExist;

        if (motiveUniqId == 0)
            return (int)EnmTextCode.EnmTextWrongParam;

        if (character.MotiveUniqId != 0)
            return (int)EnmTextCode.EnmTextMotiveAlearyEquiped;

        Replace(character with { MotiveUniqId = motiveUniqId });
        return 0;
    }

    public int UnequipMotive(ulong instId, ulong motiveUniqId)
    {
        if (Get(instId) is not {} character)
            return (int)EnmTextCode.EnmTextCharacterNotExist;

        if (character.MotiveUniqId != motiveUniqId)
            return (int)EnmTextCode.EnmTextMotiveUidInvalid;

        Replace(character with { MotiveUniqId = 0 });
        return 0;
    }

    public void ForceClearMotiveSlot(ulong instId)
    {
        if (Get(instId) is {} character && character.MotiveUniqId != 0)
            Replace(character with { MotiveUniqId = 0 });
    }
}

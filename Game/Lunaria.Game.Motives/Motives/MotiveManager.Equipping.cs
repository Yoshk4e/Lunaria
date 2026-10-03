using Lunaria.Common.Tracking;
using Lunaria.Game.Logging;
using Msg;

namespace Lunaria.Game.Motives;

public sealed partial class MotiveManager : TrackedObject
{
    public int CheckEquip(ulong motiveUniq)
    {
        if (Get(motiveUniq) is not {} motive)
            return (int)EnmTextCode.EnmTextMotiveUidInvalid;

        if (motive.EquipedTarget != 0)
            return (int)EnmTextCode.EnmTextMotiveAlearyEquiped;

        return 0;
    }

    public int ApplyEquip(ulong motiveUniq, ulong charInstId)
    {
        var code = CheckEquip(motiveUniq);

        if (code != 0)
            return code;

        if (charInstId == 0)
            return (int)EnmTextCode.EnmTextWrongParam;

        var motive = Get(motiveUniq)!;
        Replace(motive with { EquipedTarget = charInstId });
        Log.Stage("motive {UniqId} equipped on character {InstId}", motiveUniq, charInstId);
        return 0;
    }

    public int CheckUnequip(ulong motiveUniq, ulong charInstId)
    {
        if (Get(motiveUniq) is not {} motive)
            return (int)EnmTextCode.EnmTextMotiveUidInvalid;

        if (motive.EquipedTarget != charInstId)
            return (int)EnmTextCode.EnmTextMotiveUidInvalid;

        return 0;
    }

    public int ApplyUnequip(ulong motiveUniq, ulong charInstId)
    {
        var code = CheckUnequip(motiveUniq, charInstId);

        if (code != 0)
            return code;

        var motive = Get(motiveUniq)!;
        Replace(motive with { EquipedTarget = 0 });
        Log.Stage("motive {UniqId} unequipped from character {InstId}", motiveUniq, charInstId);
        return 0;
    }

    public void ForceClearEquip(ulong motiveUniq)
    {
        if (Get(motiveUniq) is {} motive && motive.EquipedTarget != 0)
            Replace(motive with { EquipedTarget = 0 });
    }

    public int SetLocked(ulong motiveUniq, bool locked)
    {
        if (Get(motiveUniq) is not {} motive)
            return (int)EnmTextCode.EnmTextMotiveUidInvalid;

        if (motive.Locked == locked)
            return 0;

        Replace(motive with { Locked = locked });
        return 0;
    }
}

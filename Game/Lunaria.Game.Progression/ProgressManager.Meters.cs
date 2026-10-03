using Lunaria.Common.Tracking;
using Msg;

namespace Lunaria.Game.Progression;

/// <summary>Natural stamina regeneration stops at StaminaRegenMax. Items can reach MaxStamina.</summary>
public sealed partial class ProgressManager : TrackedObject
{
    public int StaminaRegenMax => assets.GlobalConfig.StaminaRegenMax;

    public int StaminaMax => assets.GlobalConfig.MaxStamina;

    public int SatietyMax => assets.GlobalConfig.SatietyLimit;

    public void SetSatiety(int value)
    {
        var clamped = Math.Clamp(value, min: 0, SatietyMax);

        if (clamped == Satiety)
            return;

        Satiety = clamped;

    }

    public int SpendSatiety(int cost)
    {
        if (cost <= 0)
            return (int)EnmTextCode.EnmTextInvalidArgs;

        if (Satiety < cost)
            return (int)EnmTextCode.EnmTextItemNotEnough;

        Satiety -= cost;

        return 0;
    }

    public int Regenerate(DateTimeOffset now)
    {
        var interval = assets.GlobalConfig.StaminaRegenInterval;

        if (interval <= 0)
            return 0;

        if (Stamina >= StaminaRegenMax)
        {
            // Reset the clock while full so spending cannot claim regeneration for idle time.
            if (now > StaminaTickAt)
            {
                StaminaTickAt = now;

            }
            return 0;
        }

        var elapsed = now - StaminaTickAt;

        if (elapsed <= TimeSpan.Zero)
            return 0;

        var ticks = (long)(elapsed.TotalSeconds / interval);

        if (ticks <= 0)
            return 0;

        var gain = (int)Math.Min(ticks, StaminaRegenMax - Stamina);
        Stamina += gain;
        StaminaTickAt = Stamina >= StaminaRegenMax ? now : StaminaTickAt.AddSeconds((double)ticks * interval);

        return gain;
    }

    public int SpendStamina(int cost, DateTimeOffset now)
    {
        if (cost <= 0)
            return (int)EnmTextCode.EnmTextInvalidArgs;

        Regenerate(now);
        if (Stamina < cost)
            return (int)EnmTextCode.EnmTextStaminaNotEnough;

        var wasFull = Stamina >= StaminaRegenMax;
        Stamina -= cost;

        if (wasFull && now > StaminaTickAt)
            StaminaTickAt = now;

        return 0;
    }

    public int AddStamina(int amount, DateTimeOffset now)
    {
        if (amount <= 0)
            return (int)EnmTextCode.EnmTextInvalidArgs;

        Regenerate(now);
        if (Stamina >= StaminaMax)
            return (int)EnmTextCode.EnmTextStaminaRecoverMax;

        var wasBelowRegenMax = Stamina < StaminaRegenMax;
        Stamina = (int)Math.Min((long)Stamina + amount, StaminaMax);

        if (wasBelowRegenMax && Stamina >= StaminaRegenMax && now > StaminaTickAt)
            StaminaTickAt = now;

        return 0;
    }
}

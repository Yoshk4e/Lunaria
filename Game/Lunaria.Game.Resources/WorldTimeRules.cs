using System.Globalization;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public readonly record struct TaskStepEnvironment(uint? MinuteOfDay, WeatherType? Weather);

public static class WorldTimeRules
{
    public static bool WeatherExists(uint id) => Enum.IsDefined((WeatherType)id);

    public static TaskStepEnvironment ParseStep(PTaskStepsTyped step)
    {
        if (step.SetGameWeather != 0 && !WeatherExists(step.SetGameWeather))
            throw new ResourceException("P_TaskSteps", $"step {step.Id} names unknown weather {step.SetGameWeather}");

        uint? minute = null;
        if (step.PushGameTime.Count != 0)
        {
            // Known rows use [2, hour]. Reject unknown modes until their meaning is confirmed.
            if (step.PushGameTime.Count != 2
                || !decimal.TryParse(step.PushGameTime[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var mode) || mode != 2
                || !decimal.TryParse(step.PushGameTime[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var hour)
                || hour < 0 || hour > 24 || decimal.Truncate(hour * 60) != hour * 60)
                throw new ResourceException("P_TaskSteps", $"step {step.Id} has unsupported push_game_time [{string.Join(",", step.PushGameTime)}]");
            minute = (uint)(hour * 60) % 1440;
        }
        return new TaskStepEnvironment(minute, step.SetGameWeather == 0 ? null : (WeatherType)step.SetGameWeather);
    }
}

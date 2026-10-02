using Google.Protobuf;
using Lunaria.Game.Resources;
using Lunaria.Game.Tasks;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player
{
    public WeatherType CurrentWeather { get; private set; }

    public void LoadWeather(uint? weather)
    {
        using var operationTime = BeginOperation();
        CurrentWeather = (WeatherType)(weather is {} id && WorldTimeRules.WeatherExists(id) ? id : assets.Starter.Weather);
    }

    /// <summary>Weather zero selects the client UI's random option for the destination period.</summary>
    public SCGameTimeSetupRes SetupGameTime(uint elapsedMinutes, uint weather, Random? random = null)
    {
        using var operationTime = BeginOperation();
        if (weather != 0 && !WorldTimeRules.WeatherExists(weather))
            return new SCGameTimeSetupRes { Result = (int)EnmTextCode.EnmTextInvalidArgs };

        var selected = weather == 0
            ? assets.GameTime.ChooseWeather((uint)(((ulong)GameTimeMinutes + elapsedMinutes) % 1440), random ?? RandomSources.Weather)
            : (WeatherType)weather;
        if (selected != CurrentWeather)
        {
            CurrentWeather = selected;
            _gameTimeDirty = true;
        }
        // Capture the reply before a completed target changes time again. Later changes use slip notifications.
        var reply = new SCGameTimeSetupRes { PassTime = elapsedMinutes, Weather = (uint)CurrentWeather };
        AdvanceGameTime(elapsedMinutes);
        return reply;
    }

    private void ApplyTaskEnvironment(TaskProgressResult result, List<IMessage> notifications)
    {
        if (!result.Recorded || result.TaskFailed) return;
        foreach (var step in result.PassedSteps)
        {
            var change = assets.Tasks.EnvironmentAfterStep(result.TaskType, step);
            var previous = GameTimeMinutes;
            var next = change.MinuteOfDay ?? previous;
            var weather = change.Weather ?? CurrentWeather;
            if (next == previous && weather == CurrentWeather) continue;

            // Mode 2 means hour of day and wraps forward at midnight. The current hour means no elapsed time.
            var elapsed = (next + 1440 - previous) % 1440;
            GameTimeMinutes = next;
            CurrentWeather = weather;
            _gameTimeDirty = true;
            notifications.Add(new SCGameTimeSlipNtf {
                SlipToTime = next, CurWeather = (uint)weather, PassTime = elapsed
            });
            foreach (var target in Tasks.OnGameTimeAdvanced(previous, elapsed))
                _pendingTimeTargets.Enqueue(target);
        }
    }
}

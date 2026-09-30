using System.Globalization;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class GameTimeAssets
{
    private readonly Period[] _periods;

    public GameTimeAssets(IReadOnlyDictionary<string, PGameTimeTable> rows)
    {
        _periods = rows.Values.Select(row => {
            var weights = row.Weather.Select(value => {
                var parts = value.Split(':');
                if (parts.Length != 2 || !uint.TryParse(parts[0], out var weather) || !WorldTimeRules.WeatherExists(weather)
                    || !uint.TryParse(parts[1], out var weight) || weight == 0)
                    throw new ResourceException("P_GameTimeTable.json", $"period {row.Id} has invalid weather weight '{value}'");
                return (Weather: (WeatherType)weather, Weight: weight);
            }).ToArray();
            if (weights.Length == 0)
                throw new ResourceException("P_GameTimeTable.json", $"period {row.Id} has no weather weights");
            return new Period(ParseMinute(row.Start), ParseMinute(row.End), weights);
        }).ToArray();
        for (uint minute = 0; minute < 1440; minute++)
            if (_periods.Count(p => p.Contains(minute)) != 1)
                throw new ResourceException("P_GameTimeTable.json", $"minute {minute} must belong to exactly one time period");
    }

    public WeatherType ChooseWeather(uint minute, Random random)
    {
        var period = _periods.Single(p => p.Contains(minute % 1440));
        var roll = random.NextInt64(period.Weather.Sum(w => (long)w.Weight));
        foreach (var (weather, weight) in period.Weather)
        {
            if (roll < weight) return weather;
            roll -= weight;
        }
        throw new InvalidOperationException("Random weather roll exceeded its configured total");
    }

    private static uint ParseMinute(string value)
    {
        if (!TimeOnly.TryParseExact(value, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            throw new ResourceException("P_GameTimeTable.json", $"invalid time '{value}'");
        return (uint)(time.Hour * 60 + time.Minute);
    }

    private sealed record Period(uint Start, uint End, (WeatherType Weather, uint Weight)[] Weather)
    {
        public bool Contains(uint minute) => Start <= End ? minute >= Start && minute <= End : minute >= Start || minute <= End;
    }
}

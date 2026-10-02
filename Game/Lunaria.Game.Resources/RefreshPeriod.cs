using System.Globalization;

namespace Lunaria.Game.Resources;

/// <summary>
/// Type 2 uses Param01 for interval seconds and Param02 for the offset. Limit-group data does not confirm this.
/// Types 1, 3 and 4 follow the table's own usage: every chest sits on 1001 (type 1, opened once), the collection
/// reward caps 10101-10103 on 2001 (type 3, daily) and 10104 on 2002 (type 4, weekly).
/// </summary>
public sealed record RefreshPeriod(uint Id, int Kind, uint Param01, uint Param02)
{
    public const int Never = 1;
    public const int Window = 2;
    public const int Daily = 3;
    public const int Weekly = 4;

    public static RefreshPeriod None { get; } = new(Id: 0, Never, Param01: 0, Param02: 0);

    public bool ResetsEver => Kind != Never;

    public bool HasReset(DateTimeOffset anchor, DateTimeOffset now) => Kind switch {
        Daily => now.UtcDateTime.Date > anchor.UtcDateTime.Date,
        Weekly => WeekOf(now) > WeekOf(anchor),
        Window => WindowOf(now) > WindowOf(anchor),
        _ => false
    };

    private static int WeekOf(DateTimeOffset moment)
    {
        var date = moment.UtcDateTime;

        return ISOWeek.GetYear(date) * 100
               + ISOWeek.GetWeekOfYear(date);
    }

    private long WindowOf(DateTimeOffset moment)
    {
        long period = Param01 > 0 ? Param01 : 86400;
        var offset = Param02 % period;
        return (moment.ToUnixTimeSeconds() - offset) / period;
    }
}

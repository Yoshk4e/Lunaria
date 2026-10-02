using System.Text.Json;
using Lunaria.Game.Player.Persistence.Saves;
using Xunit;

namespace Lunaria.Tests;

public sealed class SaveMigrationTests
{
    [Fact]
    public void UnversionedFixture_MigratesAttendanceAndRetainsRewards_Idempotently()
    {
        var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "role-save-v0.json"));
        var saved = RoleSaveMigrations.Read(json, now);
        Assert.Equal(RoleSaveMigrations.CurrentVersion, saved.SchemaVersion);
        var calendar = Assert.Single(saved.SignIn!.Activities!);
        Assert.Equal(3u, calendar.ActivityId);
        Assert.Equal(3u, calendar.AttendanceDays);
        Assert.Equal(now.ToUnixTimeSeconds() / 86400, calendar.LastSignInDay);
        Assert.Equal(new uint[] { 1 }, calendar.ClaimedDays);
        Assert.Equal(2u, Assert.Single(Assert.Single(saved.PendingRewardMail)).Count);
        var migrated = JsonSerializer.Serialize(saved, SaveJson.Options);
        Assert.Equal(migrated, JsonSerializer.Serialize(RoleSaveMigrations.Read(migrated, now.AddDays(1)), SaveJson.Options));
    }

    [Fact]
    public void ExistingCalendarFixture_PreservesKnownLifetimeAttendanceAndDate()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "role-save-v0-calendars.json"));
        var saved = RoleSaveMigrations.Read(json, DateTimeOffset.UnixEpoch);
        var calendar = Assert.Single(saved.SignIn!.Activities!);
        Assert.Equal(42u, calendar.AttendanceDays);
        Assert.Equal(20000, calendar.LastSignInDay);
    }

    [Fact]
    public void FutureVersion_IsRejectedBeforeParsingChangedFieldShapes()
    {
        Assert.Throws<InvalidDataException>(() => RoleSaveMigrations.Read(
            "{\"schema_version\":999,\"wallet\":\"new format\"}", DateTimeOffset.UnixEpoch));
    }
}

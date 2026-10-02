using System.Text.Json;
using Lunaria.Game.Player.Managers;

namespace Lunaria.Game.Player.Persistence.Saves;

/// <summary>Payload migrations are independent of database schema migrations.</summary>
internal static class RoleSaveMigrations
{
    public const int CurrentVersion = 1;

    public static void CheckVersion(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a role save object.");
        var version = 0;
        if (root.TryGetProperty("schema_version", out var value)
            && (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out version)))
            throw new JsonException("Invalid role save version.");
        if (version is < 0 or > CurrentVersion)
            throw new InvalidDataException($"Unsupported role save version {version}; this server supports 0–{CurrentVersion}.");
    }

    public static RoleSaveDocument Read(string json, DateTimeOffset now)
    {
        // Inspect the version before deserializing fields whose shape may change in a future format.
        CheckVersion(json);
        var saved = JsonSerializer.Deserialize<RoleSaveDocument>(json, SaveJson.Options)
            ?? throw new JsonException("Empty role save.");
        return Upgrade(saved, now);
    }

    public static RoleSaveDocument Upgrade(RoleSaveDocument saved, DateTimeOffset now)
    {
        if (saved.SchemaVersion is < 0 or > CurrentVersion)
            throw new InvalidDataException($"Unsupported role save version {saved.SchemaVersion}.");
        if (saved.SchemaVersion == CurrentVersion) return saved;

        // v0 -> v1: name the legacy calendar and preserve the known minimum attendance.
        // A missing historical date conservatively means today, so login cannot award it twice.
        var signIn = saved.SignIn;
        if (signIn is not null)
        {
            var calendars = signIn.Activities ?? [new RoleSaveDocument.SignInActivitySave {
                ActivityId = SignInManager.LegacyActivityId, SignedDays = signIn.SignedDays,
                ClaimedDays = signIn.ClaimedDays, LastSignInDay = signIn.LastSignInDay
            }];
            signIn = signIn with { Activities = calendars.Select(calendar => calendar with {
                AttendanceDays = Math.Max(calendar.AttendanceDays ?? 0, (uint)calendar.SignedDays.Distinct().Count()),
                LastSignInDay = calendar.LastSignInDay ?? (calendar.SignedDays.Count > 0 ? now.ToUnixTimeSeconds() / 86400 : (long?)null)
            }).ToArray() };
        }
        return saved with { SchemaVersion = 1, SignIn = signIn };
    }
}

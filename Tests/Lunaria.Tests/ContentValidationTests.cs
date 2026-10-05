using System.Text.Json.Nodes;
using Lunaria.Game.Resources;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Lunaria.Tests;

[Collection("bundled-gameplay")]
public sealed class ContentValidationTests(BundledGameplayFixture fixture)
{
    [Fact]
    public void BundledContent_ReportsKnownGapsAndOverrides_WithoutRejectingValidCreatureCounts()
    {
        var warnings = ContentValidator.Validate(fixture.Root);
        Assert.Contains(warnings, w => w.Table == "P_CollectionTable" && w.Message.Contains("410002"));
        Assert.Contains(warnings, w => w.Table == "P_SkillGrowthCostTable" && w.Message.Contains("reference 3."));
        Assert.Contains(warnings, w => w.Table == "gameplay-policy" && w.Message.Contains("11720001"));
        Assert.DoesNotContain(warnings, w => w.Table == "P_RegionSequenceTable_Dayfair" && w.Row == "1142079601");
        Assert.DoesNotContain(warnings, w => w.Feature == "validation coverage");
        // Every battlefield the content references exists in the CBT1 client battlefield table.
        Assert.DoesNotContain(warnings, w => w.Message.Contains("C_BattleFieldSystemTable") || w.Usage == "coverage gap");
    }

    [Fact]
    public void UnknownBattlefields_AreReported()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(root, "tables"));
        try
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(fixture.Root, "tables"), "*.json"))
                File.Copy(file, Path.Combine(root, "tables", Path.GetFileName(file)));
            File.Copy(Path.Combine(fixture.Root, "gameplay-policy.json"), Path.Combine(root, "gameplay-policy.json"));
            var path = Path.Combine(root, "tables", "C_RepeatableDungeonsBattleTable.json");
            var json = JsonNode.Parse(File.ReadAllText(path))!;
            json["C_RepeatableDungeonsBattleTable"]!["11120201"]!["battleFieldId"] = 999;
            File.WriteAllText(path, json.ToJsonString());

            var warnings = ContentValidator.Validate(root);

            Assert.Contains(warnings, w => w.Table == "C_RepeatableDungeonsBattleTable" && w.Row == "11120201"
                && w.Message == "Missing C_BattleFieldSystemTable reference 999.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task MissingTargetsAndImpossibleObjectives_AreActionableAdvisories()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(root, "tables"));
        try
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(fixture.Root, "tables"), "*.json"))
                File.Copy(file, Path.Combine(root, "tables", Path.GetFileName(file)));
            File.Copy(Path.Combine(fixture.Root, "gameplay-policy.json"), Path.Combine(root, "gameplay-policy.json"));
            var path = Path.Combine(root, "tables", "P_RegionSequenceTable_Dayfair.json");
            var json = JsonNode.Parse(File.ReadAllText(path))!;
            json["P_RegionSequenceTable_Dayfair"]!["1142042101"]!["paramNum"] = 2;
            File.WriteAllText(path, json.ToJsonString());
            var warnings = ContentValidator.Validate(root);
            Assert.Contains(warnings, w => w.Row == "1142042101" && w.Field == "paramNum"
                && w.Usage == "referenced by content" && w.Message.Contains("at most 1"));
            var logger = new WarningLogger();
            await new GameData(root, logger).StartAsync(default);
            Assert.True(logger.Count > 0); // Advisory failures must not block resource startup.
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private sealed class WarningLogger : ILogger<GameData>
    {
        public int Count { get; private set; }
        public bool IsEnabled(LogLevel level) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (level == LogLevel.Warning) Count++; }
    }
}

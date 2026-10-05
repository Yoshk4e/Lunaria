using System.Text.Json;

namespace Lunaria.Game.Resources;

public sealed record ContentWarning(string Table, string Row, string Field, string Feature, string Usage, string Message)
{
    public override string ToString() => $"{Table}[{Row}].{Field}: {Feature} ({Usage}): {Message}";
}

/// <summary>Advisory cross-table checks. Unreferenced rows may be samples, so warnings allow startup to continue.</summary>
public static class ContentValidator
{
    public static IReadOnlyList<ContentWarning> Validate(string assetsDir)
    {
        var warnings = new List<ContentWarning>();
        var tables = new Dictionary<string, JsonElement[]>();
        JsonElement[] Rows(string table)
        {
            if (tables.TryGetValue(table, out var cached)) return cached;
            var path = Path.Combine(assetsDir, "tables", table + ".json");
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                return tables[table] = json.RootElement.GetProperty(table).EnumerateObject().Select(p => p.Value.Clone()).ToArray();
            }
            catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                warnings.Add(new(table, "*", "*", "validation coverage", "unknown", $"Could not inspect table: {ex.Message}"));
                return tables[table] = [];
            }
        }
        HashSet<ulong> Ids(string table, string field = "id") => Rows(table).SelectMany(r => Numbers(r, field)).ToHashSet();
        void Warn(string table, JsonElement row, string field, string feature, bool referenced, string message) =>
            warnings.Add(new(table, Number(row, "id").ToString(), field, feature,
                referenced ? "referenced by content" : "usage unconfirmed; may be sample data", message));
        void References(string table, string field, HashSet<ulong> targets, string targetTable, string feature,
            Func<JsonElement, bool>? referenced = null)
        {
            foreach (var row in Rows(table))
                foreach (var id in Numbers(row, field).Where(id => id != 0).Distinct())
                    if (!targets.Contains(id))
                        Warn(table, row, field, feature, referenced?.Invoke(row) ?? false, $"Missing {targetTable} reference {id}.");
        }

        var items = Ids("P_ItemTable");
        var fixedDrops = Ids("P_FixedDropTable", "dropId");
        var rolledDrops = Ids("S_DropTable", "dropId");
        var usedRolledDrops = Rows("P_CollectionTable").SelectMany(r => Numbers(r, "dropId"))
            .Concat(Rows("P_RepeatableDungeonsTable").SelectMany(r => Numbers(r, "rewardDrop").Concat(Numbers(r, "firstPassRewardDrop"))))
            .Concat(Rows("P_HordeTable").SelectMany(r => Numbers(r, "firstDrop").Concat(Numbers(r, "commonDrop")))).ToHashSet();
        var usedFixedDrops = Rows("P_RegionRewardDataTable").SelectMany(r => Numbers(r, "dropId"))
            .Concat(Rows("P_ItemTable").Where(r => Number(r, "useType") == (uint)ItemUseType.AddDrop)
                .SelectMany(r => Numbers(r, "param").Take(1))).ToHashSet();
        References("S_DropTable", "itemId", items, "P_ItemTable", "rolled rewards", r => usedRolledDrops.Contains(Number(r, "dropId")));
        References("P_FixedDropTable", "itemId", items, "P_ItemTable", "fixed rewards", r => usedFixedDrops.Contains(Number(r, "dropId")));
        References("P_CollectionTable", "dropId", rolledDrops, "S_DropTable.dropId", "collection rewards");
        References("P_RepeatableDungeonsTable", "rewardDrop", rolledDrops, "S_DropTable.dropId", "dungeon rewards");
        References("P_RepeatableDungeonsTable", "firstPassRewardDrop", rolledDrops, "S_DropTable.dropId", "first clear rewards");
        References("P_HordeTable", "firstDrop", rolledDrops, "S_DropTable.dropId", "horde rewards");
        References("P_HordeTable", "commonDrop", rolledDrops, "S_DropTable.dropId", "horde rewards");
        References("P_RegionRewardDataTable", "dropId", fixedDrops, "P_FixedDropTable.dropId", "exploration rewards");
        References("P_ShopGoodsTable", "itemId", items, "P_ItemTable", "shop inventory");
        foreach (var row in Rows("P_ItemTable").Where(r => Number(r, "useType") == (uint)ItemUseType.AddDrop))
        {
            var drop = Numbers(row, "param").FirstOrDefault();
            if (!fixedDrops.Contains(drop))
                Warn("P_ItemTable", row, "param", "item pack rewards", false, $"Missing P_FixedDropTable.dropId reference {drop}.");
        }

        var growth = Ids("P_SkillGrowthTable");
        References("P_SkillGrowthCostTable", "costItemId", items, "P_ItemTable", "skill upgrades", r => growth.Contains(Number(r, "growthId")));
        // Overrides are server policy, not recovered client definitions. Always expose the mismatch.
        try
        {
            using var policy = JsonDocument.Parse(File.ReadAllText(Path.Combine(assetsDir, "gameplay-policy.json")));
            if (policy.RootElement.TryGetProperty("skillCostItemOverrides", out var overrides))
                foreach (var replacement in overrides.EnumerateObject())
                {
                    var id = replacement.Value.GetUInt64();
                    warnings.Add(new("gameplay-policy", replacement.Name, "skillCostItemOverrides", "skill upgrades", "server override",
                        $"Client cost item {replacement.Name} is replaced with {id}; client/server costs differ."
                        + (items.Contains(id) ? "" : " Replacement item is also missing.")));
                }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or FormatException)
        { warnings.Add(new("gameplay-policy", "*", "skillCostItemOverrides", "validation coverage", "unknown", ex.Message)); }

        var regist = Rows("P_RegionProgressTypeRegistTable").GroupBy(r => Number(r, "registType"))
            .ToDictionary(g => g.Key, g => g.First());
        var collectionIds = Ids("P_CollectionTable");
        var taskIds = Ids("P_TasksList_POIQuest");
        var teleportIds = Ids("P_TeleportPointTemplateTable");
        var creatureIds = Ids("P_SilverCreatureGrowthTable");
        foreach (var suffix in new[] { "", "_Morgue", "_Four", "_Dayfair" })
        {
            var table = "P_RegionSequenceTable" + suffix;
            var usedSequences = Rows("P_RegionProgressTable" + suffix).SelectMany(r => Numbers(r, "sequenceId")).ToHashSet();
            foreach (var row in Rows(table))
            {
                var referenced = usedSequences.Contains(Number(row, "id"));
                var type = Number(row, "type");
                if (!regist.TryGetValue(type, out var registration))
                { Warn(table, row, "type", "exploration", referenced, $"Unregistered objective type {type}."); continue; }
                var task = Number(registration, "isTask") == 1;
                var targets = task ? taskIds : type switch { 2 or 7 => collectionIds, 3 => teleportIds, 6 => creatureIds, _ => null };
                var ids = Numbers(row, "paramId").ToArray();
                var required = Numbers(row, "paramNum").FirstOrDefault();
                if (targets is null)
                { if (required > 0) Warn(table, row, "type", "exploration", referenced, $"Objective type {type} has no server progress counter."); continue; }
                foreach (var missing in ids.Distinct().Where(id => !targets.Contains(id)))
                    Warn(table, row, "paramId", "exploration", referenced, $"Missing objective target {missing} for type {type}.");
                // Creature objectives count instances, so several creatures of one type are valid.
                var capacity = type == 6 && !task ? (ids.Any(targets.Contains) ? ulong.MaxValue : 0UL)
                    : (ulong)ids.Count(targets.Contains);
                if (required > capacity)
                    Warn(table, row, "paramNum", "exploration", referenced, $"Requires {required}, but supported targets can supply at most {capacity}.");
            }
        }

        // Battlefields the CBT1 client defines (c_battlefieldsystemtable). A dungeon's battleId names a
        // C_RepeatableDungeonsBattleTable row, which names the battlefield the client fights on.
        var battleFields = Ids("C_BattleFieldSystemTable");
        var dungeonBattles = Ids("C_RepeatableDungeonsBattleTable");
        References("P_RepeatableDungeonsTable", "battleId", dungeonBattles, "C_RepeatableDungeonsBattleTable", "dungeon battles",
            _ => true);
        References("C_RepeatableDungeonsBattleTable", "battleFieldId", battleFields, "C_BattleFieldSystemTable", "dungeon battles",
            _ => true);
        foreach (var row in Rows("P_WantedPosterNPC")
                     .Where(r => Number(r, "npcType") is (uint)WantedNpcType.NormalBattle or (uint)WantedNpcType.EndlessBattle))
            foreach (var id in Numbers(row, "params").Where(id => id != 0 && !battleFields.Contains(id)))
                Warn("P_WantedPosterNPC", row, "params", "wanted battles", true, $"Missing C_BattleFieldSystemTable reference {id}.");
        foreach (var row in Rows("P_NPCGroupEnterBattle"))
            foreach (var entry in Strings(row, "enterBattleList"))
                if (!ulong.TryParse(entry.Split(',')[0], out var id) || !battleFields.Contains(id))
                    Warn("P_NPCGroupEnterBattle", row, "enterBattleList", "exposed encounters", true,
                        $"Missing C_BattleFieldSystemTable reference {entry}.");
        References("P_BattleFieldRewardTable", "id", battleFields, "C_BattleFieldSystemTable", "battle loot", _ => true);
        return warnings;
    }

    private static ulong Number(JsonElement row, string field) => Numbers(row, field).FirstOrDefault();
    private static IEnumerable<string> Strings(JsonElement row, string field)
    {
        if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty(field, out var value)
            || value.ValueKind != JsonValueKind.Array) yield break;
        foreach (var element in value.EnumerateArray())
            if (element.ValueKind == JsonValueKind.String) yield return element.GetString()!;
    }

    private static IEnumerable<ulong> Numbers(JsonElement row, string field)
    {
        if (row.ValueKind != JsonValueKind.Object) yield break;
        if (!row.TryGetProperty(field, out var value)) yield break;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out var number)) yield return number;
        if (value.ValueKind == JsonValueKind.Array)
            foreach (var element in value.EnumerateArray())
                if (element.ValueKind == JsonValueKind.Number && element.TryGetUInt64(out var item)) yield return item;
    }
}

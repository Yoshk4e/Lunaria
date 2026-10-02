namespace Lunaria.Tests.Support;

public sealed partial class TestAssets
{
    public const uint DropBundle = 700;
    public const uint MailTemplate = 2;
    public const uint BagFullMailTemplate = 1;
    public const uint UnlimitedGroup = 1;
    public const uint TimedBuff = 990001;
    public const uint DailyGroup = 50;
    public const uint NeverGroup = 51;
    public const uint DailyRefresh = 9001;
    public const uint WeeklyRefresh = 9003;
    public const uint NeverRefresh = 9004;
    public const uint Shop = 1;
    public const uint SatietyShop = 2;
    public const uint Good = 5001;
    public const uint LimitedGood = 5002;
    public const uint MotiveId = 12051001;
    public const uint GachaPool = 1;
    public const uint Collection = 700;
    public const uint IndestructibleCollection = 701;
    public const uint CollectionSpawnGroup = 9002;
    public const uint RootTask = 100;
    public const uint ChainedTask = 200;
    public const ulong FirstStep = 1001;
    public const ulong SecondStep = 1002;
    public const ulong ServerAction = 100101;
    public const ulong ClientAction = 100102;

    public const uint MotiveCapAtBreakZero = 2;

    public const uint MotiveCapAtBreakOne = 3;

    private static void WriteSystemTables(DirectoryInfo tables)
    {
        // Use a timed buff here because bundled food buffs count battles.
        Write(tables, "P_OutsideBuffTable.json", mergeOverBundled: true,
            json: """
                  {"P_OutsideBuffTable": {
                    "990001": {"Id": 990001, "DurationTime": 60, "DurationBattle": -1,
                               "RefreshOnReuse": false, "TempAttribute1": [1, 10, 0]}}}
                  """);
        // Keep bundled reward rows so region references still resolve.
        Write(tables, "P_FixedDropTable.json",
            mergeOverBundled: true,
            json: """
                  {"P_FixedDropTable": {
                    "1": {"Id": 1, "DropId": 700, "ItemId": 900, "ItemCount": 3},
                    "2": {"Id": 2, "DropId": 700, "ItemId": 1, "ItemCount": 50}}}
                  """);

        // Collection rewards use the server drop table. Item packs use fixed bundles.
        Write(tables, "S_DropTable.json", mergeOverBundled: true,
            json: """
                  {"S_DropTable": {
                    "990001": {"Id": 990001, "DropId": 700, "GroupId": 1, "ItemId": 900, "ItemCount": 3, "Odds": 10000},
                    "990002": {"Id": 990002, "DropId": 700, "GroupId": 1, "ItemId": 1, "ItemCount": 50, "Odds": 10000}}}
                  """);

        Write(tables, "P_TemplateMailTable.json",
            json: """
                  {"P_TemplateMailTable": {
                    "1": {"Id": 1, "Title": 11, "From": 12, "Content": 13, "Important": false, "Expiration": 5},
                    "2": {"Id": 2, "Title": 21, "From": 22, "Content": 23, "Important": true, "Expiration": 10,
                          "Items": ["900:2", "1:100"]}}}
                  """);

        Write(tables, "P_LimitGroupTable.json",
            json: """
                  {"P_LimitGroupTable": {
                    "1": {"Id": 1, "RewardLimit": 0},
                    "50": {"Id": 50, "RewardLimit": 2, "RewardLimitRefreshConfigId": 9001},
                    "51": {"Id": 51, "RewardLimit": 3, "RewardLimitRefreshConfigId": 9004},
                    "52": {"Id": 52, "RewardLimit": 5, "RewardLimitRefreshConfigId": 4001}}}
                  """);

        Write(tables, "P_RefreshConfigTable.json",
            json: """
                  {"P_RefreshConfigTable": {
                    "9001": {"Id": 9001, "RefreshType": 3},
                    "9002": {"Id": 9002, "RefreshType": 2, "Param01": 3600, "Param02": 0},
                    "9003": {"Id": 9003, "RefreshType": 4},
                    "9004": {"Id": 9004, "RefreshType": 1}}}
                  """);

        Write(tables, "P_ShopTable.json",
            json: """
                  {"P_ShopTable": {
                    "1": {"Id": 1, "Type": 2, "GoodsGroupArray": [500], "NeedDecSatiety": false},
                    "2": {"Id": 2, "Type": 2, "GoodsGroupArray": [500, 501], "NeedDecSatiety": true}}}
                  """);

        Write(tables, "P_ShopGoodsTable.json",
            json: """
                  {"P_ShopGoodsTable": {
                    "5001": {"Id": 5001, "Group": 500, "ItemId": 900, "ItemNum": 1, "MoneyType": 1,
                             "CostNum": 100, "Priority": 1},
                    "5002": {"Id": 5002, "Group": 501, "ItemId": 901, "ItemNum": 2, "MoneyType": 1,
                             "CostNum": 250, "Priority": 2, "LimitNum": 2, "LimitType": 1}}}
                  """);

        WriteMotiveTables(tables);
        WriteGachaAndCollectionTables(tables);
        WriteTaskTables(tables);
    }

    private static void WriteMotiveTables(DirectoryInfo tables)
    {
        Write(tables, "P_MotiveTable.json",
            json: """
                  {"P_MotiveTable": {
                    "12051001": {"Id": 12051001, "Rare": 5, "Identity": 1, "MainAttribute": 101,
                                 "LevelTemplateId": 1, "BreakTemplateId": 1}}}
                  """);

        Write(tables, "P_MotiveLevelCostTable.json",
            json: """
                  {"P_MotiveLevelCostTable": {
                    "1": {"Id": 1, "Rare": 5, "Level": 1, "MaxExp": 100},
                    "2": {"Id": 2, "Rare": 5, "Level": 2, "MaxExp": 200},
                    "3": {"Id": 3, "Rare": 5, "Level": 3, "MaxExp": 0}}}
                  """);

        Write(tables, "P_MotiveLevelTemplateTable.json",
            json: """
                  {"P_MotiveLevelTemplateTable": {
                    "1": {"Id": 1, "TemplateId": 1, "Level": 1, "AddAttributeId": 2001},
                    "2": {"Id": 2, "TemplateId": 1, "Level": 2, "AddAttributeId": 2002},
                    "3": {"Id": 3, "TemplateId": 1, "Level": 3, "AddAttributeId": 2003}}}
                  """);

        Write(tables, "P_MotiveBreakTemplateTable.json",
            json: """
                  {"P_MotiveBreakTemplateTable": {
                    "1": {"Id": 1, "TemplateId": 1, "BreakLevel": 0, "MaxLevel": 2, "NeedWorldLevel": 0},
                    "2": {"Id": 2, "TemplateId": 1, "BreakLevel": 1, "MaxLevel": 3, "NeedWorldLevel": 2,
                          "CostItemId": [901], "CostItemCount": [1], "CostCurrency": 200,
                          "AddAttributeId": 2003}}}
                  """);

        Write(tables, "P_MotiveAttributeTable.json",
            json: """
                  {"P_MotiveAttributeTable": {
                    "2001": {"Id": 2001, "Atk": 10, "Maxhp": 100},
                    "2002": {"Id": 2002, "Atk": 20, "Maxhp": 200},
                    "2003": {"Id": 2003, "Atk": 30, "Maxhp": 300}}}
                  """);
    }

    private static void WriteGachaAndCollectionTables(DirectoryInfo tables)
    {
        Write(tables, "P_GachaTable.json",
            json: """
                  {"P_GachaTable": {
                    "1": {"Id": 1, "Type": 3, "ActivityId": 1, "CurrencyId": 1, "CurrencyCount": 160,
                          "SsrMaxDrawCount": 80, "SrMaxDrawCount": 10, "RebateTemplateId": 1,
                          "RepeatedTemplateId": 1, "DailyDrawLimit": 50}}}
                  """);

        Write(tables, "P_GachaRebateTable.json",
            json: """
                  {"P_GachaRebateTable": {
                    "1": {"Id": 1, "TemplateId": 1, "DrawCount": 20, "ItemId": 900, "ItemCount": 1},
                    "2": {"Id": 2, "TemplateId": 1, "DrawCount": 50, "ItemId": 901, "ItemCount": 1}}}
                  """);

        Write(tables, "P_CollectionTable.json",
            json: """
                  {"P_CollectionTable": {
                    "700": {"Id": 700, "CollectionType": 1, "Uuid": 5001, "DropId": [700],
                            "CollectionDropId": 9002, "RefreshConfigId": 9001, "RewardLimitId": 50,
                            "AutoDestroy": true, "Angle": 45.0, "Radius": 200.0},
                    "701": {"Id": 701, "CollectionType": 1, "Uuid": 5002, "DropId": [700],
                            "CollectionDropId": 9002, "RefreshConfigId": 9001, "RewardLimitId": 1,
                            "AutoDestroy": false, "Angle": 45.0, "Radius": 0.0},
                    "702": {"Id": 702, "CollectionType": 1, "Uuid": 5003, "DropId": [700],
                            "RefreshConfigId": 4001, "RewardLimitId": 1, "AutoDestroy": false},
                    "710": {"Id": 710, "DropId": [4294967295], "RewardLimitId": 50, "AutoDestroy": true},
                    "711": {"Id": 711, "DropId": [700], "CollectionDropId": 9999, "RewardLimitId": 50, "AutoDestroy": true},
                    "712": {"Id": 712, "DropId": [700], "CollectionDropId": 9003, "RewardLimitId": 50, "AutoDestroy": true},
                    "713": {"Id": 713, "DropId": [700], "CollectionDropId": 9004, "RewardLimitId": 50, "AutoDestroy": true}}}
                  """);

        Write(tables, "P_CollectionDropTable.json",
            json: """
                  {"P_CollectionDropTable": {
                    "1": {"Id": 1, "CollectionDropId": 9002, "GroupId": 1, "CollectionId": 700, "Weight": 70},
                    "2": {"Id": 2, "CollectionDropId": 9002, "GroupId": 1, "CollectionId": 701, "Weight": 30},
                    "3": {"Id": 3, "CollectionDropId": 9003, "GroupId": 1, "CollectionId": 4294967295, "Weight": 100},
                    "4": {"Id": 4, "CollectionDropId": 9004, "GroupId": 1, "CollectionId": 710, "Weight": 100}}}
                  """);

        Write(tables, "P_WorldCollectObjTable.json",
            json: """
                  {"P_WorldCollectObjTable": {
                    "7000": {"Id": 7000, "BlockId": 100, "TemplateId": 700},
                    "7100": {"Id": 7100, "BlockId": 100, "TemplateId": 710},
                    "7110": {"Id": 7110, "BlockId": 100, "TemplateId": 711},
                    "7120": {"Id": 7120, "BlockId": 100, "TemplateId": 712},
                    "7130": {"Id": 7130, "BlockId": 100, "TemplateId": 713}}}
                  """);
    }

    private static void WriteTaskTables(DirectoryInfo tables)
    {
        Write(tables, "P_TasksList.json",
            json: """
                  {"P_TasksList": {
                    "100": {"Id": 100, "NameDesc": 1, "LoadingType": 1, "Steps": [1001, 1002],
                            "NextTasks": [200], "RewardId": 700},
                    "200": {"Id": 200, "NameDesc": 1, "LoadingType": 1, "Steps": [2001]}}}
                  """);

        Write(tables, "P_TaskSteps.json",
            json: """
                  {"P_TaskSteps": {
                    "1001": {"Id": 1001, "OriginTaskId": 100, "Actions": [100101, 100102]},
                    "1002": {"Id": 1002, "OriginTaskId": 100, "Actions": [100201], "ClientStepOnly": true},
                    "2001": {"Id": 2001, "OriginTaskId": 200, "Actions": [200101]}}}
                  """);

        Write(tables, "P_TaskActions.json",
            json: """
                  {"P_TaskActions": {
                    "100101": {"Id": 100101, "OriginStep": 1001, "Necessary": true, "ServerSave": true,
                               "TargetType": 27, "MapId": 100},
                    "100102": {"Id": 100102, "OriginStep": 1001, "Necessary": false, "ServerSave": false,
                               "TargetType": 11, "MapId": 100},
                    "100201": {"Id": 100201, "OriginStep": 1002, "Necessary": true, "ServerSave": true,
                               "TargetType": 27, "MapId": 100},
                    "200101": {"Id": 200101, "OriginStep": 2001, "Necessary": true, "ServerSave": true,
                               "TargetType": 27, "MapId": 100}}}
                  """);
    }
}

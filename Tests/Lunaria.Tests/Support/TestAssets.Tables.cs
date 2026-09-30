namespace Lunaria.Tests.Support;

public sealed partial class TestAssets
{
    private static void WriteTables(DirectoryInfo tables)
    {
        Write(tables, "P_CharacterTable.json",
            json: """
                  {"P_CharacterTable": {
                    "1001": {"Id": 1001, "IdentityType": 1, "ElementType": 2, "RareType": 5,
                             "FixedAttributeId": 1001, "LevelUpTemplateId": 1001}}}
                  """);

        Write(tables, "P_CharacterSkillGroupTable.json",
            json: """
                  {"P_CharacterSkillGroupTable": {
                    "1001": {"Id": 1001, "SkillGroup": [11, 12, 13, 14, 15]}}}
                  """);

        Write(tables, "P_SkillGrowthTable.json",
            json: """
                  {"P_SkillGrowthTable": {
                    "11": {"Id": 11, "InitLevel": 1, "Skills": [1101]},
                    "12": {"Id": 12, "InitLevel": 1},
                    "13": {"Id": 13, "InitLevel": 1},
                    "14": {"Id": 14, "InitLevel": 1},
                    "15": {"Id": 15, "InitLevel": 0}}}
                  """);

        Write(tables, "P_SkillGrowthCostTable.json",
            json: """
                  {"P_SkillGrowthCostTable": {
                    "1102": {"Id": 1102, "GrowthId": 11, "Level": 2, "CostItemId": [900], "CostItemNum": [1], "CostCoinNum": 100},
                    "1103": {"Id": 1103, "GrowthId": 11, "Level": 3, "CostItemId": [900], "CostItemNum": [2], "CostCoinNum": 200},
                    "1202": {"Id": 1202, "GrowthId": 12, "Level": 2, "CostItemId": [900], "CostItemNum": [1], "CostCoinNum": 100}}}
                  """);

        Write(tables, "P_BreakTemplateTable.json",
            json: """
                  {"P_BreakTemplateTable": {
                    "1": {"Id": 1, "TemplateId": 1001, "BreakLevel": 0, "MaxLevel": 3, "NeedWorldLevel": 0},
                    "2": {"Id": 2, "TemplateId": 1001, "BreakLevel": 1, "MaxLevel": 5, "NeedWorldLevel": 2,
                          "CostItemId": [901], "CostItemCount": [2], "CostCurrency": 500}}}
                  """);

        Write(tables, "P_LevelUpTemplateTable.json",
            json: """
                  {"P_LevelUpTemplateTable": {
                    "1": {"Id": 1, "TemplateId": 1001, "Level": 1, "DevelopAttributeId": 100101},
                    "2": {"Id": 2, "TemplateId": 1001, "Level": 2, "DevelopAttributeId": 100102},
                    "3": {"Id": 3, "TemplateId": 1001, "Level": 3, "DevelopAttributeId": 100103},
                    "4": {"Id": 4, "TemplateId": 1001, "Level": 4, "DevelopAttributeId": 100104},
                    "5": {"Id": 5, "TemplateId": 1001, "Level": 5, "DevelopAttributeId": 100105}}}
                  """);

        Write(tables, "P_LevelUpExpTable.json",
            json: """
                  {"P_LevelUpExpTable": {
                    "1": {"Id": 1, "StarRequired": 0, "Experience": 0},
                    "2": {"Id": 2, "StarRequired": 0, "Experience": 10},
                    "3": {"Id": 3, "StarRequired": 0, "Experience": 20},
                    "4": {"Id": 4, "StarRequired": 0, "Experience": 30},
                    "5": {"Id": 5, "StarRequired": 0, "Experience": 40}}}
                  """);

        Write(tables, "P_TeamLevelTable.json",
            json: """
                  {"P_TeamLevelTable": {
                    "1": {"Id": 1, "RequireTeamExp": 100, "MaximumExp": 1000},
                    "2": {"Id": 2, "RequireTeamExp": 200, "MaximumExp": 1000},
                    "3": {"Id": 3, "RequireTeamExp": 0, "MaximumExp": 50}}}
                  """);

        Write(tables, "P_WorldLevelTable.json",
            json: """
                  {"P_WorldLevelTable": {
                    "1": {"Id": 1, "RequireTeamLevel": 0, "MaxTeamLevel": 2, "MaxGemCost": 3},
                    "2": {"Id": 2, "RequireTeamLevel": 2, "MaxTeamLevel": 3, "MaxGemCost": 7}}}
                  """);

        Write(tables, "P_TalentNodeTable.json",
            json: """
                  {"P_TalentNodeTable": {
                    "0": {"Id": 0, "GroupType": 1, "UnlockLevel": 1},
                    "1": {"Id": 1, "GroupType": 1, "ParentIdList": [0], "UnlockLevel": 3}}}
                  """);

        WriteAttributeTables(tables);
        WriteItemAndWorldTables(tables);
    }

    private static void WriteAttributeTables(DirectoryInfo tables)
    {
        Write(tables, "P_DevelopAttributeTable.json",
            json: """
                  {"P_DevelopAttributeTable": {
                    "100101": {"Id": 100101, "Maxhp": 100, "Atk": 10, "Def": 5},
                    "100102": {"Id": 100102, "Maxhp": 120, "Atk": 12, "Def": 6},
                    "100103": {"Id": 100103, "Maxhp": 140, "Atk": 14, "Def": 7},
                    "100104": {"Id": 100104, "Maxhp": 160, "Atk": 16, "Def": 8},
                    "100105": {"Id": 100105, "Maxhp": 180, "Atk": 18, "Def": 9}}}
                  """);

        Write(tables, "P_FixedAttributeTable.json", json: """{"P_FixedAttributeTable": {}}""");

        Write(tables, "P_OutsideAttributeTable.json",
            json: """
                  {"P_OutsideAttributeTable": {"7": {"Id": 7, "AttrEnum": 99, "Default": 5}}}
                  """);

        Write(tables, "P_InsideAttributeTable.json",
            json: """
                  {"P_InsideAttributeTable": {
                    "1": {"Id": 1, "AttrEnum": "MAXHP"}, "2": {"Id": 2, "AttrEnum": "HP"},
                    "3": {"Id": 3, "AttrEnum": "ATK"}, "4": {"Id": 4, "AttrEnum": "DEF"},
                    "5": {"Id": 5, "AttrEnum": "ATK_CRITICALCHANCE"},
                    "6": {"Id": 6, "AttrEnum": "ATK_CRITICALDAMAGE"},
                    "7": {"Id": 7, "AttrEnum": "ATK_BODYPARTBREAK_ADD_RATE"},
                    "8": {"Id": 8, "AttrEnum": "SHIELD"},
                    "9": {"Id": 9, "AttrEnum": "PERMANENT_LIQUID"},
                    "10": {"Id": 10, "AttrEnum": "PERMANENT_LIQUID_MAX"}}}
                  """);
    }

    private static void WriteItemAndWorldTables(DirectoryInfo tables)
    {
        // Item 1 is the test coin. The bundled charge tables also need item 770002.
        Write(tables, "P_ItemTable.json",
            json: """
                  {"P_ItemTable": {
                    "1": {"Id": 1, "ShowType": 1, "UseType": 9, "Param": [1], "AutoUse": true, "Rare": 3, "HoldLimit": 99999999},
                    "770002": {"Id": 770002, "ShowType": 1, "UseType": 9, "Param": [1], "AutoUse": true, "Rare": 3, "HoldLimit": 99999999},
                    "900": {"Id": 900, "ShowType": 2, "UseType": 8, "Param": [900], "Rare": 1, "HoldLimit": 10},
                    "901": {"Id": 901, "ShowType": 2, "UseType": 8, "Param": [901], "Rare": 2, "HoldLimit": 5},
                    "902": {"Id": 902, "ShowType": 2, "UseType": 8, "Param": [902], "Rare": 2, "HoldLimit": 3},
                    "903": {"Id": 903, "ShowType": 2, "UseType": 8, "Param": [903], "Rare": 2, "HoldLimit": 4}}}
                  """);

        Write(tables, "P_MoneyTable.json",
            json: """
                  {"P_MoneyTable": {"1": {"Id": 1, "MoneyType": 1}, "2": {"Id": 2, "MoneyType": 2}}}
                  """);

        Write(tables, "P_MapDataTable.json",
            json: """
                  {"P_MapDataTable": {
                    "100": {"Id": 100, "DefaultPos": "1,2,3", "BManipulateRole": 1},
                    "200": {"Id": 200, "DefaultPos": "4,5,6", "BManipulateRole": 1},
                    "300": {"Id": 300, "DefaultPos": "7,8,9", "BClientOnly": 1, "BManipulateRole": 1},
                    "400": {"Id": 400, "DefaultPos": "10,11,12", "BManipulateRole": 0}}}
                  """);

        Write(tables, "P_FunctionalNPCTable.json",
            json: """
                  {"P_FunctionalNPCTable": {
                    "8": {"Id": 8, "MapId": 100, "Type": 101, "TemplateId": 8, "Position": "1,2,3", "Rotation": "0,0,0"},
                    "9": {"Id": 9, "MapId": 100, "Type": 101, "TemplateId": 9, "Position": "4,5,6", "Rotation": "0,0,0"},
                    "10": {"Id": 10, "MapId": 100, "Type": 101, "TemplateId": 10, "Position": "7,8,9", "Rotation": "0,0,0"},
                    "500": {"Id": 500, "MapId": 100, "Type": 106, "TemplateId": 500, "Position": "10,11,12", "Rotation": "0,0,0"}}}
                  """);

        Write(tables, "P_SavePointTemplateTable.json",
            json: """
                  {"P_SavePointTemplateTable": {
                    "8": {"Id": 8, "BDefault": true, "LocationOffset": "0,0,0"},
                    "9": {"Id": 9, "BDefault": true, "LocationOffset": "0,0,0"},
                    "10": {"Id": 10, "BDefault": false, "LocationOffset": "0,0,0"}}}
                  """);

        Write(tables, "P_TeleportPointTemplateTable.json",
            json: """
                  {"P_TeleportPointTemplateTable": {
                    "500": {"Id": 500, "InteractingEntityId": 500, "ActivateRange": 400}}}
                  """);

        Write(tables, "P_GraphicGuideTable.json",
            json: """
                  {"P_GraphicGuideTable": {
                    "20001": {"Id": 20001, "DropId": 1}, "20002": {"Id": 20002, "DropId": 1}}}
                  """);

        Write(tables, "S_PlayerIniTable.json",
            json: """
                  {"S_PlayerIniTable": {
                    "1": {"Id": 1, "CharacterId": [1001], "TeamInfo": [1001],
                          "ItemId": "1#100|900#2|999#1", "Satiety": 110, "SavePoint": 0,
                          "DefaultMap": 100, "Tod": "9:00", "Weather": 1}}}
                  """);

        Write(tables, "S_ServerGlobalConfig.json",
            json: """
                  {"S_ServerGlobalConfig": {
                    "1": {"Id": 1, "Key": "SatietyLimit", "Value": "200"},
                    "2": {"Id": 2, "Key": "StaminaRegenMax", "Value": "240"},
                    "3": {"Id": 3, "Key": "MaxStamina", "Value": "9999"},
                    "4": {"Id": 4, "Key": "StaminaRegenInterval", "Value": "360"},
                    "5": {"Id": 5, "Key": "MaxSilverCreatureCost", "Value": "10"},
                    "6": {"Id": 6, "Key": "MaxSilverCreatureNum", "Value": "3"},
                    "7": {"Id": 7, "Key": "UnlockCollectionRange", "Value": "500"},
                    "8": {"Id": 8, "Key": "MaxBagCell", "Value": "3"}}}
                  """);
    }
}

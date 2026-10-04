using Lunaria.Game.Resources.Tables;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lunaria.Game.Resources;

/// <summary>
/// Use Binary/Server tables, but take DefaultMap from the client build so the prologue starts on the right map.
/// </summary>
public sealed class GameData(string assetsDir, ILogger<GameData>? logger = null) : IHostedService
{
    internal readonly Dictionary<string, PGameTimeTable> PGameTimeTable = [];
    internal readonly Dictionary<string, CNPCGroupTable> CNPCGroupTable = [];
    internal readonly Dictionary<string, CSubRegionConfigReadTarget> CSubRegionConfigReadTarget = [];
    internal readonly Dictionary<string, CSubRegionData> CSubRegionData = [];
    internal readonly Dictionary<string, CSystemIDTable> CSystemIDTable = [];
    internal readonly Dictionary<string, CTaskTaskUITable> CTaskTaskUITable = [];
    internal readonly Dictionary<string, CTaskTaskUITablePOI> CTaskTaskUITablePOI = [];
    internal readonly Dictionary<string, PAchievementTable> PAchievementTable = [];
    internal readonly Dictionary<string, PActivityTable> PActivityTable = [];
    internal readonly Dictionary<string, PBattleFieldRewardGroupTable> PBattleFieldRewardGroupTable = [];
    internal readonly Dictionary<string, PBattleFieldRewardTable> PBattleFieldRewardTable = [];
    internal readonly Dictionary<string, PBattlePass> PBattlePass = [];
    internal readonly Dictionary<string, PBattlePassLevel> PBattlePassLevel = [];
    internal readonly Dictionary<string, PBreakTemplateTable> PBreakTemplateTable = [];
    internal readonly Dictionary<string, PCaseClueTable> PCaseClueTable = [];
    internal readonly Dictionary<string, PCaseEvidenceTable> PCaseEvidenceTable = [];
    internal readonly Dictionary<string, PCaseStageTable> PCaseStageTable = [];
    internal readonly Dictionary<string, PCaseTable> PCaseTable = [];
    internal readonly Dictionary<string, PCharacterConstTable> PCharacterConstTable = [];
    internal readonly Dictionary<string, PCharacterSkillGroupTable> PCharacterSkillGroupTable = [];
    internal readonly Dictionary<string, PCharacterTable> PCharacterTable = [];
    internal readonly Dictionary<string, PChargeAwardTable> PChargeAwardTable = [];
    internal readonly Dictionary<string, PChargeMoneyTable> PChargeMoneyTable = [];
    internal readonly Dictionary<string, PCollectionDropTable> PCollectionDropTable = [];
    internal readonly Dictionary<string, PCollectionSubRegionMapping> PCollectionSubRegionMapping = [];
    internal readonly Dictionary<string, PCollectionTable> PCollectionTable = [];
    internal readonly Dictionary<string, PDailyMissionRewardTable> PDailyMissionRewardTable = [];
    internal readonly Dictionary<string, PDailyMissionTable> PDailyMissionTable = [];
    internal readonly Dictionary<string, PDevelopAttributeTable> PDevelopAttributeTable = [];
    internal readonly Dictionary<string, PDungeonsTypeTable> PDungeonsTypeTable = [];
    internal readonly Dictionary<string, PFixedAttributeTable> PFixedAttributeTable = [];
    internal readonly Dictionary<string, PFixedDropTable> PFixedDropTable = [];
    internal readonly Dictionary<string, PFunctionalNPCTable> PFunctionalNPCTable = [];
    internal readonly Dictionary<string, PGachaRebateTable> PGachaRebateTable = [];
    internal readonly Dictionary<string, PGachaTable> PGachaTable = [];
    internal readonly Dictionary<string, PGemGlobalConfig> PGemGlobalConfig = [];
    internal readonly Dictionary<string, PGemTable> PGemTable = [];
    internal readonly Dictionary<string, PGlobalEventFinishTable> PGlobalEventFinishTable = [];
    internal readonly Dictionary<string, PGraphicGuideTable> PGraphicGuideTable = [];
    internal readonly Dictionary<string, PHordeTable> PHordeTable = [];
    internal readonly Dictionary<string, PHouseIndustryTable> PHouseIndustryTable = [];
    internal readonly Dictionary<string, PHouseLevelTable> PHouseLevelTable = [];
    internal readonly Dictionary<string, PHouseV2Table> PHouseV2Table = [];
    internal readonly Dictionary<string, PInsideAttributeTable> PInsideAttributeTable = [];
    internal readonly Dictionary<string, PItemEffectTable> PItemEffectTable = [];
    internal readonly Dictionary<string, PItemEffectTypeTable> PItemEffectTypeTable = [];
    internal readonly Dictionary<string, PItemTable> PItemTable = [];
    internal readonly Dictionary<string, PItemTypeTable> PItemTypeTable = [];
    internal readonly Dictionary<string, PLevelUpExpTable> PLevelUpExpTable = [];
    internal readonly Dictionary<string, PLevelUpTemplateTable> PLevelUpTemplateTable = [];
    internal readonly Dictionary<string, PLimitGroupTable> PLimitGroupTable = [];
    internal readonly Dictionary<string, PMapDataTable> PMapDataTable = [];
    internal readonly Dictionary<string, PMoneyTable> PMoneyTable = [];
    internal readonly Dictionary<string, PMonthCardTable> PMonthCardTable = [];
    internal readonly Dictionary<string, PMotiveAttributeTable> PMotiveAttributeTable = [];
    internal readonly Dictionary<string, PMotiveBreakTemplateTable> PMotiveBreakTemplateTable = [];
    internal readonly Dictionary<string, PMotiveLevelCostTable> PMotiveLevelCostTable = [];
    internal readonly Dictionary<string, PMotiveLevelTemplateTable> PMotiveLevelTemplateTable = [];
    internal readonly Dictionary<string, PMotiveTable> PMotiveTable = [];
    internal readonly Dictionary<string, PNPCGroupEnterBattle> PNPCGroupEnterBattle = [];
    internal readonly Dictionary<string, POutsideAttributeTable> POutsideAttributeTable = [];
    internal readonly Dictionary<string, POutsideBuffTable> POutsideBuffTable = [];
    internal readonly Dictionary<string, PRefreshConfigTable> PRefreshConfigTable = [];
    internal readonly Dictionary<string, PRegionProgressTable> PRegionProgressTable = [];
    internal readonly Dictionary<string, PRegionProgressTableDayfair> PRegionProgressTableDayfair = [];
    internal readonly Dictionary<string, PRegionProgressTableFour> PRegionProgressTableFour = [];
    internal readonly Dictionary<string, PRegionProgressTableMorgue> PRegionProgressTableMorgue = [];
    internal readonly Dictionary<string, PRegionProgressTypeRegistTable> PRegionProgressTypeRegistTable = [];
    internal readonly Dictionary<string, PRegionRewardDataTable> PRegionRewardDataTable = [];
    internal readonly Dictionary<string, PRegionRewardTable> PRegionRewardTable = [];
    internal readonly Dictionary<string, PRegionSequenceTable> PRegionSequenceTable = [];
    internal readonly Dictionary<string, PRegionSequenceTableDayfair> PRegionSequenceTableDayfair = [];
    internal readonly Dictionary<string, PRegionSequenceTableFour> PRegionSequenceTableFour = [];
    internal readonly Dictionary<string, PRegionSequenceTableMorgue> PRegionSequenceTableMorgue = [];
    internal readonly Dictionary<string, PRegionTable> PRegionTable = [];
    internal readonly Dictionary<string, PRepeatableDungeonsTable> PRepeatableDungeonsTable = [];
    internal readonly Dictionary<string, PSavePointTemplateTable> PSavePointTemplateTable = [];
    internal readonly Dictionary<string, PShopGoodsTable> PShopGoodsTable = [];
    internal readonly Dictionary<string, PShopBuffTable> PShopBuffTable = [];
    internal readonly Dictionary<string, PShopTable> PShopTable = [];
    internal readonly Dictionary<string, PSignInActivityRewardTable> PSignInActivityRewardTable = [];
    internal readonly Dictionary<string, PSilverCreatureCombineTable> PSilverCreatureCombineTable = [];
    internal readonly Dictionary<string, PSilverCreatureGrowthTable> PSilverCreatureGrowthTable = [];
    internal readonly Dictionary<string, PSkillGrowthCostTable> PSkillGrowthCostTable = [];
    internal readonly Dictionary<string, PSkillGrowthTable> PSkillGrowthTable = [];
    internal readonly Dictionary<string, PSubRegionNPCGroup> PSubRegionNPCGroup = [];
    internal readonly Dictionary<string, PSubRegionTable> PSubRegionTable = [];
    internal readonly Dictionary<string, PTalentContentTable> PTalentContentTable = [];
    internal readonly Dictionary<string, PTalentNodeTable> PTalentNodeTable = [];
    internal readonly Dictionary<string, PTaskActionsDailyTask> PTaskActionsDailyTask = [];
    internal readonly Dictionary<string, PTaskActionsPOIQuest> PTaskActionsPOIQuest = [];
    internal readonly Dictionary<string, PTaskActionsQuestMain> PTaskActionsQuestMain = [];
    internal readonly Dictionary<string, PTaskActionsWanted> PTaskActionsWanted = [];
    internal readonly Dictionary<string, PTaskStepsDailyTask> PTaskStepsDailyTask = [];
    internal readonly Dictionary<string, PTaskStepsPOIQuest> PTaskStepsPOIQuest = [];
    internal readonly Dictionary<string, PTaskStepsQuestMain> PTaskStepsQuestMain = [];
    internal readonly Dictionary<string, PTaskStepsWanted> PTaskStepsWanted = [];
    internal readonly Dictionary<string, PTasksListDailyTask> PTasksListDailyTask = [];
    internal readonly Dictionary<string, PTasksListPOIQuest> PTasksListPOIQuest = [];
    internal readonly Dictionary<string, PTasksListQuestMain> PTasksListQuestMain = [];
    internal readonly Dictionary<string, PTasksListWanted> PTasksListWanted = [];
    internal readonly Dictionary<string, PTeamExpAwardTable> PTeamExpAwardTable = [];
    internal readonly Dictionary<string, PTeamLevelTable> PTeamLevelTable = [];
    internal readonly Dictionary<string, PTeleportPointTemplateTable> PTeleportPointTemplateTable = [];
    internal readonly Dictionary<string, PTemplateMailTable> PTemplateMailTable = [];
    internal readonly Dictionary<string, PTmpCharacterTable> PTmpCharacterTable = [];
    internal readonly Dictionary<string, PTmpTeamTable> PTmpTeamTable = [];
    internal readonly Dictionary<string, PUnlockConditionTable> PUnlockConditionTable = [];
    internal readonly Dictionary<string, PUnlockFeatureTable> PUnlockFeatureTable = [];
    internal readonly Dictionary<string, PWPAdvContentTable> PWPAdvContentTable = [];
    internal readonly Dictionary<string, PWPAdvDialogTable> PWPAdvDialogTable = [];
    internal readonly Dictionary<string, PWPAdvOptionTable> PWPAdvOptionTable = [];
    internal readonly Dictionary<string, PWPAdventureTable> PWPAdventureTable = [];
    internal readonly Dictionary<string, PWPBlessShop> PWPBlessShop = [];
    internal readonly Dictionary<string, PWPCreatureShop> PWPCreatureShop = [];
    internal readonly Dictionary<string, PWPRelicShop> PWPRelicShop = [];
    internal readonly Dictionary<string, SWantedPosterAwardTable> SWantedPosterAwardTable = [];
    internal readonly Dictionary<string, PWantedPosterBlessBondTable> PWantedPosterBlessBondTable = [];
    internal readonly Dictionary<string, PWantedPosterBlessTable> PWantedPosterBlessTable = [];
    internal readonly Dictionary<string, PWantedPosterConfig> PWantedPosterConfig = [];
    internal readonly Dictionary<string, PWantedPosterCreatureTable> PWantedPosterCreatureTable = [];
    internal readonly Dictionary<string, PWantedPosterEffect> PWantedPosterEffect = [];
    internal readonly Dictionary<string, PWantedPosterEntryTable> PWantedPosterEntryTable = [];
    internal readonly Dictionary<string, PWantedPosterEventTable> PWantedPosterEventTable = [];
    internal readonly Dictionary<string, PWantedPosterNPC> PWantedPosterNPC = [];
    internal readonly Dictionary<string, PWantedPosterProcessTable> PWantedPosterProcessTable = [];
    internal readonly Dictionary<string, PWantedPosterRelicTable> PWantedPosterRelicTable = [];
    internal readonly Dictionary<string, PWantedPosterRevive> PWantedPosterRevive = [];
    internal readonly Dictionary<string, PWantedPosterShop> PWantedPosterShop = [];
    internal readonly Dictionary<string, PWantedPosterStepCountTable> PWantedPosterStepCountTable = [];
    internal readonly Dictionary<string, PWantedPosterTable> PWantedPosterTable = [];
    internal readonly Dictionary<string, PWorldCollectObjTable> PWorldCollectObjTable = [];
    internal readonly Dictionary<string, PWorldLevelTable> PWorldLevelTable = [];
    internal readonly Dictionary<string, SDropTable> SDropTable = [];
    internal readonly Dictionary<string, SPlayerIniTable> SPlayerIniTable = [];
    internal readonly Dictionary<string, SServerGlobalConfig> SServerGlobalConfig = [];

    public CharacterAssets Characters { get; private set; } = null!;
    public AttribAssets Attribs { get; private set; } = null!;
    public InsideAttributeAssets Inside { get; private set; } = null!;
    public GlobalConfigAssets GlobalConfig { get; private set; } = null!;
    public ProgressionAssets Progression { get; private set; } = null!;
    public TeamExpAssets TeamExpAwards { get; private set; } = null!;
    public ItemAssets Items { get; private set; } = null!;
    public ItemEffectAssets ItemEffects { get; private set; } = null!;
    public MapAssets Maps { get; private set; } = null!;
    public SkillAssets Skills { get; private set; } = null!;
    public TalentAssets Talents { get; private set; } = null!;
    public CharacterBonusAssets Bonuses { get; private set; } = null!;
    public GuideAssets Guides { get; private set; } = null!;
    public DropAssets Drops { get; private set; } = null!;
    public MailAssets Mail { get; private set; } = null!;
    public LimitAssets Limits { get; private set; } = null!;
    public ShopAssets Shops { get; private set; } = null!;
    public MotiveAssets Motives { get; private set; } = null!;
    public GachaAssets Gacha { get; private set; } = null!;
    public NoticeAssets Notices { get; private set; } = null!;
    public CollectionAssets Collections { get; private set; } = null!;
    public GemAssets Gems { get; private set; } = null!;
    public ExposeAssets Expose { get; private set; } = null!;
    public TaskAssets Tasks { get; private set; } = null!;
    public GameTimeAssets GameTime { get; private set; } = null!;
    public CaseAssets Cases { get; private set; } = null!;
    public AchievementAssets Achievements { get; private set; } = null!;
    public UnlockAssets Unlocks { get; private set; } = null!;
    public BattlePassAssets BattlePasses { get; private set; } = null!;
    public HouseAssets Houses { get; private set; } = null!;
    public DailyMissionAssets DailyMissions { get; private set; } = null!;
    public SignInAssets SignIn { get; private set; } = null!;
    public RegionProgressAssets RegionProgress { get; private set; } = null!;
    public SilverCreatureAssets SilverCreatures { get; private set; } = null!;
    public TmpTeamAssets TmpTeams { get; private set; } = null!;
    public ChargeAssets Charge { get; private set; } = null!;
    public DropTableAssets DropTable { get; private set; } = null!;
    public BattleRewardAssets BattleRewards { get; private set; } = null!;
    public DungeonAssets Dungeons { get; private set; } = null!;
    public WantedAssets Wanted { get; private set; } = null!;
    public StarterAssets Starter { get; private set; } = null!;
    public GameplayPolicy Policy { get; private set; } = null!;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (logger is not null)
            foreach (var warning in ContentValidator.Validate(assetsDir))
                logger.LogWarning("Content: {Warning}", warning);
        Resources.Initialize(Path.Combine(assetsDir, "tables"));
        TableLoader.Initialize(this);
        Policy = GameplayPolicy.Load(Path.Combine(assetsDir, "gameplay-policy.json"));

        Inside = new InsideAttributeAssets(PInsideAttributeTable);

        Characters = new CharacterAssets(
            PCharacterTable, PCharacterSkillGroupTable, PSkillGrowthTable, PBreakTemplateTable, PLevelUpTemplateTable);
        var columns = new AttributeColumns(Inside, POutsideAttributeTable);
        Attribs = new AttribAssets(PDevelopAttributeTable, PFixedAttributeTable, POutsideAttributeTable, Inside, columns);
        GlobalConfig = new GlobalConfigAssets(SServerGlobalConfig);
        Progression = new ProgressionAssets(PLevelUpExpTable, PTeamLevelTable, PWorldLevelTable);
        TeamExpAwards = new TeamExpAssets(PTeamExpAwardTable);
        Items = new ItemAssets(PItemTable, PItemTypeTable, PMoneyTable, PCharacterConstTable);
        ItemEffects = new ItemEffectAssets(PItemTable, PItemEffectTable, PItemEffectTypeTable, POutsideBuffTable);
        Maps = new MapAssets(PMapDataTable, PFunctionalNPCTable, PSavePointTemplateTable, PTeleportPointTemplateTable);
        Expose = new ExposeAssets(PSubRegionNPCGroup, PNPCGroupEnterBattle, CNPCGroupTable);
        Skills = new SkillAssets(PSkillGrowthTable, PSkillGrowthCostTable, Items, Policy.SkillCostItemOverrides);
        Talents = new TalentAssets(PTalentNodeTable);
        Guides = new GuideAssets(PGraphicGuideTable);

        Drops = new DropAssets(PFixedDropTable);
        DropTable = new DropTableAssets(SDropTable, Items);
        BattleRewards = new BattleRewardAssets(PBattleFieldRewardTable, PBattleFieldRewardGroupTable);
        Limits = new LimitAssets(PLimitGroupTable, PRefreshConfigTable);
        Mail = new MailAssets(PTemplateMailTable);
        Shops = new ShopAssets(PShopTable, PShopGoodsTable, PShopBuffTable);

        Motives = new MotiveAssets(
            PMotiveTable, PMotiveLevelCostTable, PMotiveLevelTemplateTable,
            PMotiveBreakTemplateTable, PMotiveAttributeTable);
        Bonuses = new CharacterBonusAssets(PMotiveAttributeTable, PTalentContentTable, columns, Inside, Motives);

        Gacha = new GachaAssets(
            PGachaTable, PGachaRebateTable, Characters, Motives, Items,
            Path.Combine(assetsDir, "banners.json"));
        Notices = new NoticeAssets(Path.Combine(assetsDir, "notices.json"));
        Collections = new CollectionAssets(PCollectionTable, PCollectionDropTable, PWorldCollectObjTable, DropTable, Limits);
        Gems = new GemAssets(PGemTable, PGemGlobalConfig);

        GameTime = new GameTimeAssets(PGameTimeTable);
        Tasks = new TaskAssets(
            PTasksListQuestMain, PTaskStepsQuestMain, PTaskActionsQuestMain, CTaskTaskUITable,
            PTasksListPOIQuest, PTaskStepsPOIQuest, PTaskActionsPOIQuest, CTaskTaskUITablePOI,
            PTasksListWanted, PTaskStepsWanted, PTaskActionsWanted,
            PTasksListDailyTask, PTaskStepsDailyTask, PTaskActionsDailyTask,
            Items);

        Cases = new CaseAssets(PCaseTable, PCaseStageTable, PCaseClueTable, PCaseEvidenceTable);
        Achievements = new AchievementAssets(PAchievementTable, PGlobalEventFinishTable);
        Unlocks = new UnlockAssets(PUnlockConditionTable, PUnlockFeatureTable, CSystemIDTable, PGlobalEventFinishTable);
        BattlePasses = new BattlePassAssets(PBattlePass, PBattlePassLevel);
        Houses = new HouseAssets(PHouseV2Table, PHouseLevelTable, PHouseIndustryTable);

        DailyMissions = new DailyMissionAssets(
            PDailyMissionTable, PDailyMissionRewardTable, PGlobalEventFinishTable);
        SignIn = new SignInAssets(PActivityTable, PSignInActivityRewardTable);

        RegionProgress = new RegionProgressAssets(
            PRegionProgressTable, PRegionRewardTable, PRegionRewardDataTable, PRegionSequenceTable,
            PSubRegionTable, PRegionTable, PRegionProgressTypeRegistTable, CSubRegionConfigReadTarget,
            PRegionProgressTableMorgue, PRegionProgressTableFour, PRegionProgressTableDayfair,
            PRegionSequenceTableMorgue, PRegionSequenceTableFour, PRegionSequenceTableDayfair,
            Drops);
        SilverCreatures = new SilverCreatureAssets(PSilverCreatureCombineTable, PSilverCreatureGrowthTable);
        TmpTeams = new TmpTeamAssets(PTmpTeamTable, PTmpCharacterTable, Characters);
        Charge = new ChargeAssets(PChargeAwardTable, PChargeMoneyTable, PMonthCardTable, Items);
        Dungeons = new DungeonAssets(PRepeatableDungeonsTable, PDungeonsTypeTable, PHordeTable, DropTable);

        Wanted = new WantedAssets(
            PWantedPosterTable, PWantedPosterEntryTable, PWantedPosterProcessTable, PWantedPosterEventTable,
            PWantedPosterStepCountTable, PWantedPosterNPC, PWantedPosterBlessTable, PWantedPosterBlessBondTable,
            PWantedPosterRelicTable, PWantedPosterCreatureTable, PWantedPosterShop,
            PWPBlessShop, PWPRelicShop, PWPCreatureShop, PWantedPosterRevive,
            PWPAdventureTable, PWPAdvContentTable, PWPAdvDialogTable, PWPAdvOptionTable,
            PWantedPosterConfig, PWantedPosterEffect, Items, SWantedPosterAwardTable, Policy.Wanted);

        Starter = new StarterAssets(SPlayerIniTable, Maps, Items, Characters, GlobalConfig, Tasks);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

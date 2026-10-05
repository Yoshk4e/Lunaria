using System.Text.Json.Serialization;
using Lunaria.Game.Resources;
using Lunaria.Game.World;

namespace Lunaria.Game.Player.Persistence.Saves;

/// <summary>Saved timestamps use Unix seconds. GameTimeMinutes uses minutes.</summary>
public sealed record RoleSaveDocument
{
    // Missing in pre-versioned saves. Only capture/migration sets the current version.
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("pending_reward_mail")]
    public IReadOnlyList<IReadOnlyList<ItemGrant>> PendingRewardMail { get; init; } = [];

    [JsonPropertyName("progress")]
    public ProgressSave? Progress { get; init; }

    [JsonPropertyName("character_vitals")]
    public IReadOnlyList<CharacterVitalSave> CharacterVitals { get; init; } = [];

    [JsonPropertyName("map")]
    public MapSave? Map { get; init; }

    [JsonPropertyName("teams")]
    public IReadOnlyList<TeamSave> Teams { get; init; } = [];

    [JsonPropertyName("current_team")]
    public uint CurrentTeam { get; init; }

    [JsonPropertyName("using_member_slot")]
    public uint UsingMemberSlot { get; init; }

    [JsonPropertyName("skill_groups")]
    public IReadOnlyList<SkillGroupSave> SkillGroups { get; init; } = [];

    [JsonPropertyName("talents")]
    public IReadOnlyList<TalentSave> Talents { get; init; } = [];

    [JsonPropertyName("bag")]
    public IReadOnlyList<ItemSave> Bag { get; init; } = [];

    [JsonPropertyName("item_cds")]
    public IReadOnlyList<ItemCdSave> ItemCds { get; init; } = [];

    [JsonPropertyName("wallet")]
    public IReadOnlyList<MoneySave> Wallet { get; init; } = [];

    [JsonPropertyName("limits")]
    public IReadOnlyList<LimitSave> Limits { get; init; } = [];

    [JsonPropertyName("shop")]
    public IReadOnlyList<ShopSave> Shop { get; init; } = [];

    [JsonPropertyName("gacha")]
    public IReadOnlyList<GachaSave> Gacha { get; init; } = [];

    [JsonPropertyName("collections")]
    public IReadOnlyList<CollectionSave> Collections { get; init; } = [];

    public IReadOnlyList<uint> GatheredCollections { get; init; } = [];

    public uint GameTimeMinutes { get; init; }
    public uint? CurrentWeather { get; init; }

    [JsonPropertyName("quests")]
    public QuestsSave? Quests { get; init; }

    [JsonPropertyName("cases")]
    public CaseSave? Cases { get; init; }

    [JsonPropertyName("achievements")]
    public AchievementSave? Achievements { get; init; }

    [JsonPropertyName("houses")]
    public HouseSave? Houses { get; init; }

    [JsonPropertyName("daily_missions")]
    public DailyMissionSave? DailyMissions { get; init; }

    [JsonPropertyName("signin")]
    public SignInSave? SignIn { get; init; }

    [JsonPropertyName("battle_passes")]
    public BattlePassSave? BattlePasses { get; init; }

    [JsonPropertyName("region_progress")]
    public RegionProgressSave? RegionProgress { get; init; }

    [JsonPropertyName("silver_creatures")]
    public SilverCreatureSave? SilverCreatures { get; init; }

    [JsonPropertyName("buffs")]
    public IReadOnlyList<BuffSave> Buffs { get; init; } = [];

    [JsonPropertyName("temp_teams")]
    public IReadOnlyList<TempTeamSave> TempTeams { get; init; } = [];

    public IReadOnlyList<TemporaryTeamSelection> TemporarySelections { get; init; } = [];
    public ActiveTemporaryTeam? ActiveTemporaryTeam { get; init; }
    public ActiveTemporaryTeam? SuspendedStoryTeam { get; init; }

    [JsonPropertyName("red_point")]
    public RedPointSave? RedPoint { get; init; }

    [JsonPropertyName("month_cards")]
    public IReadOnlyList<MonthCardSave> MonthCards { get; init; } = [];

    [JsonPropertyName("charge_purchases")]
    public IReadOnlyList<uint> ChargePurchases { get; init; } = [];

    [JsonPropertyName("dungeons")]
    public DungeonSave? Dungeons { get; init; }

    [JsonPropertyName("wanted")]
    public IReadOnlyList<WantedFinishSave> Wanted { get; init; } = [];

    [JsonPropertyName("wanted_run")]
    public WantedRunSave? WantedRun { get; init; }

    [JsonPropertyName("patrol_cooldowns")]
    public IReadOnlyList<PatrolCooldownSave> PatrolCooldowns { get; init; } = [];

    public sealed record ProgressSave
    {
        [JsonPropertyName("team_level")]
        public uint TeamLevel { get; init; }

        [JsonPropertyName("team_exp")]
        public uint TeamExp { get; init; }

        [JsonPropertyName("satiety")]
        public int Satiety { get; init; }

        [JsonPropertyName("stamina")]
        public int Stamina { get; init; }

        [JsonPropertyName("stamina_tick_at")]
        public long StaminaTickAt { get; init; }

        /// <summary>Selected world level. If absent, use the earned tier.</summary>
        [JsonPropertyName("world_level_selection")]
        public uint? WorldLevelSelection { get; init; }
    }

    /// <summary>Null vitals mean the current maximum. Older saves omit this list.</summary>
    public sealed record CharacterVitalSave
    {
        [JsonPropertyName("inst_id")]
        public ulong InstId { get; init; }

        [JsonPropertyName("hp")]
        public int? Hp { get; init; }

        [JsonPropertyName("permanent_liquid")]
        public int? PermanentLiquid { get; init; }
    }

    public sealed record MapSave
    {
        [JsonPropertyName("return_point")]
        public MapReturnPoint? ReturnPoint { get; init; }

        [JsonPropertyName("map_id")]
        public ulong MapId { get; init; }

        [JsonPropertyName("savepoint")]
        public ulong Savepoint { get; init; }

        [JsonPropertyName("unlocked_savepoints")]
        public IReadOnlyList<ulong> UnlockedSavepoints { get; init; } = [];

        [JsonPropertyName("unlocked_teleports")]
        public IReadOnlyList<ulong> UnlockedTeleports { get; init; } = [];

        [JsonPropertyName("x")]
        public int X { get; init; }

        [JsonPropertyName("y")]
        public int Y { get; init; }

        [JsonPropertyName("z")]
        public int Z { get; init; }

        [JsonPropertyName("tracked_targets")]
        public IReadOnlyList<TrackedTargetSave> TrackedTargets { get; init; } = [];
    }

    public sealed record TrackedTargetSave
    {
        [JsonPropertyName("map_id")]
        public ulong MapId { get; init; }

        [JsonPropertyName("tag_id")]
        public ulong TagId { get; init; }

        [JsonPropertyName("tag_type")]
        public uint TagType { get; init; }
    }

    public sealed record TeamSave
    {
        [JsonPropertyName("team_id")]
        public uint TeamId { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("members")]
        public IReadOnlyList<TeamMemberSave> Members { get; init; } = [];

        [JsonPropertyName("temporary_liquid")]
        public TeamLiquidSave? TemporaryLiquid { get; init; }

        [JsonPropertyName("temporary_liquid_lv2")]
        public TeamLiquidSave? TemporaryLiquidLv2 { get; init; }
    }

    public sealed record TeamLiquidSave
    {
        [JsonPropertyName("fire")]
        public int Fire { get; init; }

        [JsonPropertyName("ice")]
        public int Ice { get; init; }

        [JsonPropertyName("thunder")]
        public int Thunder { get; init; }

        [JsonPropertyName("gravity")]
        public int Gravity { get; init; }

        [JsonPropertyName("radiate")]
        public int Radiate { get; init; }

        [JsonPropertyName("silver")]
        public int Silver { get; init; }

        [JsonPropertyName("blackiron")]
        public int Blackiron { get; init; }
    }

    public sealed record TeamMemberSave
    {
        [JsonPropertyName("slot")]
        public uint Slot { get; init; }

        [JsonPropertyName("inst_id")]
        public ulong InstId { get; init; }

        /// <summary>Catalyst item per gem slot, 0 for an empty slot.</summary>
        [JsonPropertyName("gems")]
        public IReadOnlyList<uint> Gems { get; init; } = [];
    }

    public sealed record SkillGroupSave
    {
        [JsonPropertyName("group")]
        public uint Group { get; init; }

        [JsonPropertyName("level")]
        public uint Level { get; init; }
    }

    public sealed record TalentSave
    {
        [JsonPropertyName("inst_id")]
        public ulong InstId { get; init; }

        [JsonPropertyName("mask0")]
        public ulong Mask0 { get; init; }

        [JsonPropertyName("mask1")]
        public ulong Mask1 { get; init; }
    }

    public sealed record ItemSave
    {
        [JsonPropertyName("item_id")]
        public uint ItemId { get; init; }

        [JsonPropertyName("count")]
        public uint Count { get; init; }

        [JsonPropertyName("is_new")]
        public bool IsNew { get; init; }
    }

    public sealed record ItemCdSave
    {
        [JsonPropertyName("cd_type")]
        public uint CdType { get; init; }

        [JsonPropertyName("ready_unix")]
        public long ReadyUnix { get; init; }
    }

    public sealed record MoneySave
    {
        [JsonPropertyName("money_type")]
        public int MoneyType { get; init; }

        [JsonPropertyName("amount")]
        public long Amount { get; init; }
    }

    public sealed record LimitSave
    {
        [JsonPropertyName("group")]
        public uint Group { get; init; }

        [JsonPropertyName("count")]
        public uint Count { get; init; }

        [JsonPropertyName("anchor")]
        public long Anchor { get; init; }
    }

    public sealed record ShopSave
    {
        [JsonPropertyName("good")]
        public uint Good { get; init; }

        [JsonPropertyName("count")]
        public uint Count { get; init; }

        [JsonPropertyName("anchor")]
        public long Anchor { get; init; }
    }

    public sealed record GachaSave
    {
        [JsonPropertyName("banner")]
        public uint Banner { get; init; }

        [JsonPropertyName("total")]
        public uint Total { get; init; }

        [JsonPropertyName("since_five")]
        public uint SinceFive { get; init; }

        [JsonPropertyName("since_four")]
        public uint SinceFour { get; init; }

        [JsonPropertyName("featured_since")]
        public uint FeaturedSince { get; init; }

        [JsonPropertyName("guaranteed")]
        public bool Guaranteed { get; init; }

        [JsonPropertyName("claimed_mask")]
        public uint ClaimedMask { get; init; }

        [JsonPropertyName("daily_count")]
        public uint DailyCount { get; init; }

        [JsonPropertyName("anchor")]
        public long Anchor { get; init; }
    }

    public sealed record CollectionSave
    {
        [JsonPropertyName("uniq")]
        public ulong Uniq { get; init; }

        [JsonPropertyName("cfg")]
        public uint Cfg { get; init; }

        [JsonPropertyName("status")]
        public int Status { get; init; }

        [JsonPropertyName("status_time")]
        public long StatusTime { get; init; }

        [JsonPropertyName("block")]
        public ulong Block { get; init; }

        [JsonPropertyName("x")]
        public int X { get; init; }

        [JsonPropertyName("y")]
        public int Y { get; init; }

        [JsonPropertyName("z")]
        public int Z { get; init; }
    }

    public sealed record QuestsSave
    {
        [JsonPropertyName("applied_effects")]
        public IReadOnlyList<TaskTargetRefSave> AppliedEffects { get; init; } = [];

        [JsonPropertyName("reported_targets")]
        public IReadOnlyList<TaskTargetRefSave> ReportedTargets { get; init; } = [];

        [JsonPropertyName("processing")]
        public IReadOnlyList<QuestSave> Processing { get; init; } = [];

        [JsonPropertyName("finished")]
        public IReadOnlyList<QuestRefSave> Finished { get; init; } = [];
    }

    public sealed record QuestSave
    {
        [JsonPropertyName("task_type")]
        public uint TaskType { get; init; }

        [JsonPropertyName("task")]
        public uint Task { get; init; }

        [JsonPropertyName("step")]
        public ulong Step { get; init; }

        [JsonPropertyName("actions")]
        public IReadOnlyList<TaskActionSave> Actions { get; init; } = [];
    }

    public sealed record QuestRefSave
    {
        [JsonPropertyName("task_type")]
        public uint TaskType { get; init; }

        [JsonPropertyName("task")]
        public uint Task { get; init; }
    }

    public sealed record TaskActionSave
    {
        [JsonPropertyName("id")]
        public ulong Id { get; init; }

        [JsonPropertyName("progress")]
        public uint Progress { get; init; }

        [JsonPropertyName("max")]
        public uint Max { get; init; }
    }

    public sealed record TaskTargetRefSave
    {
        [JsonPropertyName("task_type")]
        public uint TaskType { get; init; }

        [JsonPropertyName("action")]
        public ulong Action { get; init; }
    }

    public sealed record CaseSave
    {
        [JsonPropertyName("processing")]
        public IReadOnlyList<CaseProcessingSave> Processing { get; init; } = [];

        [JsonPropertyName("finished")]
        public IReadOnlyList<uint> Finished { get; init; } = [];
    }

    public sealed record CaseProcessingSave
    {
        [JsonPropertyName("case_id")]
        public uint CaseId { get; init; }

        [JsonPropertyName("finished_phase")]
        public uint FinishedPhase { get; init; }

        [JsonPropertyName("on_slot_clues")]
        public IReadOnlyList<ulong> OnSlotClues { get; init; } = [];

        [JsonPropertyName("decrypted_evidence")]
        public IReadOnlyList<ulong> DecryptedEvidence { get; init; } = [];

        public IReadOnlyList<ulong> OwnedClues { get; init; } = [];
        public IReadOnlyList<ulong> OwnedEvidence { get; init; } = [];
    }

    public sealed record AchievementSave
    {
        [JsonPropertyName("events")]
        public IReadOnlyList<FinishEventSave> Events { get; init; } = [];

        [JsonPropertyName("claimed")]
        public IReadOnlyList<uint> Claimed { get; init; } = [];
    }

    public sealed record FinishEventSave
    {
        [JsonPropertyName("event_id")]
        public uint EventId { get; init; }

        [JsonPropertyName("progress")]
        public ulong Progress { get; init; }

        [JsonPropertyName("finish")]
        public bool Finish { get; init; }
    }

    public sealed record HouseSave
    {
        [JsonPropertyName("houses")]
        public IReadOnlyList<HouseEntrySave> Houses { get; init; } = [];
    }

    public sealed record HouseEntrySave
    {
        [JsonPropertyName("house_id")]
        public uint HouseId { get; init; }

        /// <summary>ENM_HOUSE_STATE_PURCHASED (1) or ENM_HOUSE_STATE_OPEN (2).</summary>
        [JsonPropertyName("state")]
        public int State { get; init; }

        [JsonPropertyName("level")]
        public uint Level { get; init; }

        [JsonPropertyName("income_anchor")]
        public long IncomeAnchor { get; init; }

        public uint BankedIncome { get; init; }
    }

    public sealed record DailyMissionSave
    {
        [JsonPropertyName("day_anchor")]
        public long DayAnchor { get; init; }

        [JsonPropertyName("missions")]
        public IReadOnlyList<DailyMissionEntrySave> Missions { get; init; } = [];

        [JsonPropertyName("active_point")]
        public uint ActivePoint { get; init; }

        [JsonPropertyName("claimed_rewards")]
        public IReadOnlyList<uint> ClaimedRewards { get; init; } = [];
    }

    public sealed record DailyMissionEntrySave
    {
        [JsonPropertyName("mission_id")]
        public uint MissionId { get; init; }

        [JsonPropertyName("progress")]
        public uint Progress { get; init; }

        [JsonPropertyName("claimed")]
        public bool Claimed { get; init; }
    }

    public sealed record SignInSave
    {
        public IReadOnlyList<SignInActivitySave>? Activities { get; init; }

        [JsonPropertyName("signed_days")]
        public IReadOnlyList<uint> SignedDays { get; init; } = [];

        [JsonPropertyName("claimed_days")]
        public IReadOnlyList<uint> ClaimedDays { get; init; } = [];

        public long? LastSignInDay { get; init; }
    }

    public sealed record SignInActivitySave
    {
        public uint ActivityId { get; init; }
        public IReadOnlyList<uint> SignedDays { get; init; } = [];
        public IReadOnlyList<uint> ClaimedDays { get; init; } = [];
        public long? LastSignInDay { get; init; }
        public uint? AttendanceDays { get; init; }
    }

    public sealed record BattlePassSave
    {
        [JsonPropertyName("passes")]
        public IReadOnlyList<BattlePassEntrySave> Passes { get; init; } = [];
    }

    public sealed record BattlePassEntrySave
    {
        [JsonPropertyName("pass_id")]
        public uint PassId { get; init; }

        [JsonPropertyName("level")]
        public uint Level { get; init; }

        [JsonPropertyName("exp")]
        public uint Exp { get; init; }

        [JsonPropertyName("award_level")]
        public uint AwardLevel { get; init; }
    }

    public sealed record RegionProgressSave
    {
        [JsonPropertyName("subregions")]
        public IReadOnlyList<SubRegionProgressSave> Subregions { get; init; } = [];

        /// <summary>False in saves whose chest and resource objectives counted distinct templates instead of gathers.</summary>
        [JsonPropertyName("gather_counts")]
        public bool GatherCounts { get; init; }
    }

    public sealed record SubRegionProgressSave
    {
        [JsonPropertyName("sub_region_id")]
        public ulong SubRegionId { get; init; }

        [JsonPropertyName("sequences")]
        public IReadOnlyList<SequenceProgressSave> Sequences { get; init; } = [];

        [JsonPropertyName("claimed_values")]
        public IReadOnlyList<uint> ClaimedValues { get; init; } = [];
    }

    public sealed record SequenceProgressSave
    {
        [JsonPropertyName("sequence_id")]
        public uint SequenceId { get; init; }

        [JsonPropertyName("count")]
        public uint Count { get; init; }
    }

    public sealed record SilverCreatureSave
    {
        public uint LastMinted { get; init; }
        [JsonPropertyName("creatures")]
        public IReadOnlyList<SilverCreatureEntrySave> Creatures { get; init; } = [];

        [JsonPropertyName("in_battle_uniq_id")]
        public uint InBattleUniqId { get; init; }
    }

    public sealed record SilverCreatureEntrySave
    {
        [JsonPropertyName("uniq_id")]
        public uint UniqId { get; init; }

        [JsonPropertyName("item_id")]
        public uint ItemId { get; init; }

        [JsonPropertyName("level")]
        public uint Level { get; init; }
    }

    public sealed record BuffSave
    {
        [JsonPropertyName("buff_id")]
        public uint BuffId { get; init; }

        [JsonPropertyName("attach_unix")]
        public long AttachUnix { get; init; }

        /// <summary>Battles left, or -1 when the buff is not battle-counted.</summary>
        [JsonPropertyName("left_battle")]
        public int LeftBattle { get; init; }
    }

    public sealed record TempTeamSave
    {
        [JsonPropertyName("team_src")]
        public uint TeamSrc { get; init; }

        [JsonPropertyName("members")]
        public IReadOnlyList<TempTeamMemberSave> Members { get; init; } = [];
    }

    public sealed record TempTeamMemberSave
    {
        [JsonPropertyName("slot")]
        public uint Slot { get; init; }

        [JsonPropertyName("character_id")]
        public uint CharacterId { get; init; }

        public IReadOnlyList<uint> Gems { get; init; } = [];
    }

    public sealed record RedPointSave
    {
        [JsonPropertyName("exchange_activity_read")]
        public bool ExchangeActivityRead { get; init; }
    }

    public sealed record MonthCardSave
    {
        [JsonPropertyName("card_id")]
        public uint CardId { get; init; }

        [JsonPropertyName("overdue_unix")]
        public long OverdueUnix { get; init; }

        [JsonPropertyName("reward_unix")]
        public long RewardUnix { get; init; }
    }

    public sealed record DungeonSave
    {
        [JsonPropertyName("finishes")]
        public IReadOnlyList<DungeonFinishSave> Finishes { get; init; } = [];

        [JsonPropertyName("types")]
        public IReadOnlyList<DungeonTypeSave> Types { get; init; } = [];

        [JsonPropertyName("hordes")]
        public IReadOnlyList<HordeSave> Hordes { get; init; } = [];

        [JsonPropertyName("current")]
        public DungeonCurrentSave? Current { get; init; }
    }

    public sealed record DungeonCurrentSave
    {
        [JsonPropertyName("dungeon_id")]
        public ulong DungeonId { get; init; }

        [JsonPropertyName("battle_id")]
        public uint BattleId { get; init; }
    }

    public sealed record DungeonFinishSave
    {
        [JsonPropertyName("dungeon_id")]
        public ulong DungeonId { get; init; }

        [JsonPropertyName("count")]
        public uint Count { get; init; }
    }

    public sealed record DungeonTypeSave
    {
        [JsonPropertyName("type_id")]
        public uint TypeId { get; init; }

        [JsonPropertyName("used_day")]
        public uint UsedDay { get; init; }

        [JsonPropertyName("used_week")]
        public uint UsedWeek { get; init; }

        [JsonPropertyName("anchor")]
        public long Anchor { get; init; }
    }

    public sealed record HordeSave
    {
        [JsonPropertyName("horde_id")]
        public uint HordeId { get; init; }

        [JsonPropertyName("kill_count")]
        public uint KillCount { get; init; }

        [JsonPropertyName("star_award")]
        public uint StarAward { get; init; }
    }

    public sealed record PatrolCooldownSave
    {
        [JsonPropertyName("cluster_id")]
        public long ClusterId { get; init; }

        [JsonPropertyName("until_unix")]
        public long UntilUnix { get; init; }
    }

    public sealed record WantedFinishSave
    {
        [JsonPropertyName("entry_id")]
        public uint EntryId { get; init; }

        [JsonPropertyName("count")]
        public uint Count { get; init; }
    }

    public sealed record WantedRunSave
    {
        [JsonPropertyName("suspended")]
        public bool Suspended { get; init; }

        [JsonPropertyName("last_bionics_id")]
        public uint LastBionicsId { get; init; }

        [JsonPropertyName("redeemed_steps")]
        public IReadOnlyList<uint> RedeemedSteps { get; init; } = [];

        [JsonPropertyName("entry_id")]
        public uint EntryId { get; init; }

        [JsonPropertyName("route_id")]
        public uint RouteId { get; init; }

        [JsonPropertyName("max_step")]
        public uint MaxStep { get; init; }

        [JsonPropertyName("step")]
        public uint Step { get; init; }

        [JsonPropertyName("current")]
        public WantedStepSave? Current { get; init; }

        [JsonPropertyName("history")]
        public IReadOnlyList<WantedEventPairSave> History { get; init; } = [];

        [JsonPropertyName("blesses")]
        public IReadOnlyList<uint> Blesses { get; init; } = [];

        [JsonPropertyName("bionics")]
        public IReadOnlyList<WantedBionicsSave> Bionics { get; init; } = [];

        [JsonPropertyName("relics")]
        public IReadOnlyList<uint> Relics { get; init; } = [];

        [JsonPropertyName("coins_total")]
        public uint CoinsTotal { get; init; }

        [JsonPropertyName("revive_count")]
        public uint ReviveCount { get; init; }

        [JsonPropertyName("reset_point")]
        public WantedResetPointSave? ResetPoint { get; init; }

        [JsonPropertyName("adventures")]
        public IReadOnlyList<WantedAdventureSave> Adventures { get; init; } = [];

        [JsonPropertyName("shop_buys")]
        public IReadOnlyList<WantedShopBuySave> ShopBuys { get; init; } = [];
    }

    public sealed record WantedStepSave
    {
        [JsonPropertyName("status")]
        public int Status { get; init; }

        [JsonPropertyName("process_id")]
        public uint ProcessId { get; init; }

        [JsonPropertyName("event_id")]
        public uint EventId { get; init; }

        [JsonPropertyName("event_done")]
        public bool EventDone { get; init; }

        [JsonPropertyName("awards")]
        public IReadOnlyList<WantedStepAwardSave> Awards { get; init; } = [];
    }

    public sealed record WantedStepAwardSave
    {
        [JsonPropertyName("award_id")]
        public uint AwardId { get; init; }

        [JsonPropertyName("type")]
        public uint Type { get; init; }

        [JsonPropertyName("options")]
        public IReadOnlyList<uint> Options { get; init; } = [];

        [JsonPropertyName("chosen")]
        public bool Chosen { get; init; }
    }

    public sealed record WantedEventPairSave
    {
        [JsonPropertyName("process_id")]
        public uint ProcessId { get; init; }

        [JsonPropertyName("event_id")]
        public uint EventId { get; init; }
    }

    public sealed record WantedBionicsSave
    {
        [JsonPropertyName("entry_id")]
        public uint EntryId { get; init; }

        [JsonPropertyName("uniq_id")]
        public uint UniqId { get; init; }
    }

    public sealed record WantedResetPointSave
    {
        [JsonPropertyName("id")]
        public uint Id { get; init; }

        [JsonPropertyName("x")]
        public int X { get; init; }

        [JsonPropertyName("y")]
        public int Y { get; init; }

        [JsonPropertyName("z")]
        public int Z { get; init; }
    }

    public sealed record WantedAdventureSave
    {
        [JsonPropertyName("adventure_id")]
        public uint AdventureId { get; init; }

        [JsonPropertyName("content_id")]
        public uint ContentId { get; init; }

        [JsonPropertyName("dialog_id")]
        public uint DialogId { get; init; }

        [JsonPropertyName("option_result")]
        public int OptionResult { get; init; }
    }

    public sealed record WantedShopBuySave
    {
        [JsonPropertyName("goods_id")]
        public uint GoodsId { get; init; }

        [JsonPropertyName("count")]
        public uint Count { get; init; }
    }
}

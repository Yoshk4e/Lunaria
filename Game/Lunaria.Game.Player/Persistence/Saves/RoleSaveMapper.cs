using Lunaria.Game.Characters;
using Lunaria.Game.Inventory;
using Lunaria.Game.Resources;
using Lunaria.Game.Wanted;
using Msg;

namespace Lunaria.Game.Player.Persistence.Saves;

internal static class RoleSaveMapper
{
    public static RoleSaveDocument Capture(Player player) => new() {
        SchemaVersion = CaptureSchemaVersion(player),
        GameTimeMinutes = CaptureGameTimeMinutes(player),
        CurrentWeather = CaptureCurrentWeather(player),
        PendingRewardMail = CapturePendingRewardMail(player),
        GatheredCollections = CaptureGatheredCollections(player),
        Progress = CaptureProgress(player),
        CharacterVitals = CaptureCharacterVitals(player),
        Map = CaptureMap(player),
        Teams = CaptureTeams(player),
        CurrentTeam = CaptureCurrentTeam(player),
        UsingMemberSlot = CaptureUsingMemberSlot(player),
        SkillGroups = CaptureSkillGroups(player),
        Talents = CaptureTalents(player),
        Bag = CaptureBag(player),
        ItemCds = CaptureItemCds(player),
        Wallet = CaptureWallet(player),
        Limits = CaptureLimits(player),
        Shop = CaptureShop(player),
        Gacha = CaptureGacha(player),
        Collections = CaptureCollections(player),
        Quests = CaptureQuests(player),
        Cases = CaptureCases(player),
        Achievements = CaptureAchievements(player),
        Houses = CaptureHouses(player),
        DailyMissions = CaptureDailyMissions(player),
        SignIn = CaptureSignIn(player),
        BattlePasses = CaptureBattlePasses(player),
        RegionProgress = CaptureRegionProgress(player),
        SilverCreatures = CaptureSilverCreatures(player),
        Buffs = CaptureBuffs(player),
        TemporarySelections = CaptureTemporarySelections(player),
        ActiveTemporaryTeam = CaptureActiveTemporaryTeam(player),
        SuspendedStoryTeam = CaptureSuspendedStoryTeam(player),
        TempTeams = CaptureTempTeams(player),
        RedPoint = CaptureRedPoint(player),
        MonthCards = CaptureMonthCards(player),
        ChargePurchases = CaptureChargePurchases(player),
        Dungeons = CaptureDungeons(player),
        Wanted = CaptureWanted(player),
        WantedRun = CaptureWantedRun(player),
        PatrolCooldowns = CapturePatrolCooldowns(player)
    };

    internal static readonly SaveSection[] Sections = [
        new("schema_version", player => System.Text.Json.JsonSerializer.Serialize(CaptureSchemaVersion(player), SaveJson.Options),
            player => false),
        new("GameTimeMinutes", player => System.Text.Json.JsonSerializer.Serialize(CaptureGameTimeMinutes(player), SaveJson.Options),
            player => player.Changes.IsChanged("GameTimeMinutes")),
        new("CurrentWeather", player => System.Text.Json.JsonSerializer.Serialize(CaptureCurrentWeather(player), SaveJson.Options),
            player => player.Changes.IsChanged("CurrentWeather")),
        new("pending_reward_mail", player => System.Text.Json.JsonSerializer.Serialize(CapturePendingRewardMail(player), SaveJson.Options),
            player => player.Changes.IsChanged("_pendingRewardMail")),
        new("GatheredCollections", player => System.Text.Json.JsonSerializer.Serialize(CaptureGatheredCollections(player), SaveJson.Options),
            player => player.Changes.IsChanged("Collections")),
        new("progress", player => System.Text.Json.JsonSerializer.Serialize(CaptureProgress(player), SaveJson.Options),
            player => player.Changes.IsChanged("Progress")),
        new("character_vitals", player => System.Text.Json.JsonSerializer.Serialize(CaptureCharacterVitals(player), SaveJson.Options),
            player => player.Changes.IsChanged("Characters")),
        new("map", player => System.Text.Json.JsonSerializer.Serialize(CaptureMap(player), SaveJson.Options),
            player => player.Changes.IsChanged("Map")),
        new("teams", player => System.Text.Json.JsonSerializer.Serialize(CaptureTeams(player), SaveJson.Options),
            player => player.Changes.IsChanged("Teams")),
        new("current_team", player => System.Text.Json.JsonSerializer.Serialize(CaptureCurrentTeam(player), SaveJson.Options),
            player => player.Changes.IsChanged("Teams")),
        new("using_member_slot", player => System.Text.Json.JsonSerializer.Serialize(CaptureUsingMemberSlot(player), SaveJson.Options),
            player => player.Changes.IsChanged("Teams")),
        new("skill_groups", player => System.Text.Json.JsonSerializer.Serialize(CaptureSkillGroups(player), SaveJson.Options),
            player => player.Changes.IsChanged("Skills")),
        new("talents", player => System.Text.Json.JsonSerializer.Serialize(CaptureTalents(player), SaveJson.Options),
            player => player.Changes.IsChanged("Skills")),
        new("bag", player => System.Text.Json.JsonSerializer.Serialize(CaptureBag(player), SaveJson.Options),
            player => player.Changes.IsChanged("Bag")),
        new("item_cds", player => System.Text.Json.JsonSerializer.Serialize(CaptureItemCds(player), SaveJson.Options),
            player => player.Changes.IsChanged("Cooldowns")),
        new("wallet", player => System.Text.Json.JsonSerializer.Serialize(CaptureWallet(player), SaveJson.Options),
            player => player.Changes.IsChanged("Wallet")),
        new("limits", player => System.Text.Json.JsonSerializer.Serialize(CaptureLimits(player), SaveJson.Options),
            player => player.Changes.IsChanged("Limits")),
        new("shop", player => System.Text.Json.JsonSerializer.Serialize(CaptureShop(player), SaveJson.Options),
            player => player.Changes.IsChanged("Shop")),
        new("gacha", player => System.Text.Json.JsonSerializer.Serialize(CaptureGacha(player), SaveJson.Options),
            player => player.Changes.IsChanged("Gacha")),
        new("collections", player => System.Text.Json.JsonSerializer.Serialize(CaptureCollections(player), SaveJson.Options),
            player => player.Changes.IsChanged("Collections")),
        new("quests", player => System.Text.Json.JsonSerializer.Serialize(CaptureQuests(player), SaveJson.Options),
            player => player.Changes.IsChanged("Tasks")),
        new("cases", player => System.Text.Json.JsonSerializer.Serialize(CaptureCases(player), SaveJson.Options),
            player => player.Changes.IsChanged("Cases")),
        new("achievements", player => System.Text.Json.JsonSerializer.Serialize(CaptureAchievements(player), SaveJson.Options),
            player => player.Changes.IsChanged("Achievements")),
        new("houses", player => System.Text.Json.JsonSerializer.Serialize(CaptureHouses(player), SaveJson.Options),
            player => player.Changes.IsChanged("Houses")),
        new("daily_missions", player => System.Text.Json.JsonSerializer.Serialize(CaptureDailyMissions(player), SaveJson.Options),
            player => player.Changes.IsChanged("DailyMissions")),
        new("signin", player => System.Text.Json.JsonSerializer.Serialize(CaptureSignIn(player), SaveJson.Options),
            player => player.Changes.IsChanged("SignIn")),
        new("battle_passes", player => System.Text.Json.JsonSerializer.Serialize(CaptureBattlePasses(player), SaveJson.Options),
            player => player.Changes.IsChanged("BattlePasses")),
        new("region_progress", player => System.Text.Json.JsonSerializer.Serialize(CaptureRegionProgress(player), SaveJson.Options),
            player => player.Changes.IsChanged("RegionProgress")),
        new("silver_creatures", player => System.Text.Json.JsonSerializer.Serialize(CaptureSilverCreatures(player), SaveJson.Options),
            player => player.Changes.IsChanged("SilverCreatures")),
        new("buffs", player => System.Text.Json.JsonSerializer.Serialize(CaptureBuffs(player), SaveJson.Options),
            player => player.Changes.IsChanged("Buffs")),
        new("TemporarySelections", player => System.Text.Json.JsonSerializer.Serialize(CaptureTemporarySelections(player), SaveJson.Options),
            player => player.Changes.IsChanged("_temporarySelections")),
        new("ActiveTemporaryTeam", player => System.Text.Json.JsonSerializer.Serialize(CaptureActiveTemporaryTeam(player), SaveJson.Options),
            player => player.Changes.IsChanged("ActiveTemporaryTeam")),
        new("SuspendedStoryTeam", player => System.Text.Json.JsonSerializer.Serialize(CaptureSuspendedStoryTeam(player), SaveJson.Options),
            player => player.Changes.IsChanged("SuspendedStoryTeam")),
        new("temp_teams", player => System.Text.Json.JsonSerializer.Serialize(CaptureTempTeams(player), SaveJson.Options),
            player => player.Changes.IsChanged("TempTeams")),
        new("red_point", player => System.Text.Json.JsonSerializer.Serialize(CaptureRedPoint(player), SaveJson.Options),
            player => player.Changes.IsChanged("RedPoints")),
        new("month_cards", player => System.Text.Json.JsonSerializer.Serialize(CaptureMonthCards(player), SaveJson.Options),
            player => player.Changes.IsChanged("MonthCards")),
        new("charge_purchases", player => System.Text.Json.JsonSerializer.Serialize(CaptureChargePurchases(player), SaveJson.Options),
            player => player.Changes.IsChanged("_boughtMoneyPacks")),
        new("dungeons", player => System.Text.Json.JsonSerializer.Serialize(CaptureDungeons(player), SaveJson.Options),
            player => player.Changes.IsChanged("Dungeons")),
        new("wanted", player => System.Text.Json.JsonSerializer.Serialize(CaptureWanted(player), SaveJson.Options),
            player => player.Changes.IsChanged("Wanted")),
        new("wanted_run", player => System.Text.Json.JsonSerializer.Serialize(CaptureWantedRun(player), SaveJson.Options),
            player => player.Changes.IsChanged("Wanted")),
        new("patrol_cooldowns", player => System.Text.Json.JsonSerializer.Serialize(CapturePatrolCooldowns(player), SaveJson.Options),
            player => player.Changes.IsChanged("Battles"))
    ];

    internal static int CaptureSchemaVersion(Player player) =>
        RoleSaveMigrations.CurrentVersion;

    internal static uint CaptureGameTimeMinutes(Player player) =>
        player.GameTimeMinutes;

    internal static uint? CaptureCurrentWeather(Player player) =>
        (uint)player.CurrentWeather;

    internal static IReadOnlyList<IReadOnlyList<ItemGrant>> CapturePendingRewardMail(Player player) =>
        player.PendingRewardMail.Select(batch => (IReadOnlyList<Lunaria.Game.Resources.ItemGrant>)batch.ToArray()).ToArray();

    internal static IReadOnlyList<uint> CaptureGatheredCollections(Player player) =>
        player.Collections.Gathered.ToList();

    internal static RoleSaveDocument.ProgressSave? CaptureProgress(Player player) =>
        new RoleSaveDocument.ProgressSave {
            TeamLevel = player.Progress.TeamLevel,
            TeamExp = player.Progress.TeamExp,
            Satiety = player.Progress.Satiety,
            Stamina = player.Progress.Stamina,
            StaminaTickAt = player.Progress.StaminaTickAt.ToUnixTimeSeconds(),
            WorldLevelSelection = player.Progress.WorldLevel == player.Progress.EarnedWorldLevel ? null : player.Progress.WorldLevel
        };

    internal static IReadOnlyList<RoleSaveDocument.CharacterVitalSave> CaptureCharacterVitals(Player player) =>
        player.Characters.Vitals()
            .Where(vital => vital.Hp is not null || vital.PermanentLiquid is not null)
            .Select(vital => new RoleSaveDocument.CharacterVitalSave {
                InstId = vital.InstId,
                Hp = vital.Hp,
                PermanentLiquid = vital.PermanentLiquid
            })
            .ToList();

    internal static RoleSaveDocument.MapSave? CaptureMap(Player player) =>
        new RoleSaveDocument.MapSave {
            MapId = player.Map.MapId,
            ReturnPoint = player.Map.ReturnPoint,
            Savepoint = player.Map.Savepoint,
            UnlockedSavepoints = player.Map.UnlockedSavepoints.ToList(),
            UnlockedTeleports = player.Map.UnlockedTeleports.ToList(),
            X = player.Map.Position.X,
            Y = player.Map.Position.Y,
            Z = player.Map.Position.Z,
            TrackedTargets = player.Map.TrackedTargets
                .Select(target => new RoleSaveDocument.TrackedTargetSave {
                    MapId = target.MapId,
                    TagId = target.TagId,
                    TagType = target.TagType
                })
                .ToList()
        };

    internal static IReadOnlyList<RoleSaveDocument.TeamSave> CaptureTeams(Player player) =>
        player.Teams.All
            .Select(team => new RoleSaveDocument.TeamSave {
                TeamId = team.TeamId,
                Name = team.Name,
                Members = team.Members
                    .Select(m => new RoleSaveDocument.TeamMemberSave { Slot = m.Slot, InstId = m.InstId, Gems = m.Gems.ToList() })
                    .ToList(),
                TemporaryLiquid = ToTeamLiquidSave(team.TemporaryLiquid),
                TemporaryLiquidLv2 = ToTeamLiquidSave(team.TemporaryLiquidLv2)
            })
            .ToList();

    internal static uint CaptureCurrentTeam(Player player) =>
        player.Teams.Current;

    internal static uint CaptureUsingMemberSlot(Player player) =>
        player.Teams.UsingMemberSlot;

    internal static IReadOnlyList<RoleSaveDocument.SkillGroupSave> CaptureSkillGroups(Player player) =>
        player.Skills.SkillGroups()
            .Select(pair => new RoleSaveDocument.SkillGroupSave { Group = pair.Group, Level = pair.Level })
            .ToList();

    internal static IReadOnlyList<RoleSaveDocument.TalentSave> CaptureTalents(Player player) =>
        player.Skills.Talents()
            .Select(pair => new RoleSaveDocument.TalentSave {
                InstId = pair.InstId,
                Mask0 = pair.Masks.Mask0,
                Mask1 = pair.Masks.Mask1
            })
            .ToList();

    internal static IReadOnlyList<RoleSaveDocument.ItemSave> CaptureBag(Player player) =>
        player.Bag.All()
            .Select(stack => new RoleSaveDocument.ItemSave {
                ItemId = stack.ItemId,
                Count = stack.Count,
                IsNew = stack.IsNew
            })
            .ToList();

    internal static IReadOnlyList<RoleSaveDocument.ItemCdSave> CaptureItemCds(Player player) =>
        player.Cooldowns.Active()
            .Select(cd => new RoleSaveDocument.ItemCdSave {
                CdType = cd.CdType,
                ReadyUnix = cd.ReadyUnix
            })
            .ToList();

    internal static IReadOnlyList<RoleSaveDocument.MoneySave> CaptureWallet(Player player) =>
        player.Wallet.All()
            .Select(pair => new RoleSaveDocument.MoneySave { MoneyType = pair.MoneyType, Amount = pair.Amount })
            .ToList();

    internal static IReadOnlyList<RoleSaveDocument.LimitSave> CaptureLimits(Player player) =>
        player.Limits.Entries
            .Select(pair => new RoleSaveDocument.LimitSave {
                Group = pair.Key,
                Count = pair.Value.Count,
                Anchor = pair.Value.Anchor.ToUnixTimeSeconds()
            })
            .ToList();

    internal static IReadOnlyList<RoleSaveDocument.ShopSave> CaptureShop(Player player) =>
        player.Shop.Entries
            .Select(pair => new RoleSaveDocument.ShopSave {
                Good = pair.Key,
                Count = pair.Value.Count,
                Anchor = pair.Value.Anchor.ToUnixTimeSeconds()
            })
            .ToList();

    internal static IReadOnlyList<RoleSaveDocument.GachaSave> CaptureGacha(Player player) =>
        player.Gacha.Entries
            .Select(pair => new RoleSaveDocument.GachaSave {
                Banner = pair.Key,
                Total = pair.Value.Total,
                SinceFive = pair.Value.SinceFive,
                SinceFour = pair.Value.SinceFour,
                FeaturedSince = pair.Value.FeaturedSince,
                Guaranteed = pair.Value.Guaranteed,
                ClaimedMask = pair.Value.ClaimedMask,
                DailyCount = pair.Value.DailyCount,
                Anchor = pair.Value.DailyAnchor.ToUnixTimeSeconds()
            })
            .ToList();

    internal static IReadOnlyList<RoleSaveDocument.CollectionSave> CaptureCollections(Player player) =>
        player.Collections.Entries
            .Select(pair => new RoleSaveDocument.CollectionSave {
                Uniq = pair.Value.Uniq,
                Cfg = pair.Value.Cfg,
                Status = (int)pair.Value.Status,
                StatusTime = pair.Value.StatusTime.ToUnixTimeSeconds(),
                Block = pair.Value.Block,
                X = pair.Value.X,
                Y = pair.Value.Y,
                Z = pair.Value.Z
            })
            .ToList();

    internal static RoleSaveDocument.QuestsSave? CaptureQuests(Player player) =>
        new RoleSaveDocument.QuestsSave {
            AppliedEffects = player.Tasks.AppliedEffects.Select(e => new RoleSaveDocument.TaskTargetRefSave
                { TaskType = e.Type, Action = e.Action }).ToArray(),
            ReportedTargets = player.Tasks.ReportedTargets.Select(e => new RoleSaveDocument.TaskTargetRefSave
                { TaskType = e.Type, Action = e.Action }).ToArray(),
            Processing = player.Tasks.Processing.Values
                .OrderBy(state => state.Type).ThenBy(state => state.TaskId)
                .Select(state => new RoleSaveDocument.QuestSave {
                    TaskType = state.Type,
                    Task = state.TaskId,
                    Step = state.CurrentStep.StepId,
                    Actions = state.CurrentStep.Actions
                        .Select(pair => new RoleSaveDocument.TaskActionSave {
                            Id = pair.Key,
                            Progress = pair.Value.Progress,
                            Max = pair.Value.MaxProgress
                        })
                        .ToList()
                })
                .ToList(),
            Finished = player.Tasks.Finished
                .Select(row => new RoleSaveDocument.QuestRefSave {
                    TaskType = row.Type,
                    Task = row.Id
                })
                .ToList()
        };

    internal static RoleSaveDocument.CaseSave? CaptureCases(Player player) =>
        new RoleSaveDocument.CaseSave {
            Processing = player.Cases.Processing.Values
                .Select(state => new RoleSaveDocument.CaseProcessingSave {
                    CaseId = state.CaseId,
                    FinishedPhase = state.FinishedPhase,
                    OnSlotClues = state.OnSlotClues.ToList(),
                    DecryptedEvidence = state.DecryptedEvidence.ToList(),
                    OwnedClues = state.OwnedClues.ToList(),
                    OwnedEvidence = state.OwnedEvidence.ToList()
                })
                .ToList(),
            Finished = player.Cases.Finished.ToList()
        };

    internal static RoleSaveDocument.AchievementSave? CaptureAchievements(Player player) =>
        new RoleSaveDocument.AchievementSave {
            Events = player.Achievements.Events.Values
                .OrderBy(state => state.EventId)
                .Select(state => new RoleSaveDocument.FinishEventSave {
                    EventId = state.EventId,
                    Progress = state.Progress,
                    Finish = state.Finish
                })
                .ToList(),
            Claimed = player.Achievements.Claimed.ToList()
        };

    internal static RoleSaveDocument.HouseSave? CaptureHouses(Player player) =>
        new RoleSaveDocument.HouseSave {
            Houses = player.Houses.Houses.Values
                .Select(state => new RoleSaveDocument.HouseEntrySave {
                    HouseId = state.HouseId,
                    State = (int)state.State,
                    Level = state.Level,
                    IncomeAnchor = state.IncomeAnchor.ToUnixTimeSeconds(),
                    BankedIncome = state.BankedIncome
                })
                .ToList()
        };

    internal static RoleSaveDocument.DailyMissionSave? CaptureDailyMissions(Player player) =>
        new RoleSaveDocument.DailyMissionSave {
            DayAnchor = player.DailyMissions.DayAnchor.ToUnixTimeSeconds(),
            Missions = player.DailyMissions.Missions.Values
                .OrderBy(state => state.MissionId)
                .Select(state => new RoleSaveDocument.DailyMissionEntrySave {
                    MissionId = state.MissionId,
                    Progress = state.Progress,
                    Claimed = state.Claimed
                })
                .ToList(),
            ActivePoint = player.DailyMissions.ActivePoint,
            ClaimedRewards = player.DailyMissions.ClaimedRewards.ToList()
        };

    internal static RoleSaveDocument.SignInSave? CaptureSignIn(Player player) =>
        new RoleSaveDocument.SignInSave {
            Activities = player.SignIn.Activities.Select(a => new RoleSaveDocument.SignInActivitySave {
                ActivityId = a.ActivityId, SignedDays = a.SignedDays, ClaimedDays = a.ClaimedDays,
                LastSignInDay = a.LastSignInDay, AttendanceDays = a.AttendanceDays
            }).ToArray(),
            SignedDays = player.SignIn.SignedDays.ToList(),
            ClaimedDays = player.SignIn.ClaimedDays.ToList(),
            LastSignInDay = player.SignIn.LastSignInDay
        };

    internal static RoleSaveDocument.BattlePassSave? CaptureBattlePasses(Player player) =>
        new RoleSaveDocument.BattlePassSave {
            Passes = player.BattlePasses.Passes.Select(pair => new RoleSaveDocument.BattlePassEntrySave {
                PassId = pair.Key,
                Level = pair.Value.Level,
                Exp = pair.Value.Exp,
                AwardLevel = pair.Value.AwardLevel
            }).ToList()
        };

    internal static RoleSaveDocument.RegionProgressSave? CaptureRegionProgress(Player player) =>
        new RoleSaveDocument.RegionProgressSave {
            Subregions = player.RegionProgress.Subregions.Values
                .Select(state => new RoleSaveDocument.SubRegionProgressSave {
                    SubRegionId = state.SubRegionId,
                    Sequences = state.Sequences.Select(pair => new RoleSaveDocument.SequenceProgressSave {
                        SequenceId = pair.Key,
                        Count = pair.Value
                    }).ToList(),
                    ClaimedValues = state.ClaimedValues.ToList()
                })
                .ToList()
        };

    internal static RoleSaveDocument.SilverCreatureSave? CaptureSilverCreatures(Player player) =>
        new RoleSaveDocument.SilverCreatureSave {
            Creatures = player.SilverCreatures.Creatures.Values
                .Select(creature => new RoleSaveDocument.SilverCreatureEntrySave {
                    UniqId = creature.UniqId,
                    ItemId = creature.ItemId,
                    Level = creature.Level
                })
                .ToList(),
            InBattleUniqId = player.SilverCreatures.InBattleUniqId,
            LastMinted = player.SilverCreatures.LastMinted
        };

    internal static IReadOnlyList<RoleSaveDocument.BuffSave> CaptureBuffs(Player player) =>
        player.Buffs.Buffs.Values
            .Select(buff => new RoleSaveDocument.BuffSave {
                BuffId = buff.BuffId,
                AttachUnix = buff.AttachedAt.ToUnixTimeSeconds(),
                LeftBattle = buff.LeftBattle
            })
            .ToList();

    internal static IReadOnlyList<TemporaryTeamSelection> CaptureTemporarySelections(Player player) =>
        player.TemporarySelections.ToArray();

    internal static ActiveTemporaryTeam? CaptureActiveTemporaryTeam(Player player) =>
        player.ActiveTemporaryTeam;

    internal static ActiveTemporaryTeam? CaptureSuspendedStoryTeam(Player player) =>
        player.SuspendedStoryTeam;

    internal static IReadOnlyList<RoleSaveDocument.TempTeamSave> CaptureTempTeams(Player player) =>
        player.TempTeams.Teams.Values
            .Select(team => new RoleSaveDocument.TempTeamSave {
                TeamSrc = team.TeamSrc,
                Members = team.Members
                    .Select(member => new RoleSaveDocument.TempTeamMemberSave {
                        Slot = member.Slot,
                        CharacterId = member.CharacterId,
                        Gems = member.Gems.ToArray()
                    })
                    .ToList()
            })
            .ToList();

    internal static RoleSaveDocument.RedPointSave? CaptureRedPoint(Player player) =>
        new RoleSaveDocument.RedPointSave {
            ExchangeActivityRead = player.RedPoints.ExchangeActivityRead
        };

    internal static IReadOnlyList<RoleSaveDocument.MonthCardSave> CaptureMonthCards(Player player) =>
        player.MonthCards.Cards.Values
            .Select(card => new RoleSaveDocument.MonthCardSave {
                CardId = card.CardId,
                OverdueUnix = card.OverdueAt.ToUnixTimeSeconds(),
                RewardUnix = card.RewardAt.ToUnixTimeSeconds()
            })
            .ToList();

    internal static IReadOnlyList<uint> CaptureChargePurchases(Player player) =>
        player.BoughtMoneyPacks.Order().ToList();

    internal static RoleSaveDocument.DungeonSave? CaptureDungeons(Player player) =>
        new RoleSaveDocument.DungeonSave {
            Finishes = player.Dungeons.Finishes
                .Select(pair => new RoleSaveDocument.DungeonFinishSave {
                    DungeonId = pair.Key,
                    Count = pair.Value
                })
                .ToList(),
            Types = player.Dungeons.Types.Values
                .Select(state => new RoleSaveDocument.DungeonTypeSave {
                    TypeId = state.TypeId,
                    UsedDay = state.UsedDay,
                    UsedWeek = state.UsedWeek,
                    Anchor = state.Anchor.ToUnixTimeSeconds()
                })
                .ToList(),
            Hordes = player.Dungeons.Hordes.Values
                .Select(horde => new RoleSaveDocument.HordeSave {
                    HordeId = horde.HordeId,
                    KillCount = horde.KillCount,
                    StarAward = horde.StarAward
                })
                .ToList(),
            Current = player.Dungeons.Current is {} current ?
                new RoleSaveDocument.DungeonCurrentSave {
                    DungeonId = current.DungeonId,
                    BattleId = current.BattleId
                } :
                null
        };

    internal static IReadOnlyList<RoleSaveDocument.WantedFinishSave> CaptureWanted(Player player) =>
        player.Wanted.Finishes
            .Select(pair => new RoleSaveDocument.WantedFinishSave {
                EntryId = pair.Key,
                Count = pair.Value
            })
            .ToList();

    internal static RoleSaveDocument.WantedRunSave? CaptureWantedRun(Player player) =>
        ToWantedRunSave(player.Wanted.CaptureRun());

    internal static IReadOnlyList<RoleSaveDocument.PatrolCooldownSave> CapturePatrolCooldowns(Player player) =>
        player.Battles.PatrolCooldownEnds
            .Select(pair => new RoleSaveDocument.PatrolCooldownSave {
                ClusterId = pair.Key,
                UntilUnix = pair.Value.ToUnixTimeSeconds()
            })
            .ToList();

    /// <summary>Load the roster before resolving saved team and talent instance IDs.</summary>
    public static void Apply(Player player, RoleSaveDocument document)
    {
        if (document.Progress is {} progress)
        {
            player.Progress.Load(
                progress.TeamLevel,
                progress.TeamExp,
                progress.Satiety,
                progress.Stamina,
                DateTimeOffset.FromUnixTimeSeconds(progress.StaminaTickAt),
                progress.WorldLevelSelection);
        }

        if (document.Map is {} map)
        {
            player.Map.Load(
                map.MapId,
                map.Savepoint,
                map.UnlockedSavepoints,
                map.UnlockedTeleports,
                (map.X, map.Y, map.Z),
                map.TrackedTargets.Select(target => (target.MapId, target.TagId, target.TagType)),
                map.ReturnPoint);
        }

        player.Characters.LoadVitals(document.CharacterVitals
            .Select(vital => (vital.InstId, vital.Hp, vital.PermanentLiquid)));

        player.Teams.Load(
            document.Teams.Select(ToTeamState),
            document.CurrentTeam,
            document.UsingMemberSlot,
            player.Characters);

        player.Skills.Load(
            document.SkillGroups.Select(row => (row.Group, row.Level)),
            document.Talents.Select(row => (row.InstId, new TalentMasks { Mask0 = row.Mask0, Mask1 = row.Mask1 })),
            player.Characters);

        player.Bag.Load(document.Bag.Select(row => new ItemStack {
            ItemId = row.ItemId,
            Count = row.Count,
            IsNew = row.IsNew
        }));

        player.Cooldowns.Load(document.ItemCds.Select(row => (row.CdType, row.ReadyUnix)));

        player.Wallet.Load(document.Wallet.Select(row => (row.MoneyType, row.Amount)));

        player.Limits.Load(document.Limits.Select(row =>
            (row.Group, row.Count, DateTimeOffset.FromUnixTimeSeconds(row.Anchor))));

        player.Shop.Load(document.Shop.Select(row =>
            (row.Good, row.Count, DateTimeOffset.FromUnixTimeSeconds(row.Anchor))));

        player.Gacha.Load(document.Gacha.Select(row =>
            (row.Banner, row.Total, row.SinceFive, row.SinceFour, row.FeaturedSince,
                row.Guaranteed, row.ClaimedMask, row.DailyCount,
                DateTimeOffset.FromUnixTimeSeconds(row.Anchor))));

        player.Collections.Load(document.Collections.Select(row =>
            (row.Uniq, row.Cfg, row.Status,
                DateTimeOffset.FromUnixTimeSeconds(row.StatusTime), row.Block,
                (row.X, row.Y, row.Z))));
        player.Collections.LoadGathered(document.GatheredCollections);
        player.RestoreGameTime(document.GameTimeMinutes);
        player.LoadWeather(document.CurrentWeather);
        player.LoadPendingRewardMail(document.PendingRewardMail);

        player.Tasks.Load(
            document.Quests?.Processing.Select(row =>
                (row.TaskType, row.Task, row.Step,
                    row.Actions.Select(action => (action.Id, action.Progress, action.Max)))) ?? [],
            document.Quests?.Finished.Select(row => (row.TaskType, row.Task)) ?? []);
        player.Tasks.LoadAppliedEffects(document.Quests?.AppliedEffects.Select(e => (e.TaskType, e.Action)) ?? []);
        player.Tasks.LoadReportedTargets(document.Quests?.ReportedTargets.Select(e => (e.TaskType, e.Action)) ?? []);
        player.RestoreCompletedMindPalaceReturn();
        player.ClearTaskEvents();

        player.Cases.Load(
            document.Cases?.Processing.Select(row =>
                (row.CaseId, row.FinishedPhase,
                    row.OnSlotClues.AsEnumerable(), row.DecryptedEvidence.AsEnumerable())) ?? [],
            document.Cases?.Finished ?? []);

        player.Cases.LoadOwned(document.Cases?.Processing.Select(row =>
            (row.CaseId, row.OwnedClues.AsEnumerable(), row.OwnedEvidence.AsEnumerable())) ?? []);

        player.Achievements.Load(
            document.Achievements?.Events.Select(row =>
                (row.EventId, row.Progress, row.Finish)) ?? [],
            document.Achievements?.Claimed ?? []);

        player.Houses.Load(
            document.Houses?.Houses.Select(row =>
                (row.HouseId, (EnmHouseState)row.State, row.Level, row.IncomeAnchor)) ?? [],
            document.Houses?.Houses.ToDictionary(row => row.HouseId, row => row.BankedIncome));

        player.DailyMissions.Load(
            document.DailyMissions?.DayAnchor ?? 0,
            document.DailyMissions?.Missions.Select(row =>
                (row.MissionId, row.Progress, row.Claimed)) ?? [],
            document.DailyMissions?.ActivePoint ?? 0,
            document.DailyMissions?.ClaimedRewards ?? []);

        if (document.SignIn?.Activities is {} calendars)
            player.SignIn.LoadActivities(calendars.Select(a => new Managers.SignInManager.ActivityState(
                a.ActivityId, a.SignedDays, a.ClaimedDays, a.LastSignInDay, a.AttendanceDays)));
        else
            player.SignIn.Load(document.SignIn?.SignedDays ?? [],
                document.SignIn?.ClaimedDays ?? [], document.SignIn?.LastSignInDay);

        player.BattlePasses.Load(
            document.BattlePasses?.Passes.Select(row =>
                (row.PassId, row.Level, row.Exp, row.AwardLevel)) ?? []);

        player.RegionProgress.Load(
            document.RegionProgress?.Subregions.Select(row =>
                (row.SubRegionId,
                    row.Sequences.Select(sequence => (sequence.SequenceId, sequence.Count)),
                    row.ClaimedValues.AsEnumerable())) ?? []);

        player.SilverCreatures.Load(
            document.SilverCreatures?.Creatures.Select(row =>
                (row.UniqId, row.ItemId, row.Level)) ?? [],
            document.SilverCreatures?.InBattleUniqId ?? 0, document.SilverCreatures?.LastMinted ?? 0);

        player.Buffs.Load(
            document.Buffs.Select(row => (row.BuffId, row.AttachUnix, row.LeftBattle)),
            player.UtcNow);

        player.TempTeams.Load(
            document.TempTeams.Select(row =>
                (row.TeamSrc, row.Members.Select(member => new TeamMemberState {
                    Slot = member.Slot, InstId = member.CharacterId, CharacterId = member.CharacterId, Gems = member.Gems
                }))));

        player.RedPoints.Load(document.RedPoint?.ExchangeActivityRead ?? false);

        player.MonthCards.Load(
            document.MonthCards.Select(card => (card.CardId, card.OverdueUnix, card.RewardUnix)));

        player.LoadChargePurchases(document.ChargePurchases);

        player.Dungeons.Load(
            document.Dungeons?.Finishes.Select(row => (row.DungeonId, row.Count)) ?? [],
            document.Dungeons?.Types.Select(row => (row.TypeId, row.UsedDay, row.UsedWeek, row.Anchor)) ?? [],
            document.Dungeons?.Hordes.Select(row => (row.HordeId, row.KillCount, row.StarAward)) ?? [],
            document.Dungeons?.Current is {} current ? (current.DungeonId, current.BattleId) : null,
            player.UtcNow);
        player.LeaveStrandedDungeonMap();

        player.Wanted.Load(
            document.Wanted.Select(row => (row.EntryId, row.Count)),
            FromWantedRunSave(document.WantedRun));
        player.Battles.LoadPatrolCooldowns(document.PatrolCooldowns
            .Select(row => (row.ClusterId, DateTimeOffset.FromUnixTimeSeconds(row.UntilUnix))));
        player.RestoreWantedTaskStep();
        player.LoadTemporaryTeams(document.TemporarySelections, document.ActiveTemporaryTeam, document.SuspendedStoryTeam);
    }

    private static RoleSaveDocument.WantedRunSave? ToWantedRunSave(WantedRunSnapshot? run) =>
        run is null ?
            null :
            new RoleSaveDocument.WantedRunSave {
                EntryId = run.EntryId,
                RouteId = run.RouteId,
                MaxStep = run.MaxStep,
                Step = run.Step,
                Current = new RoleSaveDocument.WantedStepSave {
                    Status = (int)run.Current.Status,
                    ProcessId = run.Current.ProcessId,
                    EventId = run.Current.EventId,
                    EventDone = run.Current.EventDone,
                    Awards = run.Current.Awards
                        .Select(award => new RoleSaveDocument.WantedStepAwardSave {
                            AwardId = award.AwardId,
                            Type = award.Type,
                            Options = award.Options.ToList(),
                            Chosen = award.Chosen
                        })
                        .ToList()
                },
                History = run.History
                    .Select(pair => new RoleSaveDocument.WantedEventPairSave {
                        ProcessId = pair.ProcessId,
                        EventId = pair.EventId
                    })
                    .ToList(),
                Blesses = run.Blesses.ToList(),
                Bionics = run.Bionics
                    .Select(bionics => new RoleSaveDocument.WantedBionicsSave {
                        EntryId = bionics.EntryId,
                        UniqId = bionics.UniqId
                    })
                    .ToList(),
                Relics = run.Relics.ToList(),
                LastBionicsId = run.LastBionicsId,
                RedeemedSteps = run.RedeemedSteps ?? [],
                CoinsTotal = run.CoinsTotal,
                ReviveCount = run.ReviveCount,
                ResetPoint = run.ResetPoint is {} point ?
                    new RoleSaveDocument.WantedResetPointSave {
                        Id = point.Id,
                        X = point.X,
                        Y = point.Y,
                        Z = point.Z
                    } :
                    null,
                Adventures = run.Adventures
                    .Select(adventure => new RoleSaveDocument.WantedAdventureSave {
                        AdventureId = adventure.AdventureId,
                        ContentId = adventure.ContentId,
                        DialogId = adventure.DialogId,
                        OptionResult = adventure.OptionResult
                    })
                    .ToList(),
                ShopBuys = run.ShopBuys
                    .Select(pair => new RoleSaveDocument.WantedShopBuySave {
                        GoodsId = pair.Key,
                        Count = pair.Value
                    })
                    .ToList()
            };

    private static WantedRunSnapshot? FromWantedRunSave(RoleSaveDocument.WantedRunSave? run) =>
        run is null || run.Current is null ?
            null :
            new WantedRunSnapshot(
                run.EntryId,
                run.RouteId,
                run.MaxStep,
                run.Step,
                new WantedStepSnapshot(
                    (EnmWantedStepStatus)run.Current.Status,
                    run.Current.ProcessId,
                    run.Current.EventId,
                    run.Current.EventDone,
                    run.Current.Awards
                        .Select(award => new WantedStepAward(award.AwardId, award.Type, award.Options.ToList(), award.Chosen))
                        .ToList()),
                run.History.Select(pair => (pair.ProcessId, pair.EventId)).ToList(),
                run.Blesses.ToList(),
                run.Bionics
                    .Select(bionics => new WantedBionics(bionics.EntryId, bionics.UniqId))
                    .ToList(),
                run.Relics.ToList(),
                run.CoinsTotal,
                run.ReviveCount,
                run.ResetPoint is {} point ? (point.Id, point.X, point.Y, point.Z) : null,
                run.Adventures
                    .Select(adventure => new WantedAdventure(
                        adventure.AdventureId, adventure.ContentId, adventure.DialogId, adventure.OptionResult))
                    .ToList(),
                new SortedDictionary<uint, uint>(
                    run.ShopBuys.ToDictionary(pair => pair.GoodsId, pair => pair.Count)), run.LastBionicsId, run.RedeemedSteps);

    private static TeamState ToTeamState(RoleSaveDocument.TeamSave team) => new() {
        TeamId = team.TeamId,
        Name = team.Name,
        Members = team.Members
            .Select(m => new TeamMemberState { Slot = m.Slot, InstId = m.InstId, CharacterId = 0, Gems = m.Gems })
            .ToList(),
        TemporaryLiquid = ToTeamLiquid(team.TemporaryLiquid),
        TemporaryLiquidLv2 = ToTeamLiquid(team.TemporaryLiquidLv2)
    };

    private static RoleSaveDocument.TeamLiquidSave ToTeamLiquidSave(TeamLiquid liquid) => new() {
        Fire = liquid.Fire,
        Ice = liquid.Ice,
        Thunder = liquid.Thunder,
        Gravity = liquid.Gravity,
        Radiate = liquid.Radiate,
        Silver = liquid.Silver,
        Blackiron = liquid.Blackiron
    };

    private static TeamLiquid ToTeamLiquid(RoleSaveDocument.TeamLiquidSave? liquid) => new() {
        Fire = liquid?.Fire ?? 0,
        Ice = liquid?.Ice ?? 0,
        Thunder = liquid?.Thunder ?? 0,
        Gravity = liquid?.Gravity ?? 0,
        Radiate = liquid?.Radiate ?? 0,
        Silver = liquid?.Silver ?? 0,
        Blackiron = liquid?.Blackiron ?? 0
    };
}

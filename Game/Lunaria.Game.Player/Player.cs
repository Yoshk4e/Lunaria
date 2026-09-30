using Google.Protobuf;
using Lunaria.Common;
using Lunaria.Game.Battle;
using Lunaria.Game.Buff;
using Lunaria.Game.Characters;
using Lunaria.Game.Characters.Teams;
using Lunaria.Game.Collections;
using Lunaria.Game.Dungeons;
using Lunaria.Game.Gacha;
using Lunaria.Game.Guide;
using Lunaria.Game.Inventory;
using Lunaria.Game.Limits;
using Lunaria.Game.Mail;
using Lunaria.Game.Motives;
using Lunaria.Game.Player.Managers;
using Lunaria.Game.Progression;
using Lunaria.Game.Resources;
using Lunaria.Game.Shop;
using Lunaria.Game.Tasks;
using Lunaria.Game.Wanted;
using Lunaria.Game.Logging;
using Microsoft.Extensions.Logging;
using Lunaria.Game.World;
using Msg;

namespace Lunaria.Game.Player;

public sealed partial class Player(ulong sessionId, GameData assets)
{
    /// <summary>Shared logger for every Player partial. Gameplay events are trace, outcomes are info.</summary>
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Player");

    /// <summary>Session ID used by SCAccountLogin.connect_identify_id and SCSchemaInfoSync.login_id.</summary>
    public ulong SessionId { get; } = sessionId;

    public LoadingState LoadingState { get; set; } = LoadingState.Pending;

    /// <summary>All player systems share one instance ID sequence.</summary>
    public GuidManager Guid { get; } = new();

    public AccountManager Account { get; } = new();
    public RoleManager Roles { get; } = new();
    public MapManager Map { get; } = new(assets);
    public ExposeManager Expose { get; } = new(assets.Expose);
    public CharacterManager Characters { get; } = new(assets);
    public TeamManager Teams { get; } = new(assets);

    public TempTeamManager TempTeams { get; } = new(assets);
    public SkillManager Skills { get; } = new(assets);
    public ItemBagManager Bag { get; } = new(assets);

    public ItemCooldownManager Cooldowns { get; } = new();
    public WalletManager Wallet { get; } = new(assets);
    public ProgressManager Progress { get; } = new(assets);

    public GuideManager Guides { get; } = new(assets);

    public LimitGroupManager Limits { get; } = new(assets);

    public MailManager Mails { get; } = new(assets);

    public ShopManager Shop { get; } = new(assets);

    public MotiveManager Motives { get; } = new(assets);

    public GachaManager Gacha { get; } = new(assets);

    public TaskManager Tasks { get; } = new(assets);

    public bool TasksBootstrapped { get; set; }

    public CollectionManager Collections { get; } = new(assets);

    public CaseManager Cases { get; } = new(assets);

    public AchievementManager Achievements { get; } = new(assets);

    public HouseManager Houses { get; } = new(assets);

    public DailyMissionManager DailyMissions { get; } = new(assets);

    public SignInManager SignIn { get; } = new(assets);

    public BattlePassManager BattlePasses { get; } = new(assets);

    public RegionProgressManager RegionProgress { get; } = new(assets);

    public SilverCreatureManager SilverCreatures { get; } = new(assets);

    public BuffManager Buffs { get; } = new(assets);

    public RedPointManager RedPoints { get; } = new();

    public MonthCardManager MonthCards { get; } = new(assets);

    /// <summary>Battle state lasts only for the current login session.</summary>
    public BattleManager Battles { get; } = new();

    public DungeonManager Dungeons { get; } = new(assets);

    public WantedManager Wanted { get; } = new(assets);

    public bool SignInPopSent { get; set; }

    /// <summary>Keep reward RNG state across role replacement. Gacha tests can supply their own RNG.</summary>
    public Random GachaRng { get; private set; } = Random.Shared;

    public GameData Assets => assets;

    public bool IsLoggedIn => Account.IsBound;

    public bool IsDirty =>
        Roles.IsDirty
        || Characters.IsDirty
        || Mails.IsDirty
        || Motives.IsDirty
        || Guides.IsDirty
        || SaveDirty;

    public bool SaveDirty =>
        _gameTimeDirty
        || _pendingRewardMailDirty
        || Map.IsDirty
        || Teams.IsDirty
        || Skills.IsDirty
        || Bag.IsDirty
        || Cooldowns.IsDirty
        || Wallet.IsDirty
        || Progress.IsDirty
        || Limits.IsDirty
        || Shop.IsDirty
        || Gacha.IsDirty
        || Collections.IsDirty
        || Tasks.IsDirty
        || Cases.IsDirty
        || Achievements.IsDirty
        || Houses.IsDirty
        || DailyMissions.IsDirty
        || SignIn.IsDirty
        || BattlePasses.IsDirty
        || RegionProgress.IsDirty
        || SilverCreatures.IsDirty
        || TempTeams.IsDirty
        || TemporaryTeamDirty
        || Buffs.IsDirty
        || RedPoints.IsDirty
        || MonthCards.IsDirty
        || Dungeons.IsDirty
        || Wanted.IsDirty;

    /// <summary>Clear all dirty flags only after the role transaction commits.</summary>
    public void ClearSaveDirty()
    {
        _gameTimeDirty = false;
        _pendingRewardMailDirty = false;
        Map.ClearDirty();
        Teams.ClearDirty();
        Skills.ClearDirty();
        Bag.ClearDirty();
        Cooldowns.ClearDirty();
        Wallet.ClearDirty();
        Progress.ClearDirty();
        Limits.ClearDirty();
        Shop.ClearDirty();
        Gacha.ClearDirty();
        Collections.ClearDirty();
        Tasks.ClearDirty();
        Cases.ClearDirty();
        Achievements.ClearDirty();
        Houses.ClearDirty();
        DailyMissions.ClearDirty();
        SignIn.ClearDirty();
        BattlePasses.ClearDirty();
        RegionProgress.ClearDirty();
        SilverCreatures.ClearDirty();
        TempTeams.ClearDirty();
        TemporaryTeamDirty = false;
        Buffs.ClearDirty();
        RedPoints.ClearDirty();
        MonthCards.ClearDirty();
        Dungeons.ClearDirty();
        Wanted.ClearDirty();
    }

    public RoleInfo RoleInfo(RoleState role) => new() {
        BaseInfo = new RoleBaseInfo {
            RoleId = (ulong)role.Id,
            RoleName = ByteStringUtf8(role.Name),
            RoleLevel = (int)Progress.TeamLevel,
            Gender = (EnmGender)role.Gender,
            PlayerAttr = { PlayerAttrs() },
            LevelData = LevelData(),
            Money = { Wallet.MoneyData() },
            SecondRoleName = ByteStringUtf8(role.SecondName)
        },
        MapInfo = new RoleMapInfo {
            CurrentGametime = GameTimeMinutes,
            CurrentWeather = (uint)CurrentWeather
        },
        GlobalConf = assets.GlobalConfig.GlobalConf
    };

    /// <summary>The client reads meters and coin from player_attr as well as money.</summary>
    private IEnumerable<PlayerAttr> PlayerAttrs()
    {
        yield return IntAttr(PlayerAttrType.EnmPlayerAttrCoin, (int)Math.Min(Wallet.Coin, int.MaxValue));
        yield return IntAttr(PlayerAttrType.EnmPlayerAttrSatiety, Progress.Satiety);
        yield return IntAttr(PlayerAttrType.EnmPlayerAttrStaminaCur, Progress.Stamina);
    }

    private static PlayerAttr IntAttr(PlayerAttrType attrType, int value) => new() {
        AttrType = (int)attrType,
        ValueInt32 = value
    };

    private static ByteString ByteStringUtf8(string s) =>
        ByteString.CopyFromUtf8(s);
}

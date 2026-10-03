using Lunaria.Common.Tracking;
using Lunaria.Game.Player.Gameplay;
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

[TrackChildren]
public sealed partial class Player : TrackedObject
{
    private readonly GameData assets;
    private readonly OperationTimeProvider _time;
    internal Persistence.Saves.RoleSaveBaseline SaveBaseline { get; } = new();

    public Player(ulong sessionId, GameData assets, TimeProvider? timeProvider = null, GameplayRandom? random = null)
    {
        this.assets = assets;
        _time = timeProvider as OperationTimeProvider ?? new OperationTimeProvider(timeProvider ?? TimeProvider.System);
        RandomSources = random ?? new GameplayRandom();
        SessionId = sessionId;
        Map = new(assets);
        Expose = new(assets.Expose);
        Characters = new(assets);
        Teams = new(assets);
        TempTeams = new(assets);
        Skills = new(assets);
        Bag = new(assets);
        Wallet = new(assets);
        Progress = new(assets);
        Guides = new(assets);
        Limits = new(assets);
        Mails = new(assets);
        Shop = new(assets);
        Motives = new(assets);
        Gacha = new(assets);
        Tasks = new(assets);
        Collections = new(assets);
        Cases = new(assets);
        Achievements = new(assets, RandomSources.Loot);
        Houses = new(assets);
        DailyMissions = new(assets, _time);
        SignIn = new(assets, _time);
        BattlePasses = new(assets);
        RegionProgress = new(assets);
        SilverCreatures = new(assets);
        Buffs = new(assets);
        MonthCards = new(assets);
        Dungeons = new(assets);
        Wanted = new(assets, RandomSources.Wanted);
        Cooldowns = new(_time);
        CurrentWeather = (WeatherType)assets.Starter.Weather;
        GameTimeMinutes = assets.Starter.GameTime;
        ConnectTrackedChildren();
        Changes.PreserveLoadedChanges = () => HasActiveRole;
        Changes.AcceptAll();
    }

    // Log gameplay events at trace level and outcomes at info level.
    private static readonly ILogger Log = GameLog.Create("Lunaria.Game.Player");

    /// <summary>Session ID used by SCAccountLogin.connect_identify_id and SCSchemaInfoSync.login_id.</summary>
    public ulong SessionId { get; }

    [Untracked]
    public LoadingState LoadingState { get; set; } = LoadingState.Pending;

    /// <summary>All player systems share one instance ID sequence.</summary>
    public GuidManager Guid { get; } = new();

    public AccountManager Account { get; } = new();
    public RoleManager Roles { get; } = new();
    public MapManager Map { get; }
    public ExposeManager Expose { get; }
    public CharacterManager Characters { get; }
    public TeamManager Teams { get; }

    public TempTeamManager TempTeams { get; }
    public SkillManager Skills { get; }
    internal ItemBagManager Bag { get; }

    internal ItemCooldownManager Cooldowns { get; }
    internal WalletManager Wallet { get; }
    public ProgressManager Progress { get; }

    public GuideManager Guides { get; }

    public LimitGroupManager Limits { get; }

    public MailManager Mails { get; }

    public ShopManager Shop { get; }

    internal MotiveManager Motives { get; }

    public GachaManager Gacha { get; }

    public TaskManager Tasks { get; }

    [Untracked]
    public bool TasksBootstrapped { get; set; }

    public CollectionManager Collections { get; }

    public CaseManager Cases { get; }

    public AchievementManager Achievements { get; }

    public HouseManager Houses { get; }

    public DailyMissionManager DailyMissions { get; }

    public SignInManager SignIn { get; }

    public BattlePassManager BattlePasses { get; }

    public RegionProgressManager RegionProgress { get; }

    public SilverCreatureManager SilverCreatures { get; }

    public BuffManager Buffs { get; }

    public RedPointManager RedPoints { get; } = new();

    public MonthCardManager MonthCards { get; }

    /// <summary>Battle state lasts only for the current login session.</summary>
    internal BattleManager Battles { get; } = new();

    public DungeonManager Dungeons { get; }

    public WantedManager Wanted { get; }

    [Untracked]
    public bool SignInPopSent { get; set; }

    public GameplayRandom RandomSources { get; }
    public TimeProvider Time => _time;
    public DateTimeOffset UtcNow => _time.GetUtcNow();
    public IDisposable BeginOperation(DateTimeOffset? now = null) => _time.Begin(now);

    public GameData Assets => assets;

    public bool IsLoggedIn => Account.IsBound;

    public bool SaveDirty => Changes.HasChanges;

    /// <summary>Accept all current changes. Saves should accept their captured batch after commit.</summary>
    public void ClearSaveDirty() => Changes.AcceptAll();

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

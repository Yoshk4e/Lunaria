using Lunaria.Common.Tracking;
using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Gacha;

public sealed record GachaBannerState(
    uint Total,
    uint SinceFive,
    uint SinceFour,
    uint FeaturedSince,
    bool Guaranteed,
    uint ClaimedMask,
    uint DailyCount,
    DateTimeOffset DailyAnchor
);

public sealed partial class GachaManager(GameData assets) : TrackedObject
{
    [Untracked]
    private readonly SortedDictionary<uint, BannerConfig> _banners = BuildBanners(assets);
    private readonly TrackedSortedDictionary<uint, GachaBannerState> __tracked_states = [];
    [Tracked]
    private partial TrackedSortedDictionary<uint, GachaBannerState> _states { get; }

    public IReadOnlyDictionary<uint, GachaBannerState> Entries => _states;

    public void Load(
        IEnumerable<(uint Banner, uint Total, uint SinceFive, uint SinceFour, uint FeaturedSince,
            bool Guaranteed, uint ClaimedMask, uint DailyCount, DateTimeOffset Anchor)> persisted
    )
    {
        _states.Clear();

        foreach (var row in persisted)
        {
            if (_banners.ContainsKey(row.Banner))
                _states[row.Banner] = new GachaBannerState(
                    row.Total, row.SinceFive, row.SinceFour, row.FeaturedSince,
                    row.Guaranteed, row.ClaimedMask, row.DailyCount, row.Anchor);
        }
        AcceptLoadedState();
    }

    public bool BannerExists(uint bannerId) => _banners.ContainsKey(bannerId);

    public BannerConfig Banner(uint bannerId) => _banners[bannerId];

    public bool TryGetBanner(uint bannerId, out BannerConfig? banner) =>
        _banners.TryGetValue(bannerId, out banner);

    public uint DailyCountOf(uint bannerId, DateTimeOffset now)
    {
        if (!_states.TryGetValue(bannerId, out var state))
            return 0;

        if (HasReset(state.DailyAnchor, now))
        {
            _states[bannerId] = state with { DailyCount = 0, DailyAnchor = now };

            return 0;
        }

        return state.DailyCount;
    }

    public uint ClaimedMaskOf(uint bannerId) =>
        _states.TryGetValue(bannerId, out var state) ? state.ClaimedMask : 0;

    /// <summary>Pity counts include the pull that guarantees the rarity.</summary>
    public GachaPoolInfo PoolInfo(uint bannerId, DateTimeOffset now)
    {
        if (!_banners.TryGetValue(bannerId, out var banner))
            return new GachaPoolInfo {
                PoolId = bannerId,
                Ret = (int)EnmTextCode.EnmTextGachaDropErr
            };

        var state = _states.GetValueOrDefault(bannerId)
                    ?? new GachaBannerState(Total: 0, SinceFive: 0, SinceFour: 0, FeaturedSince: 0, Guaranteed: false, ClaimedMask: 0,
                        DailyCount: 0, now);
        // The client reads daily_count as the pulls left today (DailyDrawRemain in s_CSM_G_GachaData) and refuses a
        // pull above it, so send the daily limit minus today's pulls rather than the pulls done.
        var limit = assets.Gacha.DailyLimit(banner.PoolId);
        var daily = limit == 0 ? 0 : limit - Math.Min(DailyCountOf(bannerId, now), limit);
        var pityFive = assets.Gacha.FiveStarPity(banner.PoolId);
        var pityFour = assets.Gacha.FourStarPity(banner.PoolId);

        return new GachaPoolInfo {
            PoolId = bannerId,
            TotalCount = state.Total,
            Level4ItemRemain = pityFour == 0 ? 0 : pityFour - Math.Min(state.SinceFour, pityFour),
            Level5ItemRemain = pityFive == 0 ? 0 : pityFive - Math.Min(state.SinceFive, pityFive),
            ClaimedRewardsMask = state.ClaimedMask,
            DailyCount = daily,
            Ret = 0
        };
    }

    private static SortedDictionary<uint, BannerConfig> BuildBanners(GameData assets)
    {
        var banners = new SortedDictionary<uint, BannerConfig>();

        foreach (var row in assets.Gacha.Banners)
        {
            banners[row.BannerId] = BannerConfig.FromBanner(row, assets.Gacha.CostCurrencyItem(row.PoolId));
        }
        return banners;
    }

    private static bool HasReset(DateTimeOffset anchor, DateTimeOffset now) =>
        now.UtcDateTime.Date > anchor.UtcDateTime.Date;

}

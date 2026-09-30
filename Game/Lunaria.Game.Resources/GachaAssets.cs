using System.Text.Json;
using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

/// <summary>Banner pools come from assets/banners.json because the recovered tables omit pools and rates.</summary>
public sealed record GachaBanner(
    uint BannerId,
    uint PoolId,
    string Kind,
    uint FeaturedFiveStar,
    uint[] StandardFiveStar,
    uint[] FourStar,
    uint[] ThreeStar
);

public sealed class GachaAssets
{
    private readonly Dictionary<uint, GachaBanner> _banners = [];
    private readonly Dictionary<uint, uint> _costItems = [];
    private readonly Dictionary<uint, PGachaTable> _pools = [];
    private readonly Dictionary<uint, RebateMilestone[]> _rebates = [];

    public GachaAssets(
        IReadOnlyDictionary<string, PGachaTable> pools,
        IReadOnlyDictionary<string, PGachaRebateTable> rebates,
        CharacterAssets characters,
        MotiveAssets motives,
        ItemAssets items,
        string bannerFile
    )
    {
        foreach (var row in pools.Values)
        {
            _pools[row.Id] = row;
        }

        foreach (var group in rebates.Values.GroupBy(r => r.TemplateId))
        {
            _rebates[group.Key] = group
                .OrderBy(r => r.DrawCount)
                .Select((r, index) => new RebateMilestone(index, r.DrawCount, new ItemGrant(r.ItemId, r.ItemCount)))
                .ToArray();
        }

        if (_pools.Count == 0)
            throw new ResourceException("P_GachaTable.json", "p_gachatable has no rows");

        foreach (var pool in _pools.Values)
        {
            _costItems[pool.Id] = items.CurrencyItemFor((int)pool.CurrencyId)
                                  ?? throw new ResourceException("P_GachaTable.json",
                                      $"p_gachatable pool {pool.Id} charges currency {pool.CurrencyId} " +
                                      "which no p_itemtable row credits");
        }

        LoadBanners(bannerFile, characters, motives);
    }

    public IReadOnlyList<uint> PoolIds => _pools.Keys.Order().ToList();

    public int Count => _pools.Count;

    public IReadOnlyList<GachaBanner> Banners => _banners.Values.OrderBy(b => b.BannerId).ToList();

    public bool PoolExists(uint poolId) => _pools.ContainsKey(poolId);

    /// <summary>This column holds a money type from p_moneytable, despite its currency name.</summary>
    public int CostMoneyType(uint poolId) => (int)(_pools.GetValueOrDefault(poolId)?.CurrencyId ?? 0);

    public uint PullCost(uint poolId) => _pools.GetValueOrDefault(poolId)?.CurrencyCount ?? 0;

    public uint FiveStarPity(uint poolId) => _pools.GetValueOrDefault(poolId)?.SsrMaxDrawCount ?? 0;

    public uint FourStarPity(uint poolId) => _pools.GetValueOrDefault(poolId)?.SrMaxDrawCount ?? 0;

    /// <summary>Daily draw limit, or 0 for unlimited draws.</summary>
    public uint DailyLimit(uint poolId) => _pools.GetValueOrDefault(poolId)?.DailyDrawLimit ?? 0;

    public IReadOnlyList<RebateMilestone> Rebates(uint poolId)
    {
        if (_pools.GetValueOrDefault(poolId) is not {} pool)
            return [];

        return _rebates.GetValueOrDefault(pool.RebateTemplateId) ?? [];
    }

    public uint CostCurrencyItem(uint poolId) => _costItems[poolId];

    public bool BannerExists(uint bannerId) => _banners.ContainsKey(bannerId);

    public bool TryGetBanner(uint bannerId, out GachaBanner? banner) =>
        _banners.TryGetValue(bannerId, out banner);

    private void LoadBanners(string bannerFile, CharacterAssets characters, MotiveAssets motives)
    {
        if (!File.Exists(bannerFile))
            return;

        BannerFile? file;

        try
        {
            file = JsonSerializer.Deserialize<BannerFile>(
                File.ReadAllBytes(bannerFile), ResourceJson.Options);
        }
        catch (JsonException ex)
        {
            throw new ResourceException("banners.json", $"malformed banner JSON in {bannerFile}: {ex.Message}", ex);
        }

        if (file?.Banners is null)
            throw new ResourceException("banners.json", $"banner file {bannerFile} has no banners array");

        foreach (var row in file.Banners)
        {
            var banner = ValidateBanner(row, characters, motives);

            if (!_banners.TryAdd(banner.BannerId, banner))
                throw new ResourceException("banners.json", $"banners.json names banner {banner.BannerId} twice");
        }
    }

    private GachaBanner ValidateBanner(BannerJson row, CharacterAssets characters, MotiveAssets motives)
    {
        const string file = "banners.json";

        if (row.BannerId == 0)
            throw new ResourceException(file, "banners.json has a banner with id 0");

        if (!_pools.ContainsKey(row.PoolId))
            throw new ResourceException(file,
                $"banners.json banner {row.BannerId} names unknown p_gachatable pool {row.PoolId}");

        var isCharacter = row.Kind.Equals("character", StringComparison.OrdinalIgnoreCase);
        var isMotive = row.Kind.Equals("motive", StringComparison.OrdinalIgnoreCase);

        if (!isCharacter && !isMotive)
            throw new ResourceException(file,
                $"banners.json banner {row.BannerId} has unknown kind '{row.Kind}'; want character or motive");

        var standardFive = row.StandardFiveStar ?? [];
        var fourStar = row.FourStar ?? [];
        var threeStar = row.ThreeStar ?? [];

        if (isCharacter)
        {
            DemandCharacter(characters, file, row.BannerId, row.FeaturedFiveStar, rarity: 5, "featured 5-star");

            if (standardFive.Length == 0)
                throw new ResourceException(file, $"banners.json banner {row.BannerId} has an empty standard 5-star pool");

            foreach (var id in standardFive)
            {
                DemandCharacter(characters, file, row.BannerId, id, rarity: 5, "standard 5-star");
            }

            if (fourStar.Length == 0)
                throw new ResourceException(file, $"banners.json banner {row.BannerId} has an empty 4-star pool");

            foreach (var id in fourStar)
            {
                DemandCharacter(characters, file, row.BannerId, id, rarity: 4, "4-star");
            }

            if (threeStar.Length == 0)
                throw new ResourceException(file, $"banners.json banner {row.BannerId} has an empty 3-star pool");

            foreach (var id in threeStar)
            {
                DemandMotive(motives, file, row.BannerId, id, rarity: 3, "3-star");
            }
        } else
        {
            DemandMotive(motives, file, row.BannerId, row.FeaturedFiveStar, rarity: 5, "featured 5-star");

            foreach (var id in standardFive)
            {
                DemandMotive(motives, file, row.BannerId, id, rarity: 5, "standard 5-star");
            }

            if (fourStar.Length == 0)
                throw new ResourceException(file, $"banners.json banner {row.BannerId} has an empty 4-star pool");

            foreach (var id in fourStar)
            {
                DemandMotive(motives, file, row.BannerId, id, rarity: 4, "4-star");
            }

            if (threeStar.Length == 0)
                throw new ResourceException(file, $"banners.json banner {row.BannerId} has an empty 3-star pool");

            foreach (var id in threeStar)
            {
                DemandMotive(motives, file, row.BannerId, id, rarity: 3, "3-star");
            }
        }

        return new GachaBanner(
            row.BannerId, row.PoolId, isCharacter ? "character" : "motive",
            row.FeaturedFiveStar, standardFive, fourStar, threeStar);
    }

    private static void DemandCharacter(
        CharacterAssets characters,
        string file,
        uint bannerId,
        uint characterId,
        uint rarity,
        string slot
    )
    {
        var actual = characters.Get(characterId)?.Rarity ?? 0;

        if (actual != rarity)
            throw new ResourceException(file,
                $"banners.json banner {bannerId} names unknown or non-{rarity}-star character {characterId} as {slot}");
    }

    private static void DemandMotive(
        MotiveAssets motives,
        string file,
        uint bannerId,
        uint motiveId,
        uint rarity,
        string slot
    )
    {
        if (!motives.Exists(motiveId) || motives.Rarity(motiveId) != rarity)
            throw new ResourceException(file,
                $"banners.json banner {bannerId} names unknown or non-{rarity}-star motive {motiveId} as {slot}");
    }

    private sealed class BannerFile
    {
        public List<BannerJson>? Banners { get; set; }
    }

    private sealed class BannerJson
    {
        public uint BannerId { get; set; }
        public uint PoolId { get; set; }
        public string Kind { get; init; } = "";
        public uint FeaturedFiveStar { get; set; }
        public uint[]? StandardFiveStar { get; set; }
        public uint[]? FourStar { get; set; }
        public uint[]? ThreeStar { get; set; }
    }
}

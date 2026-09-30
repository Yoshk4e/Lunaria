namespace Lunaria.Game.Gacha;

/// <summary>Rates and soft pity are inferred because the dump supplies no rates.</summary>
public sealed partial class GachaManager
{
    internal const double BaseFiveStarRate = 0.008;

    internal const int SoftPityStart = 70;

    internal const double BaseFourStarRate = 0.05;

    internal const double RateUpShare = 0.5;

    internal const int FeaturedGuaranteePulls = 120;

    public IReadOnlyList<PullOutcome> Pull(uint bannerId, int count, DateTimeOffset now, Random rng)
    {
        if (!_banners.TryGetValue(bannerId, out var banner))
            throw new InvalidOperationException($"unknown gacha banner {bannerId}");

        ArgumentOutOfRangeException.ThrowIfLessThan(count, other: 1);

        var state = _states.GetValueOrDefault(bannerId)
                    ?? new GachaBannerState(Total: 0, SinceFive: 0, SinceFour: 0, FeaturedSince: 0, Guaranteed: false, ClaimedMask: 0,
                        DailyCount: 0, now);

        if (HasReset(state.DailyAnchor, now))
            state = state with { DailyCount = 0, DailyAnchor = now };
        else if (state.DailyAnchor == default)
            state = state with { DailyAnchor = now };

        state = state with {
            Total = state.Total + (uint)count,
            DailyCount = state.DailyCount + (uint)count
        };

        var outcomes = new List<PullOutcome>(count);

        for (var i = 0; i < count; i++)
            outcomes.Add(PullOne(banner, ref state, rng));

        _states[bannerId] = state;
        Dirty();
        return outcomes;
    }

    private PullOutcome PullOne(BannerConfig banner, ref GachaBannerState state, Random rng)
    {
        var pityFive = PityFive(banner);
        var pityFour = PityFour(banner);

        var pityHit = pityFive > 0 && state.SinceFive + 1 >= pityFive;
        var featureDue = state.FeaturedSince + 1 >= FeaturedGuaranteePulls;
        var isFive = pityHit || featureDue || rng.NextDouble() < FiveStarRate(state.SinceFive, pityFive);

        if (isFive)
        {
            var featured = banner.Kind == BannerKind.Motive
                           || featureDue
                           || state.Guaranteed
                           || rng.NextDouble() < RateUpShare;
            var id = featured ? banner.FeaturedFiveStar : Pick(banner.StandardFiveStar, rng);

            state = state with {
                SinceFive = 0,
                SinceFour = 0,
                FeaturedSince = featured ? 0 : state.FeaturedSince + 1,
                Guaranteed = !featured && banner.Kind == BannerKind.Character
            };

            var kind = banner.Kind == BannerKind.Motive ? PullKind.Motive : PullKind.Character;
            return new PullOutcome(kind, id, Rarity: 5, featured);
        }

        var fourPityHit = pityFour > 0 && state.SinceFour + 1 >= pityFour;
        var isFour = fourPityHit || rng.NextDouble() < BaseFourStarRate;

        if (isFour)
        {
            state = state with { SinceFive = state.SinceFive + 1, SinceFour = 0, FeaturedSince = state.FeaturedSince + 1 };
            var id = Pick(banner.FourStar, rng);
            var kind = banner.Kind == BannerKind.Motive ? PullKind.Motive : PullKind.Character;
            return new PullOutcome(kind, id, Rarity: 4, IsFeatured: false);
        }

        state = state with {
            SinceFive = state.SinceFive + 1,
            SinceFour = state.SinceFour + 1,
            FeaturedSince = state.FeaturedSince + 1
        };
        return new PullOutcome(PullKind.Motive, Pick(banner.ThreeStar, rng), Rarity: 3, IsFeatured: false);
    }

    internal static double FiveStarRate(uint sinceFive, uint pityFive)
    {
        if (pityFive == 0 || sinceFive < SoftPityStart)
            return BaseFiveStarRate;

        var span = pityFive > SoftPityStart ? pityFive - SoftPityStart : 0;

        if (span == 0)
            return 1.0;

        var t = Math.Min((double)(sinceFive - SoftPityStart + 1) / span, val2: 1.0);
        return BaseFiveStarRate + (1.0 - BaseFiveStarRate) * t;
    }

    private uint PityFive(BannerConfig banner) => assets.Gacha.FiveStarPity(banner.PoolId);

    private uint PityFour(BannerConfig banner) => assets.Gacha.FourStarPity(banner.PoolId);

    private static uint Pick(uint[] pool, Random rng) =>
        pool.Length == 0 ? throw new InvalidOperationException("gacha banner pool is empty") : pool[rng.Next(pool.Length)];
}

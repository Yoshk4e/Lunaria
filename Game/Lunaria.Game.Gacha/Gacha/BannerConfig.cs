using Lunaria.Game.Resources;

namespace Lunaria.Game.Gacha;

public enum BannerKind
{
    Character,
    Motive
}

public sealed record BannerConfig(
    uint BannerId,
    uint PoolId,
    BannerKind Kind,
    uint FeaturedFiveStar,
    uint[] StandardFiveStar,
    uint[] FourStar,
    uint[] ThreeStar,
    uint CostCurrencyItem
)
{
    /// <summary>Duplicate conversion is inferred because its table is missing from the dump.</summary>
    public ItemGrant DuplicateRefund(uint rarity) => new(CostCurrencyItem, rarity >= 5 ? 40u : 10u);

    public static BannerConfig FromBanner(GachaBanner banner, uint costCurrencyItem) => new(
        banner.BannerId,
        banner.PoolId,
        banner.Kind.Equals("motive", StringComparison.OrdinalIgnoreCase) ? BannerKind.Motive : BannerKind.Character,
        banner.FeaturedFiveStar,
        banner.StandardFiveStar,
        banner.FourStar,
        banner.ThreeStar,
        costCurrencyItem);
}

namespace Lunaria.Game.Player;

public sealed record StarterGrant(
    int Characters,
    bool TeamFormed,
    bool SkillsSeeded,
    bool WalletFunded,
    IReadOnlyList<uint> RefusedItems
)
{
    public static StarterGrant None { get; } = new(Characters: 0, TeamFormed: false, SkillsSeeded: false, WalletFunded: false, []);

    public bool GrantedAnything => Characters > 0 || TeamFormed || SkillsSeeded || WalletFunded;
}

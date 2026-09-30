namespace Lunaria.Game.Player;

/// <summary>Seed the roster before teams and skills need its instance IDs.</summary>
public sealed partial class Player
{
    public StarterGrant GrantStarterState()
    {
        var characters = Characters.GrantStarter(Guid);
        var team = Teams.GrantStarter(Characters);
        var skills = Skills.GrantStarter(Characters);
        var refused = Bag.GrantStarter();
        var wallet = Wallet.GrantStarter();

        return new StarterGrant(characters, team, skills, wallet, refused);
    }
}

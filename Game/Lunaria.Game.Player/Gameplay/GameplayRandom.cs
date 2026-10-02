namespace Lunaria.Game.Player.Gameplay;

/// <summary>Independent random streams that tests can seed or replace with controlled Random implementations.</summary>
public sealed class GameplayRandom
{
    public Random Gacha { get; }
    public Random Loot { get; }
    public Random Wanted { get; }
    public Random Weather { get; }

    public GameplayRandom(Random? gacha = null, Random? loot = null, Random? wanted = null, Random? weather = null)
    {
        Gacha = gacha ?? new Random();
        Loot = loot ?? new Random();
        Wanted = wanted ?? new Random();
        Weather = weather ?? new Random();
    }
}

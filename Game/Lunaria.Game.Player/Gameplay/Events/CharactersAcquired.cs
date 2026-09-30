namespace Lunaria.Game.Player.Gameplay;

public sealed record CharactersAcquired(IReadOnlyList<Msg.CharacterData> Characters) : IGameplayEvent;

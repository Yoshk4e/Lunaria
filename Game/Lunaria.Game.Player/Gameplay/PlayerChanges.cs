using Google.Protobuf;

namespace Lunaria.Game.Player.Gameplay;

public sealed class PlayerChanges
{
    private readonly List<IMessage> _notifications = [];

    public void Add(IMessage notification) => _notifications.Add(notification);

    public IReadOnlyList<IMessage> Drain()
    {
        if (_notifications.Count == 0) return [];

        var result = _notifications.ToArray();
        _notifications.Clear();
        return result;
    }

    public void Clear() => _notifications.Clear();
}

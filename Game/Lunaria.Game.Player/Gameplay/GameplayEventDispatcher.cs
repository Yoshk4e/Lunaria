namespace Lunaria.Game.Player.Gameplay;

/// <summary>Queue nested events so hooks cannot call themselves recursively.</summary>
public sealed class GameplayEventDispatcher(Player player, PlayerChanges changes)
{
    private readonly Dictionary<Type, List<Action<IGameplayEvent>>> _handlers = [];
    private readonly Queue<IGameplayEvent> _pending = new();
    private bool _dispatching;
    private bool _started;

    public void Register<TEvent>(IGameplayHook<TEvent> hook) where TEvent : IGameplayEvent
    {
        if (_started) throw new InvalidOperationException("Gameplay hooks must be registered before dispatch.");

        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
            _handlers.Add(typeof(TEvent), handlers = []);
        handlers.Add(occurrence => hook.Handle(player, (TEvent)occurrence, changes));
    }

    public void Publish<TEvent>(TEvent occurrence) where TEvent : IGameplayEvent
    {
        _started = true;
        _pending.Enqueue(occurrence);
        if (_dispatching) return;

        _dispatching = true;

        try
        {
            var remaining = 4096;

            while (_pending.TryDequeue(out var next))
            {
                if (--remaining == 0)
                    throw new InvalidOperationException("Gameplay hook cycle exceeded the dispatch budget.");

                if (!_handlers.TryGetValue(next.GetType(), out var handlers))
                    throw new InvalidOperationException($"No gameplay hook registered for {next.GetType().Name}.");

                foreach (var handler in handlers)
                {
                    handler(next);
                }
            }
        }
        finally
        {
            _pending.Clear();
            _dispatching = false;
        }
    }
}

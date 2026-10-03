using Lunaria.Common.Tracking;
namespace Lunaria.Game.Player.Managers;

/// <summary>role_exchange_activity uses 1 for read and 0 for unseen.</summary>
public sealed partial class RedPointManager : TrackedObject
{
    public const uint Read = 1;

    public const uint Unread = 0;

    private bool __trackedExchangeActivityRead = default!;
    [Tracked]
    public partial bool ExchangeActivityRead { get; private set; }

    public uint ExchangeActivity => ExchangeActivityRead ? Read : Unread;

    public void Load(bool exchangeActivityRead)
    {
        ExchangeActivityRead = exchangeActivityRead;
        AcceptLoadedState();
    }

    public void MarkExchangeActivityRead()
    {
        if (ExchangeActivityRead)
            return;

        ExchangeActivityRead = true;

    }

}

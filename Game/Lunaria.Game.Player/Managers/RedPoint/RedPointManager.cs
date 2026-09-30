namespace Lunaria.Game.Player.Managers;

/// <summary>role_exchange_activity uses 1 for read and 0 for unseen.</summary>
public sealed class RedPointManager
{
    public const uint Read = 1;

    public const uint Unread = 0;

    public bool IsDirty { get; private set; }

    public bool ExchangeActivityRead { get; private set; }

    public uint ExchangeActivity => ExchangeActivityRead ? Read : Unread;

    public void Load(bool exchangeActivityRead)
    {
        ExchangeActivityRead = exchangeActivityRead;
        IsDirty = false;
    }

    public void MarkExchangeActivityRead()
    {
        if (ExchangeActivityRead)
            return;

        ExchangeActivityRead = true;
        IsDirty = true;
    }

    public void ClearDirty() => IsDirty = false;
}

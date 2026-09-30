using Msg;

namespace Lunaria.Game.Player.Managers;

/// <summary>Bind the account only once to avoid reusing another account's role state.</summary>
public sealed class AccountManager
{
    public string AccountKey { get; private set; } = "";
    public string Userid { get; private set; } = "";
    public string ChannelName { get; private set; } = "";
    public string Udid { get; private set; } = "";

    public DateTimeOffset BoundAt { get; private set; }

    public bool IsBound => Id is not null;

    public long? Id { get; private set; }

    public int Bind(long accountId, string accountKey, string userid, DateTimeOffset now)
    {
        if (Id is not null)
            return (int)EnmTextCode.EnmTextAccLoginRepeat;

        Id = accountId;
        AccountKey = accountKey;
        Userid = userid;
        BoundAt = now;
        return 0;
    }

    public void SetOrigin(string channelName, string udid)
    {
        ChannelName = channelName;
        Udid = udid;
    }
}

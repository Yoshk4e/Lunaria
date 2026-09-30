namespace Lunaria.Game.Player.Persistence.Entities;

/// <summary>The client sends an empty userid and expects the server to return one in SCAccountLogin.</summary>
public sealed class Account
{
    public long Id { get; set; }
    public string AccountKey { get; set; } = "";
    public string Userid { get; set; } = "";
    public string ChannelName { get; set; } = "";
    public string ChannelUid { get; set; } = "";
    public string Udid { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }

    public List<Role> Roles { get; set; } = [];
}

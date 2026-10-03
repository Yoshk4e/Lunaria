namespace Lunaria.Game.Player.Persistence.Entities;

public sealed class RoleMail
{
    public long Id { get; set; }
    public long RoleId { get; set; }
    public long MailId { get; set; }
    public uint TemplateId { get; set; }
    public uint Type { get; set; }
    public bool Important { get; set; }
    public bool Open { get; set; }
    public bool HasAttach { get; set; }
    public string Items { get; set; } = "[]";
    public string TemplateContentParams { get; set; } = "[]";
    public string Contents { get; set; } = "[]";
    public long Time { get; set; }
    public long ExpireTime { get; set; }

    public Role? Role { get; set; }
}

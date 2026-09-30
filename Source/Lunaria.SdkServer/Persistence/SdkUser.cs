namespace Lunaria.SdkServer.Persistence;

public sealed class SdkUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? Nickname { get; set; }
    public string? AvatarUrl { get; set; }
    public bool Locked { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

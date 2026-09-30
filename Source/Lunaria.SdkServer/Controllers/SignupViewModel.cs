namespace Lunaria.SdkServer.Controllers;

public sealed class SignupViewModel
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Nickname { get; set; }

    public string? Error { get; set; }

    public string? Success { get; set; }
}

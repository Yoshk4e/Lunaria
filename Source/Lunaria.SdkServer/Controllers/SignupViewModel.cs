namespace Lunaria.SdkServer.Controllers;

public sealed class SignupViewModel
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Nickname { get; set; }

    public string? Error { get; set; }

    public string? Success { get; set; }

    /// <summary>Where the form posts: /signup, or the SDK register page.</summary>
    public string Action { get; set; } = "/signup";

    /// <summary>The SDK app id from the register URL, which the result URL must carry back.</summary>
    public string AppId { get; set; } = "";
}

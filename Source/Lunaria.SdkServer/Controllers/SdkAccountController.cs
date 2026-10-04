using Lunaria.SdkServer.Persistence;
using Lunaria.SdkServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lunaria.SdkServer.Controllers;

/// <summary>
/// The HappyElements SDK account pages. The SDK opens sdk-account.html#/retrieve-way for a forgotten password and
/// closes its WebView when the URL before "?" ends with register-result.html (register) or retrieve-result
/// (password reset) and the query carries appId equal to its own app id (UHeSDK WebView URL handler).
/// </summary>
public sealed class SdkAccountController(UserRepository users, PasswordHasher hasher, ILogger<SdkAccountController> logger)
    : Controller
{
    private const string PagePath = "/account/sdk-account.html";
    private const string RegisterResultPath = "/account/register-result.html";
    private const string RetrievePath = "/account/retrieve";

    public static string RegisterResultUrl(string appId, string email) =>
        $"{RegisterResultPath}?appId={Uri.EscapeDataString(appId)}&account={Uri.EscapeDataString(email)}";

    public static string RetrieveResultUrl(string appId, string email) =>
        $"{PagePath}#/retrieve-result?appId={Uri.EscapeDataString(appId)}&account={Uri.EscapeDataString(email)}";

    [HttpGet(PagePath)]
    public IActionResult Index() => View(new RetrieveViewModel());

    [HttpGet(RegisterResultPath)]
    public IActionResult RegisterResult() => View(nameof(Index), new RetrieveViewModel { Route = "register-result" });

    /// <summary>
    /// Resets a password without an email check. The SDK server only listens on 127.0.0.1, so only the player at
    /// this machine reaches it.
    /// </summary>
    [HttpPost(RetrievePath)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retrieve(RetrieveViewModel model)
    {
        model.ShowForm = true;

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Email))
        {
            model.Error = "Please fill in all required fields";
            return View(nameof(Index), model);
        }

        if (model.Password.Length < 8)
        {
            model.Error = "Password must be at least 8 characters";
            return View(nameof(Index), model);
        }

        if (model.Password != model.ConfirmPassword)
        {
            model.Error = "Passwords do not match";
            return View(nameof(Index), model);
        }

        try
        {
            if (!await users.UpdatePasswordAsync(model.Email, hasher.Hash(model.Password)).ConfigureAwait(false))
            {
                model.Error = "No account uses this email";
                return View(nameof(Index), model);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "failed to reset password");
            model.Error = "An error occurred. Please try again.";
            return View(nameof(Index), model);
        }

        logger.LogInformation("password reset for {Email}", model.Email);
        return Redirect(RetrieveResultUrl(model.AppId, model.Email));
    }
}

public sealed class RetrieveViewModel
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";

    /// <summary>The SDK app id, read by the page from its #/retrieve-way query.</summary>
    public string AppId { get; set; } = "";

    /// <summary>The section to show when the server knows it (register-result.html has no hash).</summary>
    public string? Route { get; set; }

    public string? Error { get; set; }

    /// <summary>Set after a failed reset, so the page shows the form whatever its hash route.</summary>
    public bool ShowForm { get; set; }
}

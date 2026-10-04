using Lunaria.SdkServer.Persistence;
using Lunaria.SdkServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lunaria.SdkServer.Controllers;

public sealed class SignupController(UserRepository users, PasswordHasher hasher, ILogger<SignupController> logger)
    : Controller
{
    /// <summary>The HappyElements SDK register button opens this page in its WebView.</summary>
    private const string SdkRegisterPath = "/account/register.html";

    /// <summary>The SDK closes its WebView and reports success once the page reaches this path.</summary>
    private const string SdkRegisterResultPath = "/account/register-result.html";

    [HttpGet("/signup")]
    [HttpGet("/signup/{*rest}")]
    public IActionResult Index() => View(new SignupViewModel());

    [HttpGet(SdkRegisterPath)]
    public IActionResult SdkRegister() => View(nameof(Index), new SignupViewModel { Action = SdkRegisterPath });

    [HttpGet(SdkRegisterResultPath)]
    public ContentResult SdkRegisterResult() =>
        Content("<!doctype html><meta charset=\"utf-8\"><title>Account created</title><p>Account created.</p>", "text/html");

    [HttpPost(SdkRegisterPath)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SdkRegister(SignupViewModel model)
    {
        model.Action = SdkRegisterPath;

        if (await RegisterAsync(model).ConfigureAwait(false) is {} failed)
            return View(nameof(Index), failed);

        return Redirect(SdkRegisterResultPath);
    }

    [HttpPost("/signup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SignupViewModel model)
    {
        if (await RegisterAsync(model).ConfigureAwait(false) is {} failed)
            return View(failed);

        return View(new SignupViewModel {
            Success = "Account created successfully! <a href=\"/login\">Sign in</a>"
        });
    }

    /// <summary>Creates the account, or returns the model with its error to show again.</summary>
    private async Task<SignupViewModel?> RegisterAsync(SignupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Error = "Please fill in all required fields";
            return model;
        }

        if (!model.Email.Contains('@'))
        {
            model.Error = "Please enter a valid email address";
            return model;
        }

        if (model.Password.Length < 8)
        {
            model.Error = "Password must be at least 8 characters";
            return model;
        }

        try
        {
            if (await users.FindByEmailAsync(model.Email).ConfigureAwait(false) is not null)
            {
                model.Error = "An account with this email already exists";
                return model;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "database error during user lookup");
            model.Error = "An error occurred. Please try again.";
            return model;
        }

        string passwordHash;

        try
        {
            passwordHash = hasher.Hash(model.Password);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "failed to hash password");
            model.Error = "An error occurred. Please try again.";
            return model;
        }

        try
        {
            var user = await users.CreateAsync(model.Email, passwordHash, model.Nickname).ConfigureAwait(false);
            logger.LogInformation("user {UserId} ({Email}) registered successfully", user.Id, user.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "failed to create user");
            model.Error = "An error occurred. Please try again.";
            return model;
        }

        return null;
    }
}

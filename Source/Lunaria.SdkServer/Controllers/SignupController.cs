using Lunaria.SdkServer.Persistence;
using Lunaria.SdkServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lunaria.SdkServer.Controllers;

public sealed class SignupController(UserRepository users, PasswordHasher hasher, ILogger<SignupController> logger)
    : Controller
{
    [HttpGet("/signup")]
    [HttpGet("/signup/{*rest}")]
    public IActionResult Index() => View(new SignupViewModel());

    [HttpPost("/signup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SignupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Error = "Please fill in all required fields";
            return View(model);
        }

        if (!model.Email.Contains('@'))
        {
            model.Error = "Please enter a valid email address";
            return View(model);
        }

        if (model.Password.Length < 8)
        {
            model.Error = "Password must be at least 8 characters";
            return View(model);
        }

        try
        {
            if (await users.FindByEmailAsync(model.Email).ConfigureAwait(false) is not null)
            {
                model.Error = "An account with this email already exists";
                return View(model);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "database error during user lookup");
            model.Error = "An error occurred. Please try again.";
            return View(model);
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
            return View(model);
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
            return View(model);
        }

        return View(new SignupViewModel {
            Success = "Account created successfully! <a href=\"/login\">Sign in</a>"
        });
    }
}

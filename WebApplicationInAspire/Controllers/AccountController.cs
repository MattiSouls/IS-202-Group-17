using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplicationInAspire.Models.Account;
using WebApplicationInAspire.Security;

namespace WebApplicationInAspire.Controllers;

/// <summary>Registrering og innlogging med Identity, leverandørrolle og trygg lokal returadresse.</summary>
public class AccountController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ILogger<AccountController> logger) : Controller
{
    /// <summary>Viser registreringsskjemaet.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    /// <summary>Oppretter brukeren med hash og leverandørrolle; ruller tilbake hvis rolletildeling feiler.</summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new IdentityUser { UserName = model.Email, Email = model.Email };
        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        var roleResult = await userManager.AddToRoleAsync(user, RoleNames.ResourceProvider);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "Registreringen feilet. Prøv igjen.");
            return View(model);
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        logger.LogInformation("Ny ressursleverandør registrert: {UserId}", user.Id);
        return RedirectToAction("Index", "Home");
    }

    /// <summary>Viser innlogging og bevarer ønsket returadresse.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    /// <summary>Logger inn med Identity-låsing og avviser eksterne returadresser.</summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return LocalRedirect(SafeReturnUrl(model.ReturnUrl));
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Kontoen er midlertidig låst. Prøv igjen om 5 minutter.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Feil e-post eller passord.");
        return View(model);
    }

    /// <summary>Logger ut ved antiforgery-beskyttet POST.</summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    /// <summary>Viser at den innloggede brukeren mangler tilgang.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    /// <summary>Godtar kun lokale returadresser for å hindre åpen redirect.</summary>
    private string SafeReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/Kart");
    }
}

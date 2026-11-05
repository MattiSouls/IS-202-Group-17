using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplicationInAspire.Security;

namespace WebApplicationInAspire.Controllers;

/// <summary>Bytter mellom egne demokontoer når lokal demomodus er aktivert.</summary>
public class DemoController(
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn,
    IWebHostEnvironment environment,
    IConfiguration configuration) : Controller
{
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchRole(string role)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("Demo:Enabled"))
            return NotFound();

        var email = role switch
        {
            RoleNames.ResourceProvider => "ressurs-demo@example.test",
            RoleNames.PublicUser => "offentlig-demo@example.test",
            RoleNames.Admin => "admin-demo@example.test",
            _ => null
        };
        if (email is null) return BadRequest();

        var user = await users.FindByIdAsync("demo-" + role);
        if (user is null)
        {
            user = new IdentityUser { Id = "demo-" + role, UserName = email, Email = email };
            // Kontoene har ikke passord og kan bare brukes gjennom aktivert demomodus.
            var created = await users.CreateAsync(user);
            if (!created.Succeeded) return Conflict("Demokontoen kunne ikke opprettes.");
            var assigned = await users.AddToRoleAsync(user, role);
            if (!assigned.Succeeded)
            {
                await users.DeleteAsync(user);
                return Conflict("Demorollen kunne ikke tildeles.");
            }
        }

        var roles = await users.GetRolesAsync(user);
        if (roles.Count != 1 || roles[0] != role || await users.HasPasswordAsync(user))
            return Conflict("Kontoen samsvarer ikke med demokontoens oppsett.");

        await signIn.SignOutAsync();
        await signIn.SignInWithClaimsAsync(user, new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20)
        }, [new Claim("DemoSession", "true")]);

        return RedirectToAction("Overview", role == RoleNames.PublicUser ? "Need" : "Resource");
    }
}

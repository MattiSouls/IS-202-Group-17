using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplicationInAspire.Security;

namespace WebApplicationInAspire.Controllers;

/// <summary>Gjenoppretter startscenarioet i den separate lokale EXPO-databasen.</summary>
[Authorize(Roles = RoleNames.PublicUser + "," + RoleNames.Admin)]
public class ExpoController(IConfiguration configuration, IHostEnvironment environment) : Controller
{
    private bool CanReset => environment.IsDevelopment()
        && configuration.GetValue<bool>("DemoData:Enabled")
        && configuration.GetValue<bool>("DemoData:AllowReset")
        && (User.IsInRole(RoleNames.Admin)
            || string.Equals(User.Identity?.Name, configuration["SeedUsers:PublicUser:Email"], StringComparison.OrdinalIgnoreCase));

    [HttpGet]
    public IActionResult Index() => CanReset ? View() : NotFound();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset()
    {
        if (!CanReset) return NotFound();
        await ExpoDemoSeeder.SeedAsync(HttpContext.RequestServices, configuration, environment, reset: true);
        TempData["SuccessMessage"] = "EXPO-demoen er klar igjen med seks ressurser og tre nye behov.";
        return RedirectToAction("Overview", "Need");
    }
}

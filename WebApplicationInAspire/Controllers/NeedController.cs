using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplicationInAspire.Models;
using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.Controllers;

[Authorize]
public class NeedController : Controller
{
    // Viser skjemaet for å registrere et nytt behov
    [HttpGet]
    public IActionResult Register()
    {
        return View(new NeedViewModel());
    }

    // Mottar informasjonen fra registreringsskjemaet
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Register(NeedViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Lagring kobles til når database/repository er klart.
        TempData["SuccessMessage"] = "Behovet er registrert.";

        return RedirectToAction(nameof(Register));
    }

    // Viser oversikten over registrerte behov
[HttpGet]
public IActionResult Overview()
{
    var needs = new List<Need>();

    return View(needs);
}


// Endrer status på et behov
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult UpdateStatus(int id, string status)
{
    var allowedStatuses = new[]
    {
        NeedStatuses.New,
        NeedStatuses.UnderReview,
        NeedStatuses.Assigned,
        NeedStatuses.Resolved
    };

    if (!allowedStatuses.Contains(status))
    {
        return BadRequest();
    }

    // Selve oppdateringen kobles til databasen senere.
    return RedirectToAction(nameof(Overview));
}

}

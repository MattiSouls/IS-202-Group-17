using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplicationInAspire.DataAccess.Repositories;
using WebApplicationInAspire.Models;
using WebApplicationInAspire.Models.Entities;
using WebApplicationInAspire.Security;

namespace WebApplicationInAspire.Controllers;

/// <summary>Offentlige brukere registrerer behov; oppretter eller administrator endrer status.</summary>
[Authorize(Roles = RoleNames.PublicUser + "," + RoleNames.Admin)]
public class NeedController(INeedRepository repository, ILogger<NeedController> logger) : Controller
{
    /// <summary>Viser skjemaet for et nytt behov.</summary>
    [HttpGet]
    public IActionResult Register() => View(new NeedViewModel());

    /// <summary>Validerer og lagrer behovet med innlogget bruker som oppretter.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(NeedViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var need = new Need
        {
            Type = model.Type,
            Region = model.Region,
            Location = model.Location,
            NeededAt = model.NeededAt!.Value,
            Priority = model.Priority,
            ContactName = model.ContactName,
            PhoneNumber = model.PhoneNumber,
            CreatedByUserId = userId,
            Status = NeedStatuses.New
        };
        await repository.AddAsync(need);
        logger.LogInformation("Behov {NeedId} opprettet av bruker {UserId}", need.Id, userId);
        TempData["SuccessMessage"] = "Behovet er registrert.";
        return RedirectToAction(nameof(Overview));
    }

    /// <summary>Viser lagrede behov for offentlige brukere og administratorer.</summary>
    [HttpGet]
    public async Task<IActionResult> Overview() => View(await repository.GetAllAsync());

    /// <summary>Oppdaterer manuell status uten å overstyre tildelingsflyten.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        if (status is not (NeedStatuses.New or NeedStatuses.UnderReview or NeedStatuses.Resolved))
        {
            return BadRequest();
        }

        var need = await repository.GetByIdAsync(id);
        if (need is null)
        {
            return NotFound();
        }

        if (need.CreatedByUserId != User.FindFirstValue(ClaimTypes.NameIdentifier)
            && !User.IsInRole(RoleNames.Admin))
        {
            return Forbid();
        }

        if (!await repository.UpdateStatusAsync(id, status))
        {
            return Conflict("Behovet kan ikke endres her. Et tildelt behov må håndteres gjennom tildelingsflyten.");
        }

        logger.LogInformation("Status for behov {NeedId} endret til {Status}", id, status);
        TempData["SuccessMessage"] = "Statusen er oppdatert.";
        return RedirectToAction(nameof(Overview));
    }
}

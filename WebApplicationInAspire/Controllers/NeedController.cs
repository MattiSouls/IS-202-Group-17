using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplicationInAspire.DataAccess.Repositories;
using WebApplicationInAspire.Models;
using WebApplicationInAspire.Models.Entities;
using WebApplicationInAspire.Security;
using WebApplicationInAspire.Services;

namespace WebApplicationInAspire.Controllers;

/// <summary>Offentlige brukere registrerer behov; oppretter eller administrator endrer status.</summary>
[Authorize(Roles = RoleNames.PublicUser + "," + RoleNames.Admin)]
public class NeedController(INeedRepository repository, AllocationService allocation, ILogger<NeedController> logger) : Controller
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
            Type = model.Type.Trim(),
            CreationToken = model.RequestId,
            Latitude = model.Latitude,
            Longitude = model.Longitude,
            Region = model.Region.Trim(),
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
    public async Task<IActionResult> Overview([FromQuery] ListFilter filter) => View(await repository.SearchAsync(filter, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""));

    /// <summary>Viser ledige ressurser med samme type og region som behovet.</summary>
    [HttpGet]
    public async Task<IActionResult> Matches(int id, int page = 1)
    {
        var need = await repository.GetByIdAsync(id);
        if (need is null) return NotFound();
        var total = await allocation.CountMatchesAsync(need);
        page = Math.Clamp(page, 1, Math.Max(1, (total + 9) / 10));
        return View(new NeedMatchesViewModel
        {
            Need = need,
            Resources = await allocation.FindMatchesAsync(need, page),
            Page = page,
            Total = total,
            CanManage = need.CreatedByUserId == User.FindFirstValue(ClaimTypes.NameIdentifier) || User.IsInRole(RoleNames.Admin)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var need = await repository.GetByIdAsync(id);
        if (need is null) return NotFound();
        ViewData["History"] = await allocation.HistoryAsync(id);
        return View(need);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var need = await repository.GetByIdAsync(id);
        if (need is null) return NotFound();
        if (!CanManage(need)) return Forbid();
        if (need.Status is NeedStatuses.Assigned or NeedStatuses.Resolved) return Conflict("Frigi eller gjenåpne behovet før redigering.");
        return View("Register", new NeedViewModel
        {
            Id = need.Id,
            Version = need.Version,
            Type = need.Type,
            Region = need.Region,
            Location = need.Location,
            NeededAt = need.NeededAt,
            Priority = need.Priority,
            ContactName = need.ContactName,
            PhoneNumber = need.PhoneNumber,
            Latitude = need.Latitude,
            Longitude = need.Longitude
        });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, NeedViewModel model)
    {
        if (model.Id != id) return BadRequest();
        var need = await repository.GetByIdAsync(id);
        if (need is null) return NotFound();
        if (!CanManage(need)) return Forbid();
        if (!ModelState.IsValid) return View("Register", model);
        need.Version = model.Version; need.Type = model.Type; need.Region = model.Region; need.Location = model.Location;
        need.NeededAt = model.NeededAt!.Value; need.Priority = model.Priority; need.ContactName = model.ContactName;
        need.PhoneNumber = model.PhoneNumber; need.Latitude = model.Latitude; need.Longitude = model.Longitude;
        if (!await repository.EditAsync(need))
        {
            Response.StatusCode = 409;
            ModelState.AddModelError("", "Behovet er endret eller tildelt siden skjemaet ble åpnet. Verdiene dine er beholdt. Åpne nyeste versjon før du prøver igjen.");
            return View("Register", model);
        }
        TempData["SuccessMessage"] = "Behovet er oppdatert. Du kan nå finne passende ressurser.";
        return RedirectToAction(nameof(Details), new { id });
    }
    private bool CanManage(Need need) => need.CreatedByUserId == User.FindFirstValue(ClaimTypes.NameIdentifier) || User.IsInRole(RoleNames.Admin);

    [HttpGet]
    public async Task<IActionResult> Review(int id, int resourceId = 0, string operation = "assign")
    {
        var need = await repository.GetByIdAsync(id);
        if (need is null) return NotFound();
        if (!CanManage(need)) return Forbid();
        if (operation is not ("assign" or "release" or "resolve")) return BadRequest();
        // Brukerens valgte kandidat hentes uten å laste alle treff.
        var resource = operation == "assign" ? await allocation.FindCandidateAsync(need, resourceId)
            : await allocation.GetResourceAsync(need.AssignedResourceId ?? 0);
        if (resource is null) return NotFound();
        return View(new AssignmentReview(need, resource, operation));
    }

    [HttpGet]
    public async Task<IActionResult> ReviewStatus(int id, string status)
    {
        var need = await repository.GetByIdAsync(id);
        if (need is null) return NotFound();
        if (!CanManage(need)) return Forbid();
        if (status is not (NeedStatuses.New or NeedStatuses.UnderReview or NeedStatuses.Resolved) || need.Status == NeedStatuses.Assigned) return BadRequest();
        ViewData["NewStatus"] = status;
        return View(need);
    }

    /// <summary>Tildeler en passende ressurs med kontroll av oppretter og samtidighet.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, int resourceId, long? needVersion = null, long? resourceVersion = null)
    {
        if (!needVersion.HasValue || !resourceVersion.HasValue) return BadRequest("Åpne bekreftelsen på nytt før tildeling.");
        ViewData["NeedId"] = id;
        return AllocationResponse(await allocation.AssignAsync(id, resourceId,
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", User.IsInRole(RoleNames.Admin), needVersion, resourceVersion), "Ressursen er tildelt. Varsel er lagret til de berørte brukerne.", id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(int id, long? needVersion = null)
    {
        if (!needVersion.HasValue) return BadRequest("Åpne bekreftelsen på nytt før frigivelse.");
        ViewData["NeedId"] = id;
        return AllocationResponse(await allocation.ReleaseAsync(id,
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", User.IsInRole(RoleNames.Admin), false, needVersion), "Ressursen er frigitt. Behovet vurderes igjen.", id);
    }

    private IActionResult AllocationResponse(AllocationResult result, string message, int? id = null)
    {
        if (result == AllocationResult.NotFound) return NotFound();
        if (result == AllocationResult.Forbidden) return Forbid();
        if (result == AllocationResult.Conflict)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            return View("AllocationConflict");
        }
        TempData["SuccessMessage"] = message;
        return id.HasValue ? RedirectToAction(nameof(Details), new { id }) : RedirectToAction(nameof(Overview));
    }

    /// <summary>Oppdaterer manuell status uten å overstyre tildelingsflyten.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status, long? needVersion = null)
    {
        if (!needVersion.HasValue) return BadRequest("Åpne behovet på nytt før statusendring.");
        ViewData["NeedId"] = id;
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

        if (need.Status == NeedStatuses.Assigned && status == NeedStatuses.Resolved)
        {
            return AllocationResponse(await allocation.ReleaseAsync(id,
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", User.IsInRole(RoleNames.Admin), true, needVersion),
                "Behovet er løst og ressursen er frigitt.", id);
        }

        if (!await repository.UpdateStatusAsync(id, status, default, needVersion))
        {
            Response.StatusCode = 409;
            return View("AllocationConflict");
        }

        logger.LogInformation("Status for behov {NeedId} endret til {Status}", id, status);
        TempData["SuccessMessage"] = "Statusen er oppdatert.";
        return RedirectToAction(nameof(Details), new { id });
    }
}

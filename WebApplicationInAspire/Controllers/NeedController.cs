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
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private bool IsAdmin => User.IsInRole(RoleNames.Admin);

    /// <summary>Viser skjemaet for et nytt behov.</summary>
    [HttpGet]
    public IActionResult Register(double? latitude = null, double? longitude = null,
        double? areaSouth = null, double? areaWest = null, double? areaNorth = null, double? areaEast = null)
    {
        // Forhåndsutfylling fra kartsiden. Ugyldige verdier ignoreres.
        if (GeoArea.From(areaSouth, areaWest, areaNorth, areaEast) is { } area)
            return View(new NeedViewModel { AreaSouth = area.South, AreaWest = area.West, AreaNorth = area.North, AreaEast = area.East });
        var point = latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
        return View(new NeedViewModel { Latitude = point ? latitude : null, Longitude = point ? longitude : null });
    }

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

        var need = new Need { CreatedByUserId = userId, CreationToken = model.RequestId, Status = NeedStatuses.New };
        model.ApplyTo(need);
        await repository.AddAsync(need);
        logger.LogInformation("Behov {NeedId} opprettet av bruker {UserId}", need.Id, userId);
        TempData["SuccessMessage"] = "Behovet er registrert.";
        return RedirectToAction(nameof(Overview));
    }

    /// <summary>Viser lagrede behov for offentlige brukere og administratorer.</summary>
    [HttpGet]
    public async Task<IActionResult> Overview([FromQuery] ListFilter filter) => View(await repository.SearchAsync(filter, CurrentUserId));

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
            CanManage = need.CreatedByUserId == CurrentUserId || IsAdmin
        });
    }

    /// <summary>Viser behov og de siste tildelingshendelsene for offentlige brukere.</summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var need = await repository.GetByIdAsync(id);
        if (need is null) return NotFound();
        ViewData["History"] = await allocation.HistoryAsync(id);
        return View(need);
    }

    /// <summary>Åpner redigering bare for oppretter/admin og et behov uten aktiv tildeling.</summary>
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
            Longitude = need.Longitude,
            AreaSouth = need.AreaSouth,
            AreaWest = need.AreaWest,
            AreaNorth = need.AreaNorth,
            AreaEast = need.AreaEast
        });
    }
    /// <summary>Lagrer validerte felt med versjonskontroll og beholder verdier ved konflikt.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, NeedViewModel model)
    {
        if (model.Id != id) return BadRequest();
        var need = await repository.GetByIdAsync(id);
        if (need is null) return NotFound();
        if (!CanManage(need)) return Forbid();
        if (!ModelState.IsValid) return View("Register", model);
        need.Version = model.Version;
        model.ApplyTo(need);
        if (!await repository.EditAsync(need))
        {
            Response.StatusCode = 409;
            ModelState.AddModelError("", "Behovet er endret eller tildelt siden skjemaet ble åpnet. Verdiene dine er beholdt. Åpne nyeste versjon før du prøver igjen.");
            return View("Register", model);
        }
        TempData["SuccessMessage"] = "Behovet er oppdatert. Du kan nå finne passende ressurser.";
        return RedirectToAction(nameof(Details), new { id });
    }
    /// <summary>Behovets oppretter eller administrator kan endre behovet.</summary>
    private bool CanManage(Need need) => need.CreatedByUserId == CurrentUserId || IsAdmin;

    /// <summary>Viser tildelings- eller frigivelsesbekreftelsen uten å endre data.</summary>
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

    /// <summary>Bekrefter tillatt manuell statusendring for oppretter/admin.</summary>
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
            CurrentUserId, IsAdmin, needVersion, resourceVersion), "Ressursen er tildelt. Varsel er lagret til de berørte brukerne.", id);
    }

    /// <summary>Frigir ressursen via transaksjonen og kontrollerer bekreftelsens versjon.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(int id, long? needVersion = null)
    {
        if (!needVersion.HasValue) return BadRequest("Åpne bekreftelsen på nytt før frigivelse.");
        ViewData["NeedId"] = id;
        return AllocationResponse(await allocation.ReleaseAsync(id,
            CurrentUserId, IsAdmin, false, needVersion), "Ressursen er frigitt. Behovet vurderes igjen.", id);
    }

    /// <summary>Oversetter tjenestens resultat til redirect, tilgangsfeil eller konfliktvisning.</summary>
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

        if (!CanManage(need)) return Forbid();

        if (need.Status == NeedStatuses.Assigned && status == NeedStatuses.Resolved)
        {
            return AllocationResponse(await allocation.ReleaseAsync(id,
                CurrentUserId, IsAdmin, true, needVersion),
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

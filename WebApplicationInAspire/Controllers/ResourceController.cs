using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplicationInAspire.DataAccess.Repositories;
using WebApplicationInAspire.Models;
using WebApplicationInAspire.Models.Entities;
using WebApplicationInAspire.Security;

namespace WebApplicationInAspire.Controllers;

/// <summary>Ressurs-CRUD og oversikter med innlogging, rolletilgang og kontroll av eierskap.</summary>
[Authorize]
public class ResourceController(
    IResourceRepository repository,
    ILogger<ResourceController> logger,
    ApplicationDbContext database) : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private bool IsAdmin => User.IsInRole(RoleNames.Admin);
    private bool IsPublic => IsAdmin || User.IsInRole(RoleNames.PublicUser);

    /// <summary>En ressurs kan bare endres av eieren eller administrator.</summary>
    private bool CanManage(Resource resource) => resource.OwnerId == CurrentUserId || IsAdmin;

    /// <summary>Viser skjemaet for leverandør eller administrator.</summary>
    [HttpGet]
    [Authorize(Roles = RoleNames.ResourceProvider + "," + RoleNames.Admin)]
    public IActionResult Register()
    {
        return View(new ResourceViewModel());
    }

    /// <summary>Lagrer gyldige inndata i MariaDB med innlogget bruker som eier.</summary>
    [HttpPost]
    [Authorize(Roles = RoleNames.ResourceProvider + "," + RoleNames.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(ResourceViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = CurrentUserId;

        if (userId.Length == 0) return Challenge();
        var resource = new Resource { OwnerId = userId, CreationToken = model.RequestId, CreatedAtUtc = DateTime.UtcNow };
        model.ApplyTo(resource);
        await repository.AddAsync(resource);
        TempData["SuccessMessage"] = "Ressursen er registrert. Du finner den i oversikten.";
        logger.LogInformation("Ressurs {Name} opprettet av bruker {UserId}", resource.Name, userId);

        // Redirect etter vellykket POST som beskrevet i oppgaven
        return RedirectToAction("Overview", "Resource");
    }

    /// <summary>Leverandører ser egne ressurser; offentlige brukere ser ledige tilbud med kjent tidsrom.</summary>
    [HttpGet]
    public async Task<IActionResult> Overview(string? region, string? q = null, string? type = null, string? status = null, int page = 1,
        [FromQuery(Name = "AreaSouth")] double? areaSouth = null, [FromQuery(Name = "AreaWest")] double? areaWest = null,
        [FromQuery(Name = "AreaNorth")] double? areaNorth = null, [FromQuery(Name = "AreaEast")] double? areaEast = null)
    {
        var filter = new ListFilter
        {
            Region = region,
            Q = q,
            Type = type,
            Status = status,
            Page = page,
            AreaSouth = areaSouth,
            AreaWest = areaWest,
            AreaNorth = areaNorth,
            AreaEast = areaEast
        };
        foreach (var error in filter.ValidateArea())
            foreach (var member in error.MemberNames) ModelState.AddModelError(member, error.ErrorMessage!);
        // Et ugyldig filter må ikke bli til et ufiltrert søk.
        if (!ModelState.IsValid)
        {
            Response.StatusCode = 400;
            ViewData["FormErrorHelp"] = "Søket er ikke utført. Rett områdegrensene; de andre filtrene dine er beholdt.";
            filter.RejectResults = true;
            return View(await BuildOverviewAsync(filter));
        }
        return View(await BuildOverviewAsync(filter));
    }

    /// <summary>Viser inntil 500 rolletilpassede rader og varsler om eventuell avkorting.</summary>
    [HttpGet]
    public async Task<IActionResult> RegionalList(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            TempData["ErrorMessage"] = "Velg en region før du åpner utskriftslisten.";
            return RedirectToAction(nameof(Overview));
        }
        // Utskrift har egen grense for å unngå at et stort datasett velter nettleseren.
        var rows = await repository.GetForOverviewAsync(IsPublic ? null : CurrentUserId, IsPublic && !IsAdmin, region);
        ViewData["Truncated"] = rows.Count > 500;
        var rowIds = rows.Take(500).Select(r => r.Id).ToArray();
        var assignedIds = (await database.Needs.Where(n => n.AssignedResourceId != null && rowIds.Contains(n.AssignedResourceId.Value) && n.Status == NeedStatuses.Assigned).Select(n => n.AssignedResourceId!.Value).ToListAsync()).ToHashSet();
        return View(new ResourceOverviewViewModel
        {
            Resources = rows.Take(500).ToList(),
            Region = region,
            IsPublicOverview = IsPublic,
            AssignedResourceIds = assignedIds
        });
    }

    /// <summary>Strømmer hele regionlisten uten å laste datasettet i minnet. CSV-formler nøytraliseres.</summary>
    [HttpGet]
    public async Task<IActionResult> ExportCsv(string region)
    {
        if (string.IsNullOrWhiteSpace(region) || region.Length > 200) return BadRequest();
        var query = database.VisibleResources(
            ownerId: IsPublic ? null : CurrentUserId,
            offersOnly: IsPublic && !IsAdmin);
        var normalized = WebApplicationInAspire.Services.MatchingRules.Normalize(region);
        query = query.Where(r => r.Region.Trim().ToUpper() == normalized).OrderBy(r => r.Name).ThenBy(r => r.Id);
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = "attachment; filename=ressurser.csv";
        Response.Headers.CacheControl = "no-store";
        await using var writer = new StreamWriter(Response.Body, new System.Text.UTF8Encoding(true), leaveOpen: true);
        await writer.WriteLineAsync("Navn;Kategori;Region;Fra;Til;Kontakt;Telefon");
        await foreach (var resource in query.AsAsyncEnumerable().WithCancellation(HttpContext.RequestAborted))
            await writer.WriteLineAsync(string.Join(";", new[] { resource.Name, resource.Type, resource.Region, resource.AvailableFrom?.ToString("yyyy-MM-dd HH:mm") ?? "", resource.AvailableUntil?.ToString("yyyy-MM-dd HH:mm") ?? "", resource.ContactName, resource.PhoneNumber }.Select(CsvCell)));
        await writer.FlushAsync(HttpContext.RequestAborted);
        return new EmptyResult();
    }
    /// <summary>Siterer CSV-felt og hindrer at brukerinnhold tolkes som regnearkformler.</summary>
    private static string CsvCell(string value)
    {
        if (value.Length > 0 && ("=+-@".Contains(value.TrimStart().FirstOrDefault()) || char.IsWhiteSpace(value[0]))) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Sender samme eier-/tilbudsavgrensning til repositoryet for hele oversikten.</summary>
    private Task<ResourceOverviewViewModel> BuildOverviewAsync(ListFilter filter)
    {
        return repository.SearchAsync(IsPublic ? null : CurrentUserId, IsPublic && !IsAdmin, filter);
    }

    /// <summary>Leverandøren får bare behovskontakt for en ressurs vedkommende eier.</summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var resource = await repository.GetByIdAsync(id);
        if (resource is null) return NotFound();
        var owner = CanManage(resource);
        var publicUser = User.IsInRole(RoleNames.PublicUser);
        var assigned = await database.Needs.AnyAsync(n => n.AssignedResourceId == id && n.Status == NeedStatuses.Assigned);
        if (!owner && (!publicUser || !resource.IsAvailable || resource.AvailableFrom == null || resource.AvailableUntil <= resource.AvailableFrom || assigned)) return Forbid();
        var contacts = await database.Needs.AsNoTracking().Where(n => n.AssignedResourceId == id && n.Status == NeedStatuses.Assigned).OrderBy(n => n.Id).Take(2).ToListAsync();
        if (contacts.Count > 1) ViewData["AssignmentWarning"] = "Ressursen har motstridende eldre tildelinger. Kontakt administrator før endring. Ressursen forblir sperret.";
        var need = contacts.FirstOrDefault();
        return View(new AssignmentInfo(resource, owner ? need : null));
    }

    /// <summary>Viser eierens redigeringsskjema og sperrer felt ved aktiv tildeling.</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var resource = await repository.GetByIdAsync(id);
        if (resource is null) return NotFound();

        if (!CanManage(resource)) return Forbid();

        var model = new ResourceViewModel
        {
            Id = resource.Id,
            Version = resource.Version,
            Name = resource.Name,
            Type = resource.Type,
            Region = resource.Region,
            IsAvailable = resource.IsAvailable,
            AvailableFrom = resource.AvailableFrom,
            AvailableUntil = resource.AvailableUntil,
            Description = resource.Description,
            ContactName = resource.ContactName,
            PhoneNumber = resource.PhoneNumber,
            Latitude = resource.Latitude,
            Longitude = resource.Longitude
        };

        ViewData["IsAssigned"] = (await repository.GetAssignedIdsAsync()).Contains(id);
        return View(model);
    }

    /// <summary>Validerer eierskap og inndata før repositoryets transaksjon utfører endringen.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ResourceViewModel model)
    {
        if (id != model.Id) return BadRequest();

        var existingResource = await repository.GetByIdAsync(id);
        if (existingResource is null) return NotFound();

        if (!CanManage(existingResource)) return Forbid();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        existingResource.Version = model.Version;
        model.ApplyTo(existingResource);

        if (!await repository.UpdateAsync(existingResource))
        {
            ViewData["IsAssigned"] = (await repository.GetAssignedIdsAsync()).Contains(id);
            ModelState.AddModelError(string.Empty, "Ressursen er tildelt eller har blitt endret. Frigi en eventuell tildeling før redigering.");
            Response.StatusCode = StatusCodes.Status409Conflict;
            return View(model);
        }
        logger.LogInformation("Ressurs {Id} oppdatert", id);

        TempData["SuccessMessage"] = "Endringene er lagret.";
        return RedirectToAction(nameof(Overview));
    }

    /// <summary>Viser bekreftelse uten å slette data ved GET.</summary>
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var resource = await repository.GetByIdAsync(id);
        if (resource is null) return NotFound();

        if (!CanManage(resource)) return Forbid();

        ViewData["IsAssigned"] = (await repository.GetAssignedIdsAsync()).Contains(id);
        return View(resource);
    }

    /// <summary>Sletter bare ved gyldig POST fra eier/admin og når ressursen ikke er tildelt.</summary>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var resource = await repository.GetByIdAsync(id);
        if (resource is null) return NotFound();

        if (!CanManage(resource)) return Forbid();

        if (!await repository.DeleteAsync(id))
        {
            ViewData["IsAssigned"] = true;
            ViewData["ErrorMessage"] = "Ressursen er tildelt eller har blitt endret. Den ble ikke slettet.";
            Response.StatusCode = StatusCodes.Status409Conflict;
            return View("Delete", resource);
        }
        TempData["SuccessMessage"] = "Ressursen er slettet.";
        logger.LogInformation("Ressurs {Id} slettet", id);

        return RedirectToAction(nameof(Overview));
    }
}

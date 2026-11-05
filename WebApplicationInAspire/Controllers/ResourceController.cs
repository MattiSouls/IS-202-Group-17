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

[Authorize] // Krever at brukeren er innlogget for å gjøre ressursoperasjoner
public class ResourceController : Controller
{
    private readonly IResourceRepository _repository;
    private readonly ILogger<ResourceController> _logger;
    private readonly ApplicationDbContext _database;

    // Dependency Injection av repository og logger
    public ResourceController(IResourceRepository repository, ILogger<ResourceController> logger, ApplicationDbContext database)
    {
        _repository = repository;
        _logger = logger;
        _database = database;
    }

    // Hjelpemetode for å hente ID-en til innlogget bruker
    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    }

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

        var userId = GetCurrentUserId();

        // Knytt ressursen til innlogget bruker på serveren
        var resource = new Resource
        {
            Name = model.Name,
            Type = model.Type,
            Region = model.Region.Trim(),
            IsAvailable = model.IsAvailable,
            AvailableFrom = model.AvailableFrom,
            AvailableUntil = model.AvailableUntil,
            Description = model.Description,
            ContactName = model.ContactName,
            PhoneNumber = model.PhoneNumber,
            Latitude = model.Latitude ?? 0,
            Longitude = model.Longitude ?? 0,
            OwnerId = userId,
            CreationToken = model.RequestId,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (userId.Length == 0) return Challenge();
        await _repository.AddAsync(resource);
        TempData["SuccessMessage"] = "Ressursen er registrert. Du finner den i oversikten.";
        _logger.LogInformation("Ressurs {Name} opprettet av bruker {UserId}", resource.Name, userId);

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

    [HttpGet]
    public async Task<IActionResult> RegionalList(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            TempData["ErrorMessage"] = "Velg en region før du åpner utskriftslisten.";
            return RedirectToAction(nameof(Overview));
        }
        var isPublic = User.IsInRole(RoleNames.PublicUser) || User.IsInRole(RoleNames.Admin);
        // Utskrift har egen grense for å unngå at et stort datasett velter nettleseren.
        var rows = await _repository.GetForOverviewAsync(isPublic ? null : GetCurrentUserId(), isPublic && !User.IsInRole(RoleNames.Admin), region);
        ViewData["Truncated"] = rows.Count > 500;
        var rowIds = rows.Take(500).Select(r => r.Id).ToArray();
        var assignedIds = (await _database.Needs.Where(n => n.AssignedResourceId != null && rowIds.Contains(n.AssignedResourceId.Value) && n.Status == NeedStatuses.Assigned).Select(n => n.AssignedResourceId!.Value).ToListAsync()).ToHashSet();
        return View(new ResourceOverviewViewModel
        {
            Resources = rows.Take(500).ToList(),
            Region = region,
            IsPublicOverview = isPublic,
            AssignedResourceIds = assignedIds
        });
    }

    /// <summary>Strømmer hele regionlisten uten å laste datasettet i minnet. CSV-formler nøytraliseres.</summary>
    [HttpGet]
    public async Task<IActionResult> ExportCsv(string region)
    {
        if (string.IsNullOrWhiteSpace(region) || region.Length > 200) return BadRequest();
        var query = _database.Resources.AsNoTracking();
        var isAdmin = User.IsInRole(RoleNames.Admin);
        var isPublic = User.IsInRole(RoleNames.PublicUser);
        if (!isPublic && !isAdmin) { var userId = GetCurrentUserId(); query = query.Where(r => r.OwnerId == userId); }
        else if (!isAdmin) query = query.Where(r => r.IsAvailable && r.AvailableFrom != null && r.AvailableUntil > r.AvailableFrom && !_database.Needs.Any(n => n.AssignedResourceId == r.Id && n.Status == NeedStatuses.Assigned));
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
    private static string CsvCell(string value)
    {
        if (value.Length > 0 && ("=+-@".Contains(value.TrimStart().FirstOrDefault()) || char.IsWhiteSpace(value[0]))) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private Task<ResourceOverviewViewModel> BuildOverviewAsync(ListFilter filter)
    {
        var isPublic = User.IsInRole(RoleNames.PublicUser) || User.IsInRole(RoleNames.Admin);
        return _repository.SearchAsync(isPublic ? null : GetCurrentUserId(), isPublic && !User.IsInRole(RoleNames.Admin), filter);
    }

    /// <summary>Leverandøren får bare behovskontakt for en ressurs vedkommende eier.</summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var resource = await _repository.GetByIdAsync(id);
        if (resource is null) return NotFound();
        var owner = resource.OwnerId == GetCurrentUserId() || User.IsInRole(RoleNames.Admin);
        var publicUser = User.IsInRole(RoleNames.PublicUser);
        var assigned = await _database.Needs.AnyAsync(n => n.AssignedResourceId == id && n.Status == NeedStatuses.Assigned);
        if (!owner && (!publicUser || !resource.IsAvailable || resource.AvailableFrom == null || resource.AvailableUntil <= resource.AvailableFrom || assigned)) return Forbid();
        var contacts = await _database.Needs.AsNoTracking().Where(n => n.AssignedResourceId == id && n.Status == NeedStatuses.Assigned).OrderBy(n => n.Id).Take(2).ToListAsync();
        if (contacts.Count > 1) ViewData["AssignmentWarning"] = "Ressursen har motstridende eldre tildelinger. Kontakt administrator før endring. Ressursen forblir sperret.";
        var need = contacts.FirstOrDefault();
        return View(new AssignmentInfo(resource, owner ? need : null));
    }

    /// <summary>Viser eierens redigeringsskjema og sperrer felt ved aktiv tildeling.</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var resource = await _repository.GetByIdAsync(id);
        if (resource == null)
        {
            return NotFound();
        }

        // Tilgangskontroll: Brukeren kan bare endre sine egne ressurser
        if (resource.OwnerId != GetCurrentUserId() && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

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

        ViewData["IsAssigned"] = (await _repository.GetAssignedIdsAsync()).Contains(id);
        return View(model);
    }

    /// <summary>Validerer eierskap og inndata før repositoryets transaksjon utfører endringen.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ResourceViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var existingResource = await _repository.GetByIdAsync(id);
        if (existingResource == null)
        {
            return NotFound();
        }

        // Tilgangskontroll
        if (existingResource.OwnerId != GetCurrentUserId() && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Oppdater feltene
        existingResource.Version = model.Version;
        existingResource.Name = model.Name;
        existingResource.Type = model.Type;
        existingResource.Region = model.Region.Trim();
        existingResource.IsAvailable = model.IsAvailable;
        existingResource.AvailableFrom = model.AvailableFrom;
        existingResource.AvailableUntil = model.AvailableUntil;
        existingResource.Description = model.Description;
        existingResource.ContactName = model.ContactName;
        existingResource.PhoneNumber = model.PhoneNumber;
        if (model.Latitude.HasValue) existingResource.Latitude = model.Latitude.Value;
        if (model.Longitude.HasValue) existingResource.Longitude = model.Longitude.Value;

        if (!await _repository.UpdateAsync(existingResource))
        {
            ViewData["IsAssigned"] = (await _repository.GetAssignedIdsAsync()).Contains(id);
            ModelState.AddModelError(string.Empty, "Ressursen er tildelt eller har blitt endret. Frigi en eventuell tildeling før redigering.");
            Response.StatusCode = StatusCodes.Status409Conflict;
            return View(model);
        }
        _logger.LogInformation("Ressurs {Id} oppdatert", id);

        TempData["SuccessMessage"] = "Endringene er lagret.";
        return RedirectToAction(nameof(Overview));
    }

    /// <summary>Viser bekreftelse uten å slette data ved GET.</summary>
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var resource = await _repository.GetByIdAsync(id);
        if (resource == null)
        {
            return NotFound();
        }

        // Tilgangskontroll
        if (resource.OwnerId != GetCurrentUserId() && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        ViewData["IsAssigned"] = (await _repository.GetAssignedIdsAsync()).Contains(id);
        return View(resource);
    }

    /// <summary>Sletter bare ved gyldig POST fra eier/admin og når ressursen ikke er tildelt.</summary>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var resource = await _repository.GetByIdAsync(id);
        if (resource == null)
        {
            return NotFound();
        }

        // Tilgangskontroll
        if (resource.OwnerId != GetCurrentUserId() && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        if (!await _repository.DeleteAsync(id))
        {
            ViewData["IsAssigned"] = true;
            ViewData["ErrorMessage"] = "Ressursen er tildelt eller har blitt endret. Den ble ikke slettet.";
            Response.StatusCode = StatusCodes.Status409Conflict;
            return View("Delete", resource);
        }
        TempData["SuccessMessage"] = "Ressursen er slettet.";
        _logger.LogInformation("Ressurs {Id} slettet", id);

        return RedirectToAction(nameof(Overview));
    }
}

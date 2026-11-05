using System.Security.Claims;
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

    // Dependency Injection av repository og logger
    public ResourceController(IResourceRepository repository, ILogger<ResourceController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // Hjelpemetode for å hente ID-en til innlogget bruker
    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    }

    // 1. CREATE - Vis skjema
    [HttpGet]
    [Authorize(Roles = RoleNames.ResourceProvider + "," + RoleNames.Admin)]
    public IActionResult Register()
    {
        return View(new ResourceViewModel());
    }

    // 1. CREATE - Lagre til MariaDB
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
            Description = model.Description,
            ContactName = model.ContactName,
            PhoneNumber = model.PhoneNumber,
            Latitude = model.Latitude ?? 0,
            Longitude = model.Longitude ?? 0,
            OwnerId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _repository.AddAsync(resource);
        _logger.LogInformation("Ressurs {Name} opprettet av bruker {UserId}", resource.Name, userId);

        // Redirect etter vellykket POST som beskrevet i oppgaven
        return RedirectToAction("Overview", "Resource");
    }

    // 2. READ - Oversikt over egne ressurser
    [HttpGet]
    public async Task<IActionResult> Overview(string? region)
    {
        return View(await BuildOverviewAsync(region));
    }

    /// <summary>Utskrift bruker samme rolle- og regionfilter som oversikten.</summary>
    [HttpGet]
    public async Task<IActionResult> RegionalList(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            TempData["ErrorMessage"] = "Velg en region før du åpner utskriftslisten.";
            return RedirectToAction(nameof(Overview));
        }
        return View(await BuildOverviewAsync(region));
    }

    private async Task<ResourceOverviewViewModel> BuildOverviewAsync(string? region)
    {
        var isPublic = User.IsInRole(RoleNames.PublicUser) || User.IsInRole(RoleNames.Admin);
        var ownerId = isPublic ? null : GetCurrentUserId();
        var all = await _repository.GetForOverviewAsync(ownerId, isPublic, null);
        return new ResourceOverviewViewModel
        {
            Resources = string.IsNullOrWhiteSpace(region) ? all : await _repository.GetForOverviewAsync(ownerId, isPublic, region),
            Regions = all.Select(resource => resource.Region.Trim()).Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList(),
            Region = region?.Trim(),
            IsPublicOverview = isPublic,
            AssignedResourceIds = await _repository.GetAssignedIdsAsync()
        };
    }

    // 3. UPDATE - Vis redigeringsskjema
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
            Name = resource.Name,
            Type = resource.Type,
            Region = resource.Region,
            IsAvailable = resource.IsAvailable,
            Description = resource.Description,
            ContactName = resource.ContactName,
            PhoneNumber = resource.PhoneNumber,
            Latitude = resource.Latitude,
            Longitude = resource.Longitude
        };

        ViewData["IsAssigned"] = (await _repository.GetAssignedIdsAsync()).Contains(id);
        return View(model);
    }

    // 3. UPDATE - Lagre endringer i MariaDB
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
        existingResource.Name = model.Name;
        existingResource.Type = model.Type;
        existingResource.Region = model.Region.Trim();
        existingResource.IsAvailable = model.IsAvailable;
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

        return RedirectToAction(nameof(Overview));
    }

    // 4. DELETE - Vis bekreftelsesside (GET skal IKKE slette direkte!)
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

    // 4. DELETE - Faktisk sletting ved POST
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
        _logger.LogInformation("Ressurs {Id} slettet", id);

        return RedirectToAction(nameof(Overview));
    }
}

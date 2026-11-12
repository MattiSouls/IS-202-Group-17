using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.DataAccess;
using WebApplicationInAspire.Models;
using WebApplicationInAspire.Models.Entities;
using WebApplicationInAspire.Security;
using WebApplicationInAspire.Services;

namespace WebApplicationInAspire.Controllers;

/// <summary>Kartet som arbeidsflate: ressurser og udekkede behov med sidepanel.</summary>
[Authorize]
[Route("Kart")]
public class MapController(ApplicationDbContext database) : Controller
{
    private const int Limit = 500;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var isAdmin = User.IsInRole(RoleNames.Admin);
        var isPublic = User.IsInRole(RoleNames.PublicUser) || isAdmin;

        var resources = await database.VisibleResources(ownerId: isPublic ? null : id, offersOnly: isPublic && !isAdmin)
            .OrderBy(r => r.Name).Take(Limit).ToListAsync();
        var resourceIds = resources.Select(r => r.Id).ToList();
        var assigned = (await database.Needs.AsNoTracking()
            .Where(n => n.Status == NeedStatuses.Assigned && n.AssignedResourceId != null && resourceIds.Contains(n.AssignedResourceId.Value))
            .Select(n => n.AssignedResourceId!.Value).ToListAsync()).ToHashSet();

        var needs = isPublic
            ? await database.Needs.AsNoTracking().Uncovered().InPriorityOrder().Take(Limit).ToListAsync()
            : [];

        return View(new MapViewModel
        {
            CanSeeNeeds = isPublic,
            CanRegisterNeed = isPublic,
            CanRegisterResource = User.IsInRole(RoleNames.ResourceProvider) || isAdmin,
            Resources = resources.Select(r => new MapResource(r.Id, r.Name, r.Type, r.Region, r.Latitude, r.Longitude,
                PortalDisplay.Availability(r, assigned.Contains(r.Id)))).ToList(),
            Needs = needs.Select(n =>
            {
                var area = MatchingRules.Area(n);
                var origin = MatchingRules.Origin(n);
                return new MapNeed(n.Id, n.Type, n.Region, PortalDisplay.Priority(n.Priority), PortalDisplay.PriorityClass(n.Priority),
                    PortalDisplay.Status(n.Status), n.NeededAt.ToString("dd.MM.yyyy HH:mm"),
                    origin?.Latitude, origin?.Longitude,
                    area is null ? null : [area.South, area.West, area.North, area.East]);
            }).ToList()
        });
    }
}

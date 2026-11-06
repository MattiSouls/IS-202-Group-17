using System.Diagnostics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.DataAccess;
using WebApplicationInAspire.Models.Entities;
using WebApplicationInAspire.Security;
using Microsoft.AspNetCore.Mvc;
using WebApplicationInAspire.Models;
using WebApplicationInAspire.Services;

namespace WebApplicationInAspire.Controllers;

/// <summary>Innlogget oversikt med rolletilpassede tall; offentlig forside uten innlogging.</summary>
public class HomeController(ApplicationDbContext database) : Controller
{
    /// <summary>Viser et avgrenset øyeblikksbilde av behov, ressurser og brukerens private varsler.</summary>
    public async Task<IActionResult> Index(string? scope = null)
    {
        if (User.Identity?.IsAuthenticated != true) return View();
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var isPublic = User.IsInRole(RoleNames.PublicUser) || User.IsInRole(RoleNames.Admin);
        var isAdmin = User.IsInRole(RoleNames.Admin);
        var showAll = isAdmin ? scope != "mine" : scope == "all";
        var now = PortalDisplay.NorwayNow;
        var resources = database.VisibleResources(ownerId: isPublic ? null : id, offersOnly: isPublic && !isAdmin);
        var needs = database.Needs.AsNoTracking();
        if (isPublic && !showAll) needs = needs.Where(n => n.CreatedByUserId == id);
        var assigned = isPublic ? await needs.CountAsync(n => n.Status == NeedStatuses.Assigned)
            : await needs.CountAsync(n => n.Status == NeedStatuses.Assigned && n.AssignedResource != null && n.AssignedResource.OwnerId == id);
        var uncovered = needs.Uncovered();
        var summary = isPublic ? await uncovered.GroupBy(n => 1).Select(group => new
        { Total = group.Count(), Urgent = group.Count(n => n.Priority == "Urgent"), TimePassed = group.Count(n => n.NeededAt < now) }).SingleOrDefaultAsync() : null;
        var upcoming = isPublic ? await uncovered.InPriorityOrder().Take(6).ToListAsync() : [];
        var notices = database.Notifications.AsNoTracking().Where(n => n.UserId == id);
        return View(new DashboardViewModel
        {
            IsPublic = isPublic, IsAdmin = isAdmin, ShowAllNeeds = showAll,
            Resources = await resources.CountAsync(),
            Uncovered = summary?.Total ?? 0, Urgent = summary?.Urgent ?? 0, TimePassed = summary?.TimePassed ?? 0,
            Assigned = assigned, Unread = await notices.CountAsync(n => n.ReadAtUtc == null), SnapshotAt = now, Upcoming = upcoming,
            UnreadNotices = await notices.Where(n => n.ReadAtUtc == null).OrderByDescending(n => n.Id).Take(3).ToListAsync(),
            Recent = await notices.Where(n => n.ReadAtUtc != null).OrderByDescending(n => n.Id).Take(3).ToListAsync()
        });
    }

    /// <summary>Forklarer lagring og bruk av konto-, kontakt- og kartopplysninger.</summary>
    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>Viser feilreferanse uten interne feildetaljer.</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

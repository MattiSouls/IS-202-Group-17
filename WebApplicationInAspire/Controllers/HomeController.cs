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

public class HomeController(ApplicationDbContext database) : Controller
{
    public async Task<IActionResult> Index(string? scope = null)
    {
        if (User.Identity?.IsAuthenticated != true) return View();
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var isPublic = User.IsInRole(RoleNames.PublicUser) || User.IsInRole(RoleNames.Admin);
        var isAdmin = User.IsInRole(RoleNames.Admin);
        var showAll = isAdmin ? scope != "mine" : scope == "all";
        var now = PortalDisplay.NorwayNow;
        var resources = database.Resources.AsNoTracking();
        if (!isPublic) resources = resources.Where(r => r.OwnerId == id);
        else if (!User.IsInRole(RoleNames.Admin)) resources = resources.Where(r => r.IsAvailable && r.AvailableFrom != null && r.AvailableUntil > r.AvailableFrom && !database.Needs.Any(n => n.AssignedResourceId == r.Id && n.Status == NeedStatuses.Assigned));
        var needs = database.Needs.AsNoTracking();
        if (isPublic && !showAll) needs = needs.Where(n => n.CreatedByUserId == id);
        var assigned = isPublic ? await needs.CountAsync(n => n.Status == NeedStatuses.Assigned)
            : await needs.CountAsync(n => n.Status == NeedStatuses.Assigned && n.AssignedResource != null && n.AssignedResource.OwnerId == id);
        var uncovered = needs.Where(n => n.AssignedResourceId == null && (n.Status == NeedStatuses.New || n.Status == NeedStatuses.UnderReview));
        var summary = isPublic ? await uncovered.GroupBy(n => 1).Select(group => new
        { Total = group.Count(), Urgent = group.Count(n => n.Priority == "Urgent"), TimePassed = group.Count(n => n.NeededAt < now) }).SingleOrDefaultAsync() : null;
        var upcoming = isPublic ? await uncovered.OrderByDescending(n => n.Priority == "Urgent")
            .ThenByDescending(n => n.Priority == "High").ThenByDescending(n => n.Priority == "Medium")
            .ThenBy(n => n.NeededAt).ThenBy(n => n.Id).Take(6).ToListAsync() : [];
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

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

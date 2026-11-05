using System.Diagnostics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.DataAccess;
using WebApplicationInAspire.Models.Entities;
using WebApplicationInAspire.Security;
using Microsoft.AspNetCore.Mvc;
using WebApplicationInAspire.Models;

namespace WebApplicationInAspire.Controllers;

public class HomeController(ApplicationDbContext database) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated != true) return View();
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var isPublic = User.IsInRole(RoleNames.PublicUser) || User.IsInRole(RoleNames.Admin);
        var resources = database.Resources.AsNoTracking();
        if (!isPublic) resources = resources.Where(r => r.OwnerId == id);
        else if (!User.IsInRole(RoleNames.Admin)) resources = resources.Where(r => r.IsAvailable && r.AvailableFrom != null && r.AvailableUntil > r.AvailableFrom && !database.Needs.Any(n => n.AssignedResourceId == r.Id && n.Status == NeedStatuses.Assigned));
        var needs = database.Needs.AsNoTracking();
        var assigned = isPublic ? await needs.CountAsync(n => n.Status == NeedStatuses.Assigned && (User.IsInRole(RoleNames.Admin) || n.CreatedByUserId == id))
            : await needs.CountAsync(n => n.Status == NeedStatuses.Assigned && n.AssignedResource != null && n.AssignedResource.OwnerId == id);
        var upcoming = isPublic ? await needs.Where(n => n.Status != NeedStatuses.Resolved).OrderByDescending(n => n.Priority == "Urgent")
            .ThenByDescending(n => n.Priority == "High").ThenByDescending(n => n.Priority == "Medium")
            .ThenBy(n => n.NeededAt).ThenBy(n => n.Id).Take(4).ToListAsync() : [];
        var notices = database.Notifications.AsNoTracking().Where(n => n.UserId == id);
        return View(new DashboardViewModel(isPublic, await resources.CountAsync(), isPublic ? await needs.CountAsync(n => n.Status != NeedStatuses.Resolved) : 0,
            assigned, await notices.CountAsync(n => n.ReadAtUtc == null), upcoming, await notices.OrderByDescending(n => n.Id).Take(3).ToListAsync()));
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

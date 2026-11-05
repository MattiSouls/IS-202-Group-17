using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.DataAccess;
using WebApplicationInAspire.Models;

namespace WebApplicationInAspire.Controllers;

/// <summary>Leser og markerer bare den innloggede brukerens egne varsler.</summary>
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificationsController(ApplicationDbContext database) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    /// <summary>Filtrerer og paginerer private varsler; ulest-tallet gjelder alle egne varsler.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, string? filter = null)
    {
        var query = database.Notifications.AsNoTracking().Where(n => n.UserId == UserId);
        var unread = await query.CountAsync(n => n.ReadAtUtc == null);
        var selectedFilter = FilterName(filter);
        if (selectedFilter == "unread") query = query.Where(n => n.ReadAtUtc == null);
        if (selectedFilter == "read") query = query.Where(n => n.ReadAtUtc != null);
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (total + 19) / 20));
        return View(new NotificationList(await query.OrderByDescending(n => n.Id).Skip((page - 1) * 20).Take(20).ToListAsync(),
            unread, page, total, selectedFilter));
    }
    /// <summary>Gir antall egne uleste varsler til toppmenyen.</summary>
    [HttpGet]
    public async Task<IActionResult> Unread() => Json(new { count = await database.Notifications.CountAsync(n => n.UserId == UserId && n.ReadAtUtc == null) });
    /// <summary>Markerer ett eget varsel som lest; andres ID-er avvises.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(long id, string? filter = null, int page = 1)
    {
        if (!await database.Notifications.AnyAsync(n => n.Id == id && n.UserId == UserId)) return NotFound();
        await database.Notifications.Where(n => n.Id == id && n.UserId == UserId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(n => n.ReadAtUtc, DateTime.UtcNow));
        TempData["SuccessMessage"] = "Varselet er markert som lest.";
        return RedirectToAction(nameof(Index), new { filter = FilterName(filter), page = Math.Max(1, page) });
    }
    /// <summary>Markerer kun egne varsler som lest og beholder valgt filter.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAll(string? filter = null)
    {
        await database.Notifications.Where(n => n.UserId == UserId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(n => n.ReadAtUtc, DateTime.UtcNow));
        TempData["SuccessMessage"] = "Alle varslene dine er markert som lest.";
        return RedirectToAction(nameof(Index), new { filter = FilterName(filter) });
    }
    /// <summary>Tillater bare kjente lesefiltre; ukjente verdier viser alle egne varsler.</summary>
    private static string FilterName(string? filter) => filter is "unread" or "read" ? filter : "all";
}

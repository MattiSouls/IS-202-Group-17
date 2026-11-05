using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.DataAccess;
using WebApplicationInAspire.Models;

namespace WebApplicationInAspire.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificationsController(ApplicationDbContext database) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var query = database.Notifications.AsNoTracking().Where(n => n.UserId == UserId);
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (total + 19) / 20));
        return View(new NotificationList(await query.OrderByDescending(n => n.Id).Skip((page - 1) * 20).Take(20).ToListAsync(),
            await query.CountAsync(n => n.ReadAtUtc == null), page, total));
    }
    [HttpGet]
    public async Task<IActionResult> Unread() => Json(new { count = await database.Notifications.CountAsync(n => n.UserId == UserId && n.ReadAtUtc == null) });
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(long id)
    {
        if (!await database.Notifications.AnyAsync(n => n.Id == id && n.UserId == UserId)) return NotFound();
        await database.Notifications.Where(n => n.Id == id && n.UserId == UserId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(n => n.ReadAtUtc, DateTime.UtcNow));
        TempData["SuccessMessage"] = "Varselet er markert som lest.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAll()
    {
        await database.Notifications.Where(n => n.UserId == UserId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(n => n.ReadAtUtc, DateTime.UtcNow));
        TempData["SuccessMessage"] = "Alle varslene dine er markert som lest.";
        return RedirectToAction(nameof(Index));
    }
}

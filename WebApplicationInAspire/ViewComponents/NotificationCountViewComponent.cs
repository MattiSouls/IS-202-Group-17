using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.DataAccess;

namespace WebApplicationInAspire.ViewComponents;

public class NotificationCountViewComponent(ApplicationDbContext database) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var id = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var count = id is null ? 0 : await database.Notifications.CountAsync(n => n.UserId == id && n.ReadAtUtc == null);
        return View(count);
    }
}

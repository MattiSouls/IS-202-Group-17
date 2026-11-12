using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace WebApplicationInAspire.Models.Entities;

/// <summary>Varig beskjed til én bruker. Opprettes sammen med tildelingens transaksjon.</summary>
public class PortalNotification
{
    public long Id { get; set; }
    [MaxLength(255)] public string UserId { get; set; } = "";
    public IdentityUser User { get; set; } = null!;
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(1200)] public string Message { get; set; } = "";
    public int? ResourceId { get; set; }
    public Resource? Resource { get; set; }
    public int? NeedId { get; set; }
    public Need? Need { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}

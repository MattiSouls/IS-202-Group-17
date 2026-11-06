using System.ComponentModel.DataAnnotations;

namespace WebApplicationInAspire.Models.Entities;

/// <summary>Uforanderlig historikk for fullførte tildelingshandlinger, også etter frigivelse.</summary>
public class AllocationEvent
{
    public long Id { get; set; }
    public int? NeedId { get; set; }
    public Need? Need { get; set; }
    public int? ResourceId { get; set; }
    public Resource? Resource { get; set; }
    [MaxLength(255)] public string ActorUserId { get; set; } = "";
    [MaxLength(30)] public string Action { get; set; } = "";
    [MaxLength(200)] public string ResourceName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

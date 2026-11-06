using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace WebApplicationInAspire.Models.Entities;

/// <summary>
/// Representerer et behov som er registrert i systemet.
/// </summary>
public class Need
{
    public int Id { get; set; }

    public long Version { get; set; }
    public Guid? CreationToken { get; set; }

    [Required, MaxLength(100)]
    public string Type { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Region { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Location { get; set; }

    public DateTime NeededAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AreaSouth { get; set; }
    public double? AreaWest { get; set; }
    public double? AreaNorth { get; set; }
    public double? AreaEast { get; set; }

    [Required, MaxLength(50)]
    public string Priority { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ContactName { get; set; } = string.Empty;

    [Required, Phone, MaxLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Status { get; set; } = NeedStatuses.New;

    public int? AssignedResourceId { get; set; }

    public Resource? AssignedResource { get; set; }

    /// <summary>Offentlig bruker som opprettet behovet. Settes på serveren.</summary>
    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    public IdentityUser CreatedByUser { get; set; } = null!;
}

/// <summary>
/// Mulige statuser for et registrert behov.
/// </summary>
public static class NeedStatuses
{
    public const string New = "New";
    public const string UnderReview = "UnderReview";
    public const string Assigned = "Assigned";
    public const string Resolved = "Resolved";
}

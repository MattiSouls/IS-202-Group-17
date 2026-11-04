using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace WebApplicationInAspire.Models.Entities;

/// <summary>
/// En lagret ressurs. ResourceViewModel brukes fortsatt til inndata fra skjemaet.
/// </summary>
public class Resource
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
  public string Type { get; set; } = string.Empty;

[Required, MaxLength(200)]
public string Region { get; set; } = string.Empty;
public bool IsAvailable { get; set; } = true;


    [MaxLength(4000)]
    public string? Description { get; set; }

    [Required, MaxLength(200)]
    public string ContactName { get; set; } = string.Empty;

    [Required, Phone, MaxLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    /// <summary>Identity-brukeren som eier ressursen. Settes på serveren.</summary>
    [Required]
    public string OwnerId { get; set; } = string.Empty;

    public IdentityUser Owner { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

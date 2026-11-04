using System.ComponentModel.DataAnnotations;

namespace WebApplicationInAspire.Models;

public class ResourceViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Navn er påkrevd")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Type er påkrevd")]
    public string Type { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Kontaktperson er påkrevd")]
    public string ContactName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefonnummer er påkrevd")]
    [Phone(ErrorMessage = "Ugyldig telefonnummer")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lokasjon må markeres på kartet")]
    public double? Latitude { get; set; }

    [Required(ErrorMessage = "Lokasjon må markeres på kartet")]
    public double? Longitude { get; set; }
}

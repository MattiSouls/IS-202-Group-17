using System.ComponentModel.DataAnnotations;

namespace WebApplicationInAspire.Models;

/// <summary>Inndata for en ressurs; tidspunktene oppgis som norsk lokal tid.</summary>
public class ResourceViewModel : IValidatableObject
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Navn er påkrevd")]
    [StringLength(200, ErrorMessage = "Navnet kan ha maks 200 tegn")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Type er påkrevd")]
    [StringLength(100, ErrorMessage = "Typen kan ha maks 100 tegn")]
    public string Type { get; set; } = string.Empty;

    [Required(ErrorMessage = "Region er påkrevd")]
    [StringLength(200, ErrorMessage = "Regionen kan ha maks 200 tegn")]
    public string Region { get; set; } = string.Empty;

    [Display(Name = "Tilgjengelig for tildeling")]
    public bool IsAvailable { get; set; } = true;

    [Required(ErrorMessage = "Oppgi når ressursen er tilgjengelig fra")]
    [Display(Name = "Tilgjengelig fra (norsk tid)")]
    public DateTime? AvailableFrom { get; set; }

    [Required(ErrorMessage = "Oppgi når tilgjengeligheten slutter")]
    [Display(Name = "Tilgjengelig til (norsk tid)")]
    public DateTime? AvailableUntil { get; set; }

    [StringLength(4000, ErrorMessage = "Beskrivelsen kan ha maks 4000 tegn")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Kontaktperson er påkrevd")]
    [StringLength(200, ErrorMessage = "Kontaktpersonen kan ha maks 200 tegn")]
    public string ContactName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefonnummer er påkrevd")]
    [Phone(ErrorMessage = "Ugyldig telefonnummer")]
    [StringLength(50, ErrorMessage = "Telefonnummeret kan ha maks 50 tegn")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lokasjon må markeres på kartet")]
    [Range(-90d, 90d, ErrorMessage = "Breddegrad må være mellom -90 og 90")]
    public double? Latitude { get; set; }

    [Required(ErrorMessage = "Lokasjon må markeres på kartet")]
    [Range(-180d, 180d, ErrorMessage = "Lengdegrad må være mellom -180 og 180")]
    public double? Longitude { get; set; }

    /// <summary>Kontrollerer databasegrensene og at sluttidspunktet følger starttidspunktet.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AvailableFrom is { Year: < 1000 })
            yield return new ValidationResult("Velg et gyldig år fra 1000", [nameof(AvailableFrom)]);
        if (AvailableUntil is { Year: < 1000 })
            yield return new ValidationResult("Velg et gyldig år fra 1000", [nameof(AvailableUntil)]);
        if (AvailableFrom.HasValue && AvailableUntil.HasValue && AvailableUntil <= AvailableFrom)
            yield return new ValidationResult("Sluttidspunktet må være etter starttidspunktet", [nameof(AvailableUntil)]);
    }
}

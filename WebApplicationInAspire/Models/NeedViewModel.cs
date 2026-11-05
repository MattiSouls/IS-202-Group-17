using System.ComponentModel.DataAnnotations;

namespace WebApplicationInAspire.Models;

public class NeedViewModel : IValidatableObject
{
    public int? Id { get; set; }
    public long Version { get; set; }
    public Guid RequestId { get; set; } = Guid.NewGuid();

    [Required(ErrorMessage = "Type er påkrevd")]
    [StringLength(100)]
    public string Type { get; set; } = string.Empty;

    [Required(ErrorMessage = "Region er påkrevd")]
    [StringLength(200)]
    public string Region { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Location { get; set; }

    [Required(ErrorMessage = "Tidspunkt er påkrevd")]
    public DateTime? NeededAt { get; set; }
    [Range(-90d, 90d, ErrorMessage = "Breddegrad må være mellom -90 og 90")]
    public double? Latitude { get; set; }
    [Range(-180d, 180d, ErrorMessage = "Lengdegrad må være mellom -180 og 180")]
    public double? Longitude { get; set; }

    [Required(ErrorMessage = "Prioritet er påkrevd")]
    [StringLength(50)]
    [RegularExpression("^(Low|Medium|High|Urgent)$", ErrorMessage = "Velg en gyldig prioritet")]
    public string Priority { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kontaktperson er påkrevd")]
    [StringLength(200)]
    public string ContactName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefonnummer er påkrevd")]
    [Phone(ErrorMessage = "Ugyldig telefonnummer")]
    [StringLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>MariaDB DATETIME støtter år fra 1000. Valider dette på serveren.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RequestId == Guid.Empty)
            yield return new ValidationResult("Skjemaet mangler registreringsnøkkel. Åpne et nytt skjema.", [nameof(RequestId)]);
        if (Latitude.HasValue != Longitude.HasValue)
            yield return new ValidationResult("Oppgi både breddegrad og lengdegrad, eller la begge stå tomme", [nameof(Latitude), nameof(Longitude)]);
        if (NeededAt is { Year: < 1000 })
        {
            yield return new ValidationResult("Velg et gyldig tidspunkt fra år 1000", [nameof(NeededAt)]);
        }
    }
}

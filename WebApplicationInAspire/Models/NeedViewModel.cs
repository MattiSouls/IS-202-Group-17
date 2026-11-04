using System.ComponentModel.DataAnnotations;

namespace WebApplicationInAspire.Models;

public class NeedViewModel : IValidatableObject
{
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
        if (NeededAt is { Year: < 1000 })
        {
            yield return new ValidationResult("Velg et gyldig tidspunkt fra år 1000", [nameof(NeededAt)]);
        }
    }
}

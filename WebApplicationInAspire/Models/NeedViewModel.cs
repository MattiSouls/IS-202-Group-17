using System.ComponentModel.DataAnnotations;

namespace WebApplicationInAspire.Models;

public class NeedViewModel
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
    public string Priority { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kontaktperson er påkrevd")]
    [StringLength(200)]
    public string ContactName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefonnummer er påkrevd")]
[Phone(ErrorMessage = "Ugyldig telefonnummer")]
[StringLength(50)]
public string PhoneNumber { get; set; } = string.Empty;
}
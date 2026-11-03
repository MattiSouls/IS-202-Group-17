using System.ComponentModel.DataAnnotations;

namespace WebApplicationInAspire.Models.Account;

public class RegisterViewModel
{
    [Required(ErrorMessage = "E-post må fylles ut.")]
    [EmailAddress(ErrorMessage = "Ugyldig e-postadresse.")]
    [Display(Name = "E-post")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Passord må fylles ut.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Passordet må være minst 8 tegn.")]
    [DataType(DataType.Password)]
    [Display(Name = "Passord")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bekreft passordet.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passordene er ikke like.")]
    [Display(Name = "Bekreft passord")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
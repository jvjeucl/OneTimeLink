using System.ComponentModel.DataAnnotations;

namespace OneTimeLink.Samples.Web.Models;

public class SendVerificationViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;
}
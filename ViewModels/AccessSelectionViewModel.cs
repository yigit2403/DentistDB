using System.ComponentModel.DataAnnotations;
using DentistDB.Models;

namespace DentistDB.ViewModels;

public sealed class AccessSelectionViewModel
{
    public IReadOnlyList<AppAccount> Accounts { get; set; } = Array.Empty<AppAccount>();

    [Required]
    public string AccountKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "PIN alanı zorunludur.")]
    [StringLength(12, MinimumLength = 4, ErrorMessage = "PIN en az 4 haneli olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "PIN")]
    public string Pin { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

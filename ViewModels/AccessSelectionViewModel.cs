using System.ComponentModel.DataAnnotations;
using DentistDB.Models;

namespace DentistDB.ViewModels;

public sealed class AccessSelectionViewModel
{
    public IReadOnlyList<AppAccount> Accounts { get; set; } = Array.Empty<AppAccount>();

    [Required(ErrorMessage = "Hesap seçimi zorunludur.")]
    public string AccountKey { get; set; } = AppAccounts.Admin.Key;

    [Required(ErrorMessage = "PIN girin.")]
    [StringLength(20, MinimumLength = 4, ErrorMessage = "PIN en az 4 karakter olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "PIN")]
    public string Pin { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public string ClinicName { get; set; } = "DentistDB";

    public int? LockoutSeconds { get; set; }
}

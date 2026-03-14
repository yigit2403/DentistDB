using DentistDB.Models;

namespace DentistDB.ViewModels;

public class PreviousOperationsIndexViewModel
{
    public Patient? Patient { get; set; }
    public int? PatientId { get; set; }
    public string? Search { get; set; }
    public IList<PreviousOperation> Operations { get; set; } = new List<PreviousOperation>();
}

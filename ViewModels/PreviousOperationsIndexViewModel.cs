using DentistDB.Models;

namespace DentistDB.ViewModels;

public class PreviousOperationsIndexViewModel
{
    public Patient? Patient { get; set; }
    public int? PatientId { get; set; }
    public string? Search { get; set; }
    public PaginatedList<PreviousOperation> Operations { get; set; } = new(Enumerable.Empty<PreviousOperation>(), 0, 1, 20);
}

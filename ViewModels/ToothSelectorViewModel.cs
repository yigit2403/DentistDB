namespace DentistDB.ViewModels;

public class ToothSelectorViewModel
{
    public string Label { get; set; } = "Diş Seçimi";
    public string InputId { get; set; } = "SelectedTeeth";
    public string InputName { get; set; } = "SelectedTeeth";
    public string SummaryId { get; set; } = "SelectedTeethSummary";
    public string SelectedTeethCsv { get; set; } = string.Empty;
}

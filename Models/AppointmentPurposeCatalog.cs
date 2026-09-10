namespace DentistDB.Models;

public static class AppointmentPurposeCatalog
{
    public static readonly IReadOnlyList<string> Default = new[]
    {
        "Muayene",
        "Kontrol",
        "Diş Taşı Temizliği",
        "Dolgu",
        "Kanal Tedavisi",
        "Diş Çekimi",
        "İmplant",
        "Protez",
        "Ortodonti Kontrolü",
        "Beyazlatma",
        "Röntgen",
        "Acil"
    };

    public static readonly int[] DurationOptions = { 15, 20, 30, 45, 60, 90, 120 };

    /// <summary>Quick follow-up offsets shown after a visit: label → days.</summary>
    public static readonly IReadOnlyList<(string Label, int Days)> FollowUpOptions = new[]
    {
        ("1 hafta", 7),
        ("2 hafta", 14),
        ("1 ay", 30),
        ("3 ay", 90),
        ("6 ay", 180)
    };

    public static readonly IReadOnlyList<(string Label, int Days)> RepeatIntervals = new[]
    {
        ("Her hafta", 7),
        ("2 haftada bir", 14),
        ("3 haftada bir", 21),
        ("4 haftada bir", 28),
        ("Her ay", 30)
    };
}

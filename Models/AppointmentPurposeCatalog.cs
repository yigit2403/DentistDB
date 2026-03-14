namespace DentistDB.Models;

public static class AppointmentPurposeCatalog
{
    public static readonly IReadOnlyList<string> Default = new[]
    {
        "Genel muayene",
        "Diş temizliği",
        "Dolgu",
        "Kanal tedavisi",
        "Diş çekimi",
        "Kontrol randevusu",
        "Ortodonti kontrolü",
        "İmplant değerlendirmesi",
        "Röntgen ve görüntüleme",
        "Protez uyumu"
    };
}

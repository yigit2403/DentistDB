using DentistDB.Models;

namespace DentistDB.Services;

/// <summary>Default Turkish consent texts. Editable from Settings; placeholders are replaced at print time.</summary>
public static class ConsentTemplates
{
    public const string TreatmentDefault = """
        Ben, aşağıda kimlik bilgileri yazılı hasta {HastaAdi} (T.C. Kimlik No: {TCKN}), {KlinikAdi} kliniğinde {HekimAdi} tarafından muayene edildim.

        Bana önerilen tedavi(ler), bu tedavilerin amacı, alternatifleri, beklenen yararları ve olası riskleri (ağrı, şişlik, kanama, enfeksiyon, geçici veya kalıcı his kaybı, kullanılan ilaçlara karşı alerjik reaksiyon, tedavinin beklenen sonucu vermemesi ve ek tedavi gereksinimi dahil) anlayabileceğim şekilde açıklandı. Sorularımı sorma fırsatı buldum ve tatmin edici yanıtlar aldım.

        Tedavi sırasında öngörülemeyen bir durum ortaya çıkarsa hekimin gerekli gördüğü ek girişimleri yapmasına izin veriyorum. Genel sağlık durumum, kullandığım ilaçlar ve alerjilerim hakkında verdiğim bilgilerin doğru ve eksiksiz olduğunu beyan ederim.

        Planlanan tedaviyi ve tedavi ücretini kabul ettiğimi, bu onamı hiçbir baskı altında kalmadan, özgür irademle verdiğimi beyan ederim.
        """;

    public const string KvkkDefault = """
        6698 sayılı Kişisel Verilerin Korunması Kanunu ("KVKK") uyarınca, veri sorumlusu {KlinikAdi} ({Adres}) tarafından kimlik, iletişim ve sağlık verileriniz; teşhis ve tedavi hizmetlerinin yürütülmesi, randevu ve hasta kayıtlarının tutulması, faturalandırma ve mevzuattan doğan yükümlülüklerin yerine getirilmesi amaçlarıyla işlenmektedir.

        Sağlık verileriniz KVKK'nın 6. maddesi kapsamında özel nitelikli kişisel veri olup, yalnızca hekim ve yetkili klinik personeli tarafından erişilebilecek şekilde saklanır; yasal zorunluluk dışında üçüncü kişilerle paylaşılmaz. Verileriniz mevzuatın öngördüğü süre boyunca saklanır ve süre sonunda silinir veya anonim hale getirilir.

        KVKK'nın 11. maddesi kapsamında verilerinize erişme, düzeltilmesini veya silinmesini isteme, işlenmesine itiraz etme haklarınızı klinik iletişim kanalları ({Telefon}) üzerinden kullanabilirsiniz.

        Yukarıdaki aydınlatma metnini okuduğumu, anladığımı ve sağlık verilerimin belirtilen amaçlarla işlenmesine açık rıza gösterdiğimi beyan ederim.
        """;

    public static string Render(string template, Patient patient, ClinicSettings clinic, ClinicIdentity identity, DateOnly date)
    {
        return template
            .Replace("{HastaAdi}", patient.FullName)
            .Replace("{TCKN}", patient.Tckn)
            .Replace("{DogumTarihi}", patient.BirthDate?.ToString("dd.MM.yyyy") ?? "—")
            .Replace("{Tarih}", date.ToString("dd.MM.yyyy"))
            .Replace("{KlinikAdi}", clinic.ClinicName)
            .Replace("{HekimAdi}", string.IsNullOrWhiteSpace(identity.DentistName) ? "hekim" : $"{identity.DentistTitle} {identity.DentistName}".Trim())
            .Replace("{Adres}", string.IsNullOrWhiteSpace(identity.Address) ? "—" : identity.Address)
            .Replace("{Telefon}", string.IsNullOrWhiteSpace(identity.Phone) ? "—" : identity.Phone);
    }
}

public sealed record ClinicIdentity(string DentistName, string DentistTitle, string DiplomaNo, string Address, string Phone, string TaxNo);

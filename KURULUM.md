# DentistDB Kurulum Rehberi

Bu rehber kliniğin bilgisayarına DentistDB'yi kurmak, ikinci bilgisayarı bağlamak ve hekimin telefonundan erişmek için gereken adımları anlatır. Teknik bilgi gerekmez; komutlar kopyala-yapıştır ile çalışır.

## Nasıl çalışır?

- Uygulama **klinikteki bir bilgisayarda** çalışır (Windows servisi olarak, kullanıcı girişi gerekmez). Tüm veriler bu bilgisayarda kalır: `C:\ProgramData\DentistDB`.
- Klinikteki **diğer bilgisayar** aynı Wi-Fi/ağ üzerinden tarayıcıyla bağlanır.
- **Telefon** için Tailscale kullanılır: klinik bilgisayarı ile telefon arasında özel, şifreli bir bağlantı kurar. İnternete hiçbir port açılmaz; router ayarı gerekmez. Telefon evden veya mobil veriden de ulaşır.
- Bilgisayar kapalıyken uygulamaya ulaşılamaz. Hekim akşam da bakacaksa bilgisayarı uyku moduna almadan açık bırakın (Ayarlar → Güç → “Uyku: Hiçbir zaman”).

## 1. Kurulum paketi hazırlama (geliştirici bilgisayarında)

**Önerilen — tek dosyalık kurulum sihirbazı:**

```powershell
.\installer\build-installer.ps1
```

`publish\DentistDB-Setup-<sürüm>.exe` oluşur. Bu tek dosyayı klinik bilgisayarına taşıyın. .NET çalışma zamanı bu pakete gömülüdür; klinik bilgisayarına ayrıca bir şey kurmak gerekmez. (Bu adım için geliştirici bilgisayarında bir kez Inno Setup kurulur: `winget install --id JRSoftware.InnoSetup --exact`.)

**Alternatif — zip + betik:** `.\scripts\Publish-DentistDB.ps1` çalıştırıp `publish\DentistDB-<sürüm>.zip` üretebilirsiniz (bu yöntemde klinik bilgisayarında .NET 8 çalışma zamanı gerekir).

## 2. Klinik bilgisayarına kurulum

**Önerilen — sihirbaz:**

1. `DentistDB-Setup-<sürüm>.exe` dosyasına çift tıklayın ve Windows'un yönetici sorusuna **Evet** deyin.
2. Telefon erişimi için **“Tailscale kur ve ayarla”** kutusunu işaretleyin (isteğe bağlı). İşaretlerseniz kurulum sırasında tarayıcı açılır; **klinik için bir Tailscale hesabıyla** giriş yapın (Google/Microsoft/Apple ile ücretsiz). Telefonda da **aynı hesap** kullanılacak.
   Deneme verileriyle çalıştıktan sonra gerçek kullanıma geçiyorsanız **“Mevcut hasta verilerini SİL ve sıfırdan başla”** kutusunu işaretleyin; kurulum bir kez daha onay ister ve veritabanı ile görüntüleri siler (yedek klasörü kalır). Normal güncellemede bu kutuyu **işaretlemeyin**.
3. **Ağ Portu** sayfasında varsayılan 5000'i bırakın; yalnızca başka bir program bu portu kullanıyorsa değiştirin. Güncellemede önceki kurulumun portu otomatik gelir.
4. Kurulum uygulamayı `C:\Program Files\DentistDB` altına yerleştirir, servisi başlatır, güvenlik duvarını açar ve gerekiyorsa Tailscale'i ayarlar.
5. Sonunda **Kurulum Bilgileri** penceresi açılır: klinik içi adres ve **Yönetici / Çalışan PIN** buradadır. Not alın (dosya olarak da `C:\Program Files\DentistDB\KURULUM-BILGILERI.txt` içinde durur).

**Alternatif — betik:** zip'i bir klasöre çıkarın, klasörde **yönetici PowerShell** açın ve `Set-ExecutionPolicy -Scope Process Bypass -Force; .\Install-DentistDB.ps1` çalıştırın.

Bilgisayar adı `klinik-pc` ise uygulama şurada açılır: `http://klinik-pc:5000`

### İlk giriş

Yönetici PIN ile girin ve şunları yapın:

- **Ayarlar → Klinik ve Hekim Bilgileri**: klinik adı, hekim adı, diploma no, adres, telefon (reçete ve formlarda basılır), çalışma saatleri.
- **Ayarlar → Erişim PIN'leri**: PIN'leri kendinize göre değiştirin.
- **Ayarlar → Fiyat Listesi**: işlem fiyatlarını düzenleyin.
- **Ayarlar → Otomatik Yedekleme**: yedek klasörünü ikinci bir diske veya OneDrive/Google Drive klasörüne verin.

## 3. İkinci bilgisayar

Tarayıcıya `http://klinik-pc:5000` yazın (adres için **Ayarlar → Cihaz Bağlantısı**). Masaüstüne kısayol yapmak için Chrome/Edge'de menü → “Kısayol oluştur” / “Uygulama olarak yükle”.

## 4. Hekimin telefonu

1. Telefona **Tailscale** uygulamasını kurun (App Store / Play Store) ve klinikte kullanılan **aynı hesapla** giriş yapın. Bağlantı anahtarını açın.
2. Klinik bilgisayarında **Ayarlar → Cihaz Bağlantısı** sayfasını açın. “Her yerden (Tailscale)” kutusundaki **QR kodu telefonun kamerasıyla okutun**.
3. Açılan sayfada PIN ile giriş yapın. **iPhone:** Paylaş → “Ana Ekrana Ekle”. **Android:** menü → “Ana ekrana ekle”. Artık uygulama simgesiyle açılır.

Randevuların telefonun takvimine düşmesi için aynı sayfadaki “Telefon takvimine randevular” adresini takvim uygulamasına **abonelik** olarak ekleyin (adımlar sayfada yazıyor). Randevular hasta adı ve işlemle görünür, kendiliğinden güncellenir.

## 5. Güncelleme

Yeni bir sürüm yayımlandığında uygulama bunu kendisi fark eder: yönetici girişinde sol menünün altında **“Yeni sürüm: 0.x.y”** yazısı çıkar ve **Ayarlar** sayfasındaki *Sürüm* satırında **“Kurulum dosyasını indir”** düğmesi görünür (bilgisayar internete bağlıysa; kontrol günde iki kez yapılır, “Yeniden kontrol et” ile hemen bakabilirsiniz). İndirdiğiniz `DentistDB-Setup-<sürüm>.exe` dosyasını klinik bilgisayarında çalıştırmanız yeterli; eskisinin üzerine kurar ve eski sürümden kalan dosyaları temizler.

Geliştirici tarafında yayımlama tek adımdır: `DentistDB.csproj` içindeki `<Version>` değerini artırın, `git tag v0.x.y && git push origin v0.x.y` deyin; GitHub Actions kurulum dosyasını derleyip sürüm sayfasına ekler (`.github/workflows/release.yml`). Betik yöntemini kullandıysanız zip'i açıp `Install-DentistDB.ps1`'i yeniden çalıştırın. Her iki yolda da veriler, PIN'ler ve ayarlar korunur (veriler `C:\ProgramData\DentistDB` altında, kurulumdan ayrı durur) ve veritabanı şeması otomatik güncellenir.

Kaldırma: Windows **Ayarlar → Uygulamalar → DentistDB → Kaldır**. Program ve servis kaldırılır; hasta verileri ve yedekler `C:\ProgramData\DentistDB` altında **silinmeden kalır** (istemezseniz o klasörü elle silin).

## Sorun giderme

| Belirti | Çözüm |
|---|---|
| İkinci bilgisayar bağlanamıyor | İki bilgisayar aynı ağda mı? `http://<IP>:5000` deneyin (IP adresi Cihaz Bağlantısı sayfasında). Windows ağ profili “Genel” ise “Özel” yapın. |
| Telefon Tailscale adresine ulaşamıyor | Telefonda Tailscale açık mı, aynı hesap mı? Klinik bilgisayarı açık mı? Cihaz Bağlantısı sayfasında “Bağlı” yazıyor mu? |
| Tailscale adresi HTTPS açılmıyor | Klinik bilgisayarında yönetici PowerShell: `tailscale serve --bg http://localhost:5000` |
| Servis çalışmıyor | Hizmetler (services.msc) → DentistDB Klinik → Başlat. Hata için Olay Görüntüleyicisi → Windows Günlükleri → Uygulama. |
| PIN unutuldu | `C:\Program Files\DentistDB\appsettings.Production.json` içindeki PIN yalnızca uygulamadan hiç PIN değiştirilmediyse geçerlidir. Değiştirildiyse `Install-DentistDB.ps1 -AdminPin 123456` ile yeni bir başlangıç PIN'i yazılamaz; bunun yerine veritabanındaki `Settings` tablosundan `Pin:Admin` satırını silin ve servisi yeniden başlatın. |

Yedekler `C:\ProgramData\DentistDB\backups` klasöründedir; sol menüde “Son yedek” ibaresi kırmızıysa yedek 36 saatten eskidir.

# DentistDB Kurulum Rehberi

Bu rehber kliniğin bilgisayarına DentistDB'yi kurmak, ikinci bilgisayarı bağlamak ve hekimin telefonundan erişmek için gereken adımları anlatır. Teknik bilgi gerekmez; komutlar kopyala-yapıştır ile çalışır.

## Nasıl çalışır?

- Uygulama **klinikteki bir bilgisayarda** çalışır (Windows servisi olarak, kullanıcı girişi gerekmez). Tüm veriler bu bilgisayarda kalır: `C:\ProgramData\DentistDB`.
- Klinikteki **diğer bilgisayar** aynı Wi-Fi/ağ üzerinden tarayıcıyla bağlanır.
- **Telefon** için Tailscale kullanılır: klinik bilgisayarı ile telefon arasında özel, şifreli bir bağlantı kurar. İnternete hiçbir port açılmaz; router ayarı gerekmez. Telefon evden veya mobil veriden de ulaşır.
- Bilgisayar kapalıyken uygulamaya ulaşılamaz. Hekim akşam da bakacaksa bilgisayarı uyku moduna almadan açık bırakın (Ayarlar → Güç → “Uyku: Hiçbir zaman”).

## 1. Kurulum paketi hazırlama (geliştirici bilgisayarında)

```powershell
.\scripts\Publish-DentistDB.ps1
```

`publish\DentistDB-<sürüm>.zip` dosyası oluşur. Bu dosyayı klinik bilgisayarına taşıyın (USB, e-posta, bulut).

## 2. Klinik bilgisayarına kurulum

1. Zip dosyasını bir klasöre çıkarın (örn. `Masaüstü\DentistDB`).
2. Klasörün içinde boş bir yere **Shift + sağ tık → “PowerShell penceresini burada aç”** (veya Başlat → PowerShell → sağ tık → **Yönetici olarak çalıştır**, sonra `cd` ile klasöre gidin).
3. Şunu çalıştırın:

   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass -Force
   .\Install-DentistDB.ps1
   ```

4. Betik sırasıyla .NET çalışma zamanını kurar, uygulamayı `C:\Program Files\DentistDB` altına yerleştirir, servisi başlatır, güvenlik duvarını açar ve Tailscale'i kurar.
5. Tailscale adımında tarayıcı açılır: **klinik için bir Tailscale hesabı** oluşturun ya da giriş yapın (Google/Microsoft/Apple hesabı ile ücretsiz). Telefonda da **aynı hesap** kullanılacak.
6. Sonunda ekranda **Yönetici PIN** ve **Çalışan PIN** yazar. Not alın.

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

Yeni bir paket geldiğinde zip'i açın ve `Install-DentistDB.ps1` betiğini yeniden çalıştırın. Veriler, PIN'ler ve ayarlar korunur; veritabanı şeması otomatik güncellenir.

## Sorun giderme

| Belirti | Çözüm |
|---|---|
| İkinci bilgisayar bağlanamıyor | İki bilgisayar aynı ağda mı? `http://<IP>:5000` deneyin (IP adresi Cihaz Bağlantısı sayfasında). Windows ağ profili “Genel” ise “Özel” yapın. |
| Telefon Tailscale adresine ulaşamıyor | Telefonda Tailscale açık mı, aynı hesap mı? Klinik bilgisayarı açık mı? Cihaz Bağlantısı sayfasında “Bağlı” yazıyor mu? |
| Tailscale adresi HTTPS açılmıyor | Klinik bilgisayarında yönetici PowerShell: `tailscale serve --bg http://localhost:5000` |
| Servis çalışmıyor | Hizmetler (services.msc) → DentistDB Klinik → Başlat. Hata için Olay Görüntüleyicisi → Windows Günlükleri → Uygulama. |
| PIN unutuldu | `C:\Program Files\DentistDB\appsettings.Production.json` içindeki PIN yalnızca uygulamadan hiç PIN değiştirilmediyse geçerlidir. Değiştirildiyse `Install-DentistDB.ps1 -AdminPin 123456` ile yeni bir başlangıç PIN'i yazılamaz; bunun yerine veritabanındaki `Settings` tablosundan `Pin:Admin` satırını silin ve servisi yeniden başlatın. |

Yedekler `C:\ProgramData\DentistDB\backups` klasöründedir; sol menüde “Son yedek” ibaresi kırmızıysa yedek 36 saatten eskidir.

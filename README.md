# DentistDB

Tek hekimli diş kliniği için hasta, randevu, tedavi, görüntü ve fatura takibi. ASP.NET Core 8 MVC + SQLite, arayüz Türkçe.

## Özellikler

- **Hastalar** – TCKN doğrulamalı hasta kartı, fotoğraf, yapılandırılmış anamnez (alerji, ilaç, sistemik hastalıklar) ve otomatik tıbbi uyarı şeridi, acil durum kişisi, Türkçe karakter duyarlı arama, arşivleme.
- **Diş Şeması (Odontogram)** – FDI şemasında diş bazında durum (çürük, dolgu, kanal, kaplama, implant, eksik…) ve tedavi geçmişi.
- **Tedavi Planı** – Fiyat listesinden planlanan işlemler; tek tıkla randevuya, tedavi kaydına ve faturaya dönüşür.
- **Randevular** – Gün / hafta / ay / liste görünümleri, süre ve çakışma uyarısı, tamamlandı / gelmedi / iptal, kontrol randevusu menüsü, seri (tekrarlı) randevular, günlük yazdırılabilir liste, klavye kısayolları.
- **Tedavi Kayıtları** – Tanı, işlem, reçete, notlar, ilgili dişler; reçete yazdırma.
- **Görüntüler** – Röntgen ve belge yükleme (JPG, PNG, WebP, PDF); yaklaştırma, döndürme, parlaklık/kontrast, yan yana karşılaştırma.
- **Formlar** – Tedavi onam ve KVKK formlarını hasta bilgileriyle yazdırma, imza kaydı; metinler Ayarlar'dan düzenlenebilir.
- **Faturalar** – Kalemli fatura, taksit planı, tahsilat, vade uyarıları, gün sonu kasa raporu. Tutarlar yalnızca yönetici hesabında görünür.
- **Ayarlar** – Klinik ve hekim bilgileri, çalışma saatleri, PIN değiştirme, fiyat listesi, form metinleri, otomatik gece yedeği, işlem günlüğü.
- **Erişim** – PIN ile Yönetici / Çalışan hesapları, hatalı deneme kilidi, tüm değişikliklerin denetim kaydı.

## Geliştirme

```bash
dotnet run --launch-profile http
```

`http://localhost:5057` adresinde açılır. Geliştirme ortamında PIN'ler `1234` (Yönetici) ve `5678` (Çalışan), veritabanı `DentistDB_dev.db` olarak oluşturulur ve örnek verilerle doldurulur.

Şema EF Core migration'ları ile yönetilir; uygulama açılışta `Migrate()` çalıştırır. Model değişikliğinden sonra:

```bash
dotnet ef migrations add <Ad>
```

## Testler

```bash
dotnet test
```

`Tests/DentistDB.Tests` birim testlerini (fatura kuralları, TCKN, sayı ayrıştırma, arama normalizasyonu, onam şablonları) ve gerçek uygulamayı geçici bir SQLite dosyasıyla ayağa kaldıran uçtan uca testleri içerir.

## Dağıtım

Üretim kurulumu için [DEPLOYMENT.md](DEPLOYMENT.md), sürüm notları için [CHANGELOG.md](CHANGELOG.md).

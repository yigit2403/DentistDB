# DentistDB Tailscale + HTTPS Kurulum Rehberi

Bu uygulamayi Tailscale uzerinden HTTPS ile calistirmanin en temiz yolu, Tailscale'in kendi sertifika akisini kullanmaktir.

Onemli nokta:

- HTTPS sertifikasi Tailscale IP adresi icin degil, tam MagicDNS adi icin uretilir.
- Yani `https://100.x.x.x:5001` yerine `https://makine-adi.tailnet-adi.ts.net:5001` kullanilmalidir.
- Tailscale'in resmi dokumanina gore bunun icin `MagicDNS` ve `HTTPS Certificates` ozellikleri acik olmalidir. Kaynaklar:
  - [Enabling HTTPS](https://tailscale.com/docs/how-to/set-up-https-certificates)
  - [Tailscale CLI cert](https://tailscale.com/docs/reference/tailscale-cli)

## 1. Tailscale panelinde gerekli ayarlari ac

Tailscale admin panelinde:

1. `DNS` sayfasina gir.
2. `MagicDNS` acik degilse ac.
3. `HTTPS Certificates` kismini etkinlestir.

Not:

- Bu ozellik acildiginda makine adin ve `*.ts.net` alan adin sertifika seffaflik kayitlarinda gorunur.
- Hassas bir cihaz adi kullaniyorsan once makine adini duzelt.

## 2. Host makinenin MagicDNS adini belirle

Host makinede Tailscale baglantisi acik olsun.

Genelde kullanacagin adres su formdadir:

```text
makine-adi.tailnet-adi.ts.net
```

Bu adrese Tailscale admin panelinden veya makine detayindan bakabilirsin.

## 3. Publish klasorunu sabit bir yere koy

Ornek:

```powershell
C:\Services\DentistDB
```

Calistiracagin dosya bu klasor icindeki `DentistDB.exe` olmalidir.

## 4. Tailscale sertifikasini al

Host makinede, publish klasorunun icindeki script ile sertifikayi al:

```powershell
cd C:\Services\DentistDB
.\scripts\Get-TailscaleCertificate.ps1 -DnsName "makine-adi.tailnet-adi.ts.net"
```

Bu komut sertifika ve key dosyalarini varsayilan olarak su klasore koyar:

```text
C:\ProgramData\DentistDB\certs
```

## 5. Tailscale IP adresini ogren

Host makinede:

```powershell
tailscale ip -4
```

Bu IP'yi Kestrel'i sadece Tailscale arayuzune baglamak icin kullanacaksin.

## 6. Ilk HTTPS testi

Host makinede publish klasorunde su sekilde calistir:

```powershell
cd C:\Services\DentistDB

$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:AccessPins__Admin = "yeni-admin-pin"
$env:AccessPins__Worker = "yeni-worker-pin"
$env:ConnectionStrings__DefaultConnection = "Data Source=C:\ProgramData\DentistDB\data\DentistDB.db"
$env:Storage__ScanStoragePath = "C:\ProgramData\DentistDB\scans"
$env:Storage__DataProtectionKeysPath = "C:\ProgramData\DentistDB\keys"
$env:Kestrel__Endpoints__HttpsPrivate__Url = "https://TAILSCALE_IP:5001"
$env:Kestrel__Endpoints__HttpsPrivate__Certificate__Path = "C:\ProgramData\DentistDB\certs\makine-adi.tailnet-adi.ts.net.crt"
$env:Kestrel__Endpoints__HttpsPrivate__Certificate__KeyPath = "C:\ProgramData\DentistDB\certs\makine-adi.tailnet-adi.ts.net.key"

.\DentistDB.exe
```

Burada:

- `TAILSCALE_IP` yerine `tailscale ip -4` sonucunu yaz
- Sertifika dosya adlarinda kendi `DnsName` degerini kullan

Tarayicidan su adresle ac:

```text
https://makine-adi.tailnet-adi.ts.net:5001
```

Not:

- Baglanti adresi DNS adi olmali
- Dinleme adresi olarak Tailscale IP kullanilmasi normaldir

## 7. Servis olarak kur

Ilk HTTPS testi calisiyorsa, servis olarak kur:

```powershell
cd C:\Services\DentistDB
.\scripts\Install-DentistDBService.ps1 `
  -PublishPath C:\Services\DentistDB `
  -PrivateUrl "https://TAILSCALE_IP:5001" `
  -CertificatePath "C:\ProgramData\DentistDB\certs\makine-adi.tailnet-adi.ts.net.crt" `
  -CertificateKeyPath "C:\ProgramData\DentistDB\certs\makine-adi.tailnet-adi.ts.net.key"
```

## 8. Firewall kuralini ekle

```powershell
cd C:\Services\DentistDB
.\scripts\Set-DentistDBFirewallRule.ps1 -Port 5001
```

Bu kural sadece o portu acar. Tailscale tarafinda tek cihaz kullanmak istiyorsan, ek olarak:

- o cihazi Tailscale tarafinda onayli tut
- gerekiyorsa ACL veya cihaz bazli erisim kisiti uygula

## 9. Sertifika yenileme

Tailscale'in resmi dokumanina gore `tailscale cert` ile dosya olarak uretilen sertifikalar otomatik yenilenmez. Sertifika suresi dolmadan tekrar almalisin.

Yenileme adimi:

```powershell
cd C:\Services\DentistDB
.\scripts\Get-TailscaleCertificate.ps1 -DnsName "makine-adi.tailnet-adi.ts.net"
Restart-Service DentistDB
```

## 10. En hizli sorun kontrolu

Eger calismazsa sirasiyla sunlari kontrol et:

1. `MagicDNS` acik mi
2. `HTTPS Certificates` acik mi
3. Sertifika `tailscale cert` ile alindi mi
4. Tarayicida IP yerine tam `*.ts.net` adresi mi kullaniyorsun
5. Uygulama Tailscale IP'sine bagli mi
6. Port `5001` firewall tarafinda acik mi
7. PIN degerleri placeholder olarak kalmis mi

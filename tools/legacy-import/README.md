# Eski Access programından hasta aktarımı

Babanın eski Microsoft Access programındaki (`.accdb`) hasta kayıtlarını yeni
DentistDB veritabanına aktarmak için iki adımlık, tek seferlik bir araç.

Kapsam: **yalnızca hasta kartları** (ad, telefon, doğum tarihi, adres, notlar,
ilk geliş tarihi). Para/borç kayıtları, tedavi geçmişi ve fotoğraflar bilinçli
olarak alınmaz. Eski programın `AnamnezBilgisi` alanı çoğunlukla ödeme/tedavi
günlüğü olduğu için o da alınmaz.

## TCKN hakkında

Eski kayıtların yaklaşık %40'ında geçerli TCKN var. TCKN'si olmayan hastalar da
aktarılır; kartlarında **"TCKN eksik — eski kayıttan aktarıldı"** rozeti görünür
ve bir sonraki gelişte doldurulması istenir. Uygulamada yeni hasta eklerken TCKN
yine zorunludur; bu esneklik sadece aktarılan kayıtlar içindir.

Aynı TCKN birden fazla kayıtta geçiyorsa (eski programdaki mükerrer kayıtlar),
TCKN ilk kayıtta bırakılır; diğerlerinde boşaltılıp karta bir not düşülür.

## Adım 1 — Access'ten temiz JSON çıkar (`extract.ps1`)

Windows PowerShell ile (pwsh değil), Access Database Engine (ACE OLEDB) kurulu
bir makinede çalıştırın:

```powershell
powershell -ExecutionPolicy Bypass -File extract.ps1 `
    -AccessPath "C:\yol\db1.accdb" -Password "<parola>" -OutPath "patients.json"
```

`patients.json` **hasta verisi içerir**; git'e eklemeyin, iş bitince silin.

## Adım 2 — JSON'u DentistDB veritabanına aktar (`LegacyImport`)

```bash
dotnet run --project tools/LegacyImport -- --json patients.json --db "C:\ProgramData\DentistDB\DentistDB.db"
```

- `--dry-run` eklerseniz hiçbir şey kaydedilmez, yalnızca özet yazılır.
- Tekrar çalıştırmak güvenlidir: hastalar eski `KisiNumara` (LegacyKey) ile
  eşleştiği için ikinci çalıştırma yeni kayıt eklemez, mevcutları günceller.
- Araç, hedef veritabanına şema göçlerini (migrations) kendisi uygular; boş bir
  dosya verirseniz tabloları oluşturur.

Aktarımdan sonra uygulama ilk açılışta fiyat listesi ve ayarları tohumlar.

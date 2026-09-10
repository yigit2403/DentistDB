# DentistDB Deployment

Turkish step-by-step guide for the clinic: [KURULUM.md](KURULUM.md). This document is the technical reference.

## Topology

- The app runs as a **Windows service** on one clinic PC (SQLite, files under `%ProgramData%\DentistDB`).
- Other clinic PCs use it over plain HTTP on the LAN (`http://<pc>:5000`), restricted to the local subnet by the firewall rule.
- Phones and remote access go through **Tailscale**: `tailscale serve` terminates HTTPS with a valid certificate on the tailnet name (`https://klinik.<tailnet>.ts.net`) and proxies to `localhost:5000`. Nothing is exposed to the public internet and no router configuration is needed.
- The app is a small PWA (manifest + icons) so phones can install it to the home screen, and it publishes an iCalendar feed (token-protected) for phone calendar subscriptions.

## Build a release

```powershell
.\scripts\Publish-DentistDB.ps1          # runs tests, publishes win-x64, zips with the installer
```

Produces `publish\DentistDB-<version>.zip` containing the app and `Install-DentistDB.ps1`.

## Install / upgrade on the clinic PC

Unzip, open an elevated PowerShell in that folder and run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\Install-DentistDB.ps1
```

The installer is idempotent. Re-running it upgrades the binaries and keeps `appsettings.Production.json`, the database, scans and keys. Parameters:

| Parameter | Default | Purpose |
|---|---|---|
| `-PublishPath` | script folder | Folder with `DentistDB.dll` |
| `-InstallPath` | `C:\Program Files\DentistDB` | Binaries |
| `-DataRoot` | `C:\ProgramData\DentistDB` | `data\DentistDB.db`, `scans`, `keys`, `backups` |
| `-Port` | `5000` | HTTP port on the LAN and behind Tailscale |
| `-AdminPin` / `-WorkerPin` | random 6 digits on first install | Initial PINs (only used until changed in the app) |
| `-SkipTailscale` | off | LAN-only install |
| `-SkipDotnetCheck` | off | Skip the runtime check/install |

What it does: checks/installs the ASP.NET Core 8 runtime via winget, copies files with `robocopy /MIR`, writes production settings, creates or updates the `DentistDB` service (automatic start, restart on failure), opens the firewall for the local subnet and the Tailscale range, installs Tailscale via winget if missing, runs `tailscale up --hostname=klinik` (opens the browser sign-in) and `tailscale serve --bg http://localhost:<port>`.

## Configuration reference

`appsettings.Production.json` in the install folder (written by the installer):

```json
{
  "Kestrel": { "Endpoints": { "Http": { "Url": "http://0.0.0.0:5000" } } },
  "AccessPins": { "Admin": "…", "Worker": "…" },
  "ConnectionStrings": { "DefaultConnection": "Data Source=C:\\ProgramData\\DentistDB\\data\\DentistDB.db" },
  "Storage": { "ScanStoragePath": "C:\\ProgramData\\DentistDB\\scans", "DataProtectionKeysPath": "C:\\ProgramData\\DentistDB\\keys" }
}
```

- The app **refuses to start** in Production with placeholder or trivial PINs (`1234`, `0000`…). Once a PIN is changed from **Ayarlar → Erişim PIN'leri**, the hashed value in the database wins and the configured value is ignored.
- HTTPS redirection and HSTS are only enabled when Kestrel itself has an HTTPS endpoint. With Tailscale the LAN stays HTTP and Tailscale provides HTTPS; the app honours `X-Forwarded-Proto` from the local proxy.
- Environment variables override the JSON (`AccessPins__Admin`, `ConnectionStrings__DefaultConnection`, …).

## First start

Sign in as Yönetici, then: **Ayarlar → Klinik ve Hekim Bilgileri** (name, dentist, diploma no, address, phone, hours), **Erişim PIN'leri**, **Fiyat Listesi**, **Otomatik Yedekleme** (point the folder at a second disk or a cloud-synced folder), **Cihaz Bağlantısı** (QR codes for the second PC and the phone, calendar subscription URL).

## Upgrading

Schema changes are applied automatically at startup from 0.3 onwards. Databases created by 0.1 or 0.2 are not upgraded in place.

## Backups

The app takes an automatic backup every day (default 13:00, while the clinic PC is certainly on; if the PC was off at that time it runs shortly after the next start) into `%ProgramData%\DentistDB\backups`: a dated copy of the database plus a mirror of new scan files, keeping the last N copies. Configure it under **Ayarlar → Otomatik Yedekleme**. The sidebar shows the last run and turns red if it is older than 36 hours or failed. **Yedeği İndir** downloads a copy on demand.

If you use an external backup tool as well, copy `data\DentistDB.db` (stop the service or use a SQLite-aware tool), `scans` and `keys` under `%ProgramData%\DentistDB`.

## Security notes

- Keep the port closed on public networks: the firewall rule only allows the local subnet (`LocalSubnet`) and the Tailscale CGNAT range (`100.64.0.0/10`).
- Do not port-forward the app on the router. The PIN screen is not designed to face the internet.
- The calendar feed URL contains a secret token; rotate it from **Cihaz Bağlantısı** if it leaks.
- Data-protection keys (`keys`) encrypt the session cookie and antiforgery tokens; back them up with the database.

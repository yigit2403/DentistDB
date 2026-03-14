# DentistDB Deployment

## Release target

- Windows host
- `ASPNETCORE_ENVIRONMENT=Production`
- SQLite database in `%ProgramData%\DentistDB\data\DentistDB.db`
- Scan storage in `%ProgramData%\DentistDB\scans`
- Private access over Tailscale or ZeroTier only

## Prepare the host

1. Install the .NET 8 Hosting Bundle.
2. Install and sign in to Tailscale or ZeroTier.
3. Approve only the single client device that should reach the app.
4. Copy the published app to a stable folder such as `C:\Services\DentistDB`.
5. Create a private certificate for the Tailscale or ZeroTier IP or hostname.

## Required configuration

Set these before starting the app in production:

- Change `AccessPins:Admin`
- Change `AccessPins:Worker`
- Keep `ConnectionStrings:DefaultConnection` as a SQLite connection string
- Set `Storage:ScanStoragePath` if `%ProgramData%\DentistDB\scans` is not desired
- Set `Storage:DataProtectionKeysPath` if `%ProgramData%\DentistDB\keys` is not desired
- Bind Kestrel to the private VPN IP only

You can configure the app with `appsettings.Production.json` or environment variables.

### Example environment variables

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "https://100.101.102.103:5001"
$env:ConnectionStrings__DefaultConnection = "Data Source=C:\ProgramData\DentistDB\data\DentistDB.db"
$env:Storage__ScanStoragePath = "C:\ProgramData\DentistDB\scans"
$env:Storage__DataProtectionKeysPath = "C:\ProgramData\DentistDB\keys"
$env:AccessPins__Admin = "replace-with-admin-pin"
$env:AccessPins__Worker = "replace-with-worker-pin"
```

### HTTPS certificate configuration

Configure Kestrel certificate settings by environment variable or appsettings:

```powershell
$env:Kestrel__Endpoints__HttpsPrivate__Url = "https://100.101.102.103:5001"
$env:Kestrel__Endpoints__HttpsPrivate__Certificate__Path = "C:\ProgramData\DentistDB\certs\dentistdb.pfx"
$env:Kestrel__Endpoints__HttpsPrivate__Certificate__Password = "replace-with-pfx-password"
```

## Run as a Windows service

Use the helper script in `scripts\Install-DentistDBService.ps1` after publishing the app.

## Firewall baseline

- Allow inbound traffic only on the chosen app port
- Scope the rule to the Tailscale or ZeroTier interface or private VPN addresses
- Do not expose the app on public WAN interfaces

Use the helper script in `scripts\Set-DentistDBFirewallRule.ps1`.

## Backups

Back up both of these paths:

- `%ProgramData%\DentistDB\data\DentistDB.db`
- `%ProgramData%\DentistDB\scans`
- `%ProgramData%\DentistDB\keys`

Stop the service before copying the SQLite database, or use a backup process that supports SQLite file consistency.

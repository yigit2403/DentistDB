<#
.SYNOPSIS
    One-step installer for DentistDB on the clinic PC.

.DESCRIPTION
    Run from an elevated PowerShell in the folder that contains the published app
    (the folder with DentistDB.exe), or pass -PublishPath.

    The script:
      1. Installs the ASP.NET Core 8 runtime if it is missing (via winget).
      2. Copies the app to C:\Program Files\DentistDB and creates the data folders.
      3. Generates strong PINs (or uses the ones you pass) and writes appsettings.Production.json.
      4. Installs (or updates) the "DentistDB" Windows service and starts it.
      5. Opens the firewall for the local network on the chosen port.
      6. Installs Tailscale if missing, signs in (opens a browser link), and publishes the app
         over HTTPS with `tailscale serve`, so phones reach https://<pc>.<tailnet>.ts.net.

    Re-running the script on the same PC upgrades the app in place and keeps data and settings.

.EXAMPLE
    .\Install-DentistDB.ps1
.EXAMPLE
    .\Install-DentistDB.ps1 -PublishPath C:\Temp\DentistDB-publish -Port 5000 -SkipTailscale
#>
[CmdletBinding()]
param(
    [string]$PublishPath = $PSScriptRoot,
    [string]$InstallPath = "$env:ProgramFiles\DentistDB",
    [string]$DataRoot = "$env:ProgramData\DentistDB",
    [int]$Port = 5000,
    [string]$AdminPin,
    [string]$WorkerPin,
    [string]$ServiceName = "DentistDB",
    [switch]$SkipTailscale,
    [switch]$SkipDotnetCheck
)

$ErrorActionPreference = "Stop"

function Write-Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }
function Write-Ok($text) { Write-Host "    $text" -ForegroundColor Green }
function Write-Warn2($text) { Write-Host "    $text" -ForegroundColor Yellow }

# --- 0. Pre-flight -----------------------------------------------------------
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Bu betiği yönetici olarak çalıştırın (PowerShell'e sağ tıklayın → Yönetici olarak çalıştır)."
}

if (-not (Test-Path (Join-Path $PublishPath "DentistDB.dll"))) {
    # Allow running from the repo's scripts folder next to a publish output.
    $candidate = Join-Path (Split-Path $PublishPath -Parent) "publish\DentistDB"
    if (Test-Path (Join-Path $candidate "DentistDB.dll")) { $PublishPath = $candidate }
    else { throw "DentistDB.dll bulunamadı. Yayınlanmış uygulama klasörünü -PublishPath ile verin (bkz. Publish-DentistDB.ps1)." }
}
$PublishPath = (Resolve-Path $PublishPath).Path
Write-Ok "Kaynak: $PublishPath"

# --- 1. .NET runtime ----------------------------------------------------------
if (-not $SkipDotnetCheck) {
    Write-Step ".NET 8 çalışma zamanı kontrol ediliyor"
    $hasRuntime = $false
    try {
        $hasRuntime = (& dotnet --list-runtimes 2>$null) -match "Microsoft.AspNetCore.App 8\."
    } catch { $hasRuntime = $false }

    if ($hasRuntime) {
        Write-Ok "ASP.NET Core 8 çalışma zamanı mevcut."
    } else {
        Write-Warn2 "Bulunamadı; winget ile kuruluyor..."
        & winget install --id Microsoft.DotNet.AspNetCore.8 --exact --accept-source-agreements --accept-package-agreements --silent
        if ($LASTEXITCODE -ne 0) { throw "Runtime kurulamadı. https://dotnet.microsoft.com/download/dotnet/8.0 adresinden 'ASP.NET Core Runtime 8 - Windows Hosting Bundle' kurun ve tekrar çalıştırın." }
        Write-Ok "Kuruldu."
    }
}

# --- 2. Folders and files -------------------------------------------------------
Write-Step "Dosyalar kopyalanıyor"
foreach ($dir in @($InstallPath, "$DataRoot\data", "$DataRoot\scans", "$DataRoot\keys", "$DataRoot\backups")) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -ne "Stopped") {
    Write-Warn2 "Çalışan servis durduruluyor..."
    Stop-Service -Name $ServiceName -Force
    $service.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(30))
}

$settingsFile = Join-Path $InstallPath "appsettings.Production.json"
$existingSettings = $null
if (Test-Path $settingsFile) {
    try { $existingSettings = Get-Content $settingsFile -Raw | ConvertFrom-Json } catch { $existingSettings = $null }
}

& robocopy $PublishPath $InstallPath /MIR /NFL /NDL /NJH /NJS /NP /XF appsettings.Production.json | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Dosya kopyalama başarısız (robocopy kodu $LASTEXITCODE)." }
Write-Ok "Uygulama: $InstallPath"

# --- 3. Settings and PINs ---------------------------------------------------------
Write-Step "Ayarlar yazılıyor"
function New-Pin { -join ((1..6) | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 }) }

$adminPinValue = $AdminPin
$workerPinValue = $WorkerPin
$pinsGenerated = $false
if (-not $adminPinValue) {
    if ($existingSettings -and $existingSettings.AccessPins -and $existingSettings.AccessPins.Admin) { $adminPinValue = $existingSettings.AccessPins.Admin }
    else { $adminPinValue = New-Pin; $pinsGenerated = $true }
}
if (-not $workerPinValue) {
    if ($existingSettings -and $existingSettings.AccessPins -and $existingSettings.AccessPins.Worker) { $workerPinValue = $existingSettings.AccessPins.Worker }
    else { $workerPinValue = New-Pin; $pinsGenerated = $true }
}

$settings = [ordered]@{
    Logging = [ordered]@{ LogLevel = [ordered]@{ Default = "Information"; "Microsoft.AspNetCore" = "Warning"; "Microsoft.EntityFrameworkCore" = "Warning" } }
    Kestrel = [ordered]@{ Endpoints = [ordered]@{ Http = [ordered]@{ Url = "http://0.0.0.0:$Port" } } }
    AllowedHosts = "*"
    AccessPins = [ordered]@{ Admin = $adminPinValue; Worker = $workerPinValue }
    ConnectionStrings = [ordered]@{ DefaultConnection = "Data Source=$DataRoot\data\DentistDB.db" }
    Storage = [ordered]@{ ScanStoragePath = "$DataRoot\scans"; DataProtectionKeysPath = "$DataRoot\keys" }
}
$settings | ConvertTo-Json -Depth 5 | Set-Content -Path $settingsFile -Encoding UTF8
Write-Ok "appsettings.Production.json güncellendi."

# --- 4. Windows service -----------------------------------------------------------
Write-Step "Windows servisi kuruluyor"
$exePath = Join-Path $InstallPath "DentistDB.exe"
if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    New-Service -Name $ServiceName -BinaryPathName "`"$exePath`"" -DisplayName "DentistDB Klinik" -Description "DentistDB klinik yönetim uygulaması" -StartupType Automatic | Out-Null
    & sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
    Write-Ok "Servis oluşturuldu."
} else {
    & sc.exe config $ServiceName binPath= "`"$exePath`"" start= auto | Out-Null
    Write-Ok "Servis güncellendi."
}
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")
# The service reads appsettings.Production.json from its own folder; set the working directory via the registry.
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName" -Name "Environment" -Value @("ASPNETCORE_ENVIRONMENT=Production") -Type MultiString

Start-Service -Name $ServiceName
Start-Sleep -Seconds 4
$service = Get-Service -Name $ServiceName
if ($service.Status -ne "Running") { throw "Servis başlamadı. Olay Görüntüleyicisi → Uygulama günlüğüne bakın." }
Write-Ok "Servis çalışıyor."

# --- 5. Firewall (LAN) -------------------------------------------------------------
Write-Step "Güvenlik duvarı kuralı"
$ruleName = "DentistDB (port $Port)"
Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Profile Private,Domain -RemoteAddress LocalSubnet | Out-Null
Write-Ok "Yerel ağdan $Port portuna erişim açıldı (yalnızca aynı alt ağ)."

# --- 6. Tailscale -----------------------------------------------------------------
$tailscaleUrl = $null
if (-not $SkipTailscale) {
    Write-Step "Tailscale"
    $tailscaleExe = "$env:ProgramFiles\Tailscale\tailscale.exe"
    if (-not (Test-Path $tailscaleExe)) {
        Write-Warn2 "Tailscale kuruluyor (winget)..."
        & winget install --id tailscale.tailscale --exact --accept-source-agreements --accept-package-agreements --silent
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $tailscaleExe)) {
            Write-Warn2 "Tailscale otomatik kurulamadı. https://tailscale.com/download/windows adresinden kurup betiği tekrar çalıştırın."
        }
    }

    if (Test-Path $tailscaleExe) {
        Write-Host "    Tarayıcıda açılan Tailscale giriş sayfasından klinik hesabıyla oturum açın..." -ForegroundColor Yellow
        & $tailscaleExe up --accept-dns=true --hostname=klinik 2>&1 | ForEach-Object { Write-Host "    $_" }
        # Publish the app on the tailnet over HTTPS (443 → localhost:$Port).
        & $tailscaleExe serve --bg "http://localhost:$Port" 2>&1 | ForEach-Object { Write-Host "    $_" }
        try {
            $status = & $tailscaleExe status --json | ConvertFrom-Json
            if ($status.Self.DNSName) { $tailscaleUrl = "https://" + $status.Self.DNSName.TrimEnd(".") }
        } catch { }
        # Tailscale traffic arrives on the Tailscale interface; allow the port there too.
        Get-NetFirewallRule -DisplayName "DentistDB (Tailscale)" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
        New-NetFirewallRule -DisplayName "DentistDB (Tailscale)" -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -RemoteAddress 100.64.0.0/10 | Out-Null
        Write-Ok "Tailscale hazır."
    }
}

# --- 7. Summary -----------------------------------------------------------------------
$lanIp = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.PrefixOrigin -ne "WellKnown" -and $_.IPAddress -notlike "169.254.*" -and $_.IPAddress -ne "127.0.0.1" -and $_.InterfaceAlias -notlike "*Tailscale*" } | Select-Object -First 1).IPAddress

Write-Host ""
Write-Host "================= DentistDB kuruldu =================" -ForegroundColor Green
Write-Host " Klinik içi adres : http://$($env:COMPUTERNAME.ToLower()):$Port   (veya http://$lanIp`:$Port)"
if ($tailscaleUrl) { Write-Host " Telefon / uzak   : $tailscaleUrl" }
if ($pinsGenerated) {
    Write-Host ""
    Write-Host " Yönetici PIN : $adminPinValue" -ForegroundColor Yellow
    Write-Host " Çalışan PIN  : $workerPinValue" -ForegroundColor Yellow
    Write-Host " Bu PIN'leri not alın; Ayarlar → Erişim PIN'leri bölümünden değiştirebilirsiniz."
}
Write-Host ""
Write-Host " Sonraki adım: uygulamada Ayarlar → Cihaz Bağlantısı sayfasını açın; telefon için QR kodlar oradadır."
Write-Host " Veriler: $DataRoot   (yedekler: $DataRoot\backups)"
Write-Host "======================================================" -ForegroundColor Green

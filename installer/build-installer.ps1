<#
.SYNOPSIS
    Builds DentistDB-Setup-<version>.exe with Inno Setup.

.DESCRIPTION
    1. Runs the tests (unless -SkipTests).
    2. Publishes the app self-contained for win-x64 (so the clinic PC needs no .NET runtime).
    3. Copies the configure script into the publish folder.
    4. Compiles installer\DentistDB.iss with Inno Setup's ISCC.exe.

    Requires Inno Setup 6 (ISCC.exe). If it is missing, install it once with:
        winget install --id JRSoftware.InnoSetup --exact

.EXAMPLE
    .\installer\build-installer.ps1
    # → publish\DentistDB-Setup-0.4.0.exe
#>
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [string]$Iscc
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "DentistDB.csproj"
$publishDir = Join-Path $root "publish\DentistDB"
$iss = Join-Path $PSScriptRoot "DentistDB.iss"

function Write-Step($t) { Write-Host "`n==> $t" -ForegroundColor Cyan }

# Version from the csproj.
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version) { throw "Sürüm okunamadı: $project" }

if (-not $SkipTests) {
    Write-Step "Testler"
    & dotnet test (Join-Path $root "Tests\DentistDB.Tests\DentistDB.Tests.csproj") -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Testler başarısız; kurulum durduruldu." }
}

Write-Step "Yayınlanıyor (self-contained win-x64)"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
& dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -o $publishDir --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız." }

# The graphical installer runs this in -ConfigureOnly mode.
Copy-Item (Join-Path $root "scripts\Install-DentistDB.ps1") $publishDir -Force
Remove-Item (Join-Path $publishDir "appsettings.Development.json") -ErrorAction SilentlyContinue
# Belt and braces: the csproj already excludes dev uploads, but never let patient images into a package.
Remove-Item (Join-Path $publishDir "wwwroot\uploads") -Recurse -Force -ErrorAction SilentlyContinue

Write-Step "Inno Setup derleniyor"
if (-not $Iscc) {
    $candidates = @(
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"   # per-user install (winget without admin)
    )
    $Iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Iscc -or -not (Test-Path $Iscc)) {
    throw "ISCC.exe bulunamadı. Inno Setup 6 kurun (winget install --id JRSoftware.InnoSetup --exact) veya -Iscc ile yolunu verin."
}

& $Iscc "/DAppVersion=$version" "/DPublishDir=$publishDir" $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup derlemesi başarısız." }

$setup = Join-Path $root "publish\DentistDB-Setup-$version.exe"
Write-Host ""
Write-Host "Kurulum dosyası hazır: $setup" -ForegroundColor Green
Write-Host "Klinik bilgisayarında bu .exe'yi çift tıklayıp yönetici onayını verin."

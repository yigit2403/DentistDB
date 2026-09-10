<#
.SYNOPSIS
    Builds a release package of DentistDB ready to copy to the clinic PC.

.DESCRIPTION
    Runs the tests, publishes the app for Windows x64 (framework-dependent, needs the
    ASP.NET Core 8 runtime on the target) and zips it together with the installer script.

.EXAMPLE
    .\scripts\Publish-DentistDB.ps1
    # → publish\DentistDB-<version>.zip
#>
[CmdletBinding()]
param(
    [string]$Output = (Join-Path (Split-Path $PSScriptRoot -Parent) "publish"),
    [switch]$SkipTests,
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "DentistDB.csproj"
$stage = Join-Path $Output "DentistDB"

if (-not $SkipTests) {
    Write-Host "==> Testler çalıştırılıyor" -ForegroundColor Cyan
    & dotnet test (Join-Path $root "Tests\DentistDB.Tests\DentistDB.Tests.csproj") -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Testler başarısız; yayın durduruldu." }
}

Write-Host "==> Yayınlanıyor" -ForegroundColor Cyan
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$publishArgs = @("publish", $project, "-c", "Release", "-r", "win-x64", "-o", $stage, "--nologo", "-v", "q")
$publishArgs += if ($SelfContained) { "--self-contained", "true" } else { "--self-contained", "false" }
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız." }

# Ship the installer next to the binaries and drop dev-only files.
Copy-Item (Join-Path $PSScriptRoot "Install-DentistDB.ps1") $stage -Force
Remove-Item (Join-Path $stage "appsettings.Development.json") -ErrorAction SilentlyContinue

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Select-Object -First 1
$zip = Join-Path $Output "DentistDB-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip

Write-Host ""
Write-Host "Paket hazır: $zip" -ForegroundColor Green
Write-Host "Klinik bilgisayarında: zip'i açın, klasörde PowerShell'i yönetici olarak açın ve .\Install-DentistDB.ps1 çalıştırın."

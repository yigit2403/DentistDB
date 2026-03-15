param(
    [Parameter(Mandatory = $true)]
    [string]$DnsName,

    [string]$OutputDirectory = "C:\ProgramData\DentistDB\certs"
)

$tailscaleCommand = Get-Command tailscale -ErrorAction SilentlyContinue
if (-not $tailscaleCommand) {
    throw "tailscale komutu bulunamadi. Tailscale CLI yuklu olmali."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$safeName = $DnsName -replace "[^a-zA-Z0-9.-]", "_"
$certPath = Join-Path $OutputDirectory "$safeName.crt"
$keyPath = Join-Path $OutputDirectory "$safeName.key"

& $tailscaleCommand.Source cert --cert-file="$certPath" --key-file="$keyPath" $DnsName

if (-not (Test-Path $certPath) -or -not (Test-Path $keyPath)) {
    throw "Sertifika dosyalari olusturulamadi."
}

Write-Host "CertificatePath=$certPath"
Write-Host "CertificateKeyPath=$keyPath"

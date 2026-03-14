param(
    [Parameter(Mandatory = $true)]
    [string]$PublishPath,

    [Parameter(Mandatory = $true)]
    [string]$PrivateUrl,

    [string]$ServiceName = "DentistDB",
    [string]$DataRoot = "C:\ProgramData\DentistDB",
    [string]$CertificatePath = "C:\ProgramData\DentistDB\certs\dentistdb.pfx",
    [string]$CertificatePassword = ""
)

$publishRoot = (Resolve-Path $PublishPath).Path
$exePath = Join-Path $publishRoot "DentistDB.exe"

if (-not (Test-Path $exePath)) {
    throw "Could not find DentistDB.exe in $publishRoot"
}

New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot "data") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot "scans") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot "certs") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot "keys") | Out-Null

[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")
[Environment]::SetEnvironmentVariable("ASPNETCORE_URLS", $PrivateUrl, "Machine")
[Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Data Source=$DataRoot\data\DentistDB.db", "Machine")
[Environment]::SetEnvironmentVariable("Storage__ScanStoragePath", "$DataRoot\scans", "Machine")
[Environment]::SetEnvironmentVariable("Storage__DataProtectionKeysPath", "$DataRoot\keys", "Machine")

if ($CertificatePassword) {
    [Environment]::SetEnvironmentVariable("Kestrel__Endpoints__HttpsPrivate__Certificate__Path", $CertificatePath, "Machine")
    [Environment]::SetEnvironmentVariable("Kestrel__Endpoints__HttpsPrivate__Certificate__Password", $CertificatePassword, "Machine")
    [Environment]::SetEnvironmentVariable("Kestrel__Endpoints__HttpsPrivate__Url", $PrivateUrl, "Machine")
}

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    throw "The service '$ServiceName' already exists."
}

New-Service -Name $ServiceName -BinaryPathName "`"$exePath`"" -DisplayName $ServiceName -StartupType Automatic
Start-Service -Name $ServiceName

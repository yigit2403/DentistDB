param(
    [Parameter(Mandatory = $true)]
    [string]$DnsName,

    [string]$OutputPath = "C:\ProgramData\DentistDB\certs\dentistdb.pfx",
    [string]$Password = ""
)

$certDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $certDirectory | Out-Null

$certificate = New-SelfSignedCertificate `
    -DnsName $DnsName `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -FriendlyName "DentistDB Private HTTPS"

if ($Password) {
    $securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
    Export-PfxCertificate -Cert $certificate -FilePath $OutputPath -Password $securePassword | Out-Null
}
else {
    Export-PfxCertificate -Cert $certificate -FilePath $OutputPath -Password (ConvertTo-SecureString -String "" -AsPlainText -Force) | Out-Null
}

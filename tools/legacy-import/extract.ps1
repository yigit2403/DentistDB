<#
  Extract patients from the old Microsoft Access (.accdb) dental program into a
  clean, UTF-8 JSON file that tools/LegacyImport consumes.

  This script contains NO patient data; the produced JSON does, so keep it out of
  git and delete it after the import.

  Requirements: the Microsoft Access Database Engine (ACE OLEDB) provider. Run with
  Windows PowerShell (not pwsh) so System.Data.OleDb is available:

      powershell -ExecutionPolicy Bypass -File extract.ps1 `
          -AccessPath "C:\path\db1.accdb" -Password "<parola>" -OutPath "patients.json"
#>
param(
    [Parameter(Mandatory = $true)][string]$AccessPath,
    [string]$Password = "",
    [string]$OutPath = "patients.json"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$tr = [System.Globalization.CultureInfo]::GetCultureInfo("tr-TR")

function Test-Tckn([string]$t) {
    if ([string]::IsNullOrWhiteSpace($t) -or $t.Length -ne 11 -or $t[0] -eq '0') { return $false }
    $d = @(); foreach ($ch in $t.ToCharArray()) { if ($ch -lt '0' -or $ch -gt '9') { return $false }; $d += [int]([string]$ch) }
    $odd = $d[0] + $d[2] + $d[4] + $d[6] + $d[8]
    $even = $d[1] + $d[3] + $d[5] + $d[7]
    $tenth = ((($odd * 7) - $even) % 10 + 10) % 10
    if ($tenth -ne $d[9]) { return $false }
    $eleventh = (($d[0..9] | Measure-Object -Sum).Sum) % 10
    return $eleventh -eq $d[10]
}

function Clean([object]$v) {
    if ($v -is [DBNull] -or $null -eq $v) { return $null }
    $s = ([string]$v).Trim()
    if ($s -eq "") { return $null }
    # collapse runs of whitespace
    return ([System.Text.RegularExpressions.Regex]::Replace($s, "\s+", " "))
}

function TitleTr([string]$s) {
    if ([string]::IsNullOrWhiteSpace($s)) { return $null }
    return $tr.TextInfo.ToTitleCase($s.ToLower($tr))
}

function DigitsOnly([object]$v) {
    $s = Clean $v
    if ($null -eq $s) { return $null }
    return ([System.Text.RegularExpressions.Regex]::Replace($s, "\D", ""))
}

function NormPhone([object]$v) {
    $d = DigitsOnly $v
    if ($null -eq $d) { return $null }
    if ($d.Length -eq 10 -and $d[0] -eq '5') { return "0" + $d }          # 5xx... -> 05xx...
    if ($d.Length -eq 11 -and $d.Substring(0,2) -eq "05") { return $d }   # already 05xx...
    return $d                                                              # keep whatever we have
}

function SaneDate([object]$v) {
    if ($v -is [DBNull] -or $null -eq $v) { return $null }
    try { $dt = [datetime]$v } catch { return $null }
    if ($dt.Year -lt 1900 -or $dt -gt [datetime]::Today) { return $null }
    return $dt.ToString("yyyy-MM-dd")
}

function Cap([string]$s, [int]$n) {
    if ($null -eq $s) { return $null }
    if ($s.Length -le $n) { return $s }
    return $s.Substring(0, $n)
}

$cs = "Provider=Microsoft.ACE.OLEDB.16.0;Data Source=$AccessPath;"
if ($Password -ne "") { $cs += "Jet OLEDB:Database Password=$Password;" }
$cn = New-Object System.Data.OleDb.OleDbConnection($cs)
try { $cn.Open() } catch {
    $cs = $cs.Replace("ACE.OLEDB.16.0", "ACE.OLEDB.12.0")
    $cn = New-Object System.Data.OleDb.OleDbConnection($cs); $cn.Open()
}

$cmd = $cn.CreateCommand()
$cmd.CommandText = @"
SELECT KisiNumara, AdSoyad, DogumTarihi, Adres, ilcesi, Sehir, EvTelefonu,
       CepTelefonu, [TC KIMLIK NO] AS TCKN, Aciklama, AnamnezBilgisi, GelisTarihi
FROM HastaKayit ORDER BY KisiNumara
"@
$rd = $cmd.ExecuteReader()

$patients = New-Object System.Collections.Generic.List[object]
$stat = [ordered]@{ total = 0; tcknValid = 0; tcknInvalidKept = 0; tcknMissing = 0; noName = 0; withPhone = 0; withBirth = 0 }

while ($rd.Read()) {
    $stat.total++
    $name = TitleTr (Clean $rd["AdSoyad"])
    if ($null -eq $name) { $stat.noName++; continue }   # cannot import a nameless patient

    # A trailing "(...)" note on the name -> move into notes, keep the name clean.
    $nameNote = $null
    $m = [System.Text.RegularExpressions.Regex]::Match($name, "\s*\(([^)]*)\)\s*$")
    if ($m.Success) { $nameNote = $m.Groups[1].Value.Trim(); $name = $name.Substring(0, $m.Index).Trim() }

    $tcknRaw = DigitsOnly $rd["TCKN"]
    $tckn = $null
    if ($tcknRaw -and (Test-Tckn $tcknRaw)) { $tckn = $tcknRaw; $stat.tcknValid++ }
    elseif ($tcknRaw) { $stat.tcknInvalidKept++; $stat.tcknMissing++ }   # invalid -> dropped, patient flagged
    else { $stat.tcknMissing++ }

    $phone = NormPhone $rd["CepTelefonu"]
    if ($phone) { $stat.withPhone++ }

    # Address = street + district + city, de-duplicated.
    $addrParts = @((Clean $rd["Adres"]), (Clean $rd["ilcesi"]), (Clean $rd["Sehir"])) | Where-Object { $_ }
    $address = if ($addrParts.Count) { Cap ((($addrParts | Select-Object -Unique) -join ", ")) 300 } else { $null }

    # Notes = old free-text note + landline (not dialable without area code) + name parenthetical.
    $noteParts = New-Object System.Collections.Generic.List[string]
    $ac = Clean $rd["Aciklama"]; if ($ac) { $noteParts.Add($ac) }
    if ($nameNote) { $noteParts.Add($nameNote) }
    $ev = Clean $rd["EvTelefonu"]; if ($ev) { $noteParts.Add("Ev tel (eski kayit): $ev") }
    $notes = if ($noteParts.Count) { Cap (($noteParts -join " | ")) 2000 } else { $null }

    $birth = SaneDate $rd["DogumTarihi"]; if ($birth) { $stat.withBirth++ }
    # AnamnezBilgisi is deliberately NOT imported: in this database it is a running
    # treatment/payment log, which is out of scope for a patients-only import.

    $patients.Add([ordered]@{
        legacyKey  = [int]$rd["KisiNumara"]
        fullName   = Cap $name 100
        tckn       = $tckn
        phone      = Cap $phone 20
        birthDate  = $birth
        arrivalDate= SaneDate $rd["GelisTarihi"]
        address    = $address
        notes      = $notes
    })
}
$rd.Close(); $cn.Close()

$json = [ordered]@{ generatedAt = (Get-Date).ToString("s"); source = $AccessPath; count = $patients.Count; patients = $patients }
$out = $json | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($OutPath, $out, (New-Object System.Text.UTF8Encoding($false)))

Write-Output "Wrote $($patients.Count) patients to $OutPath"
$stat.GetEnumerator() | ForEach-Object { Write-Output ("  {0,-16} {1}" -f $_.Key, $_.Value) }

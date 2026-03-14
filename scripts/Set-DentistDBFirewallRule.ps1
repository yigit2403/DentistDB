param(
    [Parameter(Mandatory = $true)]
    [int]$Port,

    [string]$RuleName = "DentistDB Private Access",
    [string[]]$InterfaceAlias = @("Tailscale", "ZeroTier One")
)

$existingRule = Get-NetFirewallRule -DisplayName $RuleName -ErrorAction SilentlyContinue
if ($existingRule) {
    Remove-NetFirewallRule -DisplayName $RuleName
}

New-NetFirewallRule `
    -DisplayName $RuleName `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort $Port `
    -Profile Private `
    -InterfaceAlias $InterfaceAlias

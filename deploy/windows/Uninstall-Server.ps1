<#
.SYNOPSIS
  Removes the Burhan Nexus HMS Windows service and firewall rule.
  The database, backups and the installation folder are NOT deleted (hospital data is always kept).
#>
param([string]$ServiceName = "BurhanNexusHMS", [int]$Port = 5000)
$ErrorActionPreference = "Stop"
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service $ServiceName -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Service removed."
}
Get-NetFirewallRule -DisplayName "Burhan Nexus HMS (TCP $Port)" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
Write-Host "Done. The PostgreSQL database and the folders 'data' and 'backups' were kept."

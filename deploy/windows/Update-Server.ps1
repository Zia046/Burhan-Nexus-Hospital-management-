<#
.SYNOPSIS
  Updates an existing installation with a new server package. Data, backups and configuration are kept.
  A database backup is strongly recommended before updating (Backup & Restore > Create backup now).
#>
param([string]$InstallDir = "C:\BurhanNexusHMS", [string]$ServiceName = "BurhanNexusHMS")
$ErrorActionPreference = "Stop"
$packageDir = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Stop-Service $ServiceName
Start-Sleep -Seconds 3
robocopy $packageDir $InstallDir /E /NFL /NDL /NJH /NJS /XD data backups /XF appsettings.Production.json | Out-Null
Start-Service $ServiceName
Write-Host "Updated. Database changes (if any) are applied automatically when the service starts."

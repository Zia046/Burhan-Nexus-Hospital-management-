<#
.SYNOPSIS
  Installs the Burhan Nexus Print Agent on a reception computer (for silent printing to USB / Windows thermal printers).

.DESCRIPTION
  The agent listens only on this computer (http://127.0.0.1:9123). The HMS web page sends the ESC/POS slip
  to the agent, and the agent sends it to the Windows printer queue as RAW data (no print dialog).
  Install the printer's own Windows driver first (Xprinter, Rongta, Epson, HPRT ...) or the in-box
  "Generic / Text Only" driver, and note the printer name shown in Windows "Printers & scanners".

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\deploy\windows\Install-PrintAgent.ps1 -HmsUrl "http://192.168.1.10:5000"
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "C:\BurhanNexusPrintAgent",
    [int]$Port = 9123,
    # Address of the HMS server as typed in the browser on this computer. Only this site may use the agent.
    [string[]]$HmsUrl = @(),
    [string]$ServiceName = "BurhanNexusPrintAgent",
    [switch]$NoService
)

$ErrorActionPreference = "Stop"
function Write-Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Please run PowerShell as Administrator (right-click > Run as administrator)."
}

$source = Join-Path $PSScriptRoot "..\..\tools\print-agent"
if (-not (Test-Path (Join-Path $source "BurhanNexus.PrintAgent.exe"))) { throw "Print Agent files not found in $source" }

Write-Step "Copying Print Agent to $InstallDir"
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -eq "Running") { Stop-Service $ServiceName -Force; Start-Sleep -Seconds 2 }
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $InstallDir -Recurse -Force

Write-Step "Writing configuration"
$origins = @($HmsUrl | ForEach-Object { $_.TrimEnd('/') } | Where-Object { $_ })
$config = @{ Agent = @{ Port = $Port; AllowedOrigins = $origins } } | ConvertTo-Json -Depth 4
Set-Content -Path (Join-Path $InstallDir "appsettings.Production.json") -Value $config -Encoding UTF8
if ($origins.Count -eq 0) { Write-Host "No -HmsUrl given: any site on the local network may print through this agent." -ForegroundColor Yellow }

$exe = Join-Path $InstallDir "BurhanNexus.PrintAgent.exe"
if (-not $NoService) {
    Write-Step "Registering Windows service $ServiceName"
    if (-not $existing) {
        New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName "Burhan Nexus Print Agent" -StartupType Automatic `
            -Description "Prints Burhan Nexus HMS thermal slips to local printers (127.0.0.1:$Port)." | Out-Null
    }
    sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null
    Start-Service $ServiceName
    Start-Sleep -Seconds 3
}

Write-Step "Checking the agent"
try {
    $status = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/status" -TimeoutSec 10
    Write-Host "Print Agent is running on $($status.machine). Printers found:" -ForegroundColor Green
    $status.printers | ForEach-Object { Write-Host "  - $_" }
    Write-Host "`nIn HMS: Settings > Thermal printers > choose 'Print Agent' and enter the printer name exactly as listed above."
} catch {
    Write-Host "The agent did not answer yet: $($_.Exception.Message)" -ForegroundColor Yellow
}

<#
.SYNOPSIS
  Installs Burhan Nexus Hospital Management on the hospital's central server computer.

.DESCRIPTION
  Run this script (as Administrator) from the extracted server package folder. It will:
    1. Copy the application to the installation folder.
    2. Create the PostgreSQL database and a dedicated application login (if they do not exist).
    3. Write the server configuration (appsettings.Production.json).
    4. Register and start the "BurhanNexusHMS" Windows service (starts automatically with Windows).
    5. Open the TCP port in Windows Firewall for the local network only.

  PostgreSQL 16 or newer must already be installed on this computer.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\deploy\windows\Install-Server.ps1 -PostgresPassword "postgres-admin-password"
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "C:\BurhanNexusHMS",
    [int]$Port = 5000,
    [string]$DbHost = "localhost",
    [int]$DbPort = 5432,
    [string]$DbName = "burhan_nexus_hms",
    [string]$DbUser = "bnhms_app",
    [string]$DbPassword = "",
    [Parameter(Mandatory = $true)][string]$PostgresPassword,
    [string]$PostgresAdminUser = "postgres",
    [string]$ServiceName = "BurhanNexusHMS"
)

$ErrorActionPreference = "Stop"

function Write-Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Please run PowerShell as Administrator (right-click > Run as administrator)."
}

$packageDir = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if (-not (Test-Path (Join-Path $packageDir "BurhanNexus.HMS.Web.exe"))) {
    throw "BurhanNexus.HMS.Web.exe was not found in $packageDir. Run this script from the extracted server package."
}

# ---- PostgreSQL tools ----
Write-Step "Looking for PostgreSQL"
$pgBin = Get-ChildItem "C:\Program Files\PostgreSQL" -Directory -ErrorAction SilentlyContinue |
    Sort-Object { [int]($_.Name.Split('.')[0]) } -Descending |
    ForEach-Object { Join-Path $_.FullName "bin" } |
    Where-Object { Test-Path (Join-Path $_ "psql.exe") } | Select-Object -First 1
if (-not $pgBin) { throw "PostgreSQL was not found under C:\Program Files\PostgreSQL. Install PostgreSQL 16+ first." }
$psql = Join-Path $pgBin "psql.exe"
Write-Host "Found PostgreSQL tools in $pgBin"

if ([string]::IsNullOrWhiteSpace($DbPassword)) {
    $bytes = New-Object byte[] 24
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $DbPassword = [Convert]::ToBase64String($bytes).Replace("+", "A").Replace("/", "B").Replace("=", "")
    Write-Host "Generated a strong database password for '$DbUser' (stored only in the server configuration)."
}

# ---- Database ----
Write-Step "Creating database '$DbName' and login '$DbUser' (if needed)"
$env:PGPASSWORD = $PostgresPassword
$safePwd = $DbPassword.Replace("'", "''")
$roleExists = & $psql -h $DbHost -p $DbPort -U $PostgresAdminUser -d postgres -tAc "SELECT 1 FROM pg_roles WHERE rolname='$DbUser'"
if ($LASTEXITCODE -ne 0) { throw "Could not connect to PostgreSQL as '$PostgresAdminUser'. Check the password." }
if ($roleExists -ne "1") {
    & $psql -h $DbHost -p $DbPort -U $PostgresAdminUser -d postgres -c "CREATE ROLE `"$DbUser`" LOGIN PASSWORD '$safePwd'" | Out-Null
} else {
    & $psql -h $DbHost -p $DbPort -U $PostgresAdminUser -d postgres -c "ALTER ROLE `"$DbUser`" WITH LOGIN PASSWORD '$safePwd'" | Out-Null
}
$dbExists = & $psql -h $DbHost -p $DbPort -U $PostgresAdminUser -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$DbName'"
if ($dbExists -ne "1") {
    & $psql -h $DbHost -p $DbPort -U $PostgresAdminUser -d postgres -c "CREATE DATABASE `"$DbName`" OWNER `"$DbUser`" ENCODING 'UTF8'" | Out-Null
    Write-Host "Database created."
} else {
    Write-Host "Database already exists - existing data is kept."
}
Remove-Item Env:\PGPASSWORD

# ---- Files ----
Write-Step "Copying application to $InstallDir"
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq "Running") { Stop-Service $ServiceName; Start-Sleep -Seconds 3 }
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
# Never overwrite server data, backups or configuration of an existing installation.
robocopy $packageDir $InstallDir /E /NFL /NDL /NJH /NJS /XD data backups /XF appsettings.Production.json | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $InstallDir "data"), (Join-Path $InstallDir "backups") | Out-Null

# ---- Configuration ----
$configPath = Join-Path $InstallDir "appsettings.Production.json"
if (-not (Test-Path $configPath)) {
    Write-Step "Writing server configuration"
    $config = [ordered]@{
        ConnectionStrings = @{ HmsDatabase = "Host=$DbHost;Port=$DbPort;Database=$DbName;Username=$DbUser;Password=$DbPassword" }
        Kestrel = @{ Endpoints = @{ Http = @{ Url = "http://0.0.0.0:$Port" } } }
        Hms = @{ PostgresBinDirectory = $pgBin; DataDirectory = "data"; BackupDirectory = "backups" }
    }
    $config | ConvertTo-Json -Depth 6 | Set-Content -Path $configPath -Encoding UTF8
    # Only Administrators and SYSTEM may read the configuration (it contains the DB password).
    icacls $configPath /inheritance:r /grant:r "Administrators:F" "SYSTEM:F" | Out-Null
} else {
    Write-Host "Keeping existing configuration $configPath"
}

# ---- Windows service ----
Write-Step "Registering Windows service '$ServiceName'"
$exe = Join-Path $InstallDir "BurhanNexus.HMS.Web.exe"
if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName "Burhan Nexus Hospital Management" `
        -Description "Burhan Nexus Hospital Management server (web application on port $Port)" -StartupType Automatic | Out-Null
    sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
}
Start-Service $ServiceName

# ---- Firewall ----
Write-Step "Allowing TCP port $Port on the local network"
$ruleName = "Burhan Nexus HMS (TCP $Port)"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -Profile Domain, Private | Out-Null
}

# ---- Check ----
Write-Step "Waiting for the server to start"
$ok = $false
for ($i = 0; $i -lt 45; $i++) {
    try { if ((Invoke-WebRequest -UseBasicParsing "http://localhost:$Port/health" -TimeoutSec 3).StatusCode -eq 200) { $ok = $true; break } } catch { }
    Start-Sleep -Seconds 2
}
if (-not $ok) { Write-Warning "The server did not answer yet. Check the Windows Event Viewer (Application log) and $InstallDir."; exit 1 }

$ips = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } | Select-Object -ExpandProperty IPAddress
Write-Host "`nBurhan Nexus Hospital Management is running." -ForegroundColor Green
Write-Host "On this server open:          http://localhost:$Port"
foreach ($ip in $ips) { Write-Host "On other computers open:      http://${ip}:$Port" }
Write-Host "`nThe first time, the Initial Setup Wizard will open to create the hospital profile and the administrator."

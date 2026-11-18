<#
.SYNOPSIS
  One-click installation of a NEW Burhan Nexus HMS server (started by INSTALL.cmd - no typing of commands).

.DESCRIPTION
  Put these files next to INSTALL.cmd (the extracted server package folder) - or in a "prereqs" sub-folder:
    * vc_redist.x64.exe                                   (Microsoft Visual C++ runtime, x64)
    * postgresql-16.x-windows-x64-binaries.zip   (recommended)  OR  postgresql-16.x-windows-x64.exe (installer)
  Then double-click INSTALL.cmd. The script:
    1. unblocks the downloaded files,
    2. installs the Visual C++ runtime if it is missing,
    3. installs PostgreSQL if it is missing (zip: unpacked + Windows service; installer: unattended mode),
    4. runs Install-Server.ps1 (database, application, Windows service, firewall),
    5. sets the network to "Private" so other computers can connect, and opens the setup wizard.
  Everything is written to install-log.txt next to INSTALL.cmd - send this file if something fails.
#>
param(
    [string]$PostgresPassword = "BCGH",
    [int]$Port = 5000,
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$log = Join-Path $root "install-log.txt"
try { Start-Transcript -Path $log -Force | Out-Null } catch { }

function Step($n, $text) { Write-Host "`n[$n/5] $text" -ForegroundColor Cyan }
function Ok($text) { Write-Host "      OK - $text" -ForegroundColor Green }
function Fail($text) {
    Write-Host "`n  ROKA GAYA / STOPPED: $text" -ForegroundColor Red
    Write-Host "  Log file: $log  (yeh file Burhan Nexus ko bhej dein)" -ForegroundColor Yellow
    try { Stop-Transcript | Out-Null } catch { }
    exit 1
}
function FindFile([string[]]$patterns) {
    foreach ($dir in @($root, (Join-Path $root "prereqs"), (Split-Path $root -Parent))) {
        if (-not (Test-Path $dir)) { continue }
        foreach ($p in $patterns) {
            $f = Get-ChildItem -Path $dir -Filter $p -File -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
            if ($f) { return $f.FullName }
        }
    }
    return $null
}
function PgBin {
    Get-ChildItem "C:\Program Files\PostgreSQL" -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName "bin" } | Where-Object { Test-Path (Join-Path $_ "psql.exe") } | Select-Object -First 1
}
function PortOpen([int]$p) {
    try { $c = New-Object Net.Sockets.TcpClient; $c.Connect("127.0.0.1", $p); $c.Close(); return $true } catch { return $false }
}

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { Fail "Administrator ki ijazat nahi mili. INSTALL.cmd par double-click karein aur 'Yes' dabayein." }
if (-not [Environment]::Is64BitOperatingSystem) { Fail "Yeh Windows 32-bit hai. 64-bit Windows 10/11 zaroori hai." }

Write-Host "Burhan Nexus HMS - naye server ki installation" -ForegroundColor White
Write-Host "Folder: $root"

# ---- 1. Unblock downloaded files (zip from the internet carries a 'blocked' mark) ----
Step 1 "Files unblock"
Get-ChildItem -Path $root -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue
$prereqs = Join-Path $root "prereqs"
if (Test-Path $prereqs) { Get-ChildItem $prereqs -File | Unblock-File -ErrorAction SilentlyContinue }
Ok "done"

# ---- 2. Visual C++ runtime ----
Step 2 "Visual C++ runtime (x64)"
function VcInstalled {
    foreach ($k in "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64", "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64") {
        $v = Get-ItemProperty -Path $k -ErrorAction SilentlyContinue
        if ($v -and $v.Installed -eq 1) { return $true }
    }
    return (Test-Path "$env:WINDIR\System32\vcruntime140.dll") -and (Test-Path "$env:WINDIR\System32\msvcp140.dll")
}
if (VcInstalled) { Ok "already installed" }
else {
    $vc = FindFile @("vc_redist.x64.exe", "vc_redist*x64*.exe", "VC_redist.x64*.exe")
    if (-not $vc) {
        try {
            Write-Host "      vc_redist.x64.exe folder mein nahi - internet se download ki koshish..."
            $vc = Join-Path $env:TEMP "vc_redist.x64.exe"
            Invoke-WebRequest -UseBasicParsing -Uri "https://aka.ms/vs/17/release/vc_redist.x64.exe" -OutFile $vc
        } catch { Fail "Visual C++ nahi mila. vc_redist.x64.exe ko INSTALL.cmd wale folder mein rakhein (https://aka.ms/vs/17/release/vc_redist.x64.exe)." }
    }
    $tmp = Join-Path $env:TEMP "bn_vc_redist.x64.exe"
    Copy-Item $vc $tmp -Force
    $p = Start-Process -FilePath $tmp -ArgumentList "/install", "/quiet", "/norestart" -Wait -PassThru
    if ($p.ExitCode -notin 0, 1638, 3010) { Fail "Visual C++ install nahi hua (code $($p.ExitCode))." }
    Ok "installed (code $($p.ExitCode))"
}

# ---- 3. PostgreSQL ----
Step 3 "PostgreSQL database"
$bin = PgBin
if ($bin) {
    Ok "already installed: $bin"
    Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Where-Object { $_.Status -ne "Running" } | ForEach-Object {
        Set-Service $_.Name -StartupType Automatic; Start-Service $_.Name
    }
} else {
    $zip = FindFile @("postgresql-1[6-9]*-binaries.zip", "postgresql-*-windows-x64-binaries.zip")
    $exe = FindFile @("postgresql-1[6-9]*-windows-x64.exe", "postgresql-*-windows-x64.exe")
    if (-not $zip -and -not $exe) {
        Fail "PostgreSQL ki file folder mein nahi. 'postgresql-16...-windows-x64-binaries.zip' (behtar) ya 'postgresql-16...-windows-x64.exe' ko INSTALL.cmd wale folder mein rakhein."
    }
    if ($zip) {
        Write-Host "      zip se install: $(Split-Path $zip -Leaf)"
        $target = "C:\Program Files\PostgreSQL\16"
        $tmpDir = Join-Path $env:TEMP "bn_pgsql"
        Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
        Expand-Archive -Path $zip -DestinationPath $tmpDir -Force
        $src = Get-ChildItem $tmpDir -Directory | Where-Object { Test-Path (Join-Path $_.FullName "bin\initdb.exe") } | Select-Object -First 1
        if (-not $src) { Fail "zip ke andar 'pgsql\bin\initdb.exe' nahi mila - sahi PostgreSQL binaries zip download karein." }
        New-Item -ItemType Directory -Force -Path "C:\Program Files\PostgreSQL" | Out-Null
        if (Test-Path $target) { Fail "$target pehle se mojood hai magar adhoora hai. Isay delete kar ke dobara chalayein." }
        Move-Item $src.FullName $target
        $data = Join-Path $target "data"
        $pw = Join-Path $env:TEMP "bn_pgpw.txt"
        [IO.File]::WriteAllText($pw, $PostgresPassword)
        & (Join-Path $target "bin\initdb.exe") -D $data -U postgres -E UTF8 "--pwfile=$pw" -A scram-sha-256 2>&1 | Write-Host
        $code = $LASTEXITCODE
        Remove-Item $pw -Force -ErrorAction SilentlyContinue
        if ($code -ne 0) { Fail "initdb nakam (code $code). Agar 'VCRUNTIME140.dll' ka zikr hai to Visual C++ dobara install karein." }
        icacls $data /grant "NT AUTHORITY\NetworkService:(OI)(CI)F" /T /Q | Out-Null
        & (Join-Path $target "bin\pg_ctl.exe") register -N "postgresql-x64-16" -D $data -S auto -U "NT AUTHORITY\NetworkService" 2>&1 | Write-Host
        Start-Service "postgresql-x64-16"
        Ok "installed from zip as Windows service 'postgresql-x64-16'"
    } else {
        Write-Host "      installer se install (khamoshi se, 3-10 minute): $(Split-Path $exe -Leaf)"
        $tmp = Join-Path $env:TEMP "bn_pg_setup.exe"
        Copy-Item $exe $tmp -Force
        Unblock-File $tmp -ErrorAction SilentlyContinue
        try {
            $p = Start-Process -FilePath $tmp -ArgumentList "--mode", "unattended", "--unattendedmodeui", "minimal", "--superpassword", "`"$PostgresPassword`"", "--serverport", "5432", "--disable-components", "stackbuilder" -Wait -PassThru
        } catch { Fail "PostgreSQL installer chal nahi saka ($($_.Exception.Message)). Iske bajaye 'postgresql-16...-windows-x64-binaries.zip' folder mein rakhein aur dobara chalayein." }
        if ($p.ExitCode -ne 0) { Fail "PostgreSQL installer nakam (code $($p.ExitCode)). 'binaries.zip' wala tareeqa istemal karein." }
        Ok "installed"
    }
    $bin = PgBin
    if (-not $bin) { Fail "PostgreSQL install ke baad bhi psql.exe nahi mila." }
}
for ($i = 0; $i -lt 30 -and -not (PortOpen 5432); $i++) { Start-Sleep -Seconds 2 }
if (-not (PortOpen 5432)) { Fail "PostgreSQL port 5432 par nahi chal raha. Services mein 'postgresql' service start karein." }
Ok "PostgreSQL is running on port 5432"

# ---- 4. Burhan Nexus HMS ----
Step 4 "Burhan Nexus HMS (database, Windows service, firewall)"
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "Install-Server.ps1") -PostgresPassword $PostgresPassword -Port $Port
if ($LASTEXITCODE -ne 0) {
    Fail "Software install nakam. Agar 'Could not connect to PostgreSQL' likha hai to PostgreSQL ka password ghalat hai - INSTALL.cmd dobara chalayein aur sahi password likhein."
}
Ok "server installed"

# ---- 5. Network + open the setup wizard ----
Step 5 "Network (Private) + setup wizard"
try {
    Get-NetConnectionProfile | Where-Object { $_.NetworkCategory -eq "Public" } | ForEach-Object {
        Set-NetConnectionProfile -InterfaceIndex $_.InterfaceIndex -NetworkCategory Private
        Write-Host "      '$($_.Name)' network ko Private kar diya (dosre computer jud saken)"
    }
} catch { Write-Host "      Network ko khud Private karein: Settings > Network > Properties > Private" -ForegroundColor Yellow }
$ips = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } | Select-Object -ExpandProperty IPAddress
Write-Host "`n=====================================================" -ForegroundColor Green
Write-Host " INSTALLATION MUKAMMAL / COMPLETE" -ForegroundColor Green
Write-Host " Is computer par:      http://localhost:$Port" -ForegroundColor Green
foreach ($ip in $ips) { Write-Host " Dosre computers par:  http://${ip}:$Port" -ForegroundColor Green }
Write-Host " Ab browser mein Setup Wizard bharein (hospital ka naam, Admin account)." -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green
if (-not $NoBrowser) { Start-Process "http://localhost:$Port" }
try { Stop-Transcript | Out-Null } catch { }
exit 0

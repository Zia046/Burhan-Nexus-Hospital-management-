@echo off
rem Burhan Nexus HMS - shows whether the database and the server are running and how other computers connect.
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$p = (Get-Content (Join-Path '%~dp0..' 'appsettings.Production.json') -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json).Kestrel.Endpoints.Http.Url -replace '.*:',''; if (-not $p) { $p = 5000 };" ^
  "Get-Service postgresql-x64-16, BurhanNexusHMS -ErrorAction SilentlyContinue | Format-Table @{n='Service';e={$_.DisplayName}}, Status, StartType -AutoSize;" ^
  "try { $h = Invoke-WebRequest -UseBasicParsing ('http://localhost:' + $p + '/health') -TimeoutSec 5; Write-Host ('Server answer: ' + $h.StatusCode + ' ' + $h.Content) -ForegroundColor Green } catch { Write-Host ('Server is NOT answering: ' + $_.Exception.Message) -ForegroundColor Red };" ^
  "Write-Host ''; Write-Host 'Other computers connect to:'; Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | ForEach-Object { Write-Host ('   http://' + $_.IPAddress + ':' + $p) }"
echo.
pause

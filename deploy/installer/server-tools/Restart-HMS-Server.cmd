@echo off
rem Burhan Nexus HMS - restart the server (database stays running). Asks for administrator permission.
net session >nul 2>&1 || (powershell -NoProfile -Command "Start-Process -Verb RunAs -FilePath '%~f0'" & exit /b)
echo Restarting Burhan Nexus HMS server...
net stop BurhanNexusHMS
net start BurhanNexusHMS
echo.
echo Done. Clients reconnect automatically.
timeout /t 5

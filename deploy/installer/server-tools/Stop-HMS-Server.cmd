@echo off
rem Burhan Nexus HMS - stop the server safely (for maintenance). Clients show the reconnect screen meanwhile.
net session >nul 2>&1 || (powershell -NoProfile -Command "Start-Process -Verb RunAs -FilePath '%~f0'" & exit /b)
echo Stopping Burhan Nexus HMS server (the database keeps running)...
net stop BurhanNexusHMS
echo Stopped. Use "Start HMS server" to start it again (it also starts with Windows).
timeout /t 8

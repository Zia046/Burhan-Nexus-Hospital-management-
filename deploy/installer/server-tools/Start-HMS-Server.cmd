@echo off
rem Burhan Nexus HMS - start the database and the server. Asks for administrator permission.
net session >nul 2>&1 || (powershell -NoProfile -Command "Start-Process -Verb RunAs -FilePath '%~f0'" & exit /b)
net start postgresql-x64-16 2>nul
net start BurhanNexusHMS
timeout /t 5

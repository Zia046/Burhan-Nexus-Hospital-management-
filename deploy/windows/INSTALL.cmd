@echo off
title Burhan Nexus HMS - Installation
cd /d "%~dp0"
rem ---- Ask Windows for Administrator rights (a "Yes" prompt appears) ----
net session >nul 2>&1
if errorlevel 1 (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
cls
echo.
echo  =====================================================
echo    BURHAN NEXUS HMS  -  NAYE SERVER KI INSTALLATION
echo  =====================================================
echo.
echo  Is folder mein yeh files honi chahiye:
echo    1. vc_redist.x64.exe
echo    2. postgresql-16...-windows-x64-binaries.zip   (ya postgresql-16...-windows-x64.exe)
echo.
echo  PostgreSQL ka password likhein. Sirf Enter dabayein to password BCGH hoga.
echo  (Agar PostgreSQL pehle se install hai to wohi password likhein jo install karte waqt rakha tha.)
echo.
set "PGPW=BCGH"
set /p "PGPW=  Password [BCGH]: "
if "%PGPW%"=="" set "PGPW=BCGH"
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\windows\Easy-Install.ps1" -PostgresPassword "%PGPW%"
echo.
echo  Log file: %~dp0install-log.txt
pause

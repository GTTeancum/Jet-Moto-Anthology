@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Textures.ps1" %*
set "Result=%ERRORLEVEL%"
echo.
pause
exit /b %Result%

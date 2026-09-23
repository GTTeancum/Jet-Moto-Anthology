@echo off
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Collect-Logs.ps1" %*
set "RESULT=%ERRORLEVEL%"
if not defined JETMOTO_NO_PAUSE pause
exit /b %RESULT%

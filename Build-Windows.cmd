@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Windows.ps1" %*
set "RESULT=%ERRORLEVEL%"
echo.
if "%RESULT%"=="0" (echo Processing complete. Press any key to close the application.) else (echo Build failed. Run Collect-Logs.cmd and upload the ZIP.)
if not defined JETMOTO_NO_PAUSE pause >nul
exit /b %RESULT%

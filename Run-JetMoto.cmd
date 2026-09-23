@echo off
setlocal
set "GAMEDIR=D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto"
if exist "%~dp0.build\deployed-path.txt" set /p "GAMEDIR="<"%~dp0.build\deployed-path.txt"
if not exist "%GAMEDIR%\JetMoto.exe" (
  echo Build the project with Build-Windows.cmd first.
  echo Expected: "%GAMEDIR%\JetMoto.exe"
  pause
  exit /b 1
)
start "" "%GAMEDIR%\JetMoto.exe" %*
exit /b %ERRORLEVEL%

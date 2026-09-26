@echo off
setlocal
rem Launch the current isolated visual candidate with all tracks selectable.
set "GAME=%~dp0.build\ui-buoy-bin\Release\net10.0\win-x64\JetMoto.exe"
set "DISC=%~dp0..\Jet Moto\Jet Moto (USA).cue"
if not exist "%GAME%" (
  echo Visual candidate is missing: "%GAME%"
  exit /b 1
)
if not exist "%DISC%" (
  echo Original game CUE is missing: "%DISC%"
  exit /b 1
)
set "JETMOTO_WORLD_WAKE=1"
set "JETMOTO_MENU_BACKGROUNDS=1"
set "JETMOTO_HEADLESS="
set "JETMOTO_MUTE="
set "JETMOTO_REPLAY="
set "JETMOTO_CAPTURE_DIR="
"%GAME%" --disc "%DISC%" --unlockall %*
exit /b %ERRORLEVEL%

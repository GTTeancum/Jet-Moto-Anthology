@echo off
setlocal
rem Launch the deployed user-facing game with all tracks selectable.
set "GAME=%~dp0..\Jet Moto\JetMoto.exe"
set "DISC=%~dp0..\Jet Moto\Jet Moto (USA).cue"
if not exist "%GAME%" (
  echo Deployed game is missing: "%GAME%"
  exit /b 1
)
if not exist "%DISC%" (
  echo Original game CUE is missing: "%DISC%"
  exit /b 1
)
set "JETMOTO_WORLD_WAKE="
set "JETMOTO_MENU_BACKGROUNDS="
set "JETMOTO_HEADLESS="
set "JETMOTO_MUTE="
set "JETMOTO_REPLAY="
set "JETMOTO_CAPTURE_DIR="
"%GAME%" --unlockall %*
exit /b %ERRORLEVEL%

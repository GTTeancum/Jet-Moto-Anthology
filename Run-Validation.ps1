param([string]$Name = 'baseline', [int]$Seconds = 110,
      [string]$Replay = 'reports\build12\baseline-replay.json',
      [ValidateRange(1,3600)][int]$CaptureEvery = 120,
      [long]$CaptureStart = 0, [long]$CaptureEnd = [long]::MaxValue,
      [long]$CaptureRaceStart = -1, [long]$CaptureRaceEnd = -1,
      [ValidateRange(0,8)][int]$EffectCaptureFrames = 0,
      [string]$BuildOutputRoot,
      [switch]$WorldWake,
      [switch]$ExtendedWaterLod,
      [switch]$Capture)
$ErrorActionPreference = 'Stop'
$reportDirectory = Join-Path $PSScriptRoot "reports\build12\$Name"
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$env:JETMOTO_CAPTURE_DIR = if ($Capture) { $reportDirectory } else { $null }
$env:JETMOTO_CAPTURE_EVERY = [string]$CaptureEvery
$raceRelative=$CaptureRaceStart -ge 0 -or $CaptureRaceEnd -ge 0
if($raceRelative -and ($CaptureRaceStart -lt 0 -or $CaptureRaceEnd -lt $CaptureRaceStart)){throw 'Race-relative capture needs an ordered nonnegative start/end.'}
$env:JETMOTO_CAPTURE_RACE_RELATIVE = if($raceRelative){'1'}else{$null}
if($raceRelative){$env:JETMOTO_CAPTURE_START=[string]$CaptureRaceStart;$env:JETMOTO_CAPTURE_END=[string]$CaptureRaceEnd}
else{$env:JETMOTO_CAPTURE_START=[string]$CaptureStart;$env:JETMOTO_CAPTURE_END=[string]$CaptureEnd}
$env:JETMOTO_CAPTURE_EFFECT_FRAMES = [string]$EffectCaptureFrames
$env:JETMOTO_REPLAY = Join-Path $PSScriptRoot $Replay
$env:JETMOTO_WORLD_WAKE = if ($WorldWake) { '1' } else { $null }
$env:JETMOTO_EXTEND_WATER_LOD = if ($ExtendedWaterLod) { '1' } else { $null }
$app = Join-Path $PSScriptRoot 'JetMoto\bin\Release\net10.0\win-x64\JetMoto.dll'
$runtime = Join-Path (Split-Path $app) 'RecompOne.Runtime.dll'
$builtRuntime = Join-Path $PSScriptRoot 'vendor\RecompOne\RecompOne.Runtime\bin\Release\net10.0\RecompOne.Runtime.dll'
if($BuildOutputRoot) {
    $output=[IO.Path]::GetFullPath($BuildOutputRoot)
    $app=Join-Path $output 'Release\net10.0\win-x64\JetMoto.dll'
    $runtime=Join-Path (Split-Path $app) 'RecompOne.Runtime.dll'
    $builtRuntime=Join-Path $output 'Release\net10.0\RecompOne.Runtime.dll'
}
$runtimeHash = (Get-FileHash -LiteralPath $runtime -Algorithm SHA256).Hash
if ($runtimeHash -ne (Get-FileHash -LiteralPath $builtRuntime -Algorithm SHA256).Hash) {
    throw 'The game runtime differs from the current runtime build. Finish the game build before validation.'
}
[ordered]@{
    app = $app
    appSha256 = (Get-FileHash -LiteralPath $app -Algorithm SHA256).Hash
    runtimeSha256 = $runtimeHash
    replaySha256 = (Get-FileHash -LiteralPath $env:JETMOTO_REPLAY -Algorithm SHA256).Hash
    headless = $true
    mute = $true
    worldWake = [bool]$WorldWake
    extendedWaterLod = [bool]$ExtendedWaterLod
    seconds = $Seconds
    capture = [bool]$Capture
    captureStart = $CaptureStart
    captureEnd = $CaptureEnd
    captureRaceStart = if($raceRelative){$CaptureRaceStart}else{$null}
    captureRaceEnd = if($raceRelative){$CaptureRaceEnd}else{$null}
    effectCaptureFrames = $EffectCaptureFrames
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reportDirectory 'validation-run.json')
& 'C:\Programming\JetMoto-RecompOne-Input\cache\dotnet-win-10.0.401\dotnet.exe' $app `
    'D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\Jet Moto (USA).cue' `
    --smoke-seconds $Seconds --no-dialogs --no-trace --headless --mute
$code = $LASTEXITCODE
Copy-Item -LiteralPath (Join-Path (Split-Path $app) 'logs\last-run.log') -Destination (Join-Path $reportDirectory 'run.log')
if ($code -ne 3) { throw "Validation run ended with unexpected code $code" }

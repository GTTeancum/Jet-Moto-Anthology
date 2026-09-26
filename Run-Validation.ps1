param([string]$Name = 'baseline', [int]$Seconds = 110,
      [string]$Replay = 'reports\build12\baseline-replay.json',
      [ValidateRange(1,3600)][int]$CaptureEvery = 120,
      [long]$CaptureStart = 0, [long]$CaptureEnd = [long]::MaxValue,
      [long]$CaptureRaceStart = -1, [long]$CaptureRaceEnd = -1,
      [ValidateRange(0,8)][int]$EffectCaptureFrames = 0,
      [string]$BuildOutputRoot,
      [switch]$WorldWake,
      [switch]$ExtendedWaterLod,
      [switch]$MenuBackgrounds,
      [switch]$DisposableCards,
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
$env:JETMOTO_MENU_BACKGROUNDS = if ($MenuBackgrounds) { '1' } else { $null }
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
    menuBackgrounds = [bool]$MenuBackgrounds
    disposableCards = [bool]$DisposableCards
    seconds = $Seconds
    capture = [bool]$Capture
    captureStart = $CaptureStart
    captureEnd = $CaptureEnd
    captureRaceStart = if($raceRelative){$CaptureRaceStart}else{$null}
    captureRaceEnd = if($raceRelative){$CaptureRaceEnd}else{$null}
    effectCaptureFrames = $EffectCaptureFrames
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reportDirectory 'validation-run.json')
$settingsPath=Join-Path (Split-Path $app) 'settings.json'
$settingsBytes=$null
try {
    if($DisposableCards) {
        if(-not $BuildOutputRoot){throw 'Disposable-card validation requires an explicit isolated BuildOutputRoot.'}
        $cards=Join-Path $reportDirectory 'disposable-cards'
        if(Test-Path -LiteralPath $cards){throw 'Use a new validation name; disposable cards must not be reused.'}
        $original=[IO.File]::ReadAllBytes($settingsPath)
        $settings=[Text.Encoding]::UTF8.GetString($original) | ConvertFrom-Json
        $settings.CardAPath=Join-Path $cards 'carda.sav'
        $settings.CardBPath=Join-Path $cards 'cardb.sav'
        $settings.CardAEnabled=$true
        $settings.CardBEnabled=$true
        New-Item -ItemType Directory -Path $cards | Out-Null
        $settingsBytes=$original
        [IO.File]::WriteAllText($settingsPath,($settings | ConvertTo-Json -Depth 100))
    }
    & 'C:\Programming\JetMoto-RecompOne-Input\cache\dotnet-win-10.0.401\dotnet.exe' $app `
        'D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\Jet Moto (USA).cue' `
        --smoke-seconds $Seconds --no-dialogs --no-trace --headless --mute
    $code = $LASTEXITCODE
}
finally {
    if($null -ne $settingsBytes){[IO.File]::WriteAllBytes($settingsPath,$settingsBytes)}
}
Copy-Item -LiteralPath (Join-Path (Split-Path $app) 'logs\last-run.log') -Destination (Join-Path $reportDirectory 'run.log')
if ($code -ne 3) { throw "Validation run ended with unexpected code $code" }

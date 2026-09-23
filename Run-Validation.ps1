param([string]$Name = 'baseline', [int]$Seconds = 110,
      [string]$Replay = 'reports\build12\baseline-replay.json',
      [ValidateRange(1,3600)][int]$CaptureEvery = 120,
      [long]$CaptureStart = 0, [long]$CaptureEnd = [long]::MaxValue,
      [ValidateRange(0,8)][int]$EffectCaptureFrames = 0,
      [switch]$Capture)
$ErrorActionPreference = 'Stop'
$reportDirectory = Join-Path $PSScriptRoot "reports\build12\$Name"
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$env:JETMOTO_CAPTURE_DIR = if ($Capture) { $reportDirectory } else { $null }
$env:JETMOTO_CAPTURE_EVERY = [string]$CaptureEvery
$env:JETMOTO_CAPTURE_START = [string]$CaptureStart
$env:JETMOTO_CAPTURE_END = [string]$CaptureEnd
$env:JETMOTO_CAPTURE_EFFECT_FRAMES = [string]$EffectCaptureFrames
$env:JETMOTO_REPLAY = Join-Path $PSScriptRoot $Replay
$app = Join-Path $PSScriptRoot 'JetMoto\bin\Release\net10.0\win-x64\JetMoto.dll'
$runtime = Join-Path (Split-Path $app) 'RecompOne.Runtime.dll'
$builtRuntime = Join-Path $PSScriptRoot 'vendor\RecompOne\RecompOne.Runtime\bin\Release\net10.0\RecompOne.Runtime.dll'
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
    seconds = $Seconds
    capture = [bool]$Capture
    captureStart = $CaptureStart
    captureEnd = $CaptureEnd
    effectCaptureFrames = $EffectCaptureFrames
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reportDirectory 'validation-run.json')
& 'C:\Programming\JetMoto-RecompOne-Input\cache\dotnet-win-10.0.401\dotnet.exe' $app `
    'D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\Jet Moto (USA).cue' `
    --smoke-seconds $Seconds --no-dialogs --no-trace --headless --mute
$code = $LASTEXITCODE
Copy-Item -LiteralPath (Join-Path (Split-Path $app) 'logs\last-run.log') -Destination (Join-Path $reportDirectory 'run.log')
if ($code -ne 3) { throw "Validation run ended with unexpected code $code" }

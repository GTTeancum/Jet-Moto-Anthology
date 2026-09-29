param([Parameter(Mandatory)][string]$Name,
      [string]$AppDirectory = '.build/release-gates-stage',
      [string]$Replay = 'Validation/Replays/baseline-replay.json',
      [int]$Seconds = 90,
      [ValidateSet('race','global')][string]$CaptureClock = 'race',
      [int]$CaptureStart = 0, [int]$CaptureEnd = 950, [int]$CaptureEvery = 450,
      [switch]$AdjacentDisc,
      [switch]$UnlockAll,
      [switch]$AudioOutput)
$ErrorActionPreference = 'Stop'
$appRoot = (Resolve-Path -LiteralPath $AppDirectory).Path
$report = Join-Path $PSScriptRoot "reports/build12/$Name"
if(Test-Path -LiteralPath $report){throw 'Use a fresh report name.'}
foreach($required in @('JetMoto.exe','Textures/Native4x-Test/pack-manifest.json','Lighting/catalog.json')){
    if(!(Test-Path -LiteralPath (Join-Path $appRoot $required))){throw "Missing player artifact: $required"}
}
New-Item -ItemType Directory -Path $report | Out-Null
$env:JETMOTO_WORLD_WAKE=$null
$env:JETMOTO_MENU_BACKGROUNDS=$null
$env:JETMOTO_REPLAY=(Resolve-Path -LiteralPath $Replay).Path
$env:JETMOTO_CAPTURE_DIR=$report
$env:JETMOTO_CAPTURE_RACE_RELATIVE=if($CaptureClock -eq 'race'){'1'}else{$null}
$env:JETMOTO_CAPTURE_START=[string]$CaptureStart
$env:JETMOTO_CAPTURE_END=[string]$CaptureEnd
$env:JETMOTO_CAPTURE_EVERY=[string]$CaptureEvery
$env:JETMOTO_CAPTURE_EFFECT_FRAMES='0'
$exe=Join-Path $appRoot 'JetMoto.exe'
$before=@('JetMoto.exe','Textures/Native4x-Test/pack-manifest.json','Lighting/catalog.json') | ForEach-Object {
    Get-FileHash -LiteralPath (Join-Path $appRoot $_)
}
$options=@('--headless','--mute','--no-dialogs','--smoke-seconds',"$Seconds")
if($AudioOutput){
    # Hidden process-local rendering/input replay with the real sound device.
    $env:JETMOTO_HEADLESS='1'
    $env:JETMOTO_MUTE=$null
    $env:JETMOTO_AUDIO_PROBE=$null
    $env:ALSOFT_DRIVERS=$null
    $options=@('--no-dialogs','--smoke-seconds',"$Seconds")
}
if(!$AdjacentDisc){$options+=@('--disc','D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\Jet Moto (USA).cue')}
if($UnlockAll){$options+='--unlockall'}
# This directly exercises the self-contained apphost, not the installed SDK.
# Windows GUI apphosts can return early from a bare invocation. Wait for this
# exact process and retain its exit code before inspecting logs or captures.
$quotedOptions=$options | ForEach-Object {
    if($_.Contains('"')){throw 'Unexpected quote in validation argument.'}
    '"' + $_ + '"'
}
$process=Start-Process -FilePath $exe -ArgumentList $quotedOptions -WindowStyle Hidden -Wait -PassThru `
    -RedirectStandardOutput (Join-Path $report 'console.log') -RedirectStandardError (Join-Path $report 'stderr.log')
$code=$process.ExitCode
Copy-Item -LiteralPath (Join-Path $appRoot 'logs/last-run.log') -Destination (Join-Path $report 'run.log')
[ordered]@{ exitCode=$code; defaultVisualFeatures=$true; selfContainedApphost=$true;
    adjacentDisc=[bool]$AdjacentDisc; captureClock=$CaptureClock; audioOutput=[bool]$AudioOutput;
    appHashes=$before; replaySha256=(Get-FileHash -LiteralPath $env:JETMOTO_REPLAY).Hash;
    captureFrames=@(Get-ChildItem -LiteralPath $report -File | Where-Object Extension -in '.png','.rgba' | Select-Object -ExpandProperty Name)
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $report 'validation-run.json')
if($code -ne 3){throw "Unexpected release validation exit: $code"}
Write-Host "Release validation captured in $report; inspect content before accepting."

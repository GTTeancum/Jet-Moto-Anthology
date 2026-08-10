<#
.SYNOPSIS
Build a Jet Moto port from your own disc.

.DESCRIPTION
Nothing derived from the game ships with this repository, so the recompiler has
to run against your copy here. This fetches RecompOne, applies the fork,
translates the executable off your disc into C#, and builds the port.

.EXAMPLE
.\build.ps1 -Game jm1 -Cue "D:\rips\Jet Moto (USA).cue"

.EXAMPLE
.\build.ps1 -Game jm2 -Cue "D:\rips\Jet Moto 2 (v1.1).cue" -Loose
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet('jm1', 'jm2')][string]$Game,
    [Parameter(Mandatory = $true)][string]$Cue,
    [switch]$Loose
)

$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot

if ($Game -eq 'jm1') {
    $proj = 'JetMoto'; $config = 'JetMoto/config/jetmoto.json'; $looseDir = 'JetMoto_loose'
} else {
    $proj = 'JetMoto2'; $config = 'JetMoto2/config/jetmoto2.json'; $looseDir = 'JetMoto2_loose'
}

if (-not (Test-Path $Cue)) {
    Write-Error "disc not found: $Cue`nYou need a bin/cue rip of your own disc; none is included here."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'dotnet SDK not found on PATH'
}

Write-Host '==> RecompOne'
& bash "$repo/tools/apply-fork.sh"
if ($LASTEXITCODE -ne 0) { Write-Error 'could not restore the RecompOne fork' }

Write-Host '==> building the recompiler'
dotnet build "$repo/tools/RecompOne" -c Release -v q --nologo
if ($LASTEXITCODE -ne 0) { Write-Error 'recompiler build failed' }

Write-Host '==> recompiling your disc''s executable'
# The config points at the disc; override it for this run without editing it.
$cfgPath = Join-Path $repo $config
$buildCfg = [IO.Path]::ChangeExtension($cfgPath, 'build.json')
$text = Get-Content -Raw $cfgPath
$json = ConvertTo-Json (Resolve-Path $Cue).Path        # quotes and escapes it
$text = [regex]::Replace($text, '("cue"\s*:\s*)"(?:[^"\\]|\\.)*"', "`$1$json", 1)
Set-Content -Path $buildCfg -Value $text -Encoding utf8

dotnet run --project "$repo/tools/RecompOne/RecompOne.Recompiler" -c Release --no-build -- $buildCfg
$rc = $LASTEXITCODE
Remove-Item $buildCfg -ErrorAction SilentlyContinue
if ($rc -ne 0) { Write-Error 'recompilation failed' }

Write-Host '==> building the port'
dotnet build "$repo/$proj/$proj.csproj" -c Release -v q --nologo
if ($LASTEXITCODE -ne 0) { Write-Error 'port build failed' }

if ($Loose) {
    Write-Host '==> extracting loose files + ogg soundtrack'
    python "$repo/tools/extract-disc.py" --cue $Cue --out (Join-Path $repo $looseDir) --force
}

Write-Host ''
Write-Host 'done. run it with:'
Write-Host "  dotnet $proj/bin/Release/net10.0/$proj.dll `"$Cue`""

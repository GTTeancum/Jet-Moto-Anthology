param([string]$Name = 'approved-baseline-regression',
      [string]$Disc = 'D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\Jet Moto (USA).cue')
$ErrorActionPreference = 'Stop'
$dotnet = 'C:\Programming\JetMoto-RecompOne-Input\cache\dotnet-win-10.0.401\dotnet.exe'
$report = Join-Path $PSScriptRoot "reports\build12\$Name"
New-Item -ItemType Directory -Force -Path $report | Out-Null
$tests = @(
    @{ Project = 'PerformanceTests'; Args = @() },
    @{ Project = 'AudioTests'; Args = @('--device') },
    @{ Project = 'TrackAccessTests'; Args = @() },
    @{ Project = 'ReplayTests'; Args = @() },
    @{ Project = 'WorldLightingTests'; Args = @() },
    @{ Project = 'RiderDetailTests'; Args = @($Disc) },
    @{ Project = 'NativeTextureTests'; Args = @() },
    @{ Project = 'NeuralPackTests'; Args = @((Join-Path $PSScriptRoot 'Textures\Native4x-Test')) },
    @{ Project = 'PerspectiveTests'; Args = @() },
    @{ Project = 'WidescreenTests'; Args = @() },
    @{ Project = 'RendererTests'; Args = @('--static-only') },
    @{ Project = 'EffectTextureTests'; Args = @() },
    @{ Project = 'LauncherTests'; Args = @() },
    @{ Project = 'Tests\CdReadRegression.csproj'; Args = @($Disc) }
)
$results = @()
foreach ($test in $tests) {
    $label = [IO.Path]::GetFileNameWithoutExtension($test.Project)
    $log = Join-Path $report ($label + '.log')
    $arguments = @('run', '--project', (Join-Path $PSScriptRoot $test.Project), '-c', 'Release', '-p:UseSharedCompilation=false')
    if ($test.Args.Count) { $arguments += '--'; $arguments += $test.Args }
    & $dotnet @arguments *> $log
    $code = $LASTEXITCODE
    $results += [pscustomobject]@{ test = $label; exitCode = $code; log = $log }
    Write-Host "$label : exit $code"
}
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $report 'summary.json')
if ($results.Where({ $_.exitCode -ne 0 }).Count) { throw "Regression failures; see $report" }

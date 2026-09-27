[CmdletBinding()]
param(
    [string]$KitRoot = 'C:\Programming\JetMoto-RecompOne-Input',
    [string]$DeployDir = 'D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto',
    [string]$Disc,
    [string]$VisualAssetRoot,
    [switch]$Recompile,
    [switch]$RunTests
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$Log = Join-Path $Root 'logs\build.log'
$OldLocation = Get-Location
$Started = Get-Date
$script:PublishStages = @()
. (Join-Path $Root 'Deployment.ps1')

function Write-Step([string]$Message) {
    $line = '[{0:HH:mm:ss}] {1}' -f (Get-Date), $Message
    Write-Host $line
    Add-Content -LiteralPath $script:Log -Value $line
}
function Invoke-Dotnet([string[]]$Arguments) {
    Write-Step ('dotnet ' + ($Arguments -join ' '))
    $previous = $ErrorActionPreference
    try {
        # Native stderr is diagnostic output. Judge native success by its exit code.
        $ErrorActionPreference = 'Continue'
        & $script:Dotnet @Arguments 2>&1 | Tee-Object -FilePath $script:Log -Append | Out-Host
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previous }
    if ($code -ne 0) { throw "dotnet exited with $code. See $script:Log" }
}
function Get-Sha256([string]$Path) {
    if (Get-Command Get-FileHash -ErrorAction SilentlyContinue) {
        return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    }
    $stream = [IO.File]::OpenRead($Path)
    try {
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '')
        } finally {
            $sha.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
}
function Publish-Game {
    # Fresh staging keeps previous logs/settings/saves out of the deployment file list.
    $script:Output = Join-Path $Root ('.build\publish-' + [Guid]::NewGuid().ToString('N'))
    $script:PublishStages += $script:Output
    Invoke-Dotnet -Arguments @('restore', $script:Project, '-r', 'win-x64', '--configfile', $script:NuGetConfig,
        '-p:SelfContained=true', '-p:PublishSingleFile=true', '-p:EnableSingleFileAnalyzer=false', '-p:NuGetAudit=false')
    Invoke-Dotnet -Arguments @('publish', $script:Project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
        '--no-restore', '-o', $script:Output, '-p:UseSharedCompilation=false', '-m:1',
        '-p:PublishSingleFile=true', '-p:EnableSingleFileAnalyzer=false',
        '-p:IncludeAllContentForSelfExtract=true', '-p:DebugType=embedded')
    if ($VisualAssetRoot) {
        & (Join-Path $Root 'Stage-ReleaseAssets.ps1') -CandidateRoot $VisualAssetRoot -PublishRoot $script:Output
    }
    $prefix = $script:Output.TrimEnd('\') + '\'
    $script:PublishedFiles = @(Get-ChildItem -LiteralPath $script:Output -File -Recurse |
        ForEach-Object { $_.FullName.Substring($prefix.Length) })
    if (@($script:PublishedFiles | Where-Object { $_ -match '\.(dll|pdb)$' }).Count) {
        throw 'Single-file publish left loose dependencies or debug symbols.'
    }
}

function Verify-GeneratedHooks {
    $patcher = Join-Path $Root 'GeneratedPatcher\GeneratedPatcher.csproj'
    Invoke-Dotnet -Arguments @('restore', $patcher, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
    Invoke-Dotnet -Arguments @('run', '--project', $patcher, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false', '--', $Root)
}

try {
    New-Item -ItemType Directory -Force -Path (Join-Path $Root 'logs'), (Join-Path $Root '.build') | Out-Null
    Set-Content -LiteralPath $Log -Value 'Jet Moto / RecompOne build 12: directional lighting, rider/bike cast shadows, moving wakes and layered non-mirror water; cumulative prior fixes'
    $DeployDir = [IO.Path]::GetFullPath($DeployDir)
    $KitRoot = (Resolve-Path -LiteralPath $KitRoot).Path
    if ($Disc) { $Disc = (Resolve-Path -LiteralPath $Disc).Path }
    if (($Recompile -or $RunTests) -and !$Disc) { throw '-Recompile and -RunTests require -Disc with the extracted USA CUE.' }
    Set-Location -LiteralPath $Root
    $Source = Join-Path $KitRoot 'bundle\RecompOne'
    $Feed = Join-Path $KitRoot 'bundle\nuget-feed'
    $InputManifest = Join-Path $KitRoot 'bundle\manifest.json'
    if (!(Test-Path -LiteralPath $Source -PathType Container) -or !(Test-Path -LiteralPath $Feed -PathType Container)) {
        throw "The original packager work folder is required: $KitRoot\bundle\RecompOne and bundle\nuget-feed. Pass -KitRoot with its real location."
    }
    if (!(Test-Path -LiteralPath $InputManifest)) { throw "Missing $InputManifest" }
    $manifest = Get-Content -Raw -LiteralPath $InputManifest | ConvertFrom-Json
    $pin = 'd81dec8c9622fdcd0865d73588a3baa8d3c3a605'
    if ($manifest.commit -ne $pin) { throw "Different RecompOne source revision: $($manifest.commit). Expected $pin." }

    # Use the already downloaded SDK, or the same exact SDK installed system-wide.
    $privateDotnet = Join-Path $KitRoot 'cache\dotnet-win-10.0.401\dotnet.exe'
    if (Test-Path -LiteralPath $privateDotnet) { $script:Dotnet = $privateDotnet }
    else {
        $command = Get-Command dotnet.exe -ErrorAction SilentlyContinue
        if (!$command) { throw 'The Windows .NET SDK used by the input packager could not be found. Keep its cache folder or make that SDK available on PATH.' }
        $script:Dotnet = $command.Source
    }
    $env:DOTNET_ROOT = Split-Path -Parent $script:Dotnet
    $env:DOTNET_CLI_HOME = Join-Path $Root '.build\dotnet-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
    $env:DOTNET_NOLOGO = '1'
    $env:NUGET_PACKAGES = Join-Path $Root '.nuget\packages'
    $version = & $script:Dotnet --version
    if ($LASTEXITCODE -ne 0 -or "$version".Trim() -ne '10.0.401') { throw "Expected SDK 10.0.401; received '$version'." }
    Write-Step "Using SDK $version at $script:Dotnet"

    # Verify the cached feed against the original packager's manifest.
    $packageHashes = Join-Path $KitRoot 'bundle\packages.sha256'
    if (!(Test-Path -LiteralPath $packageHashes)) { throw "Missing $packageHashes" }
    $count = 0
    foreach ($line in Get-Content -LiteralPath $packageHashes) {
        if ($line -match '^([0-9a-fA-F]{64})\s+(.+)$') {
            $expected = $Matches[1]
            $leaf = [IO.Path]::GetFileName($Matches[2].Trim().Replace('/', '\'))
            $file = Join-Path $Feed $leaf
            if (!(Test-Path -LiteralPath $file)) { throw "Missing dependency: $file" }
            if ((Get-Sha256 $file) -ne $expected) { throw "Dependency checksum mismatch: $leaf" }
            $count++
        }
    }
    if ($count -ne 47) { throw "Expected 47 checksummed dependencies, found $count." }
    Write-Step 'All 47 dependency checksums match.'

    $Vendor = Join-Path $Root 'vendor\RecompOne'
    $marker = Join-Path $Vendor 'jetmoto-source-commit.txt'
    if (!(Test-Path -LiteralPath $Vendor)) {
        Write-Step 'Copying the pinned source into this build folder; the original kit is left unchanged.'
        New-Item -ItemType Directory -Force -Path $Vendor | Out-Null
        Get-ChildItem -LiteralPath $Source -Force | Where-Object { $_.Name -ne '.git' } |
            Copy-Item -Destination $Vendor -Recurse -Force
        Set-Content -LiteralPath $marker -Value $pin
    } elseif (!(Test-Path -LiteralPath $marker) -or (Get-Content -Raw -LiteralPath $marker).Trim() -ne $pin) {
        throw 'The existing vendor folder is not marked as this pinned source. Move it aside rather than overwriting unknown changes.'
    }
    $patchRoot = Join-Path $Root 'upstream-patches'
    $patches = Get-Content -Raw -LiteralPath (Join-Path $patchRoot 'patch-manifest.json') | ConvertFrom-Json
    foreach ($patch in $patches.files) {
        $from = Join-Path $patchRoot $patch.path
        $to = Join-Path $Vendor $patch.path
        if ((Get-Sha256 $from) -ne $patch.afterSha256) { throw "Damaged patch: $($patch.path)" }
        if (Test-Path -LiteralPath $to) {
            $actual = Get-Sha256 $to
            if ($actual -ne $patch.beforeSha256 -and $actual -ne $patch.afterSha256) {
                throw "Local changes detected in $to. They have NOT been overwritten."
            }
        } elseif ($patch.beforeSha256) { throw "Missing upstream source: $to" }
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $to) | Out-Null
        Copy-Item -LiteralPath $from -Destination $to -Force
    }
    Write-Step 'Applied the cumulative game fixes plus original-geometry world lighting and native water rendering.'
    $escapedFeed = [Security.SecurityElement]::Escape($Feed)
    $script:NuGetConfig = Join-Path $Root '.build\NuGet.Config'
    Set-Content -LiteralPath $script:NuGetConfig -Encoding UTF8 -Value @"
<?xml version="1.0" encoding="utf-8"?>
<configuration><packageSources><clear/><add key="uploaded-offline-feed" value="$escapedFeed"/></packageSources></configuration>
"@
    $script:Project = Join-Path $Root 'JetMoto\JetMoto.csproj'
    Verify-GeneratedHooks
    Publish-Game
    $gameExe = Join-Path $script:Output 'JetMoto.exe'
    if (!(Test-Path -LiteralPath $gameExe)) { throw "Publish did not produce $gameExe" }
    if ($Disc) {
        Write-Step 'Validating the disc with the newly built launcher, without opening a graphics window.'
        # Wait for the bundled GUI apphost directly; no loose JetMoto.dll exists.
        $discCheckLog = Join-Path $Root '.build/disc-validation.log'
        $discCheck = Start-Process -FilePath (Join-Path $script:Output 'JetMoto.exe') -ArgumentList @('"' + $Disc + '"', '--validate-disc', '--no-dialogs') -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $discCheckLog
        Get-Content -LiteralPath $discCheckLog | Tee-Object -FilePath $script:Log -Append | Out-Host
        if ($discCheck.ExitCode -ne 0) { throw "Disc validation exited with $($discCheck.ExitCode)." }
    }
    if ($Recompile) {
        Write-Step 'Regenerating from the verified disc and the corrected function map.'
        $compilerProject = Join-Path $Vendor 'RecompOne.Recompiler\RecompOne.Recompiler.csproj'
        $compilerOut = Join-Path $Root '.build\recompiler'
        Invoke-Dotnet -Arguments @('restore', $compilerProject, '-r', 'win-x64', '--configfile', $script:NuGetConfig, '-p:SelfContained=true', '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('build', $compilerProject, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore', '-o', $compilerOut, '-p:UseSharedCompilation=false', '-m:1')
        $configPath = Join-Path $Root 'JetMoto\config\Jet_Moto.json'
        $tempConfig = Join-Path $Root 'JetMoto\config\.run-config.json'
        $config = Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json
        $config.cue = $Disc
        $config | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $tempConfig -Encoding UTF8
        try { Invoke-Dotnet -Arguments @((Join-Path $compilerOut 'recompone.dll'), $tempConfig) }
        finally { if (Test-Path -LiteralPath $tempConfig) { Remove-Item -LiteralPath $tempConfig } }
        Verify-GeneratedHooks
        Publish-Game
    }
    if ($RunTests) {
        Write-Step 'Checking session-only track access and normal progression defaults.'
        $trackTests = Join-Path $Root 'TrackAccessTests\TrackAccessTests.csproj'
        Invoke-Dotnet -Arguments @('restore', $trackTests, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('run', '--project', $trackTests, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false')
        Write-Step 'Checking world-camera transforms, geometry-surface provenance and safe renderer buffers.'
        $worldTests = Join-Path $Root 'WorldLightingTests\WorldLightingTests.csproj'
        Invoke-Dotnet -Arguments @('restore', $worldTests, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('run', '--project', $worldTests, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false')
        Write-Step 'Checking highest rider/bike LOD, original riding pose, expanded render buffers, and host packet precision against the supplied disc.'
        $riderTests = Join-Path $Root 'RiderDetailTests\RiderDetailTests.csproj'
        Invoke-Dotnet -Arguments @('restore', $riderTests, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('run', '--project', $riderTests, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false', '--', $Disc)
        Write-Step 'Checking original-asset 4x PNG loading, native material linkage, command reuse and transparency categories.'
        $nativeTests = Join-Path $Root 'NativeTextureTests\NativeTextureTests.csproj'
        Invoke-Dotnet -Arguments @('restore', $nativeTests, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('run', '--project', $nativeTests, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false')
        Write-Step 'Verifying every enhanced PNG through the runtime loader and original bank/ID manifest.'
        $packTests = Join-Path $Root 'NeuralPackTests\NeuralPackTests.csproj'
        Invoke-Dotnet -Arguments @('restore', $packTests, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('run', '--project', $packTests, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false', '--', (Join-Path $Root 'Textures\Native4x-Test'))
        Write-Step 'Checking mandatory perspective tracking, memory provenance, GTE precision, and packet fallback safety.'
        $precisionTests = Join-Path $Root 'PerspectiveTests\PerspectiveTests.csproj'
        Invoke-Dotnet -Arguments @('restore', $precisionTests, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('run', '--project', $precisionTests, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false')
        Write-Step 'Checking widescreen camera planes, preserved projection and menu/race scopes.'
        $wideTests = Join-Path $Root 'WidescreenTests\WidescreenTests.csproj'
        Invoke-Dotnet -Arguments @('restore', $wideTests, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('run', '--project', $wideTests, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false')
        Write-Step 'Checking permanent no-dither shader and GPU-state contracts (no graphics window).'
        $rendererTests = Join-Path $Root 'RendererTests\RendererTests.csproj'
        Invoke-Dotnet -Arguments @('restore', $rendererTests, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('run', '--project', $rendererTests, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false', '--', '--static-only')
        Write-Step 'Checking explicit particle alpha metadata and native-ID safety.'
        $effectTests = Join-Path $Root 'EffectTextureTests\EffectTextureTests.csproj'
        Invoke-Dotnet -Arguments @('restore', $effectTests, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('run', '--project', $effectTests, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false')
        Write-Step 'Checking bounded texture residency, exact reloads, camera inverses and shadow allocations.'
        $performanceTests = Join-Path $Root 'PerformanceTests\PerformanceTests.csproj'
        Invoke-Dotnet -Arguments @('restore', $performanceTests, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('run', '--project', $performanceTests, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false')
        Write-Step 'Checking audio arithmetic and queue recovery on the process-local null device.'
        $audioTests = Join-Path $Root 'AudioTests\AudioTests.csproj'
        Invoke-Dotnet -Arguments @('restore', $audioTests, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('run', '--project', $audioTests, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false', '--', '--device')
        Write-Step 'Running byte-for-byte CD read and command/data interrupt regression tests.'
        $launcherTests = Join-Path $Root 'LauncherTests\LauncherTests.csproj'
        Invoke-Dotnet -Arguments @('restore', $launcherTests, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('run', '--project', $launcherTests, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false')
        $testProject = Join-Path $Root 'Tests\CdReadRegression.csproj'
        $testOut = Join-Path $Root '.build\tests'
        Invoke-Dotnet -Arguments @('restore', $testProject, '--configfile', $script:NuGetConfig, '-p:NuGetAudit=false')
        Invoke-Dotnet -Arguments @('build', $testProject, '-c', 'Release', '--no-restore', '-o', $testOut, '-p:UseSharedCompilation=false', '-m:1')
        Invoke-Dotnet -Arguments @((Join-Path $testOut 'CdReadRegression.dll'), $Disc)
    }
    Write-Step "Deploying the application to $DeployDir (disc, saves and settings are not overwritten)."
    $gameExe = Copy-JetMotoDeployment -PublishRoot $script:Output -Destination $DeployDir -PublishedFiles $script:PublishedFiles
    [pscustomobject]@{
        build = '12'
        deployedAt = [DateTimeOffset]::Now.ToString('O')
        deployDirectory = $DeployDir
        executable = $gameExe
        files = $script:PublishedFiles
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Root '.build\deployment.json') -Encoding UTF8
    # The optional developer launcher reads this; normal players double-click JetMoto.exe.
    Set-Content -LiteralPath (Join-Path $Root '.build\deployed-path.txt') -Value $DeployDir -Encoding Default
    # Clean only fresh temporary publish directories created by this invocation,
    # never the deployment directory or any pre-existing source/build folder.
    foreach ($stage in $script:PublishStages) {
        try { Remove-Item -LiteralPath $stage -Recurse -Force }
        catch { Write-Warning "The game was deployed, but temporary staging could not be removed: $stage" }
    }
    Write-Step ('Build complete in {0:n0} seconds. Executable: {1}' -f ((Get-Date)-$Started).TotalSeconds, $gameExe)
    Write-Host 'Build 12 directional lighting, cast shadows, moving wakes and layered native water rendering are active in normal JetMoto.exe launches. No preview launcher or setting is required.'
    Write-Host 'Double-click JetMoto.exe in the deployment folder. It reads the CUE beside it; no command-line arguments are required.'
    Write-Host 'Runtime and dependencies are bundled in JetMoto.exe. Keep Textures and Lighting beside it. Gameplay is 16:9 and menus are 4:3.'
    exit 0
} catch {
    $message = $_ | Out-String
    Write-Host "BUILD FAILED: $message" -ForegroundColor Red
    if (Test-Path -LiteralPath (Split-Path -Parent $Log)) { Add-Content -LiteralPath $Log -Value $message }
    Write-Host 'Run Collect-Logs.cmd and upload its ZIP here.'
    exit 1
} finally { Set-Location $OldLocation }

[CmdletBinding()]
param([string]$DeployDir)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
try {
    if (!$DeployDir) {
        $DeployDir = 'D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto'
        $record = Join-Path $PSScriptRoot '.build\deployment.json'
        if (Test-Path -LiteralPath $record -PathType Leaf) {
            $DeployDir = (Get-Content -Raw -LiteralPath $record | ConvertFrom-Json).deployDirectory
        }
    }
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $stage = Join-Path $PSScriptRoot ".build\logs-$stamp"
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    foreach ($rel in @('logs\build.log', '.build\deployment.json', 'BUILD11-REPORT.md', 'provenance.json', 'generated-hooks.json')) {
        $source = Join-Path $PSScriptRoot $rel
        if (Test-Path -LiteralPath $source -PathType Leaf) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $stage ([IO.Path]::GetFileName($source)))
        }
    }
    foreach ($leaf in @('last-run.log', 'last-state.txt', 'native-textures.json')) {
        $source = Join-Path $DeployDir (Join-Path 'logs' $leaf)
        if (Test-Path -LiteralPath $source -PathType Leaf) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $stage $leaf)
        }
    }
    $packManifest = Join-Path $DeployDir 'Textures\Native4x-Test\pack-manifest.json'
    if (Test-Path -LiteralPath $packManifest -PathType Leaf) {
        Copy-Item -LiteralPath $packManifest -Destination (Join-Path $stage 'installed-pack-manifest.json')
    }
    $lightingCatalog = Join-Path $DeployDir 'Lighting\catalog.json'
    if (Test-Path -LiteralPath $lightingCatalog -PathType Leaf) {
        Copy-Item -LiteralPath $lightingCatalog -Destination (Join-Path $stage 'installed-lighting-catalog.json')
    }
    @("Collected: $([DateTimeOffset]::Now.ToString('O'))", "OS: $([Environment]::OSVersion)",
      "64-bit OS: $([Environment]::Is64BitOperatingSystem)", "PowerShell: $($PSVersionTable.PSVersion)",
      "Deployment: $DeployDir",
      'Logs may contain local file paths. No disc, memory-card saves, or SDK binaries are collected.') |
        Set-Content -LiteralPath (Join-Path $stage 'host.txt')
    $zip = Join-Path $PSScriptRoot "JetMoto-Logs-$stamp.zip"
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
    Write-Host "Upload this file: $zip"
    exit 0
} catch { Write-Host $_ -ForegroundColor Red; exit 1 }

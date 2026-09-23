$ErrorActionPreference = 'Stop'
$sourceRoot = 'C:\Programming\JetMoto-RecompOne-Input\bundle\RecompOne'
$overlayRoot = Join-Path $PSScriptRoot 'upstream-patches'
$vendorRoot = Join-Path $PSScriptRoot 'vendor\RecompOne'
$manifestPath = Join-Path $overlayRoot 'patch-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$files = @()
foreach ($file in Get-ChildItem -LiteralPath $overlayRoot -File -Recurse) {
    if ($file.FullName -eq $manifestPath) { continue }
    $relative = [IO.Path]::GetRelativePath($overlayRoot, $file.FullName).Replace('\','/')
    if (!$relative.StartsWith('RecompOne.')) { continue }
    $original = Join-Path $sourceRoot $relative
    $target = Join-Path $vendorRoot $relative
    $before = if (Test-Path -LiteralPath $original) { (Get-FileHash -LiteralPath $original).Hash.ToLowerInvariant() } else { $null }
    $after = (Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant()
    $old = $manifest.files | Where-Object path -eq $relative
    if (Test-Path -LiteralPath $target) {
        $actual = (Get-FileHash -LiteralPath $target).Hash.ToLowerInvariant()
        if ($actual -ne $before -and $actual -ne $after -and $actual -ne $old.afterSha256) {
            throw "Untracked vendor edit: $relative"
        }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
    $files += [ordered]@{path=$relative; beforeSha256=$before; afterSha256=$after}
}
$manifest.files = $files
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8
Write-Output "Synchronized $($files.Count) cumulative overlays."

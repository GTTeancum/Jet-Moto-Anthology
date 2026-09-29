# Promote accepted local artwork into shipped assets, leaving user Overrides alone.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$CandidateRoot,
      [Parameter(Mandatory)][string]$PublishRoot)
$ErrorActionPreference = 'Stop'
$candidate = (Resolve-Path -LiteralPath $CandidateRoot).Path
$publish = (Resolve-Path -LiteralPath $PublishRoot).Path
if ($candidate -eq $publish) { throw 'Use a separate fresh publish directory.' }
$pack = Join-Path $publish 'Textures/Native4x-Test'
$manifestPath = Join-Path $pack 'pack-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$entries = @{}
foreach ($bank in $manifest.banks) {
    foreach ($entry in $bank.textures) { $entries[$entry.png.Replace('/', '\')] = $entry }
}
$plan = @()
$regions = @()
$overrides = Join-Path $candidate 'Textures/Overrides'
foreach ($file in Get-ChildItem -LiteralPath $overrides -Recurse -File) {
    $relative = $file.FullName.Substring($overrides.TrimEnd('\','/').Length + 1)
    $entry = $entries[$relative]
    if (!$entry -and $relative -match '^MISC\\OPTIONS\\Regions\\(0001-0000867C|0002-00008679)\\(\d{3})-(\d{3})-(\d{3})-(\d{3})\.png$') {
        $owner = 'MISC\OPTIONS\' + $Matches[1] + '.png'
        if (!$entries.ContainsKey($owner)) { throw "Missing region owner: $owner" }
        $regions += [ordered]@{ png=$relative.Replace('\','/'); width=[int]$Matches[4]; height=[int]$Matches[5];
            png_sha256=(Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant() }
    } elseif ($file.Extension -ne '.png' -or !$entry) {
        throw "Unknown replacement asset: $relative"
    }
    $plan += [pscustomobject]@{ Source=$file.FullName; Destination=(Join-Path $pack $relative);
        Hash=(Get-FileHash -LiteralPath $file.FullName).Hash; Entry=$entry }
}
$menus = Join-Path $candidate 'Textures/Menu4x'
$menuPlan = @()
foreach ($file in Get-ChildItem -LiteralPath $menus -Recurse -Filter *.png -File) {
    $meta = Get-Content -LiteralPath ($file.FullName + '.json') -Raw | ConvertFrom-Json
    $relative = $file.FullName.Substring($menus.TrimEnd('\','/').Length + 1)
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne $meta.outputSha256 -or
        [IO.Path]::ChangeExtension($meta.source.source, '.png').Replace('/', '\') -ne $relative) {
        throw "Menu identity/checksum mismatch: $relative"
    }
    $menuPlan += [pscustomobject]@{ Source=$file.FullName;
        Destination=(Join-Path $publish ('Textures/Menu4x/' + $relative)) }
}
if (!$plan.Count -or !$menuPlan.Count) { throw 'Accepted visual assets are missing.' }
foreach ($item in $plan) {
    New-Item -ItemType Directory -Force -Path (Split-Path $item.Destination) | Out-Null
    Copy-Item -LiteralPath $item.Source -Destination $item.Destination
    if ($item.Entry) { $item.Entry.png_sha256 = $item.Hash.ToLowerInvariant() }
}
foreach ($item in $menuPlan) {
    New-Item -ItemType Directory -Force -Path (Split-Path $item.Destination) | Out-Null
    Copy-Item -LiteralPath $item.Source -Destination $item.Destination
    Copy-Item -LiteralPath ($item.Source + '.json') -Destination ($item.Destination + '.json')
}
$manifest | Add-Member -NotePropertyName packagedRegions -NotePropertyValue @($regions) -Force
[IO.File]::WriteAllText($manifestPath,($manifest | ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
$record = [ordered]@{ overridesPromoted=$plan.Count; menuImages=$menuPlan.Count;
    assets=@($plan | ForEach-Object { [ordered]@{ path=$_.Destination.Substring($publish.TrimEnd('\','/').Length + 1); sha256=$_.Hash } }) }
New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot '.build/release-records') | Out-Null
[IO.File]::WriteAllText((Join-Path $PSScriptRoot '.build/release-records/release-artwork.json'),($record | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
Write-Host "Promoted $($plan.Count) native replacements and $($menuPlan.Count) menu images into fresh publish."

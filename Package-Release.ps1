[CmdletBinding()]
param([Parameter(Mandatory)][string]$GateReport,
      [switch]$Prerelease,
      [switch]$ReleaseApproved,
      [string]$DeploymentRecord = '.build/deployment.json',
      [string]$OutputRoot = '.build/distributions/JetMoto-Build12-Windows')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Deployment.ps1')
$gate=Get-Content -LiteralPath $GateReport -Raw | ConvertFrom-Json
$deployment=Get-Content -LiteralPath $DeploymentRecord -Raw | ConvertFrom-Json
$source=(Resolve-Path -LiteralPath $deployment.deployDirectory).Path
if((!$gate.passed -and !$Prerelease -and !$ReleaseApproved) -or !$gate.singleFile -or
   (Get-FileHash -LiteralPath (Join-Path $source 'JetMoto.exe')).Hash -ne $gate.exeSha256) {
    throw 'Stable releases require completed gates; every package must identify the exact tested single-file executable.'
}
if(($Prerelease -or $ReleaseApproved) -and !$gate.passed -and ($gate.exitCode -ne 3 -or @($gate.visualFramesInspected).Count -eq 0 -or !$gate.userDataRestored)) {
    throw 'Publication still requires inspected native smoke evidence and restored user data.'
}
if (@($deployment.files | Where-Object { $_ -match '\.(dll|pdb)$' }).Count) {
    throw 'Dependencies and debug symbols must be bundled in JetMoto.exe.'
}
$output=[IO.Path]::GetFullPath($OutputRoot)
$archive=$output+'.zip'
if((Test-Path -LiteralPath $output) -or (Test-Path -LiteralPath $archive) -or (Test-Path -LiteralPath ($archive+'.records'))){throw 'Choose a new output path; existing releases are never overwritten.'}
# Only runtime content belongs in the player folder.
$playerFiles=@($deployment.files | Where-Object { $_ -eq 'JetMoto.exe' -or $_ -match '^(Textures|Lighting)[\\/]' })
Copy-JetMotoDeployment -PublishRoot $source -Destination $output -PublishedFiles $playerFiles | Out-Null
$records=$archive+'.records'
New-Item -ItemType Directory -Path $records | Out-Null
[ordered]@{prerelease=[bool]$Prerelease;releaseApproved=[bool]$ReleaseApproved;fullValidationPassed=[bool]$gate.passed} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $records 'publication.json')
Copy-Item -LiteralPath $GateReport -Destination (Join-Path $records 'release-verification.json')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'provenance.json') -Destination $records
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'vendor/RecompOne/LICENSE') -Destination (Join-Path $output 'RecompOne-LICENSE.txt')
$sdk='C:/Programming/JetMoto-RecompOne-Input/cache/dotnet-win-10.0.401'
Copy-Item -LiteralPath (Join-Path $sdk 'LICENSE.txt') -Destination (Join-Path $output 'DotNet-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $sdk 'ThirdPartyNotices.txt') -Destination (Join-Path $output 'DotNet-ThirdPartyNotices.txt')
@'
Jet Moto Anthology - 1.0 (Windows x64)

Extract this entire folder. Add your original Jet Moto (USA).cue and all 14
referenced BIN tracks beside JetMoto.exe, then double-click JetMoto.exe.
Runtime and dependencies are bundled in JetMoto.exe. Keep Textures and Lighting beside it.
No SDK or separate .NET installation is needed.

Ordinary launches retain normal progression. Saves/settings are created locally.

Keyboard defaults: arrows steer; Z is Cross/accelerate; X is Circle;
A is Square; S is Triangle/boost; Enter is Start; ShiftRight is Select.

This package contains no disc images, personal saves, settings or test logs.
Install in a new folder; preserve any existing saves and settings.
'@ | Set-Content -LiteralPath (Join-Path $output 'READ-ME.txt') -Encoding utf8
$files=@(Get-ChildItem -LiteralPath $output -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{ path=$_.FullName.Substring($output.TrimEnd('\','/').Length + 1).Replace('\','/');
        bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName).Hash }
})
$files | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $records 'SHA256-MANIFEST.json') -Encoding utf8
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($output,$archive,[IO.Compression.CompressionLevel]::Optimal,$true)
(Get-FileHash -LiteralPath $archive).Hash + '  ' + [IO.Path]::GetFileName($archive) |
    Set-Content -LiteralPath ($archive+'.sha256') -Encoding ascii
Write-Host "Package created: $archive. Verify archive contents before delivery."

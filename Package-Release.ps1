[CmdletBinding()]
param([Parameter(Mandatory)][string]$GateReport,
      [string]$DeploymentRecord = '.build/deployment.json',
      [string]$OutputRoot = '.build/distributions/JetMoto-Build12-Windows')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Deployment.ps1')
$gate=Get-Content -LiteralPath $GateReport -Raw | ConvertFrom-Json
$deployment=Get-Content -LiteralPath $DeploymentRecord -Raw | ConvertFrom-Json
$source=(Resolve-Path -LiteralPath $deployment.deployDirectory).Path
if(!$gate.passed -or
   (Get-FileHash -LiteralPath (Join-Path $source 'JetMoto.dll')).Hash -ne $gate.appSha256 -or
   (Get-FileHash -LiteralPath (Join-Path $source 'RecompOne.Runtime.dll')).Hash -ne $gate.runtimeSha256) {
    throw 'Completed gates must identify these exact application/runtime binaries.'
}
$output=[IO.Path]::GetFullPath($OutputRoot)
$archive=$output+'.zip'
if((Test-Path -LiteralPath $output) -or (Test-Path -LiteralPath $archive)){throw 'Choose a new output path; existing releases are never overwritten.'}
Copy-JetMotoDeployment -PublishRoot $source -Destination $output -PublishedFiles $deployment.files | Out-Null
Copy-Item -LiteralPath $GateReport -Destination (Join-Path $output 'release-verification.json')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'provenance.json') -Destination $output
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'vendor/RecompOne/LICENSE') -Destination (Join-Path $output 'RecompOne-LICENSE.txt')
$sdk='C:/Programming/JetMoto-RecompOne-Input/cache/dotnet-win-10.0.401'
Copy-Item -LiteralPath (Join-Path $sdk 'LICENSE.txt') -Destination (Join-Path $output 'DotNet-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $sdk 'ThirdPartyNotices.txt') -Destination (Join-Path $output 'DotNet-ThirdPartyNotices.txt')
@'
Jet Moto - Build 12 (Windows x64)

Extract this entire folder. Add your original Jet Moto (USA).cue and all 14
referenced BIN tracks beside JetMoto.exe, then double-click JetMoto.exe.
Keep the accompanying libraries, Textures and Lighting folders together.
No SDK or separate .NET installation is needed.

For visual testing with all ten tracks available, double-click run.bat.
The --unlockall switch applies to that session; saved unlock progress is unchanged.
Ordinary launches retain normal progression. Saves/settings are created locally.

Keyboard defaults: arrows steer; Z is Cross/accelerate; X is Circle;
A is Square; S is Triangle/boost; Enter is Start; ShiftRight is Select.

This package contains no disc images, personal saves, settings or test logs.
Release verification is described in release-verification.json. Visual checks
are sampled gameplay, not a claim of exhaustive full-race or audio validation.
Install in a new folder; preserve your known-good Build 11 installation.
'@ | Set-Content -LiteralPath (Join-Path $output 'READ-ME.txt') -Encoding utf8
[IO.File]::WriteAllText((Join-Path $output 'run.bat'),"@echo off`r`ncd /d `"%~dp0`"`r`n`"%~dp0JetMoto.exe`" --unlockall %*`r`n",[Text.Encoding]::ASCII)
$files=@(Get-ChildItem -LiteralPath $output -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{ path=$_.FullName.Substring($output.TrimEnd('\','/').Length + 1).Replace('\','/');
        bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName).Hash }
})
$files | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'SHA256-MANIFEST.json') -Encoding utf8
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($output,$archive,[IO.Compression.CompressionLevel]::Optimal,$true)
(Get-FileHash -LiteralPath $archive).Hash + '  ' + [IO.Path]::GetFileName($archive) |
    Set-Content -LiteralPath ($archive+'.sha256') -Encoding ascii
Write-Host "Package created: $archive. Verify archive contents before delivery."

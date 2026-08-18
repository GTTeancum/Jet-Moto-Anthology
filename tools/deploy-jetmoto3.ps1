param(
    [string] $TargetDirectory = ""
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$target = if ($TargetDirectory) {
    [IO.Path]::GetFullPath($TargetDirectory)
} else {
    Join-Path $repo "JetMoto3_loose"
}

if (-not (Test-Path -LiteralPath (Join-Path $target "SCUS_945.55"))) {
    throw "Target is not the extracted Jet Moto 3 disc: $target"
}

dotnet publish (Join-Path $repo "JetMoto3\JetMoto3.csproj") `
    -p:PublishProfile=WindowsSingleFile
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$publish = Join-Path $repo "JetMoto3\bin\Release\net10.0\win-x64\publish"
Copy-Item -LiteralPath (Join-Path $publish "JetMoto3.exe") -Destination $target -Force

# Remove only obsolete application artifacts from the verified disc directory.
Get-ChildItem -LiteralPath $target -File | Where-Object {
    $_.Extension -in ".dll", ".pdb" -or
    $_.Name -in "JetMoto3.deps.json", "JetMoto3.runtimeconfig.json"
} | ForEach-Object { [IO.File]::Delete($_.FullName) }

$runtimes = Join-Path $target "runtimes"
if (Test-Path -LiteralPath $runtimes) {
    $resolvedTarget = [IO.Path]::GetFullPath($target).TrimEnd('\') + '\'
    $resolvedRuntimes = [IO.Path]::GetFullPath($runtimes)
    if (-not $resolvedRuntimes.StartsWith($resolvedTarget, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove runtime directory outside deployment: $resolvedRuntimes"
    }
    [IO.Directory]::Delete($resolvedRuntimes, $true)
}

$exe = Get-Item -LiteralPath (Join-Path $target "JetMoto3.exe")
Write-Host "Deployed $($exe.FullName) ($([Math]::Round($exe.Length / 1MB, 1)) MB)"

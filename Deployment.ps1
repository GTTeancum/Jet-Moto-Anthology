# Used by Build-Windows.ps1. Only an explicit list of fresh publish artifacts is copied.
# No synchronization, folder clearing, wildcard deletion, disc, settings or save copying.
function Copy-JetMotoDeployment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$PublishRoot,
        [Parameter(Mandatory=$true)][string]$Destination,
        [Parameter(Mandatory=$true)][string[]]$PublishedFiles
    )
    $sourceRoot = [IO.Path]::GetFullPath($PublishRoot).TrimEnd('\', '/')
    $targetRoot = [IO.Path]::GetFullPath($Destination).TrimEnd('\', '/')
    if ($sourceRoot.Equals($targetRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Publish staging and deployment must be different folders.'
    }
    $plan = @()
    foreach ($relative in $PublishedFiles) {
        $relative = $relative.Replace('/', '\')
        if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|\\)\.\.(\\|$)') {
            throw "Unsafe deployment filename: $relative"
        }
        if ($relative -match '^Textures\\Overrides(\\|$)') { throw "Refusing to overwrite custom native textures: $relative" }
        $leaf = [IO.Path]::GetFileName($relative)
        if ($relative -match '^(logs|saves|memcards|mods)(\\|$)' -or
            $leaf -in @('settings.json', 'interface.ini', 'imgui.ini') -or
            [IO.Path]::GetExtension($leaf) -in @('.bin', '.cue', '.iso', '.chd', '.7z', '.sav', '.mcr', '.mcd', '.srm')) {
            throw "Refusing to deploy user-data file: $relative"
        }
        $from = Join-Path $sourceRoot $relative
        $to = Join-Path $targetRoot $relative
        if (!(Test-Path -LiteralPath $from -PathType Leaf)) { throw "Missing publish artifact: $from" }
        $plan += [pscustomobject]@{ From = $from; To = $to }
    }
    if (!($PublishedFiles -contains 'JetMoto.exe')) { throw 'Publish did not produce JetMoto.exe.' }
    # Check all existing destination artifacts before modifying any of them. In particular,
    # a running Windows executable/DLL must be closed before updating this folder.
    foreach ($item in $plan) {
        if (Test-Path -LiteralPath $item.To) {
            if (!(Test-Path -LiteralPath $item.To -PathType Leaf)) { throw "A folder blocks deployment: $($item.To)" }
            try {
                $handle = [IO.File]::Open($item.To, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                $handle.Dispose()
            } catch { throw "Cannot update $($item.To). Close Jet Moto and check folder permissions. $($_.Exception.Message)" }
        }
    }
    New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
    foreach ($item in $plan) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $item.To) | Out-Null
        Copy-Item -LiteralPath $item.From -Destination $item.To -Force
    }
    return (Join-Path $targetRoot 'JetMoto.exe')
}

<#
.SYNOPSIS
    Build and package the three releases into C:\Programming\GitHub\Jet-Moto-Anthology-Recomp\dist.

.DESCRIPTION
    The releases published before this script existed were assembled by hand,
    which is how one of them shipped without a licence and how a backslash in
    the instructions turned "D:\rips" into "D:ips". This makes the packaging
    reproducible: same layout, same documents, same three files, every time.

    One launcher binary serves all three games -- it contains no game code and
    picks which port to build from its own file name -- so each package is that
    binary renamed, plus both licences and a per-game READ ME FIRST.

    The executable is a single self-contained file. Nothing else is copied: no
    DLLs, and specifically no .pdb symbols, which the publish folder also holds.
#>
param(
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $repo "dist"
$publish = Join-Path $repo "Launcher\bin\Release\net10.0\win-x64\publish"

$games = @(
    @{ Key = "jm1"; Exe = "JetMoto";  Name = "Jet Moto";   Version = "1.2.0"
       Cue = "Jet Moto (USA).cue"
       State = "Complete. A full 3-lap race has been played end to end."
       Extra = @() }
    @{ Key = "jm2"; Exe = "JetMoto2"; Name = "Jet Moto 2"; Version = "1.2.0"
       Cue = "Jet Moto 2 (v1.1).cue"
       State = "Playable. Boots, menus, controls and races all work."
       Extra = @() }
    @{ Key = "jm3"; Exe = "JetMoto3"; Name = "Jet Moto 3"; Version = "1.1.0"
       Cue = "Jet Moto 3 (USA).cue"
       State = "Playable at a locked 60 fps, with optional true 16:9 widescreen."
       Extra = @(
           "WIDESCREEN",
           "----------",
           "Settings > Display offers true 16:9: the world is extended at the",
           "sides rather than the 4:3 picture being stretched, and the HUD is",
           "anchored to the new edges. Pre-rendered movies stay 4:3.",
           "") }
)

if (-not $SkipBuild) {
    Write-Host "publishing the launcher..."
    # -p:SelfContained=true rather than --self-contained: the property has to
    # reach the referenced recompiler project too, or the two disagree and the
    # build fails with NETSDK1150.
    dotnet publish (Join-Path $repo "Launcher\JetMotoLauncher.csproj") `
        -c Release -r win-x64 -p:SelfContained=true
    if ($LASTEXITCODE -ne 0) { throw "publish failed" }
}

$built = Join-Path $publish "JetMoto.exe"
if (-not (Test-Path -LiteralPath $built)) { throw "launcher not published: $built" }

if (Test-Path -LiteralPath $dist) { Remove-Item -LiteralPath $dist -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dist | Out-Null

foreach ($g in $games) {
    $folder = "$($g.Exe)-win-x64"
    $stage = Join-Path $dist $folder
    New-Item -ItemType Directory -Force -Path $stage | Out-Null

    Copy-Item -LiteralPath $built -Destination (Join-Path $stage "$($g.Exe).exe") -Force
    Copy-Item -LiteralPath (Join-Path $repo "LICENSE") -Destination $stage -Force
    Copy-Item -LiteralPath (Join-Path $repo "tools\RecompOne\LICENSE") `
              -Destination (Join-Path $stage "LICENSE.RecompOne") -Force

    # Single-quoted here-string: the instructions contain backslashes, and
    # letting the shell interpret them is exactly how "D:\rips" shipped as
    # "D:ips" in the previous release.
    $readme = @"
$($g.Name)
$('=' * $g.Name.Length)

A native PC port, built by static recompilation. Version $($g.Version).


YOU SUPPLY THE GAME
-------------------
This download contains no game code and no game data. You need a bin/cue rip
of a $($g.Name) disc you own.

The recompiler runs here, on your machine, against your disc. Without one this
program does nothing but say so.


PLAYING IT
----------
Put your disc rip in this folder and double-click $($g.Exe).exe.

That is the whole procedure. There is no installer, no command prompt and
nothing else to install -- no .NET, no SDK, no build step. One file.

If it cannot find a disc beside it, it opens a file picker and asks for your
.cue.

The first launch spends a little time preparing your disc: it unpacks the disc
to loose files and translates the executable, showing a progress window while
it works. That result is saved next to the program, so every launch after the
first starts straight into the game.


CONTROLS
--------
    arrows  D-pad          Z  Cross      X  Circle
    Enter   Start          A  Square     S  Triangle
    Q / W   L1 / R1

Keyboard and gamepad bindings are under Settings > Input. Display options --
window size, internal resolution, FXAA -- are under Settings > Display.

$(($g.Extra + @()) -join "`r`n")
IF SOMETHING GOES WRONG
-----------------------
A log is written to $($g.Exe).log beside the program, flushed as it goes so a
crash still leaves a record. The previous run is kept as $($g.Exe).prev.log.
Attach it to any bug report.


ADVANCED
--------
Everything above works by double-clicking. These exist for the curious:

    $($g.Exe).exe [--disc <path>] [--extract [folder]] [--rebuild]

    --disc <path>       a .cue, or a folder made by --extract
    --extract [folder]  unpack the disc to loose files, then exit
    --rebuild           discard the saved translation and redo it

The extracted folder is a browsable copy of the disc with the CD music as ogg,
which takes the soundtrack from about 450 MB of raw audio down to about 45 MB.
Music extraction needs ffmpeg on PATH; without it everything else still works.


STATE
-----
$($g.State)

Windows x64. Other platforms build from source but are untested.


LICENCE
-------
Ours is MIT -- see LICENSE. Built on RecompOne by flaffy, also MIT, whose
licence is LICENSE.RecompOne.

$($g.Name) is (c) Sony Interactive Entertainment. This project is not
affiliated with or endorsed by them.

    https://github.com/GTTeancum/Jet-Moto-Anthology
"@

    Set-Content -LiteralPath (Join-Path $stage "READ ME FIRST.txt") -Value $readme -Encoding ascii

    $zip = Join-Path $dist "$folder.zip"
    Compress-Archive -Path $stage -DestinationPath $zip -Force

    $size = [Math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Host ("  {0,-22} {1,6} MB  tag {2}-v{3}" -f "$folder.zip", $size, $g.Exe.ToLower(), $g.Version)
}

Write-Host ""
Write-Host "packaged into $dist"

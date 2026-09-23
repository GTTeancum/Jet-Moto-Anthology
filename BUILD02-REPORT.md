# Build 02 validation report

Date: 2026-09-21

## Scope

This update makes the existing Build 01 project launchable by double-click and changes the Windows build's deployment destination. It does not change the generated game functions, function maps, CD fixes, or runtime compatibility.

Default deployed executable:

```text
D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\JetMoto.exe
```

## Changes

- `JetMoto.csproj`: Windows GUI output type, product/title Jet Moto, version 0.2.0.
- `Program.cs`: automatically resolve an adjacent valid CUE before runtime initialization; ignore stale saved paths for normal launches; retain explicit diagnostic overrides; show Windows error dialogs unless running automated validation/smoke diagnostics; keep file logging; use game window title Jet Moto.
- `DiscLocator.cs`: exact shared selection logic. Checks the executable's own directory, validates candidates, handles preferred names deterministically, accepts a single matching custom filename, and reports missing/ambiguous/mismatched discs.
- `Build-Windows.ps1` / `Deployment.ps1`: publish into fresh temporary staging, capture the published-file list before any diagnostic run, and copy only those application artifacts into the requested destination. Never clear or mirror the game folder. Reject disc/save/settings files in the copy plan. Preflight existing application file locks before copying. Clean only this invocation's temporary publish staging after success.
- `Run-JetMoto.cmd`: optional developer shortcut now follows the deployed location. It is not required to play.
- `Collect-Logs.ps1`: collect runtime diagnostics from the deployed game folder rather than the old source-tree binary directory.

## Executed checks

1. **14 launcher regression tests passed.** These compile the exact `DiscLocator.cs` used by the game. Cases include missing CUEs, arbitrary filenames, case-insensitive names/extensions, invalid preferred discs, ambiguity, unrelated working directories, nested files, stale settings, validator exceptions, relocation, and BIN-only folders.
2. **Linux-targeted managed publish passed.** Full generated game project plus patched runtime, Release configuration, using the uploaded SDK and local package feed.
3. **Eight real-disc launcher checks passed.** The actual compiled launcher used the uploaded CUE and 14 BIN tracks. It validated the exact expected executable hash without any explicit CUE argument from `/tmp`; ignored a deliberately obsolete saved path; reported a deliberately missing track; rejected missing CUEs; rejected ambiguous matching CUEs; retained explicit CLI override support; rejected unknown arguments; and worked after moving the entire application folder. Temporary missing-file tests removed only scratch symlinks, not the source disc files.
4. **Windows-targeted managed publish passed.** `RuntimeIdentifier=win-x64`, `SelfContained=false`, `UseAppHost=false`, `EnableAppHostPackDownload=false`. Managed output's PE subsystem is 2 (Windows GUI). This is a cross-compilation check, not native Windows execution.
5. **Ten-second auto-load smoke run passed its limited objective.** Linux Xvfb/software OpenGL, null audio; no CUE argument, working directory `/tmp`. The launcher verified the adjacent disc, opened the graphics context, entered original game entry point `0x800EC310`, and stopped cooperatively at the requested deadline with expected exit code 3. This is not a gameplay/audio/playability test and does not establish reaching a particular screen in this run.
6. **Preservation check passed.** Every included generated-code, config/function-map, and upstream-patch file matches Build 01 byte-for-byte.

Raw evidence is in `reports/build02/`. Historical Build 01 validation remains in `BUILD-REPORT.md` and the other original reports.

## Limits

The Windows build/deployment PowerShell scripts, native Windows EXE, error dialogs, Explorer double-click behavior, and native file-lock preflight have not been executed on Windows in this scratch. The uploaded Linux kit does not include the Windows native apphost pack; the Windows build script uses the user's original matching Windows SDK to create the actual self-contained GUI EXE and accompanying libraries. Compilation and CUE-selection tests do not establish Windows graphics/audio behavior or race gameplay.

Multi-file deployment is non-clearing but not atomic. A disk/permission error during copying can require rebuilding after the error is corrected. It intentionally does not migrate old saves from a previous build's output directory; existing saves/settings at the new destination are preserved.

For scratch restoration only, package hashes were independently checked against the uploaded manifest, and NuGet signature online checks were disabled to avoid unavailable certificate-network lookups. This scratch setting was not added to the Windows build script.

# Validation status

The 0.12.0 candidate completed its remaining release gates on **2026-09-26**. Raw captures and logs remain in the local ignored reports/build12 directory.

| Area | Completed evidence |
| --- | --- |
| Regression | All 12 suites passed: track access, replay, world lighting, rider detail, native textures, texture pack, perspective, widescreen, renderer contracts, effects, launcher, and CD reading |
| Renderer | 717 actual offscreen pixel checks passed on each of GL45, GL33, and GL21 |
| Artwork | Runtime decoding of 1,406 textures, 15 effect materials, and 56 packaged menu regions; 237 promoted artwork/metadata files matched the accepted candidate byte for byte |
| Gameplay | Two native samples per track across Joyride, Hammerhead, Cypress Run, Blackwater Falls, Suicide Swamp, Nightmare, Willpower, Ice Crusher, Snow Blind, and Cliffdiver; original track-bank identities verified |
| Menus | Title, rider selection, and race-type captures individually inspected; 4:3 presentation and refreshed artwork present |
| Startup | Self-contained application relocated to a path with spaces; adjacent-disc discovery and four real command-line cases passed |
| Deployment | Seven cases passed in PowerShell 7 and Windows PowerShell, including user-data protection and rejecting unsafe paths or a locked executable before mutation |
| Package | 1,859 files verified against their archive manifest; archive checksum checked; no discs, saves, settings, logs, or custom overrides |

All 24 saved menu/gameplay images were individually inspected, including an extra Cypress start sample. These are sampled visuals, not complete races. Audio was muted and not listened to; exhaustive multiplayer play was not performed.

## Current local distribution

The verified archive is .build/distributions/JetMoto-Build12-Windows.zip (462,348,807 bytes). This is a local artifact, not a download supplied by the source repository.

SHA-256:

```text
C31221695F06F90FF17CD94E64BA257F72A7255911803425506AB2487F704621
```

It includes the Windows x64 runtime, current artwork and lighting, player instructions, an unlock-all launcher, provenance, and verification manifests. Its embedded provenance reflects the pre-archive audit snapshot; this record and the verified checksum capture the completed archive check.

## Rendering limits

Static world occlusion uses a bounded height field and cannot represent every overhang or layer. Rider shadows are projected silhouettes. Water shading approximates sky/specular response rather than reflecting nearby objects, and does not simulate fluid geometry. Unknown or mismatched receiver geometry falls back to original rendering.

Previously accepted visual artifacts, including thin effect streaks and some low-resolution original signs, remain. Validation does not claim every corner or effect is defect-free. The staged testing game and its original application/runtime hashes were preserved.

## README screenshot sources

The gallery uses unchanged native capture PNGs:

| Image | Local source beneath reports/build12 |
| --- | --- |
| Title | release-relocated-menus-v1/frame-000900.png |
| Hammerhead | release-slot-1-v1/race-000450.png |
| Blackwater Falls | release-slot-3-v1/race-000900.png |
| Snow Blind | release-slot-8-v1/race-000450.png |
| Nightmare | release-slot-5-v1/race-000450.png |
## User-facing installation check — 2026-09-26

The verified package was deployed to D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto beside the original CUE/BIN files. All 1,860 installed files (including the archive's checksum manifest) matched the expanded package. Dependencies and HD artwork are bundled in that folder; no SDK is required.

The actual installed executable completed a 100-second first-launch replay with no existing settings or memory cards, no explicit disc path, and no visual-feature environment overrides. All five native captures were inspected individually: title at frame 901, race-type menu at 1800, Joyride starting grid at 2700, shoreline gameplay at 3600, and later water gameplay at 4500. Menus, upgraded HUD, riders, scenery, water, and advancing race time were visible. The straight-input replay eventually crossed the track boundary; previously documented thin effect streaks remained visible.

The process exited with the expected bounded-test code 3. Original settings and memory cards were restored byte-for-byte afterward. Raw evidence is local at reports/build12/user-first-launch-20260926. This headless test does not verify audible sound, physical controller input, or full races.

## Single-executable delivery — 2026-09-26

The current installed JetMoto.exe bundles the .NET runtime and all managed/native dependencies (126,354,205 bytes). There are no loose DLLs in the user-facing game folder. The former runtime files were archived locally under .build/archive/pre-single-exe-20260926. Textures, Lighting, and original disc images remain external.

Full dependency extraction initially exposed a startup bug: AppContext.BaseDirectory referred to the extraction cache. The launcher now resolves the actual process executable directory for disc discovery, artwork, logs, and saved data. A corrected publish was deployed to the user's Jet Moto folder; all 1,623 published files matched their staging hashes.

The installed executable passed a 100-second first-launch replay with a fresh extraction cache, no settings/cards, automatic adjacent-disc discovery, and default HD feature settings. Every saved frame was individually inspected: title 900, race-type menu 1800, Joyride grid 2701, moving water gameplay 3600, and later track-boundary/collision state 4500. The timer advanced, native menus and HD HUD rendered, and water/buoy artwork loaded. The straight-input replay ran out of bounds; this is a smoke test, not a completed race. Exit was the expected timeout code 3, with empty stderr. Original settings and memory cards were restored byte-for-byte.

Fourteen launcher cases and seven deployment cases also passed. The publisher rejects loose DLL/PDB outputs, and packaging requires the tested EXE's hash. The build still emits MonoMod's general single-file warning; full extraction preserves its assembly locations, and the tested normal startup/menu/gameplay flow succeeded. Mod compilation, audio listening, and physical controller input were not tested.

Current local distribution: .build/distributions/JetMoto-Windows-SingleExe.zip. The earlier JetMoto-Build12-Windows.zip described above is retained as a historical multi-file package. Evidence: reports/build12/single-exe-fixed-first-launch and reports/build12/single-exe-delivery-gate.json.

Single-executable archive SHA-256: 512E5ED261916063C71C79499326C8C311097308FC16F6F6E680B304A71FF1C2. Archive verification passed for all 1,630 manifest entries plus the manifest itself.

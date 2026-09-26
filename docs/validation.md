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
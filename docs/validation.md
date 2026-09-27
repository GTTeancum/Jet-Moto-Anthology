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

## Audio crackling investigation — 2026-09-26

The user reported progressive crackling in both music and effects within a minute. Two concrete defects were corrected: a stopped OpenAL queue could replay already-consumed buffers after a stall during refill, and reverb multiplication used a 32-bit intermediate that could wrap a loud positive sample negative. The output reserve is now about 93 ms rather than 46 ms, with more frequent mixer service and above-normal thread priority.

Eleven regression checks cover queue behavior and reverb arithmetic. The old code fails the loud positive reverb case; the fixed code passes. A native OpenAL null-device test also recovered from five injected 200 ms SPU stalls without losing buffers or stopping playback. The diagnostics and tests are process-local and do not send desktop input.

Baseline measurements comprised 180 seconds on the null device and 120 seconds on the real sound device. Neither reproduced ongoing queue starvation after initial SPU attachment. The numerical tests establish the defects, but do not establish that either caused the user's audible symptom. Listening confirmation remains required.

The installed correction candidate completed a 210-second audio-enabled first-launch replay in the actual user folder with fresh settings/cards and dependency cache. All five native captures were inspected individually (menu 1800; gameplay 3601, 5400, 7200, 9001). Original settings/cards were restored. The final log recorded one initial queue restart, a 35.8 ms maximum mixer gap, 3.9 ms maximum mix time, and zero reverb overflow corrections. The user still heard occasional crackling; the audio release gate remains open. The installed EXE hash is 6FA95EB49C2280B9A114210CFD2A2B265F37E6CA97878CB6122B50C95FAF5591.

A separate 100-second null-device probe captured 96.36 seconds of mixed stereo PCM before OpenAL. Sample analysis found 14,855 near-full-scale channel samples and sharp transients, but no pronounced discontinuity pattern at the 512-frame queue boundaries. Neither loud transients nor near-full-scale samples alone establish audible corruption. An eight-second excerpt was supplied for comparison with the reported symptom. Evidence is retained locally under reports/build12/audio-pcm-probe and .build/audio-pcm-analysis.json; the diagnostic build has not replaced the installed candidate.

The user reported that the captured excerpt sounds clean, then reproduced crackling in Instagram. Switching from onboard Bluetooth to a USB Bluetooth adapter appeared to resolve it. This points to the shared device path for the remaining symptom; it does not establish that the earlier game changes caused the improvement. The experimental six-period device buffer was withdrawn without installing it in the user-facing folder. The installed correction candidate remains unchanged, and no Windows audio settings were modified by the agent.

## Performance implementation — 2026-09-26

The production renderer retains the accepted shader and artwork. Reused shadow buffers remove 6 MiB of allocation per camera update; the 60-update fixture allocated zero bytes (48.92 ms locally), compared with 360 MiB for the former arrays alone. This is an allocation measurement, not a claimed whole-game FPS gain. Shadow and wake textures now keep their GPU storage, camera inverses are cached, execution tracing defaults off, and decoded/GPU native images have bounded LRU residency. Known images are prepared during loading, with menu integrity validation deferred to decode.

All 24 performance regression checks passed, including byte-identical reloads, deferred checksum rejection, cache ownership, stale-shadow clearing and scaled-camera inverses. Native rendering passed 756 GL 4.5, 756 GL 3.3 and 747 GL 2.1 checks across 1x/2x/4x. These include a forced 16 MiB GPU cache, eviction with pending geometry, and reuse of an already-registered menu image. The full build regression suite also passed, including disc reads, native asset validation, geometry, perspective, widescreen, launcher and audio recovery checks.

GPU lighting experiments preserved fixture pixels but did not improve all workloads. Some sunlit cases regressed about 10–12%; production keeps the original shader. GPU elapsed-time fixtures are retained for future driver comparisons. This workstation's backend/scaling tests do not establish performance across other physical machines, integrated GPUs or drivers.

Local evidence: .build/performance-regression-full.log and .build/performance-production-Gl45.log, -Gl33.log, -Gl21.log. Earlier shader comparison measurements remain in .build/perf-*.log.

The final single-file EXE (SHA-256 D91A6D4BEA11628668D73DE3B423EC2FE73F8542194D7A39E3FAA2DAB048B0E7) was deployed to the actual user-facing Jet Moto folder with all published artifact hashes verified and no loose dependency DLLs. The 110-second first-launch smoke used fresh settings/cards and extraction cache, adjacent-disc discovery and normal HD defaults. All five native captures were inspected individually: title 900, race-type menu 1801, starting grid 2701, shoreline gameplay 3600 and later water gameplay 4500. Backgrounds, HUD numbers/boosts, riders, water and buoys rendered, and race time advanced. Existing thin effect streaks remain outside this performance change. Exit was the expected bounded-test code 3; stderr was empty. All four original settings/save files were restored byte-for-byte, and the launcher record points to the user folder.

A separate 105-second Nightmare replay used 32 MiB CPU / 64 MiB GPU image budgets. Both native captures (race 450 and 900) were inspected. The HUD and start-area artwork rendered; the straight-input replay subsequently drove off the elevated track. The final resident image totals were 30.6 MiB CPU / 63.5 MiB GPU, with 38 GPU evictions and three draw-time uploads. The default-budget first-launch run ended at 60.9 MiB CPU / 114.2 MiB GPU, with 13 evictions and three draw-time uploads. Remaining first-use uploads are permitted for dynamically resolved or evicted assets; preparation does not promise zero stalls. These are texture residency measurements, not total application memory.

Evidence: reports/build12/performance-nightmare-low-memory and reports/build12/performance-first-launch. The tests were muted and process-local: audio listening, physical controls, complete races, and performance on other machines were not retested. Prior audio recovery tests passed and the user's adapter-resolution report remains recorded; the overall release gate is not silently marked complete. The existing distribution ZIP is an older checkpoint, not this updated EXE.

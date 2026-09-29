# Windows development

Run commands from the repository root. The build uses .NET SDK **10.0.401**, pinned by global.json, and RecompOne revision d81dec8c9622fdcd0865d73588a3baa8d3c3a605.

## Local inputs

Build-Windows.ps1 expects the existing RecompOne input kit supplied through -KitRoot. It is a prepared packager work directory, not a plain SDK installation. The local toolchain, dependency cache, generated game source, native texture pack, and accepted menu/HUD artwork are excluded from Git. A fresh clone cannot yet build without those inputs.

Use an explicit output directory and the accepted artwork candidate:

```powershell
./Build-Windows.ps1 -KitRoot "C:\Programming\JetMoto-RecompOne-Input" -VisualAssetRoot .build/ui-buoy-bin/Release/net10.0/win-x64 -DeployDir .build/local-game -RunTests
```

Build-Windows.cmd is the Windows PowerShell wrapper. -Recompile regenerates game code when required. Stage-ReleaseAssets.ps1 promotes accepted local artwork into the published pack; include -VisualAssetRoot to ship the refreshed menu/HUD assets. Normal launches enable menu artwork and world wakes by default.

Deployment copies an explicit artifact list and preserves existing disc images, saves, settings, and custom texture overrides. Build into a fresh folder when comparing candidates. Sync-DevelopmentOverlay.ps1 updates the local patched vendor tree and its manifest during runtime development.

## Tests and captures

```powershell
./Run-Regression.ps1 -Name local-regression
./Test-ReleaseDeployment.ps1
./Run-ReleaseValidation.ps1 -Name local-track-check -AppDirectory .build/release-gates-stage -Replay Validation/Replays/unlockall-nightmare-replay.json -UnlockAll
```

The scripts retain this workstation's SDK and disc defaults; pass supported path parameters or adjust local defaults on another machine. Release validation requires an isolated published candidate with release-artwork.json. It uses process-local replay input and framebuffer capture. Inspect resulting images and behavior: reaching a frame count alone does not prove correctness. The bounded replay exits with code 3 at its intended deadline.

Run-Validation.ps1 supports development captures; Play-VisualQA.cmd launches the deployed user-facing game for manual play with all tracks available. Reusable replay inputs live in Validation/Replays. Generated evidence remains under ignored reports/build12 for compatibility with existing authoring tools.

## Packaging

```powershell
./Package-Release.ps1 -GateReport "path\to\completed-gates.json" -OutputRoot .build/distributions/JetMoto-Windows
./Verify-Release.ps1 -Archive .build/distributions/JetMoto-Windows.zip
```

Packages require singleFile=true and the exact tested exeSha256. Publication normally requires passed=true; -ReleaseApproved records an explicit maintainer publication decision without changing the underlying test results. An explicitly selected -Prerelease permits incomplete full validation only with inspected native smoke evidence and restored user data; the original failed/full-validation status is preserved. Packaging uses .build/deployment.json and refuses existing output paths. Player archives contain only the executable, runtime artwork, instructions and licenses. Internal checksums, provenance and gate reports stay outside the archive in <archive>.records; Verify-Release.ps1 reads those records. The separate archive checksum is also retained for publication. Disc images, saves, settings, logs and test launchers are excluded. Artwork staging records live under .build/release-records.

## Repository layout

| Location | Purpose |
| --- | --- |
| JetMoto, GeneratedPatcher, upstream-patches | Application, generated-code integration, and pinned runtime changes |
| *Tests, Tests, Validation/Replays | Regression projects and reusable input sequences |
| Lighting, LightingTools, TextureTools | Shipped lighting data and asset authoring tools |
| docs | Current documentation and README screenshots |
| .build, reports, logs | Ignored local builds, captures, and diagnostics |

Old numbered build reports and cloud handoffs were retired from the source tree. Their history remains in Git, with local originals archived under .build/archive/repository-cleanup-20260926. Existing generated reports remain available locally. The staged game, saves, original disc, previous build, and verified distribution were preserved during cleanup.
## Required user-facing delivery

The current playable installation is D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto, beside the original CUE and BIN tracks. Publishing into .build is only staging. Delivery requires deploying the self-contained runtime, dependencies, and accepted HD artwork into that user-facing folder, then validating the executable there with adjacent-disc discovery and fresh settings/save state. Back up and restore existing user data for this test.

Run-JetMoto.cmd launches the recorded deployment. Play-VisualQA.cmd launches the same user-facing game with all tracks available for development testing. Normal JetMoto.exe launch enables the HD visuals without extra environment flags.

## Single-executable publishing

Build-Windows.ps1 publishes one self-contained JetMoto.exe with managed assemblies, the .NET runtime, native libraries, and debug symbols bundled inside. No dependency DLLs belong beside the executable. Textures, Lighting, and the user's original disc remain external.

The bundle extracts its dependencies into the runtime's per-user cache on first launch. Full extraction preserves assembly locations needed by runtime integration and mod compilation. The game explicitly anchors disc discovery, artwork, saves, and logs to the actual process executable directory, rather than the extraction cache. A first-launch check must use a fresh cache and no loose DLLs, and inspect native menu and gameplay captures.

## Audio diagnosis

Normal launches write an audio summary every ten seconds to logs/last-run.log: mixed frame count, output restarts, maximum mixer service gap, maximum SPU mix duration, buffered CD samples, and prevented reverb arithmetic overflows. One early restart can occur while the SPU attaches; increasing restarts during gameplay indicate starvation.

AudioTests covers stopped-queue recovery, a stall during refill, repeated recovery without queue growth, and loud reverb arithmetic. Its --device option injects five 200 ms stalls into a test-only SPU using OpenAL's null device. This produces no audible output or desktop input.

Run-ReleaseValidation.ps1 normally mutes audio. Pass -AudioOutput to exercise the real sound device while rendering and replay input remain confined to the hidden game process. This can produce audible game sound. For silent mixing diagnostics, use JETMOTO_AUDIO_PROBE=1 together with ALSOFT_DRIVERS=null. Neither numerical checks nor a successful audio-device launch proves audible quality; distinguish these from listening tests.
Set `JETMOTO_AUDIO_CAPTURE` to an absolute WAV filename to capture the mix before OpenAL. The mixer stores up to 180 seconds of stereo 44.1 kHz PCM in preallocated memory and writes the file on normal shutdown. Use an existing writable parent directory. This is opt-in diagnosis, not enabled in normal play; no host recording device is used.

## Performance and memory

Normal launches leave execution tracing disabled; `--trace` enables it for diagnosis and `--no-trace` remains compatible. Stop checks remain enabled.

Decoded native/menu images use a shared 64 MiB CPU cache. Native GPU images use a 128 MiB cache, with stable menu registrations and reload after eviction. These budgets cover image residency, not total process/driver memory; decoder scratch space, queued references, render targets and original game data are additional. One image larger than the budget is permitted. Diagnostic overrides `JETMOTO_CPU_TEXTURE_MB` and `JETMOTO_GPU_TEXTURE_MB` accept 16–1024 MiB. Smaller budgets trade memory for reload work and should not be advertised as a free speed boost.

Known native banks and menu images are prepared at loading boundaries. Menu PNG checksum validation occurs when the image is decoded, while original-disc identity checks remain at startup. Dynamically identified regions and evicted images can still load on first use. Shadow CPU buffers and shadow/wake GPU storage are reused without reducing resolution; camera inverses are cached once per camera.

Set `JETMOTO_PERF=1` before launch to include bounded performance summaries in the normal log. These report host presentation interval percentiles, allocation rate, GC counts, shadow/wake CPU time, native texture residency, uploads and draw-time misses. Host presentation calls are not a measurement of physical display delivery. Captures and diagnostics add overhead; use ordinary uncaptured play for representative timing. RendererTests also uses GPU elapsed-time queries on GL 3.3/4.5 for repeatable sunlit, terrain-shadow and rider-shadow fixtures at 1x/2x/4x.

The original lighting shader remains in production. Pixel-equivalent shortcut experiments had workload-dependent regressions, including roughly 10–12% slower sunlit fixtures in some local 4x measurements, and variable warm-up stalls. A universal performance improvement was not established. The GPU fixture remains for measurements on other drivers before revisiting this change.

## Two-player replay validation

Replay steps optionally set `Player` to 1 or 2 (default 1). A replay containing player 2 steps connects a second process-local virtual controller; each player's pressed buttons are evaluated independently. Ordinary launches do not install replay providers. `Validation/Replays/split-screen-validation.json` enters head-to-head, accelerates the riders independently, pauses/resumes and changes the split orientation. This validates the game path without operating the desktop; it does not validate physical controller selection or bindings.

Pausing keeps the race's 16:9 presentation and retained side areas, while the menu remains centered at its original scale. Gameplay camera/lighting updates remain paused. Leaving the race restores the 4:3 front-end policy.

For short native video captures, `JETMOTO_CAPTURE_RAW=1` writes uncompressed RGBA frames instead of PNGs, with the same JSON dimensions and VBlank timestamps. This removes PNG compression from the render thread, uses more disk space, and remains opt-in alongside `JETMOTO_CAPTURE_DIR`. Convert offline using the recorded dimensions and preserve timestamp gaps when assembling video. This captures the game's own framebuffer, not the desktop. Recording overhead still makes it unsuitable for performance measurements.

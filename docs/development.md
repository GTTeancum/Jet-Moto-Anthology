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

The gate report must have passed set to true and identify the exact application and runtime SHA-256 hashes. Packaging uses .build/deployment.json, refuses existing output paths, and includes the self-contained runtime, artwork, instructions, licenses, and verification manifests. It excludes disc images and user data.

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

Run-JetMoto.cmd launches the recorded deployment. Play-VisualQA.cmd and the installed run.bat launch the same user-facing game with all tracks available. Normal JetMoto.exe launch enables the HD visuals without extra environment flags.

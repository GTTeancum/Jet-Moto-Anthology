# Build 10 — reconstructed experimental shader package

## Recovery and scope

The active runtime contained the original uploaded disc/toolchain/dependency/model files but no Build10 source directory. The complete Build09 ZIP was recovered from the Library and materialized in this runtime: 248,636,868 bytes. Its ZIP CRC and file-manifest hashes were checked before packaging.

The shader changes visible in the previous conversation were reconstructed onto that actual source. No unseen Build10 checkpoint, previous shadow implementation, original-game lighting test, or earlier screenshot is claimed as recovered. This is an explicitly experimental packaging checkpoint, not the final requested world-lighting/water feature.

The draft contains neighbour-luminance screen shading and blue/green-pixel shimmer. It cannot distinguish water from other similarly colored pixels. It does not implement real cast/contact shadows or world/material lighting. To avoid silently changing gameplay HUD/sky/vehicles, the draft is disabled unless `JETMOTO_BUILD10_PREVIEW=1`; the included Preview-Build10.cmd supplies it for one child process. Menus/pause and 24-bit display conversion are excluded. No geometry moves.

## Source changes

- Two cumulative overlay files extended: `GlCore.cs` and `GlShaders.cs`.
- One new overlay: `Diagnostics/Build10Preview.cs`, with explicit off-by-default environment policy.
- All other twenty Build09 overlays unchanged. All twenty-three overlay entries have exact pristine-before and delivered-after hashes.
- Game Program.cs adds preview configuration/reporting and the Build10 label. Project version is 0.10.0 and publishes the optional preview launcher.
- Generated game C#, 31 existing hooks, native bank identities, rider/LOD/pose/arena code, widescreen code, native material resolver and texture/effect PNGs/sidecars remain unchanged.
- Build/deployment labels and log collector are updated; the non-clearing deployment implementation is unchanged. Texture-only installation is blocked because it cannot add renderer code.

Reconstruction also clamps relief-neighbour sampling to the displayed framebuffer region, uses viewport-local shimmer coordinates, and bounds time over a common trigonometric period to avoid long-runtime float growth. These are integration changes made in this turn; they are not represented as preserved original scratch bytes.

## Checks in this turn

The supplied SDK, source ZIP and dependency ZIP matched UPLOAD-CHECKSUMS.sha256. All 47 local NuGet package hashes matched the supplied manifest. Scratch restore used the local feed only, offline certificate revocation and disabled package signature network validation after independent uploaded-hash verification. These scratch-only settings were not inserted into the Windows build script.

The full generated Linux game source built with zero errors. Clean runtime compilation retained the two existing nullable-reference warnings in the upstream CD/SPU debug panels. Subsequent incremental builds may report zero warnings; no claim is made that the upstream warnings were repaired.

Inherited actual OpenGL pixel regressions passed: **1,557 checks (519 each on GL45, GL33 and GL21 at 1x/2x/4x)**, plus 13 static checks and three isolated-suite success checks. Preview remained off for these baseline checks.

New preview tests passed on those three backends at 1x/2x/4x: **90 actual GPU checks**, plus 32 repeated environment-policy checks and three child-suite exit checks. They exercise shader compile/link, unchanged menu pixels, changed preview pixels, changing time uniform, exact restoration after disabling, race-to-menu bypass, unchanged source VRAM, retained no-dither state and no GL errors. These are controlled colored-frame fixtures, not actual game screenshots and not proof of correctly identifying real water.

All **31 generated hooks** verified with zero new applications. No generated gameplay instruction was changed by this package.

Final Linux build and Windows-targeted managed publish both passed. Both published trees contain all 1,406 original PNGs, 15 effect sidecars and the exact preview launcher. Both managed game assemblies have PE GUI subsystem 2. The audit and published-asset hashes are recorded in reports/build10/final-integrity.json. The Windows check is `win-x64`, framework-dependent, `UseAppHost=false`, `EnableAppHostPackDownload=false` from Linux. The normal local Windows script still uses the user's Windows SDK to create the actual self-contained EXE.

## Not tested / not implemented

No new real-disc game session, new video/audio capture, native Windows execution, Windows batch/PowerShell execution, driver-specific performance, all-track test or split-screen gameplay test was performed for Build10. Existing Build09 gameplay logs/screenshots are historical evidence only. The new preview may affect gameplay HUD, riders, sky and spray appearance because the final framebuffer has no semantic mask in this draft.

Actual world normals, static/dynamic shadow maps, contact occlusion, material-tagged water, world-anchored ripples, reflection/refraction and correct selective HUD/effect exclusion have not been implemented by this screen-space draft. Packaging and passing shader tests must not be read as completion of those features.

## Delivery

The cumulative ZIP includes generated/buildable source, twenty-three upstream overlays, the unchanged full 1,406-PNG Build09 texture/effect pack, fifteen native material sidecars, tests and reports. It excludes the supplied disc, model weights, SDK, NuGet cache, vendor source copy, fonts and compiled executable/DLL outputs. The source and ZIP checksum manifests are checked against the exact packaged bytes.

Default target: `D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\JetMoto.exe`.
Normal EXE launch keeps preview off. `Preview-Build10.cmd` enables the experimental shading/shimmer for a single launch. A full build, not texture-only replacement, is required.

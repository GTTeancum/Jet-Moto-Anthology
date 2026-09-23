# Build 04 validation report

Date: 2026-09-21

## Delivered behaviour

True Hor+ 16:9 gameplay, original 4:3 front-end and pause menus, and the cumulative permanent no-dithering fix. The game's original projection and vertical field of view are retained; additional geometry is rendered outside the old horizontal boundaries. The single-player HUD remains proportionally unchanged in its original central safe area. Existing double-click disc discovery and the deployment location remain unchanged:

`D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\JetMoto.exe`

No disc files, memory cards or settings are included or overwritten. No new runtime dependencies or graphics toggle are required. Audio, texture assets and original colour precision are unchanged.

## Game-specific changes, verified against the supplied executable

Target: SCUS-94309, boot executable SCUS_943.09, SHA-256 `f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48`. RecompOne source remains pinned to `d81dec8c9622fdcd0865d73588a3baa8d3c3a605`.

`JetMoto/Widescreen.cs` implements the policy. Five exact generated-code hooks are declared in `generated-hooks.json`:

- `8014CA18`: enter the real race loop with an exception-safe scope. Leaving this function restores the front-end aspect. The outer track-information/loading state is deliberately not used as the race detector.
- `8011B3D0`: create a deferred pause scope. This function runs every frame, so ordinary checks do not change the aspect.
- `8011B4E8`: activate that scope only on the actual pause branch. Returning from the pause handler restores the race aspect.
- `800F96CC`: prepare the widened camera-local horizontal visibility planes before the game transforms them into object space.
- `800DD2E0`: observe the number of visible scene-list entries submitted to the custom renderer.

The ten camera profiles start at `8016E834`, stride `0x60`. Original side normals are read from the selected profile at offsets `0x30` and `0x38`; profile selections and camera-context pointers are at `801CBB10` and `801CBB00`. The hook writes the two appropriate matrix columns in the selected camera context. It derives them afresh from the immutable profile every time, avoiding accumulated widening. The horizontal component is reduced to accommodate the wider field, then the vector is renormalized to the original length. This preserves the meaning of the game's bounding-sphere rejection tests. A four-native-pixel guard beyond the rounded full-view margin is included.

Near/far and vertical planes, camera transforms, GTE H/OFX/OFY, physics and guest HUD coordinates are not modified. Restoring a menu profile restores its exact original side normals.

The original visible-scene list has a capacity of 280 entries. Its backing storage ends immediately before another game buffer. This update does not blindly raise the limit or risk overwriting adjacent memory. Logs include `visibleMax` and `visibleLimitHits`, and emit a warning if capacity is reached. No saturation was observed in the sampled runs. This is not a guarantee that every location on every circuit has been tested.

## Renderer changes

The shared `GlCore.cs` covers Gl45, Gl33 and Gl21. Its display classification cache now notices aspect-only changes. Pending geometry is flushed before an old render target is destroyed, so menu/race target replacement does not drop an existing batch.

A 320-column gameplay image allocates a 428-column target, but samples the exact 426 2/3-column field needed for 16:9. This avoids a small scaling error from treating the rounded allocation as the exact field of view. Pausing can reuse the existing race target: the presenter crops its centre and presents it at 4:3 rather than squeezing the entire wide picture into the menu.

Full-viewport solid clears/fades cover the extra area. During Blackwater Falls testing, its solid sky clear exposed black outside strips; extending this clear corrected the cutoff. Textured sprites, partial HUD rectangles and small inset viewports are not stretched by this rule.

Top/bottom gameplay views retain their independent vertical clip regions. For side-by-side views, each camera receives a horizontal translation to its new centre, not a scale. Only outside clipping edges extend; the shared centre divider does not move and cannot be crossed by the other player's primitives. Both x-offset double-buffer layouts are covered by the pixel suite. Split-screen renderer tests are not a claim of a complete two-player in-game test.

All Build03 dither removals remain: no shader dither matrices/branches, no packed dither request, guest GPU status preserved, and native GL_DITHER forced off. No audio code is changed.

## Executed final validation

1. **Linux compilation passed.** A clean full generated-game build completed with zero errors and the two existing upstream nullable debug-panel warnings. The final incremental build after the last renderer change completed with zero warnings/errors. SDK: uploaded .NET 10.0.401, with the uploaded local NuGet feed.
2. **Windows-targeted managed publication passed.** `win-x64`, `SelfContained=false`, `UseAppHost=false`, `EnableAppHostPackDownload=false`. The managed PE subsystem is 2, Windows GUI. This does not execute Windows code or produce/test the native self-contained Windows apphost here. The supplied Windows script still creates that EXE from the user's matching Windows SDK.
3. **108 camera/policy assertions passed.** Both camera slots, all single-player and split-screen horizontal-plane variants, preserved normal length, unchanged vertical/near/far data and GTE projection registers, nonaccumulating updates, exact menu restoration, near/mid/far edge tests, meaningful original-plane negative controls, nested scopes, 100 transition cycles, and exception unwinding.
4. **558 renderer assertions passed: 186 per backend.** Gl45, Gl33 and Gl21 each ran in an isolated process at 1x, 2x and 4x. Coverage includes real shader compilation/linking, real pixel comparisons for permanent no-dither rendering, polygons wholly outside each old screen edge, pending-batch preservation, cached-frame pause cropping, 640x480 menu transitions, repeated race/menu cycles, full sky clears, unchanged partial HUD bands, offset double buffers, translated split-screen centres, unchanged object widths, divider isolation, and independent top/bottom clears. Thirteen non-graphics renderer assertions and three child-process success checks also passed.
5. **All 14 existing launcher regression tests passed.** These use the exact shared disc-selection implementation.
6. **All 11 CD regression assertions passed.** Boot hash, startup-asset byte comparison, status and synchronization checks, and command/data-ready interrupt distinction were exercised using the uploaded disc. The CD runtime patch is unchanged.
7. **Actual RecompOne regeneration from the uploaded disc passed.** It emitted 1,874 functions with 36 HLE replacements. Applying the five hooks produced a `main.cs` byte-for-byte identical to the shipped file. A second patch pass was idempotent. Deliberately modifying an anchor caused a nonzero exit without overwriting any file bytes. Removing only the five hooks reproduces the exact Build03 `main.cs`.
8. **Real-disc graphical smoke testing was performed.** Development runs entered Joyride and Blackwater Falls, rendered newly exposed scenery, and tested pause/resume/return-to-title transitions. The Blackwater sky issue was identified and corrected during those runs. The final combined renderer was also run with the complete game: Joyride countdown, driving and turning rendered at 16:9 with no scene-list saturation; the 90-second run stopped cooperatively with expected smoke exit code 3. That code denotes the test deadline, not a crash or a completed playthrough.
9. **Cumulative-source integrity checks passed.** Overlay hashes match the exact vendor files used for compilation and the original source hashes. The remaining pinned upstream files are unchanged. Deployment copier, disc locator, CMD launchers, function maps, Entry.cs and Stubs.cs remain unchanged from Build03. Temporary scratch instrumentation is not shipped.

## Evidence and test limitations

`reports/build04/final-regressions.log` contains the final complete passing suites. `final-builds.log`, `linux-clean-build.log`, `real-disc-regeneration.log`, `regeneration-integrity.txt` and `preservation-and-integrity.txt` document compilation and reproducibility.

`final-gameplay.log` and `gameplay-16x9.png` are from the final combined renderer's Joyride run. `menu-transition-gameplay.log`, `pause-4x3.png` and `return-to-title-4x3.png` document the earlier Build04 pause/menu checks before the final split-screen refinement. `blackwater-sky-fix.png` was captured when correcting the solid sky clear; the final renderer suite retests that behaviour, including both buffers and split-screen layouts. These are in-game captures, not mockups.

Environment: Linux scratch, Xvfb, Mesa 25.0.7-2 llvmpipe (LLVM 19.1.7), null audio output. The Gl21 suite exercises the actual GLSL 120 backend using the driver's compatibility context; it is not a test on physical OpenGL 2.1-only hardware. Initial test-development failures were corrected before the final complete passing run; notably the split-screen centre test was corrected to measure quarter points of the presented texture rather than coordinates in the pre-presentation render target.

Native Windows execution, Windows PowerShell deployment execution, audio listening, complete laps/all tracks, and a complete two-player session were not tested here. Compilation, pixel tests and sampled gameplay are not an exhaustive playability guarantee. If a missing object or edge gap is encountered, the collected logs include the current aspect and scene-list counters.

Historical Build01/02/03 reports remain unchanged for provenance. Normal users build this package once and open the same deployed JetMoto.exe; they do not need to apply older builds first.

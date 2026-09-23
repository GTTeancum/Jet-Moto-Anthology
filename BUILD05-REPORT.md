# Build 05 — perspective-correct rendering

## Scope

This cumulative source package adds always-on perspective-correct texture interpolation and subpixel GTE projection to the supplied USA Jet Moto recompilation. It retains Build 04's Hor+ 16:9 gameplay, 4:3 menus/pause, camera visibility-plane expansion and sky clears; Build 03's permanent no-dither policy; and the original disc, CD, double-click launcher and safe Windows deployment fixes.

Pinned RecompOne source: `d81dec8c9622fdcd0865d73588a3baa8d3c3a605`.

Game executable: `SCUS_943.09`, SHA-256 `f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48`.

The source contains 1,874 generated functions and retains 36 HLE substitutions. The SDK/dependency set is unchanged. The package does not contain a disc, SDK, NuGet feed, compiled game or font files.

## Rendering and precision changes

The game requires PGXP CPU/memory tracking and texture correction at startup. Loading old settings with PGXP disabled cannot disable the game-selected policy. The original culling/backface path is retained; the screen-coordinate vertex cache is not used to guess depth.

All three graphics backends interpolate `(U/Z, V/Z, 1/Z)` and divide in the fragment shader. Screen-space positions keep constant clip W, so clipping does not depend on the depth used for texture interpolation. This is projective texturing, not stretching, filtering, mesh subdivision or a texture replacement. Original screen-linear Gouraud lighting and draw ordering are retained.

During testing, using camera depth directly as homogeneous clip W exposed large missing wedges at the outside corners of Joyride's starting grid. Disabling subpixel XY alone did not remove those wedges; separating screen-space clipping from projective UV interpolation did. The final Joyride and Blackwater Falls captures use the corrected shader path. The legacy oversized-primitive rejection was not disabled as a workaround.

The GTE retains fractional transformed X/Y before integer IR rounding for rendering. Original integer registers, packed screen coordinates, flags, projection distance and gameplay camera configuration remain available to the original game unchanged. Precision XY is only used when it stays within the configured compatibility tolerance. Valid memory-proven depth remains usable even when precise XY is outside that tolerance.

Precision metadata now distinguishes RAM mirrors, scratchpad and MMIO. Ordinary writes invalidate the affected coordinate halves, including same-value writes. Partial writes retain the origin of each surviving coordinate half: replacing last frame's X and Y separately can reconstruct this frame's new depth. This fixed a large loss of correction on reused vertex buffers. Unrelated vertex halves are not combined merely because their integer positions or depths happen to match.

Generated LH/LHU/LW instructions capture their effective address before updating the destination register. This matters when the destination is also the address base. GPU packet handling preserves command-source addresses through multi-command and split contiguous packets, including DMA and generic SDK fallback paths. Scalar CPU operations cannot silently carry unrelated old geometry depth into rendering.

If any corner lacks a usable finite positive depth, the entire primitive uses consistent original mapping. A quad cannot mix a corrected triangle with a mismatched fallback triangle. This conservative behavior is intentional for genuinely untracked, 2D or software-generated data; the update is not a claim that every possible primitive is corrected or that every remaining visual artifact is eliminated.

A separate presentation regression was fixed: a valid cached widescreen frame remains eligible between draws, rather than reverting to 4:3 solely because a few host presentation frames have elapsed. Cached pause frames retain the centre crop. Pixel tests cover held frames through 1,000 presentations.

## Executed validation

All listed checks below were executed in the Linux scratch environment against the final implementation, not inferred solely from compilation.

| Check | Result |
|---|---|
| Linux game build | Passed; two existing nullable warnings in upstream debug panels |
| Windows-targeted managed publish | Passed: `win-x64`, framework-dependent, no apphost |
| Windows managed PE subsystem | 2, Windows GUI; inspected in the produced assembly |
| Precision/provenance regression suite | 56 assertions passed |
| OpenGL pixel suites | 819 assertions passed: 273 each on Gl45, Gl33 and Gl21, covering 1x/2x/4x |
| Non-graphics renderer checks | 13 assertions passed, plus 3 isolated-backend success checks |
| Widescreen camera/policy tests | 108 assertions passed |
| Launcher/disc-selection tests | 14 tests passed |
| CD regression checks | 11 assertions passed against the actual supplied disc |
| Recompiler regeneration | Actual disc recompilation succeeded; generated main.cs plus the five hooks reproduces byte-for-byte |
| Generated load-address audit | 19,552 load instructions capture the original effective address |
| Hook integrity | Reapplication is byte-idempotent; a changed instruction anchor is rejected without modifying the file |
| Source overlays | 18 cumulative files verified against the exact uploaded pristine source hashes |

The pixel suite checks analytical inverse-depth UV interpolation, affine negative controls, equal-depth invariance, depth-unit invariance, off-screen clipping coverage, invalid/missing-depth fallback, preserved lighting, and planar quad diagonal independence. It also reruns the retained dithering, true-wide, sky-clear, menu transition, split-screen proportion and divider-clipping tests. Contexts were provided by Mesa llvmpipe; this is real GPU API readback through software OpenGL, not a test on the user's graphics driver.

## Actual game checks

A bounded 200-second final Joyride session exercised the starting grid, racing/steering, pause, resume and quitting to the title menu. A second bounded 75-second session automatically selected Blackwater Falls and exercised its starting grid and driving. That second session launched without a disc argument, using the CUE and all 14 BIN tracks adjacent to the application. Old PGXP-disable settings were present during these tests; the required correction policy still took effect.

The corresponding logs and real captures are under `reports/build05`. Both sessions deliberately end with the documented smoke-timeout exit code 3; this is not a crash or a playability certification. The observed scene lists did not reach the fixed capacity of 280.

The diagnostics count submitted textured polygons, including off-screen submissions and menu/pause work. They are not a frame count, image-quality score or proof of universal coverage. In the Joyride run, all submitted textured polygons were depth-tracked before the pause test; some later pause/resume work used the conservative fallback. The Blackwater run likewise included a small partial-depth fallback count. Those counts are preserved in the logs rather than concealed or labelled full coverage.

Audio output was not listened to: the scratch environment uses null audio. Audio code, disc assets, physics and saves were not changed by this update.

## Not verified here

Native Windows execution, the Windows PowerShell build/deployment script on an actual Windows machine, the user's GPU/driver behavior, every location on every track, a complete race/tournament, and a complete two-player session were not tested here. Split-screen rendering has automated pixel coverage, not a complete gameplay claim.

## Build and deployment

Use `Build-Windows.cmd` with the existing original input kit. No previous build installation or packager rerun is required. Default deployment remains:

`D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\JetMoto.exe`

The build produces a self-contained Windows application locally, including its required accompanying files. It preserves adjacent CUE/BIN tracks, settings and memory-card files. The application continues to launch by double-click and discover its adjacent disc. Full commands and diagnostic options are in `README.md`.

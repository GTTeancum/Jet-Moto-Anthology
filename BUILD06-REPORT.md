# Build 06 — native-asset texture replacements + 4x test pack

## Scope and source

Cumulative on Build05. All previous source overlays, generated-code changes, CD fixes, adjacent-disc GUI/deployment behavior, permanent no-dither policy, true 16:9 gameplay/4:3 menus, widened visibility/sky fixes, perspective mapping and subpixel projection are retained.

Pinned uploaded RecompOne commit: `d81dec8c9622fdcd0865d73588a3baa8d3c3a605`. Original USA SCUS_943.09 SHA-256: `f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48`. SDK 10.0.401 and the same 47-package feed. 1,874 generated functions, 36 HLE substitutions. 21 cumulative upstream overlays, including all 18 Build05 paths. There are now 11 exact-anchor generated hooks.

## Native integration, not VRAM replacement

The original file loader at 800EDF28 reports the native DMD filename, destination and completion. The loaded bytes must equal the corresponding original disc model before an identity is activated. TMS and DMD v0x43 headers/timestamps and original TMS source-file fingerprints are checked. A source primitive's DMD offset resolves to an original TMS image record and its original ordinal/ID. Original placement, palette and UV metadata are read from those source records, not discovered from live VRAM.

The main native renderer 800DD2E0 has source/emission hooks at 800DD5B4 and 800DDF44. The late testing run exposed unfinished deferred packets: the original engine reserves them at 800DDDCC without linking their headers. They are now explicitly deferred, not interpreted as finished GPU packets. Native function 8010F75C subdivides them, and a hook at the 8010FF50 finalizer's 8011014C return binds the verified original material to all four finalized children. This preserves replacement identity for close-range geometry instead of dropping those surfaces to original-resolution sampling.

CPU command provenance is attached to native outgoing payload words, not to VRAM addresses or pixel hashes. All ordinary payload writes, including same-value writes, invalidate the old token. Header linking does not erase it. The queued renderer material is an immutable snapshot. Unbound/invalid/ambiguous material stays original. Emission spans and all subdivision children are bounds-checked before binding; no guessed partial-packet association is used.

The PNG is a separate native GL texture sampled with explicit original UV origin/dimensions and the previous perspective interpolation. The upstream VRAM matching/dumping resolver is bypassed by a hard renderer guard. Original low-resolution VRAM still implements normal guest GPU behavior and fallback; it is not the HD replacement lookup.

## 4x pack and authoring

All 1,406 images in the 34 original TMS banks were decoded and upscaled at four times width and height. The PNGs and manifest are included. They are deterministic Lanczos-filtered test images, **not AI-generated detail or a completed hand-restored pack**. Pillow 12.3.0 was used. RGB filtering uses opaque occupancy, not STP as fractional opacity; native alpha categories 0/128/255 are retained. Native sampling preserves their categorical meaning.

The original files remain unchanged. PNG filenames are original bank path, record ordinal and original texture ID. Source SHA-256 values are revision checks, not rendered-pixel identities. Optional `Textures/Overrides` files take priority, but must have exact 4x dimensions and valid alpha classes. Invalid or absent replacements use the original material. Images decode lazily and remain cached until exit.

The batch extraction/upscale Python tool is provided in TextureTools, but is not needed to build/play this package. The built-in pack is copied automatically by the game project and safe deployment script. Custom Overrides are excluded from publishing and explicitly refused by the deployment copier.

## Executed validation

| Check | Result |
|---|---|
| Linux final game compilation | Passed |
| Windows-targeted managed publish | Passed, win-x64, framework-dependent, no apphost; GUI subsystem 2 verified |
| Native asset tests | 149 passed, including all 34 original banks and 1,406 parsed native records |
| Actual GL pixel tests | 999 passed: 333 each on GL45, GL33, GL21, across 1x/2x/4x renderer scales |
| Static renderer checks | 13 passed, plus three successful isolated backend runs |
| Previous precision/provenance tests | 56 passed |
| Previous widescreen camera/policy tests | 108 passed |
| Previous launcher tests | 14 passed |
| Previous CD checks | 11 passed against the uploaded disc |
| Original-disc code regeneration | Passed: main.cs and Entry.cs reproduce byte-for-byte after the 11 hooks |
| Hook integrity | Idempotent; changed anchor rejected without modifying the target |
| Cumulative overlay integrity | All 21 before/after hashes verified against the exact uploaded pristine source and compiled working tree |
| PNG integrity | All 1,406 hashes, exact 4x RGBA dimensions and categorical-alpha values verified |
| Windows publish content | All 1,406 PNGs copied byte-for-byte; no disc, save or Overrides content |

GL tests use actual API readback through Mesa llvmpipe, not the user's GPU. They verify that fine 4x detail survives rather than being resized into original slots; native identities remain distinct when page/palette/UV placement is identical; changing VRAM pixels does not alter an explicitly bound PNG; original and invalid-material fallback remains valid; transparent/STP/opaque behavior and projective mapping remain correct. The texture-window tests cover noncontiguous bit masks and retained fractional coordinates, including exact boundary tests for GL1.20. The complete prior no-dither, wide-edge, sky, held-frame, menu-transition and split-screen pixel suites are also retained.

The first GL21 test run exposed a test readback point exactly at a texture-window discontinuity. Its sample was moved off the phase-dependent boundary and seven constant-UV boundary probes were added. The final all-backend results above include those stricter probes. No failing graphics assertions were skipped.

## Actual game checks

The final 240-second bounded session launched with no CUE argument and discovered all adjacent disc tracks. It traversed the title/attract path, rider and race-type menus, track selector, Joyride grid and driving, including close-range track surfaces, and pause/resume. Screenshots and the complete runtime log are in reports/build06. The run deliberately ended at the smoke deadline with exit 3; this is not a crash or a complete playability certification. Menu-input automation initially missed the title transition; the subsequent gameplay controls were sent manually through X11. A full return-to-title command from the final pause was not completed before that deadline and is not claimed as tested here.

At final stop: 715,047 native command bindings, 1,297,459 resolutions, 127 distinct decoded PNGs. 17,389 original deferred primitives completed subdivision binding; zero malformed-packet fallbacks and zero renderer material-validation fallbacks were recorded. The legacy resolver reported **calls=0 and hashed=0**. These counters include repeat frames/off-screen work and are not unique texture coverage or image-quality scores. Original/unmapped primitives and prior conservative depth fallback still exist. The observed scene list peaked at 165 with zero capacity hits (limit 280).

Earlier exploratory sessions also visited other native banks, but are not substituted for full final-track coverage. Audio was not listened to; the scratch run used null audio. Audio code and game physics were not changed.

## Limits / not verified

Native Windows execution, Windows PowerShell deployment on the user's machine, the user's graphics driver, every track location, a complete race/tournament and full two-player gameplay were not tested here. The Windows check is managed publishing, not a native Windows run; the user's existing Windows SDK supplies its own apphost when the normal build script creates the self-contained EXE.

Providing every TMS image is not equivalent to covering every asset path. Movies, .BS backgrounds, standalone TIM-only paths, unidentifiable runtime graphics and palette-content animation are not universally remastered or exhaustively exercised. Dynamic page/palette selection and UV bounds are validated, but content-changing effects need further visual review. Missing or untrusted depth continues the Build05 whole-primitive fallback rather than inventing perspective. There are no new mipmaps/anisotropic enhancements. This is a functional original-asset-linked replacement system with a filtered first-pass test pack.

## Deployment

Default EXE remains `D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\JetMoto.exe`. Double-click to load adjacent CUE/BIN and built-in 4x PNGs automatically. Close the app before updating. Original input kit, disc, saves, settings and custom Overrides are preserved; built-in pack files are updated. No earlier build or packager rerun is required. See README.md for exact commands.

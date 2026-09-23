# Jet Moto / RecompOne — Build 08 validation

Build 08 is a cumulative source and texture update based on Build 07. This report describes checks actually performed for this release on 2026-09-21. Earlier reports remain historical evidence, not additional Build 08 test runs.

## Delivered result

The verified original rider and moto selectors now choose the highest original LOD, without an option to downgrade. This covers all 20 race riders in all ten original race-model banks and the 20 verified rider-selection preview chains. Normal visibility, clipping and backface tests remain. Track/environment LOD is not globally disabled.

A focused texture pass updates **44 PNG entries**, representing four original rider/moto atlases duplicated across ten race banks and the rider-selection bank. **The other 1,362 PNGs are byte-identical to Build 07.** The complete 1,406-image pack is included. The green and blue jacket decals receive clearer Mountain Dew and AXIOM artwork sourced from the original disc, not invented replacement lettering.

All earlier fixes are retained: startup/CD handling, the adjacent-disc GUI launcher and protected deployment, permanent dithering removal, true Hor+ 16:9 gameplay with pillarboxed 4:3 menus/pause, expanded visibility/sky behavior, depth-correct textures and subpixel projection, and original DMD/material/TMS bank/ordinal/ID texture loading including subdivision. There is no runtime VRAM dumping or image-hash matching.

**A full rebuild is required.** Texture-only installation cannot add the LOD, pose or render-storage fixes to an older executable.

## What caused the flattened riders

Inspection of the supplied original DMDs established four rider/bike levels: 136 source polygons, 49/50, 30/33, and four. These are source primitives, not a claim about a modern engine's triangulated polygon counter. The original per-race shared detail allocation can assign a low level even to a nearby opponent. Its normal budget allows only one highest-level rider, with additional groups using progressively cheaper models.

`RiderDetail.SelectRaceLod` selects branch zero before the original game copies/updates the selected articulated branch. `SelectDrawLod` reinforces the same verified selection at rendering. The guards require original loaded DMD provenance, the correct node type, tag, rider ID and four-child layout; unrelated scene/accessory selectors are unchanged. `SelectPreviewDistance` applies only to verified rider-selection preview chains. The final gameplay session saw all 20 selector IDs (`0xFFFFF`) and zero corrective draw-time selections: the earlier update hook was supplying the highest branch.

Forcing the articulated model exposed an initial-pose issue during the introductory grid presentation, before ordinary per-frame animation had populated its pose. `InitializeIdlePose` now initializes only a verified, completely empty 24-word pose from the game's own neutral riding pose. It validates ownership of the translation node and seven bones against the specific rider's highest branch, not merely the same bank. Existing animation/pose values are not replaced. The fixed-point matrix arithmetic reuses the original sine/cosine table; tests compare it against the original generated routines.

This retains the game's highest **original** 136-primitive model. It does not create newly sculpted high-polygon riders or remove all limitations of the original mesh, faces, silhouettes or tiny source artwork.

## Safe render capacity, not an unchecked constant change

A first high-LOD development run exceeded the original GPU draw-command allocation and dropped the final riders. The original CPU scene/matrix arrays also had fixed adjacent storage. Raising their limits in place would risk corrupting other game data.

`RenderArena` therefore relocates the related CPU work arrays together into a bounded independent host region and expands scene/matrix capacities to 1,024. GPU packet storage uses up to eight independent 512 KiB slots, with reserved guard space. The entire host render arena is 4.25 MiB. The original 2 MiB guest RAM, its mirror window, original assets and VRAM are not enlarged or replaced.

Command slots are keyed to verified original allocations, not just view-context addresses. Menu/subview contexts that share an allocation preserve the existing host cursor, so one view does not overwrite another view's queued packets. Original-RAM packets already queued in ordering tables keep their addresses and links. Unknown pointers, invalid ranges, capacity overruns and changed canaries fail explicitly rather than silently corrupting or truncating geometry.

The runtime memory, PGXP metadata and native material-provenance paths support this independent host work region. Only the explicitly registered command range is accepted as host GPU packet addresses. Both the SDK ordering-table draw path and hardware GPU linked-list DMA were tested. Unregistered addresses and original end markers retain their original behavior. This is **render-command/work storage, not a VRAM texture-replacement hack**.

The final session observed a maximum scene list of **303**, above the old 280 limit, and **128,980 bytes** of commands, above the original small allocation. All capacity/canary limit counters stayed zero. The final session used two command slots. Highest-detail opponents require more rendering work; no native-Windows performance figure is claimed.

## Focused texture work actually performed

The inputs are the four original 128×256 RGBA atlases. Outputs remain 512×1024, preserving their UV layout and exact original transparency categories. The old Build 07 upscaled images were not used as inference inputs.

The general-purpose `RealESRGAN_x4plus` RRDB network actually ran four transformed passes per original atlas: identity, horizontal reflection, 180-degree rotation, and their combination. Outputs were transformed back and averaged. Inference used reflected source context padding, followed by broad color-drift correction. Sixteen neural inference passes produced the four unique refined atlas results. The logged per-atlas batch durations were 64.75, 88.90, 68.85 and 64.46 seconds on this CPU environment. No anime, face-enhancement, diffusion or style-transfer model was used.

Two decals were restored from clearer original artwork already present in the disc assets:

- Mountain Dew: `ISLAND1/ISLAND1/0036-00005B22.png`, original source crop and original jacket placement recorded in `rider-decal-profiles.json`.
- AXIOM: `ISLAND1/ISLAND1/0035-0000ED2A.png`, using the original winged-A artwork rather than the indistinct low-resolution mark.

The yellow and white atlases receive the stronger neural pass without newly invented logos. The clearer decals are the most obvious targeted changes. This is not a claim that neural inference recovers every lost fine detail or that every pixel is objectively better. Faces and very small features remain source-limited. Original categorical alpha is preserved exactly; this does not create smoother new alpha-mask geometry.

`TextureTools/refine_riders.py` reproduces the authoring pass using the original disc or original PNG tree, supplied model weights and a complete base pack. It validates model/source fingerprints and writes only to a new output directory. The exported/refactored tool was actually rerun on the green atlas (four passes, 63.26 seconds); neural and final restored-decal pixels matched the original batch. The refactored decal stage was checked against all four final atlas results. Race/preview ID aliases are explicit; runtime identification remains original bank/ordinal/material identity, not these offline reuse hashes.

The four final atlases were copied consistently to their 44 bank/ID entries. The complete pack still has 1,390 neural images and 16 unchanged flat-color entries. All 551 duplicate-original groups remain internally consistent. No Python, model data or AI processing is needed when the user runs the game.

The included `rider-texture-comparison.png` shows actual Build 07 and final Build 08 PNG crops at the same output-pixel scale. `texture-comparison-sources.json` records those crops. It is not an AI-generated depiction of gameplay.

## Fresh automated verification

| Check actually run | Result |
| --- | --- |
| Rider detail / pose / host render arena | **1,771 passed, zero failed** |
| Original race/preview source coverage | Ten race banks / 200 rider selectors; 20 preview chains |
| Fixed-point rotation comparison | 512 random angle triples / 4,608 matrix elements match original generated rotation routine |
| Initial-pose application | All 200 original rider bindings compared with original generated pose application |
| Native asset regressions | **149 passed** |
| Perspective / precision regressions | **56 passed** |
| Widescreen / visibility regressions | **108 passed** |
| Adjacent-disc launcher regressions | **14 passed** |
| CD regressions against supplied disc | **11 assertions passed** |
| Generated exact-anchor hook integrity | **31 verified**, newly applied zero on final generated source |
| Graphics pixel regressions | **999 passed**: 333 each on Gl45, Gl33 and Gl21 |
| Renderer static / child-suite checks | 16 passed, zero failed |
| Actual runtime texture PNG loader | **1,406/1,406** loaded with exact dimensions, original IDs, hashes and cache behavior |
| Original alpha correspondence | **187,508,736** output alpha pixels match exact 4× source footprint |
| Complete Python pack audit | All identities, output hashes, 4× dimensions, alpha and duplicate consistency passed |
| Cumulative texture comparison | 44 changed; 1,362 byte-identical to Build 07 |
| Cumulative source comparison | Original generated instructions unchanged outside 20 new hooks; all 11 prior hooks retained |
| Linux final managed compilation | Passed; actual compiled game executed in this environment |
| Windows-targeted managed publish | Passed, win-x64 framework-dependent without Windows apphost; GUI subsystem 2 |
| Published texture verification | Both Linux game output and Windows-targeted output contain the complete exact final pack |

The new arena tests cover independent RAM/mirror/scratchpad/MMIO behavior, range boundaries, negative lengths, all CPU storage slots and canaries, capacity failures, copied menu/subview cursors, separate framebuffer allocations, native material invalidation, CPU-to-host-packet depth/subpixel provenance, SDK DrawOTag and GPU linked-list DMA. Deliberately invalid unit-test inputs are expected to throw; no such failure occurred in the final gameplay session.

The clean builds retained two pre-existing nullable warnings in the upstream CD/SPU debug panels. No claim of warning-free upstream source or native Windows execution is made.

Windows-targeted validation used the uploaded offline packages with `UseAppHost=false`, `EnableAppHostPackDownload=false` and `SelfContained=false`; it validates the Windows-targeted managed build, not a locally executed Windows EXE. The user's normal `Build-Windows.cmd` still creates the self-contained Windows application using their original Windows SDK/input kit.

## Final game session

The final combined source and exact final texture pack were run on Linux with Mesa llvmpipe and a null audio backend. The application found the adjacent CUE and all 14 BIN tracks without a disc argument. The disc bytes were not modified.

The final session lasted approximately **871 seconds**, covering title/attract rendering, rider selection, race/track menus, Joyride grid and player-controlled driving, pause/resume, return to title, a second rider/race selection, Blackwater Falls grid and player-controlled driving, and pause/resume on that second track. A deliberate Ctrl+C ended the session cooperatively with exit **130**, not a runtime crash. No completed lap, completed race or multiplayer session is claimed.

Screenshots visually checked include `final-grid.png`, `final-driving.png`, `final-pause.png`, `final-rider-menu.png`, `blackwater-selection.png`, `blackwater-grid.png`, `blackwater-driving.png` and `blackwater-pause.png`. Highest-detail riders are present on both observed starting grids; the last green riders are no longer omitted by the old command-buffer ceiling. Menus/pause remain pillarboxed and resumed gameplay returns to the widened view. The baseline Build 07 grid capture is included for context; it is not a perfectly synchronized frame comparison.

Final logged values:

```text
riderLod: updates=39070, upgraded=33895, draws=24745, drawCorrections=0,
          preview=24020, poseSeeds=57, matrixMax=189, matrixLimitHits=0, ids=0xFFFFF
renderArena: views=18382, emissions=18382, visibleMax=303, matrixMax=189,
             aboveOldLimit=61, guardChecks=36764, limitHits=0,
             commandSlots=2, packetBytesMax=128980
native: bound=5734522, resolved=9896062, png=177, draw-fallback=0
material: seen=8986053, linked=8672216, unmapped=313837,
          packets=5734522, bad=0, deferred=35988, subdivided=35988
legacy VRAM matcher: calls=0; hashed=0; tiles=0
```

The nonzero unmapped-material count is retained honestly: unsupported or untrusted material paths still use the original image, as before. It is not a claim that every draw in the game has an HD replacement. The 177 loaded PNGs are the images used during this session; all 1,406 were separately checked through the real loader.

Native Windows execution, the user's GPU driver/performance, listened audio, every track location, stunt/animation edge cases and a complete two-player session remain untested. The screenshot concern was addressed through verified rider LOD and pose/storage fixes; this is not a claim that every possible edge clipping or projection artifact in the game has been eliminated.

## Cumulative audit, inputs and installation

The package contains **22 cumulative upstream overlays**: 16 unchanged from Build 07, five extended for safe host command/provenance handling, and the new MemoryMap overlay. Each overlay has before/after SHA-256 values against the supplied pinned source. All original generated instructions are unchanged when the 20 new exact-anchor hooks are removed; the 11 prior hooks remain. The complete source, generated code, tests, authoring tools and 1,406 PNGs are included.

The source ZIP, dependency ZIP and Linux SDK match the supplied upload checksum list; all 47 offline NuGet packages match the original package manifest. The RRDB weights match the uploaded model checksum. These are integrity checks against the provided metadata, not independent publisher signatures. Logs and machine-readable evidence are under `reports/build08`.

Run the included `Build-Windows.cmd` with the existing `-KitRoot`. It builds and safely deploys the cumulative application and complete texture pack to:

```text
D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\JetMoto.exe
```

The original kit is unchanged. Disc files, saves, settings and `Textures\Overrides` are protected by the existing deployment policy. Matching custom overrides still take precedence over the built-in pack and can hide a changed texture. `Install-Textures.cmd` now requires an already rebuilt Build 08 installation and is only a later texture-refresh helper; do not use it to upgrade a Build 07 executable. Windows deployment scripts were reviewed but not executed natively here.

Model weights, SDK/NuGet caches, original disc/model-bank files, vendor source, generated binaries and font files are excluded from the release ZIP. The existing original input kit is still required for building. No new collector, dependency download, Python installation or runtime AI service is required.

Pinned RecompOne revision: `d81dec8c9622fdcd0865d73588a3baa8d3c3a605`.
Original executable SHA-256: `f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48`.

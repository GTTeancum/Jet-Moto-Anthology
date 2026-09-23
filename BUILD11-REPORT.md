# Build 11 — native world lighting and surface-specific water

## Delivered feature

Build11 replaces the Build10 opt-in screen-color experiment with an always-on, original-geometry-bound lighting path. The presentation shaders are restored to ordinary rendering: no neighbour-luminance relief, blue/green water detector, or preview environment flag remains in the active code.

Static terrain/building surfaces use original mesh normals, native world coordinates and a bounded original-geometry heightfield for restrained ambient/directional light, static occlusion shadows and nearby-geometry darkening. Explicit original water/surface materials use world-anchored animated shading normals, view-dependent sheen and sun highlights. The water's geometry, original alpha, UV animation, timing and draw order are not replaced. The added oscillator changes shading only.

These are not full dynamic shadow maps, screen-space ambient occlusion, scene reflections or fluid simulation. A single top-height per XY cell cannot describe every overhang, thin cutout, dynamic actor or multi-layer structure. Existing rider shadows remain; there are no newly generated rider-cast shadow maps. The water's sky-colored response is an analytic approximation, not an image of reflected surrounding objects. Unrecognized, nonstatic, invalid or inconsistent receivers keep original rendering.

The update is cumulative on the actual delivered Build10 archive, with the unchanged Build09 non-preview presentation restored. It retains the complete 1,406 texture PNGs and all effect sidecars, native material-ID linkage, highest rider/moto LOD and pose/arena fixes, perspective/subpixel mapping, 16:9 gameplay/4:3 menus, visibility/sky coverage, no-dither policy, CD/startup fixes and safe adjacent-disc GUI deployment.

## Native scene and water binding

The offline authoring tool walks the exact original static DMD hierarchy: translation, rotation, coordinate scale, mesh references, LOD families and native animation frames. Receiver metadata is tied to original mesh/primitive offsets and the original DMD fingerprint. Heightfield PNGs contain encoded geometry DATA, not redesigned game texture artwork. All ten original track banks have generated data.

Original native material identities and approximately horizontal planes identify water. The Island family includes its original CA0B blue water/underwater surface and C624/C626 wave materials. Swamp identities are listed in `LightingTools/water-identities.json`. This source classification is not a runtime color or pixel-hash test. Native LOD-family relationships can carry identity to an untextured variant; unrelated untextured surfaces are not guessed from their colors.

The runtime calibrates the camera from an unambiguous original static instance before emitting world polygons, then matches other instances against that view. The original camera matrix contains projection scaling, so its **true inverse** is used; treating it as an orthonormal matrix was incorrect and was fixed before final validation. Original geometry transforms and GTE/gameplay registers are read, not modified. Source-frame polygon identities are retained when native animation changes a mesh's referenced frame.

Per-command immutable camera/plane metadata follows native outgoing packets. Payload writes invalidate stale bindings, including same-value writes and expanded render-arena writes. Ordering-table header links do not erase the payload identity. Deferred native subdivision inherits the original surface plane. Untagged HUD, sky, rider and effect primitives keep their original pixels; the material shader does not run a filter over the final screen.

A game camera snapshot is separate per view. World coordinates are reconstructed using each original source plane and view transform. The fragment shader samples U/Z-derived world position, uses a per-scene static height texture, and changes water shading without moving any vertex. Separate batch state prevents one camera/scene leaking into another. The existing soft spray/effect blending paths remain unchanged unless a primitive has an explicit world surface, which sprite effects do not.

## Source integrity and reproduction

- Pinned upstream: `d81dec8c9622fdcd0865d73588a3baa8d3c3a605`.
- Supplied executable: SCUS_943.09, SHA-256 `f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48`.
- .NET SDK 10.0.401; the same uploaded 47-package offline feed.
- **23 cumulative upstream overlays**. The Build10 preview class is removed, one world-surface data/binding class is added, and the necessary renderer, vertex flags and memory-provenance plumbing are included. Overlay before/after hashes refer to the actual supplied pristine source and final compiled source, not inferred previous working directories.
- **32 generated hooks**: the prior 31 are retained, two native emission hooks pass additional read-only geometry context, and one new camera hook begins a view snapshot after the existing camera preparation hook.
- Stripping only the verified hook snippets leaves the original generated instruction stream exactly identical to Build10.
- Actual original-disc recompilation followed by the 32 hooks reproduces `main.cs`, `Entry.cs` and `Stubs.cs` byte-for-byte. No hand-edited generated instruction is needed to preserve the integration.
- All original texture PNGs and effect sidecars are byte-identical to the delivered Build10/Build09 content. RiderDetail, RenderArena, Widescreen, DiscLocator and the non-clearing deployment implementation remain byte-identical.
- Two independent complete runs of the packaged lighting authoring code reproduce all ten height PNGs, ten receiver gzip files and the catalog byte-for-byte. Every source DMD fingerprint, native mesh vertex-table pointer, receiver normal and encoded height hash was checked. The model and height hashes are rechecked when the game loads the data.

Machine-readable evidence: `cumulative-audit.json`, `lighting-data-audit.json`, `published-content-audit.json`, `regeneration.log`, `generated-hooks.json` and the cumulative patch manifest.

## Executed checks

| Check | Final result |
|---|---:|
| World camera, projection, plane and provenance unit assertions | 107 passed |
| Actual OpenGL pixel assertions | 1,719 passed: 573 each on GL45, GL33 and GL21 |
| New world/water GPU assertions within that total | 162 passed: 18 at each of three resolutions on each of three backends |
| Existing static shader checks and child-suite completion checks | 13 static + 3 child successes |
| Native asset regressions | 149 passed |
| Perspective/precision regressions | 56 passed |
| Widescreen/camera policy | 108 passed |
| Rider/pose/render-arena regressions | 1,771 passed |
| Adjacent-disc launcher | 14 passed |
| Effect compatibility assertions | 34 passed |
| CD regression | 11 passed against the supplied original disc |
| Actual runtime image loader | All 1,406 PNGs accepted; 186,525,696 categorical alpha samples and 983,040 authored effect coverage samples checked |
| Final Linux game compilation | Passed, 0 errors |
| Windows-targeted managed publish | Passed; GUI PE subsystem 2 |
| Published content verification | All 1,406 PNG hashes and all 21 lighting-data files match in both Linux and Windows-targeted output trees |

The GPU checks use actual API rendering/readback under Mesa llvmpipe, not source-text matching. They test spatial shadowing from a known height blocker, static-light time invariance, water-only time response, material selection independent of RGB, unchanged untagged pixels, unchanged polygon coverage, original mask bits, invalid-plane fallback, world-coordinate stability under camera changes, draw offsets, scene isolation and native transparency. The inherited suites retain the previous widescreen edge, split-view, no-dither, perspective, soft-effect and overlap tests.

The initial world integration had an orthonormal-camera assumption and omitted the common legacy GLSL lighting return. Both were fixed and the final suites rerun. The final all-backend results include GL21, not just the modern path. Clean compilation retained the two existing nullable warnings in the upstream CD/SPU debug panels; incremental builds may report zero warnings. These warnings were not represented as repaired.

The Windows check is `win-x64`, framework-dependent, `UseAppHost=false`, `EnableAppHostPackDownload=false`, from Linux. The normal user's Windows build still publishes a self-contained EXE using the existing Windows SDK's apphost. Native Windows execution and PowerShell/batch execution were not performed here.

## Actual final game sessions

Both sessions used the compiled final code, the shipped original-ID texture pack, adjacent CUE and all 14 BIN tracks, without a CUE command-line argument. Geometry data were verified against the supplied disc. Audio used the null backend and was not listened to.

**Joyride:** a 300-second bounded session traversed the title/rider/race/track menus, grid, driving/steering to the beach/water area, and a confirmed pillarboxed pause. It ended at the requested smoke deadline with exit 3. Final counters: 418,088 lit solid triangles and 384,057 water triangles submitted; 64,279 matched static transforms, zero transform mismatches, zero invalid camera rotations. Native texture diagnostics reported zero bad packets, zero native draw fallback and zero legacy VRAM-matcher calls. These are repeated submission counters, not unique-surface coverage or performance measurements. The final pause was observed; a subsequent resume attempt happened after the deadline, so that attempt is not claimed as an in-game resume validation. Automated pixel tests do cover returning between camera/material states.

**Blackwater Falls:** a separate 240-second bounded session traversed menus, the grid and driving along the initial mud/forest section. Final counters: 331,297 lit solid triangles, 31,631 matched transforms, zero transform mismatches, zero bad native packets and zero legacy VRAM-matcher calls. The sampled route did not submit a recognized water receiver (water counter zero); it is evidence for world lighting and preserved mud effects, not a claim of visually testing every Swamp water material. This run also ended normally at its smoke deadline with exit 3. A last pause/resume capture attempt missed the deadline and is not presented as proof.

Actual images: `joyride-grid.png`, `joyride-world-water.png`, `joyride-driving.png`, `joyride-pause.png`, `blackwater-grid.png`, `blackwater-driving.png`. They are unaltered game-window captures; no generated promotional artwork or simulated final-game scene is used as evidence. Raw logs, final usage diagnostics and exit records are included. Earlier exploratory runs and screenshots are not substituted for the final-session claims above.

## Limits and deployment

This is a completed first-pass native lighting/water feature within the stated bounded approach—not a full new physically based renderer. Static heightfield resolution, finite ray distance, unknown receivers, alpha cutouts, overhangs and changing actor geometry limit shadow coverage. Water has analytic ripple normals and sky-colored view response, not full scene reflection/refraction. All ten track data sets are structurally/reproducibly verified, but every location, material, driver and multiplayer mode was not manually exercised.

A full rebuild is required. Lighting is active on normal `JetMoto.exe` launch; no experimental flag or Preview-Build10 launcher is needed. The target remains `D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\JetMoto.exe`. Disc files, saves, settings and custom Overrides are preserved. Existing old preview launchers may remain because deployment is intentionally non-clearing, but their old environment flag cannot enable removed code.

The release includes buildable cumulative source, generated game code, original-ID enhanced texture/effect PNGs, baked lighting data, optional authoring tools, regression tests and reports. It excludes disc images, extracted original game binaries, SDK/feed caches, vendor source copies, font files, model weights, native build outputs and scratch saves. The ZIP CRC and package-file SHA-256 manifest are verified after packaging.

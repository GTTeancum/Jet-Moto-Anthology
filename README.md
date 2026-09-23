# Jet Moto / RecompOne — Build 12

**Directional world lighting, rider/bike cast shadows, moving wakes and layered non-mirror water rendering, enabled in the normal game launch.** This replaces Build 11's failed mirror-like water path and Build 10's experimental screen-color filter. No preview launcher, environment variable, framebuffer color heuristic, blue-pixel detection or VRAM matching hack is used.

This is cumulative: all previous CD/startup fixes, user-facing adjacent-disc launch and safe deployment, highest original rider/moto LOD, initial pose and enlarged render buffers, the full 4x texture pack, native spray/wake effects, perspective-correct texturing, subpixel projection, true 16:9 gameplay, pillarboxed menus/pause, widened visibility/sky coverage and permanent dithering removal remain included.

## Build and deploy

**A full rebuild is required.** Close Jet Moto, save the ZIP in Downloads, then run:

```powershell
Expand-Archive `
    -LiteralPath "$HOME\Downloads\JetMoto-RecompOne-Build12.zip" `
    -DestinationPath "D:\Programming\GitHub\Jet-Moto-Recomp" -Force

& "D:\Programming\GitHub\Jet-Moto-Recomp\JetMoto-RecompOne-Build12\Build-Windows.cmd" `
    -KitRoot "C:\Programming\JetMoto-RecompOne-Input"
```

`-KitRoot` is the ORIGINAL input packager work directory. This update needs no new SDK/dependency collector, model weights, Python installation, or intermediate build. Your existing Windows SDK builds the self-contained GUI application. The ZIP contains source and game-support data, not a prebuilt Windows EXE.

Launch the same EXE as before:

```text
D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\JetMoto.exe
```

It reads the adjacent original CUE and all 14 BIN tracks. The normal build deploys the application, unchanged texture/effect pack and the new `Lighting` data directory. Keep those accompanying files. The original disc, saves, settings and `Textures\Overrides` are not overwritten. The build does not clear or mirror the destination folder.

**Do not use the old texture-only installer or Preview-Build10.cmd.** Lighting and water are on in normal Build 12 launches, and the old preview flag has no effect. The non-clearing deployment may leave an old preview batch file in the folder, but it cannot re-enable the removed shader code.

## What changed

**World shading:** original static scene geometry supplies receiver planes and normals. Stronger directional light gives scenery and riders clearly readable lit and shaded sides. Baked original terrain/structure data plus the rider silhouette pass add recognizable cast/contact shadowing without darkening the whole rider/moto.

**Water:** verified original water/surface materials and audited source faces receive layered world-position ripple/detail, irregular light/dark variation and broken-up highlights/reflection response. Water hue stays anchored to the original track material/fill color; the update adds surface life without replacing it with a bright new cyan palette. No water vertices are displaced; original geometry, draw order and native material identity remain intact.

**Wake/spray effects:** replacement wake coverage now has shader-driven internal motion, drift and breakup instead of only static retextured source sprites.

**Selective application:** the game binds these effects to known original static world polygons. Riders, HUD, sky, menus, sprite effects and unrecognized geometry retain their prior rendering rather than being selected by color. Menu and pause behavior stays as before. Cached pause backgrounds naturally retain the already rendered scene; the menu foreground is not post-filtered.

Original game camera matrices include non-unit projection scaling. This is handled with a true inverse, not a transpose assumption. Camera snapshots are per view, avoiding reuse across split-screen batches. The original generated game instructions, rider/pose/arena/widescreen code and all 1,406 texture PNGs are preserved.

## What the approximation does not do

This is intentionally restrained, not a new high-end renderer. Static occlusion uses a bounded 1024-square height field per track: it cannot represent every overhang, thin cutout or arbitrary multi-layer geometry. Rider/bike cast shadows are projected silhouettes, not full shadow maps. Water sheen is a broken-up sky/specular approximation, not a reflection of actual surrounding objects or a full reflection/refraction pass. There is no new fluid simulation, water-geometry motion, exposure filter, bloom, or camera-relative post-process lighting.

Unknown, non-static, mismatched or invalid receiver geometry falls back to the original rendering. There is data for all ten original track banks, but every corner of every track and complete multiplayer gameplay were not manually exercised. These limits are preferable to guessing that every blue object is water or changing the HUD with a screen filter.

## Validation and logs

`reports/build12/VALIDATION.md` distinguishes unit/pixel tests, actual gameplay captures, source/data reproduction and Windows-targeted compilation. Build 12 was validated from process-local native framebuffer captures, not desktop screenshots.

After your local run, `Collect-Logs.cmd` collects the runtime log, world receiver/water counters, existing native texture diagnostics and installed lighting catalog. It does not collect disc images, texture images, lighting maps or saves. Logs may contain local paths.

The optional `-RunTests` build flag includes camera/plane/provenance checks along with inherited regression projects. Actual GL pixel suites can be run separately in a desktop session. `LightingTools/README.md` explains optional regeneration from the original disc; the completed data are already included, so normal builds do not run Python.

Pinned RecompOne: `d81dec8c9622fdcd0865d73588a3baa8d3c3a605`.
Original SCUS_943.09 SHA-256: `f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48`.

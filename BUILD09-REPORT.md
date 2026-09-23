# Jet Moto / RecompOne — Build 09 validation

Validated in this scratch workspace on 2026-09-21. Earlier build reports are
retained as historical records, not additional Build 09 tests.

## Result and scope

A cumulative source/texture update replaces 15 verified original particle images
with five newly authored, 256×256 RGBA sprites: water/wake (six bank entries),
sand/dust (two), dry soil (one), mud (three), and snow/powder (three). The original
images are 64×64. This pass does not upscale the old spray shapes, run a neural
model or restyle the game. It renders new deterministic particle artwork using
original visible-color palette anchors. The original native UV animation,
particle geometry, lifetime, movement, vertex tint and blend-mode selection remain.
It is not a new physical fluid/particle simulation.

`TextureTools/replace_effects.py` is the actual authoring tool. It builds the
sprites at 512×512 for antialiased rendering and downsamples that NEW artwork to
256×256. The original bitmap contributes palette values and identity checks,
not enlarged pixel shapes. Broken jets, fine droplets, foam, powder, irregular
clods and particulate streaks are explicitly authored. Five deterministic seeds
are recorded in the manifest. There was no successful image-generator output
used in this release; the delivered artwork is the reproducible procedural pass.

The 15 images are identified by original bank, record ordinal and texture ID.
`original-effect-identities.json` records their original DMD primitive/UV evidence.
The original bike shadow (`EF79`), scenery shadow (`B9D2`) and unverified scenery
images were deliberately excluded. No effect was guessed into the DARK bank.
Source coverage is nine race banks; this is not a claim of runtime testing of
all nine tracks.

## Scoped transparency implementation

The existing native DMD → material → TMS identity linkage is retained, including
queued commands and close-range subdivision. There is no VRAM dumping or pixel-
hash replacement lookup. The normal renderer's destination read for blending is
not a texture-identification mechanism.

Smooth alpha is accepted only when BOTH the game-specific original-identity
allowlist and a versioned, adjacent PNG material sidecar authorize it. Sidecars
bind the exact source key and original dimensions. They are limited to 4 KiB;
wrong identities, dimensions, format/type, missing required fields and malformed
JSON are rejected. The original PNG dimension/allocation checks remain.

These effects use coverage alpha, not the PS1 0/128/255 transparency categories.
The output is `(1-a)*destination + a*nativeBlend(source,destination)`, with the
original opaque, half-add, additive, subtractive or quarter-add choice retained.
Explicit GPU mask requests still apply; coverage does not become a mask/STP bit.
Zero-alpha pixels discard. Nearest-color padding and linear filtering avoid dark
fringes at the soft edges. All other native textures retain categorical alpha
and their existing sampling path.

The Gl45/Gl33 dual-source blend path handles coverage in one pass. Gl21 refreshes
its destination snapshot between overlapping effect primitives so particles
accumulate rather than overwrite each other's blend result. Classic/coverage
batch changes are tracked separately. No dithering was reintroduced.

Older categorical effect packs/custom overrides without a sidecar remain usable
with the original semantics. Invalid smooth-alpha input falls back safely. Only
a sidecar beside the selected override is considered; the built-in sidecar is
never applied to a different custom PNG.

## Cumulative audit

The release retains 22 upstream overlays. Eighteen are byte-identical to Build
08; four were extended: native texture bindings, replacement texture metadata,
GlCore and GlShaders. Original upstream before-hashes and all delivered after-
hashes match the pinned source and the actual files compiled in scratch.

`JetMoto/generated/main.cs` and all 31 generated hooks are byte-identical to
Build 08. The only changed game-project files are `NativeTextures.cs` (effect
allowlist and telemetry), `Program.cs` (build label/message), and the project
version (0.9.0). Rider detail/pose, host render arena, widescreen and disc-launcher
code are unchanged. The full prior functionality remains included.

Exactly 15 PNGs changed; the other 1,391, including all 44 rider/moto atlas entries,
are byte-identical to Build 08. All 551 identical-original groups remain internally
consistent. The complete pack has 1,375 retained neural images, 16 retained flat
swatches and 15 newly authored effect entries. Fifteen identity-bound material
sidecars are new. Original CUE/BIN, model-bank files, physics and audio code were
not modified.

Evidence: `cumulative-audit.json`, `published-audit.json`, `pack-audit.json`, and
the cumulative `upstream-patches/patch-manifest.json` / `changes.diff`.

## Fresh tests actually run

| Test | Result |
| --- | --- |
| OpenGL pixel regressions | **1,557 passed, 0 failed**: 519 per backend on Gl45, Gl33 and Gl21, at 1×/2×/4× |
| New coverage pixel checks within that total | **558 passed**; 62 per backend/scale combination |
| Static graphics / isolated child-suite checks | **16 passed, 0 failed** |
| Effect metadata/provenance/source allowlist | **1,456 passed, 0 failed**, including all 1,406 original TMS records |
| Actual runtime complete PNG loader | **1,406/1,406** passed identity, SHA256, dimensions, cache and correct material mode |
| Unchanged original categorical alpha | **186,525,696 pixels** matched the exact 4× original footprint |
| Authored effect coverage | **983,040 pixels** checked across 15 images; smooth levels and transparent margins verified |
| Rider/pose/render-arena regressions | **1,771 passed, 0 failed** |
| Native material regressions | **149 passed, 0 failed** |
| Perspective/precision regressions | **56 passed, 0 failed** |
| Widescreen/visibility regressions | **108 passed, 0 failed** |
| Adjacent-disc launcher regressions | **14 passed** |
| CD regression against original supplied disc | **11 assertions passed** |
| Generated-hook integrity | **31 verified; zero changes applied** |
| Linux final game compilation | Passed; actual resulting managed game executed |
| Windows-targeted managed publication | Passed; framework-dependent `win-x64`, without a native Windows apphost |
| Published asset audit | Both output trees contain all 1,406 exact PNG hashes and all 15 exact sidecar hashes |

Coverage tests include all five alpha levels across all native blend modes,
overlapping same-asset particles, transparent/half-covered/opaque filtered edges,
explicit mask setting, protected destination masks, and classic/coverage batch
transitions. The full prior 999 graphics checks remain in the 1,557 total.

The source ZIP, dependency ZIP and Linux SDK matched the supplied upload checksum
list. All 47 NuGet packages matched the uploader's package manifest before use.
These establish input integrity against provided metadata, not independent
publisher signatures. Scratch restore used offline certificate revocation mode
because online certificate checks stalled in this restricted environment; this
was not added to the user's Windows build script.

Linux final game compilation reported zero warnings/errors incrementally; clean
upstream/test compilation retained the two existing nullable warnings in the
CD/SPU debug panels. Do not interpret an incremental result as warning-free
upstream source. Windows managed PE subsystem was verified as GUI (2); this is
**not native Windows execution or a prebuilt Windows EXE**.

## Actual game session

The combined final source and final texture pack ran for approximately **771
seconds** on Linux, with Mesa llvmpipe and null audio. No CUE argument was supplied;
the launcher found the verified adjacent CUE and its 14 BIN tracks. The original
disc bytes remained unchanged. Keyboard inputs navigated menus, selected riders
and tracks, drove Joyride and Blackwater Falls, paused, resumed and returned to
the title menu. Track-boundary resets occurred during driving; this was not a
clean end-to-end race or multiplayer test.

The water spray was visually inspected in moving captures and on both tracks.
Sand was exercised during Joyride's shoreline/land transitions, and mud on
Blackwater Falls. Four effect asset instances actually loaded and resolved:

| Original identity | Resolved native commands |
| --- | ---: |
| ISLAND1/ISLAND1.TMS#30:0000C6A1 — sand | 3,080 |
| ISLAND1/ISLAND1.TMS#62:0000EF77 — water | 30,120 |
| SWAMP2/SWAMP2.TMS#64:00008F87 — mud | 3,230 |
| SWAMP2/SWAMP2.TMS#65:0000EF77 — water | 17,420 |

The coverage-resolved aggregate was **53,804**. Per-source resolved commands sum
to 53,850 because the aggregate counts only after a lazy image load has completed;
the first commands can resolve native identity before loading the PNG. Neither
counter is a count of unique displayed particles or a performance metric.

Final counters: 6,459,308 native bindings; 11,189,277 native resolutions; 257 loaded
replacement PNGs; zero native draw-fallback counts; zero malformed native packets;
83,816 subdivided source primitives. The legacy VRAM matcher recorded zero calls.
The scene list reached 316, render packets reached 127,900 bytes, and render-arena
limit/guard failure counters remained zero. Highest-LOD rider IDs covered 0xFFFFF.
Unsupported material visits remained nonzero (405,450); these retain the original
path. This is not a claim that every draw has a replacement.

A deliberate Ctrl+C stopped execution cooperatively with exit **130**, not a
runtime crash. The final few deferred primitives can remain unfinished during
that cooperative stop. Logs and original-ID effect telemetry are included.

`water-spray.png` and `blackwater-driving09.png` are actual game captures. The
seven-second, 84-frame `mud-motion.mp4` is a direct 12 fps screen capture with only
the outer window borders cropped, not synthesized motion. `effect-art-preview.png`
is a visualization of the actual five replacement PNGs, not a gameplay render.

## Deployment and remaining limits

A full rebuild is required to add coverage support. `Build-Windows.cmd` uses the
unchanged original kit and safely deploys to:

`D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\JetMoto.exe`

Disc files, settings, saves and custom overrides are preserved. The optional
texture-refresh script requires an existing Build 09 executable and validates
all PNGs plus the 15 material sidecars. Build/deployment metadata and log collection
were updated to Build 09. Windows scripts were statically reviewed, not executed
on native Windows here.

Native Windows execution, driver-specific performance, all track locations,
listened audio, snow/soil in live driving, and a complete multiplayer session
remain untested. All 15 effect images were checked through the actual loader and
all three graphics backends were tested with analytical coverage fixtures.
Original particle sizes, counts, animation timing and placement remain; a sprite
art replacement cannot remove every limitation of the original particle system.

The ZIP excludes the original disc/model banks, vendor source copy, SDK/NuGet
cache, neural weights, compiled binaries and font files. It includes complete
cumulative game source/generated code, overlays, tests, all 1,406 PNGs, sidecars,
reproducible authoring tools and validation evidence.

Pinned RecompOne revision: `d81dec8c9622fdcd0865d73588a3baa8d3c3a605`.
Original SCUS_943.09 SHA256:
`f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48`.

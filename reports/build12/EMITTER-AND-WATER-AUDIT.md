# Reopened Effects And Water Audit

Status: active development; no release acceptance or package.

## Reproduction

The muted, headless `frame-material-audit` run completed. Native captures
004150, 004157 and 004162 were inspected sequentially. All show the rejected
speckled surface wake and dense purple water. Visible water-detail boundaries
remain; three images alone do not prove the cause of temporal flickering.

Bounded JSON logging now distinguishes submitted versus viewport-intersecting
primitives by exact native material. Empty host-frame records are not proof of
missing game draws: rendering and VBlank are pipelined. The recorded interval
contained only EF77 water effects, so it does not validate any land effect.

## Rider Identity

Original type-9 rider selectors supply IDs 200 through 219. The graph audit
found 602 shared polygons, plus four or six uniquely owned polygons per rider.
Shared polygons retain their shadows but are explicitly excluded from actor
attribution. No proximity, screen color, or VRAM matching is used.

Per-camera bounds of the uniquely owned subset are now recorded. These are
identity anchors, NOT whole rider bounds or ground-contact measurements.
The completed `rider-ownership-audit/run.log` records all 20 IDs during the
3400-4600 VBlank interval, with 43-430 observations per ID. This proves usable
separate observations in that replay, not visible opponent wakes.

WorldLightingTests: final run 111 passed, zero failed, including finite bounds,
camera isolation and native plane selection. The isolated game build succeeded with two existing nullable
warnings and zero errors. The already-open user game was not rebuilt in place.

## Water Coordinate Mismatch

A fresh original-disc audit, `user-reported-effects-flicker/water-plane-audit.json`,
finds 18 flat native water faces at Z=-0.125. The previous added underlay used
Z=-120. This changes the world ripple scale along the same screen ray, despite
using the same shader. The mismatch is verified; it is not yet proof of every
reported flicker cause.

The loader now derives an unambiguous flat level from verified native water
receivers. Island backdrop shading and the added underlay use that plane;
source geometry is unchanged. When the flat near-water level is ambiguous or
absent, an unambiguous native backdrop level can retain that source geometry's
sampling plane; neither level is guessed. Swamp reflection behavior is not
changed by this fix.

`native-water-plane-check` completed headlessly and muted. All five captures,
004150, 004153, 004157, 004160 and 004164, were inspected in order. The dense
ripple scale is replaced by broader visible surface structure. The separate
native wave band remains conspicuous. The old wake is absent in the first four
images and returns as rejected speckled coverage in the fifth. This is NOT a
visual pass or proof of temporal stability. The replay reached a different
camera position than the earlier run at the same VBlank, so it is not a
pixel-aligned before/after comparison.

The captures precede the source-backdrop fallback addition for other banks;
that addition does not change ISLAND1's -0.125 sampling level. Its behavior in
ISLAND2 and ISLAND3 remains unverified in-game.

## Remaining Work

Also requested by the user: upscale UI text and yellow/red buoys, including
their edge artifacts. Inspect actual menu/HUD text and both buoy colors after
the changes; do not treat higher texture dimensions alone as acceptance.

Replace the rejected footprint effect with persistent, rider-specific moving
trails and directional spray. Establish surface/contact provenance before
emitting water, sand, dirt, mud or snow effects. Validate the player and
opponents on those surfaces, plus ocean/swamp temporal stability and the
original requested medium/close/shadow views. Do not package or mark complete
on the basis of these diagnostics or tests.

## Persistent Motion Checkpoint

Added RiderMotionHistory and connected it to completed renderer camera
snapshots. It records separate world-space paths with distance-based samples,
interpolated birth times, a 1.5-second lifetime, and a bounded per-racer history.
Repeated batches do not advance it. Scene/view changes, clock resets, missing
observations and teleports cannot connect unrelated positions. Stationary
racers do not keep their old paths alive indefinitely.

These are motion paths only: no material/contact classification or replacement
wake rendering is claimed. Original effect polygons still render unchanged.

WorldLightingTests now reports 124 passed, zero failed, including twenty-actor
separation, frame-frequency-independent sample spacing, expiry, reacquisition,
teleports and storage bounds. The isolated game build succeeded.
`rider-path-audit` completed headlessly and muted with capture disabled. Its
3250-3800 VBlank interval contains 185 game-draw records, 14 distinct racer
path identities, and a peak of 2031 path segments. This interval does not
establish all twenty racers' live paths or any visible wake quality. Earlier
ownership logging established all twenty source identities separately.

## Experimental World-Space Wake

`Run-Validation.ps1 -WorldWake` now enables an isolated replacement surface
wake. It rasterizes persistent rider paths into world-space ribbon coverage,
with spreading edges and age fade. Only explicitly tagged water samples it;
there is no screen color mask or water-vertex movement. Known flat-water level
and path-height proximity reject airborne and below-surface paths. This is
not yet collision-system contact classification or a land-effect solution.
The normal launch remains unchanged while this prototype is evaluated.

WorldLightingTests: 130 passed, zero failed. RendererTests with the option
enabled: 672 passed per GL45/GL33/GL21, zero failed. New pixel cases establish
visible foam without native spray polygons, untouched remote pixels/solids,
and no duplicate advancement on repeated camera draws. They do not establish
in-game visual quality.

`world-wake-first-native` completed headlessly and muted. Its two captures,
003550 and 003557, were inspected in sequence. The new foam is not convincingly
visible, despite populated fields in the logs. This attempt FAILS the visual
gate. Native airborne droplets remain; the rejected surface strip is suppressed
only in the prototype. It is not suitable for deployment.

`LightingTools/audit_water_planes.py` reads the original graph and exact native
material identities. The generated `material-water-planes.json` establishes
that C490/CA0B ocean-art surfaces reach Z=-6, while C624/C626 crest strips reach
Z=3.97. The loader now uses the verified common ocean plane for shading both
these water-art layers and the backdrop, preserving source geometry and UVs.
This removes another source of inconsistent surface/wake sampling; the next
native capture run must determine whether it actually resolves visibility.

`common-water-plane-wake` completed with three native captures (003550,
003554, 003558), all inspected in order. The new wake is still not convincingly
visible. This second prototype run also FAILS the visual gate. Broader water
detail remains, along with conspicuous source-color/geometry boundaries.
No visual acceptance is inferred from successful compilation or field counts.
The next run logs world positions, projected pixels and stored field coverage
at the nearest racers' trailing positions, with screenshot capture disabled.

`wake-projection-probe` completed headlessly and muted, with no captures.
At camera time 19.533333, samples six world units behind rider 217 project to
native pixel Y=299.52..303.60, below the 240-line gameplay viewport. Stored
coverage at the central sample is R=30/G=255 and one side is R=87/G=14: this
portion of the field exists but is off-screen. Earlier, higher rider positions
projected still farther below the view and were rejected by the height gate.
This establishes a specific visibility constraint, not acceptance of the
surface wake or a complete explanation of every missing pixel. The next
replacement stage needs actual world-space airborne spray, not a brighter
surface decal or a screen-space offset pretending to be water foam.

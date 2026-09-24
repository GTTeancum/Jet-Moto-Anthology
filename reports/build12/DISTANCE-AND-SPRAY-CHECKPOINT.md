# Distance detail and world-spray checkpoint

Status: partial progress, NOT visually accepted, NOT deployed or packaged.
Build 11 remains untouched. All native runs below were headless and muted.

## Distance detail

The water-detail sampler used isotropic trilinear mipmaps. Added capability-
checked anisotropic filtering, capped at 16, with the existing trilinear
fallback on unsupported hardware. No brightness, hue or water-vertex changes
were made by this filtering change.

Measured the same grazing-angle renderer fixture before and after:

| GL33 resolution | Before row contrast | After row contrast |
| --- | --- | --- |
| 1x | 2.3811 | 5.2316 |
| 2x | 4.7666 | 7.8787 |
| 4x | 6.7200 | 9.1616 |

This supports filtering as one cause of lost distance detail, not a diagnosis
of all reported temporal flicker. The fixture uses known tagged geometry,
not screen-color identification. Added checks for grazing-angle contrast,
12 successive shader times, and agreement between near/backdrop kinds when
source color and plane are identical. Largest measured mean brightness step
was 0.1042 in the fixture's summed 5-bit RGB units. Native layer/color changes
and longer gameplay sequences are not covered by that result.

`grazing-filter-bay` captures 003500 and 003508 were individually inspected in
order. They show open water and ripple detail into the middle distance, but
no convincing replacement wake. This is partial water evidence, not release
acceptance. Added `bay-replay.json` with process-local steering; it still
eventually leaves the track. VBlank-based runs are not pixel-aligned A/Bs.

## Spray visibility

`spray-projection-bay` and `spray-projection-bay-extended` used logging only.
Opponent particle radii were mostly subpixel at the sampled distance. Across
199 player-spray records in the extended run, average viewport-overlapping
particle count was 0.94; most player particles projected below the view.
This is projection evidence, not proof of fragment visibility or occlusion.

Increased the world-space fan's rise/spread and lifetime. `expanded-fan-bay`
captures 003900, 003903, 003907 were inspected individually in order. Wake
quality still failed. The bike outran the low-momentum airborne particles;
raising them alone did not resolve the camera visibility problem.

Airborne spray now inherits 85% of craft forward speed before analytic drag.
The separate surface foam field remains world-anchored. `momentum-fan-bay`
captures 003900, 003904, 003908 were inspected individually in order. Soft
spray is now visible around the bike and changes across the sequence. Water
detail is visible at near and medium distance. The effect remains too sparse
and soft to qualify as the requested substantial, layered wake.

That run's 25 player-spray audit records averaged 44.64 viewport-overlapping
particles. This is not a matched-camera comparison with the earlier run and
does not establish final visual quality. Thin blue vertical lines also remain
visible by the bike near the track boundary; their origin is not established.

## Tests and limitations

- Current isolated build succeeds: two existing nullable warnings, zero errors.
- Current motion tests: 137 passed, zero failed.
- Current renderer tests with experimental world wakes enabled: 690 passed
  per GL45/GL33/GL21, zero failed; each covers 1x/2x/4x.
- Added actual world-spray pixel checks using explicit synthetic source
  material and no native wake packets. They test visible coverage, movement
  and expiry independently of the old sprite tests.
- The first new pixel fixture stopped the bike and kept a small stationary
  camera, so momentum-carrying particles exited the test view (six failures
  per backend). Replaced that motion with a continuously moving emitter and
  following camera. Coverage and expiry assertions were retained. These are
  bounded rendering tests, not evidence for terrain contact or occlusion.
- General flicker, depth/occlusion, substantial persistent foam, opponent
  visual coverage, sand/dirt/mud/snow, swamp mirrors, and UI/buoy upgrades
  remain open. The proximity gate is still NOT simulation contact provenance.
- No cumulative release acceptance is claimed. No release archive was created.

Next required work: establish native racer/contact state, fix the wake's
surface-plus-spray composition and verify its ordering/occlusion, then validate
all terrain families and longer native temporal sequences. Preserve source
water hue and fixed geometry throughout.

## Intermittent distance-detail investigation

Added opt-in source-binding diagnostics bounded by the existing audit frame
range. Recognized near-water and backdrop polygons log successful bindings or
specific camera, transform, instance and normal rejection reasons. Diagnostics
do not change shading, material color, geometry, gameplay or input.

`water-binding-bay` ran headlessly and muted for 80 seconds, logging only.
Its isolated build succeeded with zero warnings/errors. Across 348 camera
records, VBlanks 3100 through 4098, 25,275 near-water and 9,760 backdrop
source polygons bound successfully, with no recorded rejection of recognized
water polygons. This rules out those binding rejection paths in this bounded
run, not missing catalog entries, later packet invalidation, overdraw, filtering
or visible temporal instability. Counts are not visible-pixel evidence.

The existing shader has no explicit distance cutoff. Native overlapping water
layers, their source colors and the resulting filtered detail remain under
investigation. No new visual acceptance, deployment or release is claimed.

# Wake Continuity Work

Scope: verified EF77 water effects only. The approved ocean material, water
geometry, rider/scenery lighting and airborne droplet trajectories are unchanged.

## Implementation

- Replace the surface layer's repeating fan/plume artwork with moving foam
  coverage: two advected turbulence scales and fine, distance-filtered breakup.
  Source artwork still supplies the foam color; no blanket white/cyan tint.
- Extend only the native foam-effect footprint along its longitudinal texture
  axis by 50 percent. Soft edges overlap neighboring pieces instead of exposing
  separate fan outlines. Airborne sprite geometry is not stretched by this.
- Keep the previous triangle-independent airborne lift fix.
- Interpret brightness modulation on verified water effects as opacity fade,
  preserving their color instead of fading into dark surface decals. Other
  coverage effects retain their original modulation and animation behavior.
- Keep the original game's emitter and removal decisions. This is not a new
  persistent world-space particle simulation, and does not extrapolate effects
  after the original game removes them.
- Add optional bounded wake-window logging for submitted/viewport-overlapping
  triangles and modulation range. These counts are not a visibility verdict.

## Tests

`aerated-foam-tests.log`: 660 pixel checks passed on each of GL45, GL33 and GL21;
zero failures. Added tests cover filled coverage, independent foam flow,
bounded footprint, opacity fading, zero-brightness removal, and monotonic fade
across the full effect. Existing geometry, blend, mask and non-water tests pass.

`aerated-foam-build.log`: game build succeeded, zero warnings/errors.

## Rejected Iterations

All five captures in `flowing-foam-native-check` were individually inspected
(3550, 3554, 3558, 3562, 3566). The fan outlines were gone, but the replacement
looked like soft mist. Rejected as final visual evidence.

All four captures in `foam-coast-native-check` were individually inspected
(3550, 3601, 4050, 4501). The attempted filaments became large contour loops
close to the camera, so that shading was also rejected. After throttle release,
the wake cleared and no foam ghost remained at rest. Frame 4050 was partially
obscured by a buoy; frame 4501 had an unobstructed rear-bike region.

The coast diagnostic measured water-effect brightness 127 throughout the active
draw windows, then no submitted wake geometry by vblank 4070. Thus that run
demonstrates original effect removal, not the new brightness-fade branch in
live gameplay. The latter is covered by renderer pixel tests; do not claim a
new native lifetime simulation or verified smoothness at every despawn.

## Current Native Evidence

`aerated-foam-coast-check` completed 82 seconds with expected game exit 3 and
runner exit 0. Runtime-hash preflight passed. All four captures were inspected
individually in order: 3550, 3600, 4051, 4501. Fine foam breakup is visible in
3550 without the rejected contour loops or mist. The original airborne droplets
remain visible. At 3600 the bike is moving beside the shoreline at throttle
release; 4051 is partly buoy-obscured; 4501 shows no persistent foam behind the
stopped bike. This verifies a bounded coast/removal flow, not every despawn.

The current implementation retains the game's existing emission/lifetime
decisions. Brightness-to-opacity conversion prevents darkening when brightness
is supplied, but this replay again used constant native brightness. Do not
describe it as a new independently simulated lifetime or proof of smooth
despawning under every native emission condition.

`aerated-foam-motion-check` completed 76 seconds with expected game exit 3 and
runner exit 0. All three captures (3551, 3556, 3559) were inspected individually
in order. The overlapping surface trail is visible throughout this sample,
with changing fine coverage and airborne droplet positions, without the old
fan outlines, mist or contour-loop artifacts. This is a narrow visual pass for
the wake replacement, not an all-track or general-flicker acceptance verdict.
The capture intervals are recorded VBlank samples, not every native game frame.

Both current native runs verified runtime SHA256:
`DBD6DAA63BEE55F955E9A57D5504446F37E8DF14AC362A82CBF6571E6D309662`.

Cumulative regression: all ten suites in
`wake-continuity-regression/summary.json` completed with exit code zero. This
includes lighting, rider detail, native/neural textures, perspective,
widescreen, static renderer checks, other effects, launcher and disc reads.
All gameplay checks use process-local replay, headless execution and mute.
No release package or deployment has been produced by this work.

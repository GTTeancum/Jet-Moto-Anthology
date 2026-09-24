# Water Transition And Swamp

Status: visual acceptance incomplete; no release/package/deployment.
Spray is deferred. Build 11 is untouched.

## Ocean

`water-clipping-transition`: 75-second native headless/muted run, isolated
audit build with the source-verified world span-clipping repair. Harness
completed 0 after expected application timeout exit 3. WorldWake was disabled
to keep this check focused on water; this does not disable all existing
legacy replacement effects or constitute spray validation.

All three saved captures were inspected individually in chronological order:
race 800, 1000, 1200. Capture 800 is beside/under the bridge on the road, with
dark scenery and a readable shaded rider. The latter two show shoreline water
at near and medium distances. The large purple foreground coverage hole from
the rejection diagnostic is absent. Captures 1000/1200 retain clear surface
texture. Capture 1200 shows thin vertical streaks around a close buoy's native
reflection; this is an outstanding visual issue, not a pass. Deep water has a
visibly different purple color band, also present in the historical approved
image; preserve native hue while investigating distance/surface continuity.
The route hits the course boundary. Three saved captures do not establish
frame-by-frame temporal stability or complete rider-shadow regression coverage.

## Swamp

`swamp-clipping-check`: 86-second headless/muted native run using the existing
swamp replay. Harness completed 0 after expected timeout exit 3. Logs confirm
SWAMP2 loaded. All three saved captures were inspected individually in order:
4795, 4800, 4810. Original dark/muddy color and native mirrored vegetation are
preserved, but the central pool still has broad flat bands and no clearly
readable ripple detail. This fails the requested swamp surface quality.

Next: trace the actual visible swamp pool polygons and layering. Establish
whether surface classification is missing or detail is covered by native
reflection layers before changing material response. Do not recolor the swamp
or remove its mirrored scenery to hide the failure.

# Water Horizon Clipping

Status: partial correction; overall water visual acceptance FAILED.
Spray work deferred at the user's request.

The water shader previously discarded behind-plane rays only for the added
screen-fill underlay. Other verified water surfaces returned their unlit
source color when inverse depth was nonpositive. This could leave source-color
triangles above the physical water horizon. Both shader backends now discard
nonpositive-depth fragments for explicit water kinds 2/4, including additive
crests, while leaving untagged sky/scenery/UI behavior unchanged. Geometry,
source hues, UVs and original reflections are not moved or recolored.

Actual-pixel tests now require background preservation above the water plane
and retained textured shading below it. Renderer suites: 699 passed per
GL45/GL33/GL21 at 1x/2x/4x, zero failures. Release build: zero warnings/errors.

An initial native launch was prevented by the runtime-hash guard while the
build was still finishing; no stale game was tested. After confirmed build
completion, `water-horizon-clipping` ran for 65 seconds headlessly and muted.
All five saved captures were inspected individually and sequentially: race
980, 986, 991, 996, 1000 (HUD 9.5 through 10.0 seconds).

Findings:
- No source-colored water triangle in the sky in this bounded sequence.
- Rider/bike cast shadows remain visible on the beach.
- Near/medium water remains textured, with conspicuous shallow/deep boundaries.
- A purple patch appears at the lower-left shore in 986, absent in 980 and
  subsequent captures. Surface flicker/material discontinuity therefore remains.
- The player route differs from earlier captures and is on the beach here.
  This is not a pixel-aligned before/after proof or a full-course flicker pass.

Next water work must address inconsistent layer/material selection on the
surface, separately from invalid horizon fragments. Keep all remaining water,
original-hue, stationary-geometry, swamp and cumulative validation requirements.
No packaging/deployment; Build 11 untouched.

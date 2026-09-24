# Additive water composition checkpoint

Status: experimental; native visual acceptance FAILED. Not packaged or deployed.

## Source evidence

Extended `LightingTools/audit_water_planes.py` to report original polygon
command transparency and texture-page blend mode, alongside the existing
source material and receiver heights. The ISLAND1 original DMD graph contains:

- Opaque kind-4 untextured backdrop polygons.
- Opaque kind-2 materials C490 and CA0B.
- Additive kind-2 materials D1CC, C626 and C624 (semi-transparent, blend mode 1).

These are source polygon roles, not a screen-color or live VRAM heuristic.
Graph traversal includes animation variants and instances; its counts must
not be interpreted as concurrent visible polygons.

## Change

Previously every kind-2 polygon ran the complete water material, including
the persistent wake field. Additive crest polygons could therefore contribute
another copy of the foam over the base surface. Their shader now contributes
source-colored, ripple-broken radiance without another base body or wake.
The subtype is selected only from an already verified kind-2 water binding
and its native additive command. Other transparent materials are unchanged.

Original blending, packet order, source colors, water geometry and native
swamp reflection geometry are retained. This fixes a composition error but
does not, by itself, prove the user's intermittent flicker is resolved.

## Checks

- Renderer suite: 696 passed per GL45/GL33/GL21, zero failed, at 1x/2x/4x.
- New pixel regression: additive crest output is identical with and without
  the underlying wake field, while the opaque base still receives visible foam.
- New pixel regression: ripple-broken additive water does not amplify the
  corresponding original additive radiance in the fixture.
- Isolated game build succeeded with zero warnings/errors.
- Native replay `additive-water-bay`: completed headlessly and muted, expected
  bounded exit. Inspected every capture individually in chronological order:
  003500, 003505, 003511, 003519, 003524 and 003530.

## Native findings

Ripple detail is visible across foreground and middle-distance open water
throughout this captured sequence. It is not a smooth mirror. However, a
hard-edged blue/cyan water region beside the shore becomes conspicuous in
003505 and changes coverage through the later captures. It remains an obvious
layer/material discontinuity, not an acceptable natural transition. Its exact
origin is not proven by these screenshots. There is also a purple polygon at
the upper-right edge of 003530 which needs investigation.

A thin trail appears behind the right-side opponent in 003500; that is not
sufficient evidence of reference-quality opponent wakes. No convincing player
wake is shown in any of the six captures. Sailboat reflections remain sharp.
This run does not establish shadow/contact or swamp acceptance.

VBlank gaps between captures mean this is a sampled temporal sequence, not
inspection of every native frame or proof that all flicker is absent. Native
visual acceptance remains failed despite the targeted composition regression
passing. Next water investigation: identify the opaque shore-water layer
responsible for the visible discontinuity and its interaction with the common
water sampling plane, while preserving its original shallow-water color.

Still open: general temporal stability and distant surface quality, substantial
player/opponent wakes, verified land-contact effects, swamp visual acceptance,
UI/buoy cleanup and cumulative release acceptance. Build 11 is untouched.

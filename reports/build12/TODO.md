# Build 12 To-Do

Baseline: user-approved playable checkpoint, 2026-09-24. Preserve its ocean
appearance and cumulative fixes. This is a source checkpoint, not a final
release acceptance claim. Build 11 remains untouched.

## Water And Lighting

- [ ] Improve swamp surface detail while retaining muddy hues and native fake
  mirrored scenery. Current visible pool uses unclassified background polygons
  and separate reflected foliage; trace their original provenance before editing.
- [ ] Verify ocean near/medium-distance detail continuity without changing the
  approved hue or making highlights excessively bright.
- [ ] Investigate thin vertical streaks around close buoy reflections.
- [ ] Extend native temporal checks across shorelines, dry land, sky and swamp
  to catch remaining flicker or missing polygons after the span-clipping repair.
- [ ] Verify rider/bike cast shadows, scenery/contact shadows, and the reported
  intermittent whole-rider darkening across multiple native gameplay situations.

## UI And Buoys

- [ ] Upscale menu/HUD text while preserving 4:3 menus and Hor+ gameplay.
- [ ] Upscale yellow and red buoys and fix edge artifacting.

## Deferred Effects

- [ ] Spray/wake visual and movement upgrades remain deferred until requested.
- [ ] When resumed: water-only placement, player and opponent effects, and
  appropriate sand/dirt/mud effects; do not treat existing retextures as finished.

## Release Gates

- [ ] Finish cumulative regression checks, including native/neural textures,
  highest rider/bike LOD, perspective/subpixel projection, aspect handling,
  dithering removal, and startup/deployment.
- [ ] Retain actual native evidence for directional/cast shadows and close/medium
  water, with an honest validation report. Use muted headless tests and logs;
  no desktop automation or OS input.
- [ ] Package cumulative Build 12 only after the remaining visual gates pass.
  Never overwrite the known-good Build 11 release.

## Approved Test Build

Path: `.build/audit-bin/Release/net10.0/win-x64/JetMoto.dll`.
Manual test launch used `JETMOTO_WORLD_WAKE=1`, with no automated replay,
capture, headless, mute, or extended-water-LOD override.
Application SHA256: `D2C09E1AF287900921AD27FEDE9281FB1CFF724C0044783D577F0D4DD66F3333`.
Runtime SHA256: `6C452BF113BBFB34196E706DC0AA625EEF63A45A178152D631DF16B767A082AE`.
The binary and user-supplied game assets remain local, not part of this source commit.

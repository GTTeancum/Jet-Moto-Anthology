# Spray Projection and Atlas Isolation

Status: experimental; visual acceptance remains unproven. Not deployed.

Previous continuation made progress by reaching water and recording actual
emission state. This continuation adds projected bounds and submitted particle
counts to capture sidecars, then fixes a synthetic/native state dependency.

## Projection evidence

`wet-spray-projection` completed headlessly and muted. Both captures, race
980 and 992, were inspected individually in order. Faint isolated droplets
and some foam are visible near/below the bike at the bottom of the view;
this is not a substantial reference-quality wake. Actor 217 has 95/99
generated particles, but only 16/30 submitted in the view. Many older particles
are behind the camera or below the frame. This contradicts the idea that all
spray is necessarily overwritten after drawing; some is visibly present.

## Correction

Generated spray uses full-sprite UVs but inherited the native GPU texture
window. A conflicting native atlas window can reject its explicit material or
remap its samples. The injection now temporarily clears only texture-window
state, flushes its batch, and restores the original environment in finally.
Native geometry, clipping, mask semantics, source asset identity and original
material windows remain unchanged outside generated spray.

The shader already converts brightness fade into coverage opacity through
waterCoverage. An initial suspicion of brightness-only fading was disproven
by following that function; no opacity/shader change was made.

## Tests

- Renderer suites: 699 passed per GL45/GL33/GL21; zero failures, at 1x/2x/4x.
- Added actual-pixel equality test for generated spray with a conflicting
  inherited atlas window versus no atlas window.
- Corrected the open-water pixel fixture to contain no solid occupancy;
  it previously reused a solid ground plane exactly at water height, which
  the new dry-ground veto correctly rejects.
- Isolated native Release build succeeded with zero warnings/errors.
- `wet-spray-atlas-isolation` completed headlessly and muted, expected bounded
  exit. Both captures (race 980, 990) were inspected individually in order.
  Droplets and a foam strip are visible behind the player, mostly near the
  lower edge, but remain sparse and visually insufficient. A purple polygon
  intrudes at the top right of 980; water-layer boundaries remain conspicuous.
  This fails visual acceptance and does not establish that atlas state caused
  the primary native weakness. No opacity shader change was made.

Road contact, close/medium water transitions, swamp, land effects, temporal
stability, UI/buoys and cumulative release acceptance remain in scope.

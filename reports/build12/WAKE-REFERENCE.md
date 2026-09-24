# Wake replacement reference audit

Status: experimental, not visually accepted or deployed.

Reference: https://github.com/elliotttate/wave-race-64-recomp
Inspected revision: 9041cc8024f6d2015859f88f5a0307b36cd4e50a

Read src/water.cpp and tools/patches/rt64-water.patch, specifically
WaterFieldCS, foamBreakup, stepSpray, WaterSprayVS and WaterSprayPS.
The root LICENSE is MIT for original project code, not Nintendo assets.
This implementation uses the design principles below, not copied game assets.

## Concrete differences

- The reference transports persistent foam separately from airborne droplets.
  Its field includes a center trail, outward wings and contact/landing impulses.
- Emission uses individual craft movement and wet contact, including opponents.
  Jet Moto currently has actor-specific motion but only a water-plane proximity
  gate, which is not equivalent to verified terrain contact.
- Foam uses irregular connected patches with subpixel filtering, not isolated
  bright speckles or a repeated square/checker pattern.
- Spray has drag, gravity, lifetime fade, camera proximity/size fade and depth
  intersection softening. Jet Moto's experimental painter-order injection does
  not yet provide equivalent verified occlusion.
- The reference keeps previous/current simulation state and interpolates it.
  Jet Moto's deterministic path-age evaluation needs native temporal validation.

## Experimental changes

Use a layered mipmapped water-detail field for connected foam breakup, with
derivative-based distant coverage filtering. Add analytic drag to world spray,
speed-dependent fan power, varied droplet sizes and near-camera size fade.
No water vertices are moved. Preserve source material hue and native swamp
mirrored scenery. These changes alone do not establish reference-level quality.

## Still required

- Native muted replay evidence that both player and opponents visibly emit.
- Coherent spreading foam plus airborne spray, without sparkling or flicker.
- Correct occlusion and terrain contact, including separate sand/dirt/mud effects.
- Original water hue and swamp reflection checks.
- UI text upscale and yellow/red buoy upscale with clean transparent edges.

Keep Build 11 untouched. Do not package this experiment as an accepted Build 12.

## Validation checkpoint

- Isolated release build: zero warnings/errors.
- Motion tests: 135 passed, zero failed.
- Renderer pixel checks: 660 per backend with experiment disabled; 672 per
  backend enabled (GL45, GL33, GL21), zero failed. These do not validate the
  new airborne injection's in-game appearance or occlusion.
- Muted headless reference-wake-native exited normally for the bounded test.
  Inspected captures 003550 then 003555 individually: bike on structure,
  purple water visible to the left, no useful wake proof. FAIL coverage.
- Muted headless reference-wake-open-water also exited normally. Despite its
  intended name, captures 003101 then 003109 show the starting road, not an
  open-water riding segment. Both inspected individually; FAIL coverage.
  Replay capture selection needs verified game-state/position, not assumed
  frame numbers. Do not present either run as wake-quality evidence.

User reports improved water visible only intermittently/close-up. Medium
distance detail and continuity between native layers are explicit open gates.
Source inspection shows mipmapped detail sampling and distinct base-color
paths for kind 2 versus kind 4 water; neither is a proven temporal failure
cause. No claim of a fixed flicker or acceptable distance appearance.

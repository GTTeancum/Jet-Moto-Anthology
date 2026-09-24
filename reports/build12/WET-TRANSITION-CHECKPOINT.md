# Wet Transition Checkpoint

Status: FAILED visual wake acceptance; no release or deployment.

Previous turn made progress: implemented road-emission rejection, corrected
the pre-race replay gate, and inspected native road evidence. This continuation
tests the complementary wet case instead of treating suppression as success.

## Distance experiment

`road-water-lod-comparison` completed an 85-second headless muted run with
extended water LOD enabled. All four captures were inspected individually in
order: race 781, 795, 812, 825. The player remained on the road; nearby racers
showed no large white road droplets. At 825, thin white trails are visible
behind opponents already on the water. These are not accepted-quality wakes.
The bike trajectory and race HUD time differ from the disabled-policy run;
this is not a pixel-aligned A/B comparison and does not prove the LOD policy.
Leave the policy opt-in.

## Open-water route

`wet-transition-replay.json` removes the ineffective early left input and
applies a short right input after countdown. `wet-transition-contact` completed
85 seconds headlessly and muted. Inspected all four captures individually in
order: race 981, 992, 1005, 1021 (HUD 9.4 through 10.0 seconds).

The bike is now visibly riding water alongside the beach, not the road or a
wall. Near and medium water have detail, but the player has no convincing
spray/foam trail in any capture. Shallow/deep material boundaries remain
conspicuous; the captures do not establish absence of intermittent flicker.
This provides a useful failing wet-case replay, not an accepted result.

Added compact emission state to native capture sidecars: scene/time/water
level, per-racer bounds center, solid-ground gate, path/particle counts and
whether injection was visited. These fields are explicitly not visible-pixel
proof or positive wet-contact evidence. Diagnostic rebuild succeeded with
two existing nullable warnings and zero errors.

## Diagnostic result

`wet-emission-diagnostic` completed its bounded 65-second headless muted run.
Both saved captures (race 980, 990) were inspected individually in order.
The rider is visibly on water, but no convincing player spray/foam is visible.
Sidecars report ISLAND1 water level -0.125, with 472 then 378 rasterized wake
segments. Actor 217 passes the ground veto, has 139 then 144 path segments,
116 then 106 generated spray particles, and injectionVisited=true. Other
recorded actors near the dry side fail the gate and have zero particles.
Actor 217's exact player identity is not independently established here.

The observation rules out global suppression/no emission as the sole cause.
It does not prove those particles survived clipping, projected behind the
visible bike, or survived later painter-order water draws. Next investigation
must measure projected particle bounds and rendering order/coverage rather
than relaxing the solid-ground veto or increasing brightness blindly.

Original broad scope remains open: water transitions/flicker, player/opponent
wake quality and movement, land effects, swamp mirror handling, lighting,
UI/buoy cleanup and cumulative packaging. No acceptance or deployment.

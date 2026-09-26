# Build 12 Validation

Latest HUD work: [speedometer checkpoint](HUD-SPEEDOMETER-CHECKPOINT.md).
Approved V4 digits/buoys retained; restored speedometer staged in the isolated
candidate and individually inspected in three native ISLAND1 captures.

2026-09-25 menu continuation: [credits/scoring evidence](MENU-CREDITS-SCORING-CONTINUATION.md).
Eight credits pages and three scoring selections tested with individually
inspected native captures. Tiny credits trademark defects repaired using source
samples and native inspected; loading contour experiment rejected. DEVELOP
also rejected after native inspection and withdrawn. Isolated catalog 41. Full menu
quality and release gates remain open; these are partial, sampled checks.

Status: USER-APPROVED PLAYABLE CHECKPOINT; FINAL RELEASE GATES STILL OPEN. NOT PACKAGED.
On 2026-09-24, after testing the current isolated Build 12 with the world-span
clipping repair, the user said "I think this is great" and requested a commit
and push. Preserve this playable baseline. Earlier rejection notes below are
historical context, not the user's latest assessment.
The user subsequently said "Close effects/water. That's sufficient". Water and
effects are closed at the current quality level, including swamp, reflections,
spray and wakes. Do not resume their earlier to-dos without a new request.
This acceptance does not claim that every previously documented defect was
technically eliminated.
On 2026-09-25, the user said "Lighting task can be closed". Lighting is closed
by user direction; its pending verification and dedicated release-gate entries
have been removed. This records acceptance, not new test evidence or a claim
that every historical lighting issue was eliminated. Preserve the current
lighting implementation and do not resume lighting work without a new request.
The user subsequently directed: "Close menu-based tasks, removing them".
Menu work is closed by that direction and removed from TODO; older open-menu
notes in this log are historical, not the current scope.
On 2026-09-26 the user approved the speedometer and cumulative HUD/buoy updates,
requested commit/push, and directed removal of the Remaining HUD And Buoys
section from TODO. HUD/buoy work is closed at this accepted quality level;
this is acceptance, not a claim that every historical artifact was eliminated.
The user's subsequent minimap smoothing request is a separate follow-up.
Release gates remain open.
Current remaining work: [To-do](TODO.md).

Approved pre-menu test build (historical baseline, moved out of the TODO):
`.build/audit-bin/Release/net10.0/win-x64/JetMoto.dll`.
Manual launch used `JETMOTO_WORLD_WAKE=1`, without replay, capture, headless,
mute or extended-water-LOD override.
Application SHA256: `D2C09E1AF287900921AD27FEDE9281FB1CFF724C0044783D577F0D4DD66F3333`.
Runtime SHA256: `6C452BF113BBFB34196E706DC0AA625EEF63A45A178152D631DF16B767A082AE`.
The binary and user-supplied game assets remain local, not part of the source commit.

Track-dial correction, native rotation captures, and still-open UI/buoy work:
[UI upscale checkpoint](UI-UPSCALE-CHECKPOINT.md).
Latest clipping repair, matched native coverage evidence, and regression results:
[Geometry coverage checkpoint](GEOMETRY-COVERAGE-CHECKPOINT.md).
Latest wider ocean and swamp checks:
[Water transition and swamp checkpoint](WATER-TRANSITION-SWAMP-CHECKPOINT.md).
Latest source-geometry diagnostic and dry-sand streak evidence:
[Shoreline order checkpoint](SHORELINE-ORDER-CHECKPOINT.md).
Latest water-only correction and remaining native flicker:
[Water horizon checkpoint](WATER-HORIZON-CHECKPOINT.md).
Historical reports below retain their original findings and status language;
the user-directed water/effects and lighting closures above supersede their
open-work lists for those areas.
Spray projection evidence and native atlas-state isolation:
[Spray projection checkpoint](SPRAY-PROJECTION-CHECKPOINT.md).
Complementary open-water failure and emission diagnostics:
[Wet transition checkpoint](WET-TRANSITION-CHECKPOINT.md).
Road spray rejection and replay-clock correction:
[Road emission checkpoint](ROAD-EMISSION-CHECKPOINT.md).
Latest distance-selection investigation (opt-in, native coverage failed):
[Water LOD checkpoint](WATER-LOD-CHECKPOINT.md).
Latest composition fix and six-frame native failure review:
[Additive water checkpoint](ADDITIVE-WATER-CHECKPOINT.md).
The a50bab8 wake is a speckled strip, not an accepted replacement. The user
also reports unstable water, missing opponent wakes and missing land effects.
Prior pixel tests and isolated captures did not cover those requirements.
The muted frame-material-audit replay reproduced the dense purple water and
flat speckled wake. Frames 004150, 004157 and 004162 were inspected in order;
these are failure evidence, not release proofs. Rider/material logging is
being extended before the replacement is accepted on any surface.

The user approved `fine-airborne-spray-verified-runtime/frame-003554.png` with
"PERFECT. That's great. That's what I'm looking for". This is a historical
single-image reference, not approval of the current implementation.
SHA256: `43AD4B79175BA7A99B3030E557F21D191B6051B1056D7D5E0E1D4273A0963654`.
Remaining gates: swamp material/fake-reflection checks, temporal stability,
and final package verification. The historical
results below are superseded by REOPENED-DEFECTS.md where noted.

## Latest Validation Checkpoint

- See DISTANCE-AND-SPRAY-CHECKPOINT.md for the latest filtering fix, native
  captures inspected in order, spray projection diagnosis and current tests.
  Distance detail and spray visibility improved; wake quality still fails.
  The replacement remains experimental and is not deployed or packaged.
- Reference-driven replacement investigation: see WAKE-REFERENCE.md. The
  Wave Race field/spray implementation was inspected at a pinned revision.
  Latest experiments remain unaccepted; native runs did not exercise visible
  open-water wakes. Stronger medium-distance water detail and stable material
  coverage are required, not just occasional close-up improvements.
- Additional user-requested work: upscale UI text and yellow/red buoys; fix
  artifacting around buoy edges. Verify menu/HUD text and both buoy colors in
  native in-game captures, preserving 4:3 menus and widescreen gameplay.
- Water/wake acceptance remains open; the above UI/buoy work is not yet done.

- Surface wakes now use moving, overlapping foam coverage instead of repeating
  fan artwork. See WAKE-CONTINUITY.md for implementation, rejected iterations,
  pixel tests, individually inspected native captures and explicit lifecycle
  limits. Ocean shaders, water geometry and lighting were not changed by this
  wake pass. General flicker and swamp acceptance remain open.
- Follow-up wake geometry fix: sprite-axis lift replaces per-triangle bounding
  width. The skewed-quad diagonal regression went from failing on all three GL
  backends to zero changed above-wake pixels at 1x/2x/4x. All 642 pixel checks
  passed per backend. Build succeeded with zero warnings/errors. All three
  captures from the headless, muted `wake-diagonal-native-check` were inspected
  in order; wake emergence and developed airborne spray are visible. This
  short check does not clear general flicker or release acceptance.
- All ten suites in `approved-baseline-regression/summary.json` completed with
  exit code zero. This does not establish visual acceptance.
- Inspected both native captures in `swamp-reflection-ripples-check`, frames
  004800 and 004805, individually. The swamp retains its dark original hue and
  fake mirrored vegetation, but the central pool still lacks clearly readable
  ripple detail and shows broad polygon bands. Swamp surface quality remains
  an open visual gate; do not package on the basis of these images.
- Added audited A522 flat swamp receiver records only: SWAMP1 +106, SWAMP2 +32,
  SWAMP3 +94. Existing records and all height maps were unchanged. Evidence is
  in `swamp2-a522-geometry.json` and `swamp-reflection-bake.log`.
- Preserve the native swamp fake reflection when investigating its surface
  shading/render order. Do not replace it with an ocean-colored reflection.
- The user-approved ocean/spray shader appearance has not been retuned.
  No release packaging or deployment was performed at this checkpoint.
- Goal service reports `usageLimited`, not complete. Remaining visual and
  packaging work is explicitly outstanding.

## Baseline

Build 12 was developed from the recovered cumulative Build 11 release in
`D:\Programming\GitHub\Jet-Moto-Recomp\JetMoto-RecompOne-Build11`.
The known-good Build 11 release folder was left in place and was not
overwritten.

All prior cumulative fixes remain part of this tree: native texture
integration, neural texture upgrades, replacement spray/wake assets, highest
rider/bike LOD, perspective correction, subpixel projection, 16:9 gameplay,
4:3 menus/pause, widened visibility/sky coverage, dithering removal and
startup/deployment fixes.

## Build 12 Rendering Changes

- Removed the failed development water-base path and replaced it with audited
  original-world water/source-face coverage.
- Added layered, world-position water detail with irregular light/dark breakup
  and broken highlights/reflection response.
- Kept original water material identity: water hue stays anchored to the
  original track material/fill color instead of being replaced by a bright new
  cyan palette.
- Kept water geometry stationary. No vertex displacement or water-plane motion
  is used.
- Added stronger directional lighting for readable lit/shaded sides.
- Added rider/bike silhouette casting onto receivers while preventing the
  rider/moto receiver from whole-body darkening.
- Added shader-driven wake/spray coverage motion so the replacement effect art
  is no longer only a static retexture.

The implementation does not use screen-wide color heuristics, blue-pixel
detection, VRAM matching hacks or post-process fake lighting.

## Build And Automated Tests

Release build after the Build 12 label change:

- `dotnet build .\JetMoto\JetMoto.csproj -c Release -r win-x64 --no-restore -p:UseSharedCompilation=false -m:1`
- Result: succeeded, 0 warnings, 0 errors.

Renderer pixel/static suite after the Build 12 label change:

- `dotnet run --project .\RendererTests -c Release -- --pixels-only`
- Result: `585 passed; 0 failed.`
- Static/child suite summary: `3 passed; 0 failed.`

Important checked contracts include:

- Rider receiver cannot black out from unstable dynamic silhouette
  self-shadowing.
- Rider receiver ignores static terrain shadowing that could darken the whole
  bike.
- Rider receiver lighting keeps enough source brightness to avoid whole-bike
  dimming.
- Posed rider silhouette casts onto ground receivers.
- Water animation changes shading/detail only, never polygon boundaries or
  water geometry.
- Untagged HUD, sky, rider and effect-style primitives retain original
  rendering outside their intended paths.
- Native asset selection remains based on explicit original asset metadata, not
  replacement texture guesses.

## Actual In-Game Visual Capture

Final process-local native framebuffer validation:

- Capture directory: `reports\build12\build12-final-visual-validation`
- Command: `.\Run-Validation.ps1 -Name build12-final-visual-validation -Seconds 105`
- Result: completed to the expected smoke deadline. Native exit code 3 is the
  scripted validation timeout, not a crash.

Inspected acceptance frames:

- `frame-003000.png`: visible rider/bike cast shadow on track, directional
  lighting remains readable on the rider and bike, and the whole rider/moto is
  not darkened.
- `frame-004800.png`: medium-distance Island water shows surface texture,
  layered ripple breakup and irregular light/dark variation instead of a smooth
  mirror.
- `frame-005520.png`: close-up water shows visible texture, broken highlights,
  moving wake breakup and preserved original hue without geometric water
  displacement.

## Water Brightness Correction

After the first Build 12 package, user review found the ocean water and wave
bands far too bright. The corrected package lowers water body/highlight gain,
removes the white/cyan wave wash, keeps native textured water source-hue based,
and uses the audited original Island fill hue for the underlay.

Correction test and capture pass:

- Release build: succeeded, 2 pre-existing nullable warnings in debug panels,
  0 errors.
- Renderer pixel/static suite: `597 passed; 0 failed.`
- Static/child suite summary: `3 passed; 0 failed.`
- Capture directory:
  `reports\build12\build12-water-original-hue-correction`
- `frame-003000.png`: rider/bike contact shadow and directional lighting still
  intact; rider/moto is not whole-body darkened.
- `frame-004800.png`: medium-distance ocean now uses a dark original
  blue/purple hue, with visible ripple texture and subdued wave bands instead
  of blown-out cyan.
- `frame-005520.png`: close-up water keeps layered moving texture and wake
  breakup while waves/highlights are much darker than the rejected pass.

Residual note: medium-distance Island water can still reveal some native strip
joins from the original PS1 water layout. This is visually different from the
failed mirror-like Build 11 approach and was accepted for the Build 12 package,
but it remains a good target for a later refinement pass.

## Package Gate

Build 12 may be packaged from this source state. The package must be named
separately from Build 11 and must not overwrite the known-good Build 11 release.

Packaged archive:

- `C:\Users\smmel\Downloads\JetMoto-RecompOne-Build12.zip`
- Sidecar: `C:\Users\smmel\Downloads\JetMoto-RecompOne-Build12.zip.sha256.txt`

Archive inspection after packaging confirmed that `vendor`, `.nuget`, `.build`,
`bin` and `obj` directories were excluded, while this validation report and the
three accepted visual-capture frames were included. The final archive checksum
is recorded in the sidecar because the ZIP cannot contain its own stable hash.
# Menu Candidate Checkpoint, 2026-09-25

Continuation: all 20 authored track overviews were reassessed individually and
withdrawn after lettering/linework failures. Two new offline experiments also
failed visual inspection and were not staged. Eleven new native captures verify
Joyride original Loading/Continue fallback; this is not an upscale acceptance.
See `MENU-OVERVIEW-REASSESSMENT.md` for exact artifacts and remaining scope.
Approved application hash remains unchanged. No new release or runtime build.

Options dark row fragments were traced to preceding-label shadow pixels included
in the original atlas rectangles. Source-owned isolated label crops remove them.
Native captures with Intermediate, Professional and Turbo Off states were
individually checked; normal, highlighted and disabled labels remain readable.
See `UI-UPSCALE-CHECKPOINT.md` for exact captures, provenance and remaining scope.
Removed the resolved row-fragment item from TODO; options submenus and all-menu
quality validation remain open. No new release or approved-build replacement.

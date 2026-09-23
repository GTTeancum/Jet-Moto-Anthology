# Build 12 Validation

Status: VISUAL BASELINE APPROVED BY USER; VALIDATION IN PROGRESS, NOT YET PACKAGED.
The user approved `fine-airborne-spray-verified-runtime/frame-003554.png` with
"PERFECT. That's great. That's what I'm looking for". Preserve this appearance.
SHA256: `43AD4B79175BA7A99B3030E557F21D191B6051B1056D7D5E0E1D4273A0963654`.
Remaining gates: swamp material/fake-reflection checks, temporal stability,
and final package verification. The historical
results below are superseded by REOPENED-DEFECTS.md where noted.

## Latest Validation Checkpoint

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

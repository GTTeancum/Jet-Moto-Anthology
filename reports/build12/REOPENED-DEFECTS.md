# Build 12: acceptance withdrawn

The user's deployed-game review rejects the current water quality and reports flickering and absent spray. Prior unit-test results and selected screenshots do not establish acceptance.

Requirements remain: original track hue with visibly improved layered water detail and broken reflections, no water geometry displacement, moving and visible spray/wakes, readable directional lighting and cast shadows, and all cumulative fixes. Preserve Build 11. Do not package another release until native in-game visual and temporal checks pass.

Initial source evidence:

- Both water shader paths multiply already narrow texture variation by another small coefficient. Underlay detail has at most a 0.0612 contribution to its body multiplier before lighting. This suppresses surface contrast independently of hue.
- The deployed run log reports two resolved effect materials and 1,574 effect resolutions. These counts prove metadata resolution only, not visible spray or correct animation.
- Existing validation captures every 120 vblanks, insufficient to diagnose consecutive-frame flicker. The native capture hook and runner now accept bounded capture ranges and a configurable interval. This is diagnostic infrastructure, not a rendering fix.

Next: rebuild the overlay, record bounded consecutive native frames, inspect every captured frame, trace surface/coverage discontinuities, and correct the renderer against that evidence. Water quality, flicker, and spray visibility are all unresolved.

## Diagnostic workflow correction

Per user direction, routine validation now defaults to `--headless --mute` with logs only. Headless mode creates a hidden OpenGL context, omits the debug UI and desktop input initialization, and retains process-local replay. Audio output is disabled without changing saved volume. `Run-Validation.ps1 -Capture` explicitly opts into native images for limited visual acceptance evidence.

The 16-second `headless-muted-check/run.log` confirms GL45 initialization, headless and mute flags, original title loading, and cooperative exit code 3. Its report directory contains only the log, no screenshots. This verifies the diagnostic lifecycle only, not water/spray quality. An initial shutdown crash caused by saving an absent ImGui context was fixed before this successful check.

Before the workflow correction, `reopened-water-temporal` captured nine images and exited early with code 0, so it is not a completed validation run. Frames 4800, 4802, 4804, 4807, 4809 and 4811 were inspected sequentially: very dark underlay, separated wave bands, narrow vertical streaks near the rider, and no convincing spray plume. Remaining frames 4814, 4816 and 4818 are unreviewed. The streaks' source and reported flicker's cause remain unproven. The ray-plane/fixed-depth underlay fallback is a source-level discontinuity requiring investigation, not a confirmed diagnosis.

## Projective water correction

The headless `water-spray-draw-diagnostic/run.log` reproduced fixed-depth substitutions at the water horizon, including rays with positive vertical direction (above the plane horizon) and intersections beyond the 50,000-unit cutoff. It also confirmed actual spray triangles reached the GL renderer with coverage enabled and valid depth, rather than merely loading their assets.

Screen-fill water now passes homogeneous plane coordinates directly to the fragment shader. Coordinates remain affine through the horizon; no per-corner fixed-depth substitution is needed. A finite far-water color handles the horizon. Original vertices, water plane, and track tint are unchanged.

Verification: renderer pixel suites passed 600 checks per backend on GL45, GL33 and GL21, including the new horizon interpolation regression; child-suite summary 3 passed, 0 failed. The game rebuilt successfully. `projective-water-headless/run.log` completed 70 seconds with expected exit code 3, zero water fallback reports, 24 bounded effect draw reports, and no screenshots. This proves execution of the corrected path, not visual acceptance or elimination of every flicker source.

Remaining: replace the suppressed water material response, diagnose absent/insufficient spray visibility and provide moving spray, and perform limited final native visual evidence checks. No new release was packaged or deployed.

## Unified material and spray correction

Both water kinds now use the same world-anchored ripple field and material response. Centered height/detail variation produces visible contrast independently of source hue. Normal-based crest highlights are broken up by the same field. Source color, geometry and original scene reflections are preserved; this does not add a scene-reflection render pass.

The original effect catalog shows water spray frames exposing UV rows 2..4 through 2..59. The replacement plume was authored as a full image with much of its coverage near the bottom; early frames sampled mostly padding. Verified 64x64 coverage effect frames now normalize their own UV extent to the authored plume. Cross-faded vertical texture transport replaces the earlier small wobble/banding animation. Native effect geometry growth remains intact.

Renderer checks: `unified-water-spray-tests.log`, 612 passes per backend (GL45, GL33, GL21), zero failures, child suites 3/0. Added actual-pixel checks for newborn spray coverage, consistent complete-image coverage across source frame ages, temporal movement, and no phase-wrap flash.

The headless muted 102-second `unified-water-spray-check` completed with expected exit 3. Only two native images were captured and both inspected: frame 4801 shows substantially stronger water texture, and frame 5040 shows visible spray behind the bike. Both still show thin vertical streaks near the rider, so visual acceptance FAILS. Surface quality and temporal stability need further review. No release was packaged or deployed.

`vertical-streak-diagnostic` ran headlessly for 86 seconds, logs only, expected exit 3. Its first bounded sample was dominated by offscreen/clamped triangles, so it did not identify the visible streak. Logging is now limited to nonzero-width tall triangles overlapping the active clip at vblanks 4800..5100. This refined diagnostic has not yet been run.

## Effect/world provenance conflict

`visible-streak-diagnostic` identified ordinary sign-support material BBB0, not the pale effect streak. The expanded `tall-material-diagnostic` then captured an actual conflict: at vblank 4802, the water-spray material `ISLAND1/ISLAND1.TMS#62:0000EF77` arrived with world kind 4 (ocean underlay). That shader discarded its source color in favor of ocean tint. Verified effects now bypass world metadata both at native emission and at renderer vertex/camera setup. The actual-pixel regression deliberately tags a red effect with blue-ocean metadata and verifies that original red coverage blending survives.

Continuous projection was extended to textured water to avoid dropping an entire triangle's shading when a corner crosses the horizon. The first visual run exposed sky tinting on non-underlay backdrop faces, and was rejected. Source backdrop rays behind the plane now retain original shading; only the added underlay discards pixels above its horizon. New tests cover both cases. Water-clock wrapping now uses 1000 seconds, matching whole texture-scroll periods instead of the previous nonperiodic reset.

`effect-world-isolation-tests.log`: 630 passed, 0 failed on each of GL45, GL33, GL21; child suites 3/0. Game build succeeded. All three images in `effect-isolation-visual-check` (3000, 3600, 4801) were inspected; the sky regression makes them failure evidence, not accepted artifacts. `horizon-effect-corrected-check` is the next in-game visual check. No package/deployment performed.

## Latest limited visual check

`horizon-effect-corrected-check` completed 86 seconds headlessly and muted, with expected game exit 3 and successful runner exit 0. Exactly two native captures were produced and individually inspected: frame 3000 has a recognizable rider/bike cast shadow on the road; frame 4800 shows textured purple-blue ocean water and no recurrence of the large purple sky patches. A foreground buoy obscures much of the near-bike region in frame 4800, so this is not acceptable spray evidence. Two separated stills cannot verify flicker elimination or spray motion. Surface quality, temporal stability, visible animated spray, and track-hue comparisons remain unaccepted. Logs confirm the EF77 spray draw now has no world kind; that establishes metadata isolation, not visual quality.

Routine validation remains logs-only; native capture is an explicit, bounded opt-in for visual evidence. No new package or deployment was produced. Build 11 remains untouched.

## Source classification audit and short temporal check

`spray-temporal-current` completed headlessly/muted with expected exit 3. Its three captures (5000, 5003, 5006) were inspected individually in order. A bright spray strip appears in 5000 and is absent in 5003/5006; the latter two appear unchanged. This does not establish smooth moving spray or distinguish native expiry from a renderer defect. Cyan source wave bands remain visibly distinct from the purple ocean base. No full-frame flash was visible within this tiny sample, but it is insufficient to clear the reported flickering.

Effect draw diagnostics now accept `JETMOTO_DIAG_EFFECT_START`/`END`, with a maximum of 96 records. `spray-expiry-diagnostic` completed with expected exit 3 and no screenshots, but no effect draw occurred within 4995..5020 on that run. Vblank-based replays are not reliably aligned to the same race moment across runs; this diagnostic is inconclusive.

The source-face inspector now supports `--all-roots`. `backdrop-source-audit.json` shows that hardcoded `IslandWaterFillOffsets` included root-tag-2 sky-dome polygons, e.g. A5CDC/A5CFC at Z=1910.5, although the fallback assigned them a water plane at Z=-120. Removed this incorrect offset fallback entirely; uncatalogued polygons now retain original rendering. Catalogued water and the dedicated underlay remain. The game rebuilt with zero errors.

`source-classification-corrected` completed 85 seconds headlessly/muted with expected exit 3. Its single capture, frame 4800, was inspected: ocean texture remains visible across the foreground and medium distance, without a newly exposed coverage hole or purple sky patch in this view. A conspicuous cyan/bright source wave band remains, and no convincing spray plume is visible behind the player in this capture. This is a narrow regression check, not release acceptance. Next work must address the source-wave/base-water response mismatch and obtain reproducibly timed effect lifecycle evidence. No package or deployment.

## Event-triggered effect evidence and ripple field replacement

`Run-Validation.ps1 -Capture -EffectCaptureFrames 3` now opts into a bounded sequence triggered by a native coverage-effect draw after CaptureStart. Routine runs still produce logs only. Capture metadata includes the renderer frame and last effect draw vblank/count; draw submission and displayed frames are pipelined, so these counts must not be interpreted as exact visible particle counts.

`event-triggered-spray` completed 76 seconds, expected exit 3. All three images (3530, 3535, 3540) were inspected sequentially. The wake becomes visible around the bike and expands, but appears as thin overlapping plume outlines, not convincing airborne spray. This is evidence of visible evolving wake geometry, not acceptance of spray quality or proof that shader motion is independently visible.

Direct inspection of native replacement materials CA0B and C490 showed shoreline/underwater artwork with a beige-to-cyan-to-blue gradient and submerged objects. Bright bands cannot all be treated as new specular highlights without changing source material identity. No global recoloring or blanket brightness adjustment was applied.

The water normal/height data now uses three periodic, noise-warped crest-line frequencies with irregular amplitude and spacing, replacing the granular value-noise height field. Existing layered scrolling, source colors and fixed geometry are unchanged. `curved-ripple-tests.log`: 630 passed, zero failures on each GL45/GL33/GL21; child suites 3/0. Game build succeeded.

`curved-ripple-water-check` completed 86 seconds headlessly/muted with expected exit 3. Its single native capture (4800) was inspected. Foreground ripple ridges are more coherent and visible, with dark/light variation and an evolving wake visible behind the bike. However, fine-scale repetition remains conspicuous and the wake still looks like flattened, separated sprite stamps. This is not accepted as the requested final quality. Native object reflections remain comparatively smooth; the current ripple shader modulates water material response but does not yet establish distortion of the independently drawn reflected geometry. Further work is required on reflection treatment, spray structure/lifetime, temporal stability and swamp verification. No release packaging/deployment.

## Separate airborne water droplets

Added explicit `WaterSpray` semantics only for already allowlisted EF77 water effects. The original surface wake remains; an additional lifted sprite layer contains animated droplet trajectories with continuous birth/death fades, using the source effect color. It does not move water geometry, use screen color detection, or affect ordinary coverage effects. This remains a sprite-local spray implementation, not a world-space particle simulation.

Renderer pixel tests now verify particles above the original wake footprint, their independent motion, and no added geometry for other coverage effects. Initial `airborne-water-spray-tests.log`: 639 passes per backend, child suites 3/0. All three native frames in `airborne-spray-native-check` (3551, 3554, 3558) were inspected sequentially. Motion was visible but oversized round droplets failed visual acceptance.

Reduced droplet radius/opacity and antialias fringe. An overly fine version failed visibility tests on GL45/GL33 at 4x (`fine-water-spray-tests.log`); the corrected size passed the unchanged tests (`resolved-water-spray-tests.log`, 639 passes per backend, child suites 3/0). Effect identity tests pass 37/0 (`airborne-effect-identity-tests.log`). EffectTextureTests and NativeTextureTests project files now include the missing RiderGeometry dependency required by their linked current WorldLighting source.

The first fine-spray game test started before its build completed, locking the old runtime DLL. That headless process was stopped; the run and failed build are not validation evidence. A fresh build succeeded, and both runtime DLL copies were verified as SHA256 `37B7BAD6BF2762665DB51E3CD2E37DCAF737C2AFC47CE83924B26E5D06C85D0E` before launching `fine-airborne-spray-verified-runtime`.

That fresh headless/muted run completed 76 seconds with expected exit 3. All three native frames (3550, 3554, 3558) were inspected sequentially. Smaller airborne droplets are visible and change position/fade between frames without the previous oversized circles. The surface wake still shows separate repeated plume outlines, so overall effect quality remains unaccepted. This short sequence is not sufficient to clear general water flickering, and reflections/swamp verification remain outstanding.

Validation runner now rejects a mismatched built/runtime DLL and writes app/runtime/replay hashes plus launch/capture parameters to `validation-run.json` for future runs. PowerShell syntax validation passed; that new preflight has not yet been exercised by an actual game run. NativeTextureTests also ran successfully after repairing its RiderGeometry project dependency (see `airborne-native-texture-tests.log`). No release package/deployment; Build 11 untouched.

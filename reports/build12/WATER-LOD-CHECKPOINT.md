# Water distance-selection investigation

Status: opt-in experiment; visual result UNVERIFIED. No deployment or package.

## Evidence

The additive-water replay's material log has no opaque C490/CA0B water at
VBlanks 3496/3498. C490 appears at 3505, matching the first reviewed frame with
a conspicuous hard-edged blue patch. CA0B appears at 3510 and its submitted
coverage grows afterward. These are submitted primitives, not measured pixels.

The original ISLAND1 DMD places those materials under type-2 distance selectors.
Source node 0x277D8 permits the near branch only for squared distance below
160000. Nested branches switch at 90000, 40000 and 14400. The native traversal
at 8010EC60..8010ECF8 computes squared model-scaled camera distance and chooses
the interval containing it. This is direct evidence of distance selection,
though not proof that it explains every reported flicker.

Extended the source audit with original distance ranges. Added WaterLodPolicy
to discover only selectors whose entire reachable source subtree is water,
including every animation/switch variant. Missing catalog faces, mixed land,
unknown node types, invalid pointers and cycles cannot qualify a branch.

## Experimental behavior

`JETMOTO_EXTEND_WATER_LOD=1`, exposed as `Run-Validation.ps1 -ExtendedWaterLod`,
divides squared selection distance by 16 for verified water-only selectors.
This extends linear detail distances fourfold. It does not move vertices,
change source colors, change the camera frustum or alter land/rider selectors.
The exact generated-code hook is anchored after 8010ECB0. The normal game path
does not enable this policy or perform its extra source-graph traversal.

ISLAND1 yields 401 fully classified water meshes and 166 eligible selectors.
The first parser omitted animation-node traversal and found zero selectors;
the first native experiment was therefore ineffective. Its captures are not
used as visual evidence and were not reviewed. Added all-variant traversal
and a regression rejecting a branch with a single non-water variant.

## Verification and failed coverage

- Policy tests: 13 passed, zero failed; real source audit finds 166 selectors.
- Exact-anchor generated hook patcher succeeded.
- Native texture tests: 114 passed, zero failed.
- Effect texture tests: 37 passed, zero failed.
- Neural pack: all 1406 PNGs accepted by the native loader, all 15 effect
  materials checked; original categorical/coverage checks passed.
- Isolated game builds succeeded. Final disabled-path guard build is tracked
  in `user-reported-effects-flicker/water-lod-final-build.log`.
- `extended-water-variants-bay` ran headlessly and muted with the policy enabled.
  Inspected all four captures individually in order: 003500, 003504, 003507,
  003511. The bike is on/against a structure, not the earlier open-water route.
  Water visible on the left has detail, but the intended shore transition is
  not visible. This FAILS the required test coverage and proves no LOD fix.
- The 60-second heartbeat reports visibleMax=258, matrixMax=180 and zero
  capacity/guard limit hits. This does not establish acceptable performance
  or full-course correctness. The bounded native process completed normally.

The absolute-VBlank replay did not reproduce the same gameplay state under
the changed run conditions. Texture tests were also executed while this native
run was active, adding load; do not treat it as a controlled timing comparison.
Next: use race-relative process-local replay timing and avoid competing test
loads while capturing, then compare the same shore approach with the policy
off/on. Keep the experiment disabled until actual visual/cost checks pass.

All original acceptance requirements remain in force: stable near/medium water,
original hues, fixed geometry, substantial player/opponent and land effects,
shadow evidence, swamp mirrors and cumulative compatibility. Build 11 untouched.

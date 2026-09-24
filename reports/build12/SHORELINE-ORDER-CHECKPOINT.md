# Shoreline Draw-Order Probe

Status: diagnostic progress only; water acceptance still FAILED. Spray deferred.

Added an opt-in point-coverage trace during the existing audit interval.
`JETMOTO_AUDIT_POINT_X/Y` are source-screen coordinates; finite triangle
barycentrics determine geometric coverage. Records include source material,
world kind, transparency/blend, OT value, clip bounds and water inverse depth.
At most 128 hits are retained per frame. This is not a screen-color heuristic,
does not alter rendering, and does not prove final visible pixel ownership.

First run `shoreline-draw-order` completed headlessly/muted but the probe
incorrectly rejected the selected x=-30 margin using the original 4:3 scissor.
The actual renderer expands that scissor for Hor+. All three captures
(981, 990, 1000) were inspected individually in order; no diagnostic ownership
claim is made from its empty point records.

Corrected the probe to record pre-clip geometric coverage and retain clip
bounds for interpretation. Rebuild succeeded. `shoreline-margin-order` then
completed its 65-second headless muted run. The point (-30,205) is predominantly
covered by verified opaque kind-2 ISLAND1 material CA0B. Across the interval,
55 records contain CA0B alone, four contain two CA0B triangles, two contain
CA0B followed by transparent C935, and one contains only C935. These are draw
records, not independent screenshots or proof of the flicker's exact source.

Inspected all three corrected-run captures individually in order:
race 981, 990, 1000. Water is textured but near/deep boundaries remain visible.
Capture 1000 also shows a thin purple streak across dry sand at the right.
That is evidence of a geometry/projection/ordering issue beyond merely
different water material colors. The selected left-margin probe does not
cover that right-side streak; do not attribute it to CA0B without tracing it.

Next: associate complete source-primitive geometry with selected native
captures so a newly visible streak can be traced after capture without
guessing its screen location before a nondeterministic replay.
No release/package/deployment. Build 11 preserved.

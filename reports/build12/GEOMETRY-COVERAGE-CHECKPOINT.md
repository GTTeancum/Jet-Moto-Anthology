# Water Geometry Coverage

Status: water acceptance FAILED; diagnostic progress, not a visual fix.
Spray remains deferred. No package or deployment; Build 11 untouched.

The first full geometry trace incorrectly associated submission-frame records
with the framebuffer selected later for presentation. Its `available` flag
must not be interpreted as proof of correct association.

Changed the optional trace to collect pending triangles after batch selection,
then associate them with the actual target at Flush. The first corrected run
(`water-target-geometry`, 65 seconds, headless/muted) showed that one native
image also spans multiple host render-frame counters. Retaining only the last
counter omitted most of the image in captures 991 and 1000. All three saved
captures (980, 991, 1000) were individually inspected in chronological order;
991 has a large purple foreground polygon absent in 1000.

The trace now retains batches across counters until a native buffer switch or
full clear. `water-sequence-geometry` completed 65 seconds headlessly/muted;
the harness returned 0 after expected application timeout exit 3. Both saved
captures, 982 then 991, were individually inspected. Their trace targets and
clip rectangles agree, with 979 and 912 triangles respectively, neither
truncated. A purple foreground patch appears in 982 and disappears in 991.

At image pixel (1040,925) in sequence capture 982, geometric candidates are
the background rectangle, kind-4 water background, and a transparent EF79
effect. No kind-2 near-water triangle covers that point. At adjacent pixel
(1250,850), an opaque CA0B kind-2 triangle is present. Its vertices include
(172.11656,199.87833), (1023,684.71094), (475.89288,241.14691).
This is evidence of missing near-water coverage, not proof that a new purple
triangle was drawn over it. Geometry candidates do not evaluate texture masks
or shader discards, and are not final pixel-ownership results.

Source inspection found that GpuHleForward rejects all triangles with native
integer spans exceeding 1023 horizontally or 511 vertically before they reach
the backend. Added bounded opt-in logging of rejected geometry to test whether
this rule removes triangles covering the observed gaps. This remains a
hypothesis until the rejected geometry is checked against a matched capture.
The diagnostic changes alone did not alter rendering or remove that rule.

## Span Rejection Evidence And Repair

`water-span-rejection` completed the 65-second native headless/muted run.
Both saved captures (981, 991) were individually inspected in order. Capture
991 has a purple foreground hole at image pixel (650,930), native target
coordinate (428.785046,232.625). The drawn sequence there contains only the
background and kind-4 water rectangle. The rejection log contains a CA0B
kind-2 triangle covering this point, submitted at vblank 3387 to target x=320,
before capture vblank 3393. Its vertices are (362.25018,204.45258),
(621,733), (603.00604,250.6912); its integer span is 260 by 529. It was
discarded by the 511-pixel height rule despite covering the viewport.

Changed HLE forwarding to let source-verified world kinds 1, 2 and 4 reach
host viewport clipping even when larger than guest span limits. Unverified
primitives and rider kind 3 retain their previous policy. This does not move
vertices, alter world geometry, recolor water, or classify screen pixels.
PerspectiveTests adds vertical/horizontal oversized packet cases for kinds
0 through 4 and verifies unchanged projected vertex positions. Result:
72 passed, 0 failed. Isolated game build succeeded with 0 warnings/errors.
Native visual verification of the repair follows; these tests alone do not
establish water acceptance.

`water-world-clipping` then completed a 65-second headless/muted native run.
Individually inspected both saved captures, 980 then 990. Neither shows the
large foreground purple coverage hole seen in the preceding diagnostic runs.
Near-water texture is visible in both, with existing rider directional shading.
This is limited supporting evidence, not a temporal pass: the replay path
varies between runs, only two saved images were inspected, and distant deep
water still differs visibly from the near-water surface. Full shoreline,
land/sky streak, swamp, and lighting regression checks remain outstanding.

Post-change renderer suite completed successfully: GL45, GL33 and GL21 each
reported 669 passes and 0 failures; parent static checks and child-suite
statuses reported 18 passes and 0 failures. Results are in
`user-reported-effects-flicker/world-span-renderer-tests.log`. This validates
the current synthetic renderer cases, not the outstanding native visual gates.

Remaining: prove the coverage failure's cause, repair it without changing
water geometry or using screen-color heuristics, verify native temporal and
near/medium-distance water consistency, then complete the broader lighting,
swamp, hue, and cumulative-regression gates before packaging.

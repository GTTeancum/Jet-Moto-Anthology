# Loading source continuation, 2026-09-25

Current user scope is **loading screens only**. Other menu work remains pending.
No new loading upscale was authored, staged, or accepted during this pass.
The rejected neural and contour outputs remain rejected.

## Original visual inspection

Viewed Joyride Loading first, then individually viewed the nine remaining original
Loading images in this order: Cypress Run, Blackwater Falls, Suicide Swamp,
Cliffdiver, Hammerhead, Willpower, Ice Crusher, Snow Blind, Nightmare.
Each has its original track map, paragraph, labels, legend and Loading footer.
All are 320 by 240. This closes the previous offline original-L inspection gap;
it does not establish native coverage of all ten tracks or upgrade quality.

The separately extracted JMFONT atlas was also viewed. Its uppercase italic
lettering is different from the overview paragraph's mixed-case lettering.
It is not a faithful replacement for those baked paragraph glyphs. All ten
disc files named JMFONT share one SHA-256; they do not offer larger variants.
This finding is limited to that named font family, not every possible disc asset.

## Verified pair evidence

New `TextureTools/audit_loading_sources.py` verifies the supported executable,
source TIM hashes, extracted PNG hashes and all decoded RGBA samples against
a fresh disc decode. All 20 originals passed. Full differing source samples
are retained in `menu-loading-source-pairs.json` for later paired authoring.

| Track | Differing samples | Outside footer prompt rectangle |
| --- | ---: | ---: |
| Cypress Run | 693 | 3 |
| Blackwater Falls | 696 | 0 |
| Suicide Swamp | 689 | 0 |
| Joyride | 693 | 0 |
| Cliffdiver | 689 | 0 |
| Hammerhead | 689 | 0 |
| Willpower | 690 | 0 |
| Ice Crusher | 667 | 4 |
| Snow Blind | 689 | 0 |
| Nightmare | 708 | 1 |

Footer rectangle is source x=[209,306), y=[211,228). The eight exceptions are
at y=207..209 and each differs by one 5-bit color step in one RGB channel.
Preserve these original differences; do not silently normalize both variants.
The pairs otherwise repeat the same map/paragraph pixels. They do not provide
shifted samples from which additional map or paragraph detail can be recovered.

## Remaining problem

Full-image neural reconstruction, source-constrained neural output, separate
neural lettering masks and six source-contour variants have already failed
visual review. The repeated pair samples and JMFONT atlas do not solve the
small-lettering problem. No successful reconstruction method is established.
Do not turn this source audit, a resized original, or another metric-only result
into acceptance. Higher-resolution original overview artwork would remove this
source limitation; no such source has been established in this workspace.

No runtime change or new native run was required for this source-only audit.
No approved-output changes, release packaging, commit or push were performed.

## Repeated-letter reconstruction follow-up

The next source-only experiment used four manually checked occurrences of the
word "the" from Joyride's original paragraph. Registration found horizontal
offsets of 0, 0.270, 0.400 and 0.503 pixels, but effectively no vertical phase
diversity (absolute fitted dy below 0.025 source pixels). This is distinct from
the identical Loading/Continue pair data: there are horizontally shifted word
samples within a single image.

`TextureTools/experiment_loading_repeated_word.py` fits a 4x horizontal field
with exact pixel-overlap sampling and three curvature penalties. No model,
replacement font or runtime change was used. Vertical enlargement is explicitly
interpolation and the monochrome diagnostic background is not a menu candidate.
Artifacts: `menu-loading-repeated-word-experiment`. The original enlarged word
and each of the three outputs were individually inspected in order.

**All three outputs are rejected.** The e crossbar is lost and the t becomes
rounded. Source-fit RMSE of 4.33, 4.86 and 5.71 does not establish fidelity.
Nothing was staged, and a native run would not resolve this offline failure.

Loading reconstruction remains at an impasse under the original constraints.
Repeated pixels, the unrelated JMFONT atlas, neural reconstruction, separated
mask inference, source constraints, contour tracing and now repeated-word
reconstruction have not supplied faithful missing glyph detail. Further progress
requires a better original source or user direction permitting a different
standard for reconstructing tiny lettering. This is not completion and does
not authorize a substitute font, restaging rejected artwork, or resuming other
menu work despite the current loading-only scope.

## Deferred controller context

Before the loading-only direction, controller accept/reopen/reverse-wrap
captures were inspected. P1 selection persisted in-session; P2 changes were
not exercised because the current replay provider disconnects P2. A controller
small-print source fallback was staged in the isolated candidate only, with
backups under `menu-controller-before-print-restoration`. Its native run
`menu-controller-restored-print-native` completed (game 3 / wrapper 0), but its
captures remain uninspected. Do not claim that repair is native-verified or
resume that work while the loading-only scope remains in force.

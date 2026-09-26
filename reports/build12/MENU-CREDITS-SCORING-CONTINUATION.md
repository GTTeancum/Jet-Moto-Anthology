# Credits and scoring continuation — 2026-09-25

Work remains isolated in `.build/ui-buoy-bin`. No runtime code changed or rebuild,
release package, commit, or push was made. Approved application hash remains
`D2C09E1AF287900921AD27FEDE9281FB1CFF724C0044783D577F0D4DD66F3333`.
All native runs below are muted/headless, process-local replay, disposable cards.
Saved captures were inspected individually, in order; these are sampled states,
not an every-animation-frame audit. Audio is unverified because runs are muted.

## Loading experiment rejected

`trace_overview_lettering.py` generated six source-contour paragraph variants:
thresholds 0.4/0.5/0.6, each with zero/one Chaikin pass. Every output was inspected.
All fail: bubbly/jagged letter shapes and changed holes. Never stage these files.
No loading upgrade passes. All twenty rejected OVERV candidates remain withdrawn.
Cross-track equal-pixel comparison finds portions of a shared backdrop, but does
not establish a complete recoverable text-free paragraph background.

## New original sources and authoring

Exported original BS assets using original VLC/MDEC decoding, source hashes and
640x480 provenance: SCORING, TRACKS0, DEVELOP, and CREDITS0 through CREDITS7.
Every source was viewed individually. TRACKS0 has not been authored/staged.
Existing SRVGG authoring produced ten candidates: scoring, developer card, and
eight credits pages. All ten were viewed offline and staged in isolated Menu4x;
catalog count is now **42**, superseding 32. No OVERV assets were added back.
DEVELOP has only load/submission log evidence, not native visual acceptance;
its learned bevel versus original flat-gray lettering needs reassessment.

## Credits page navigation

Credits are eight paginated BS screens in this route, not scrolling text.
`menu-credits-entry-native` five captures were all inspected:
1560/1620 Options with Credits highlighted, 1800 page 0, 1980/2160 Options
after Triangle. Game exit 3 / wrapper exit 0.

`menu-credits-all-pages-native` finished game exit 3 / wrapper exit 0. All 21
captures were inspected individually, sequentially:

| Frame | Visible page/state |
| --- | --- |
| 1800 | Page 0, legal/design credits |
| 1920 | Page 1, Sony staff |
| 2161 | Page 2, testers |
| 2400 | Page 3, SingleTrac staff |
| 2640 | Page 4, graphics/sound staff |
| 2880 | Page 5, additional support |
| 3121 | Page 6, Axiom/illustration |
| 3361 | Page 7, concept/music/thanks; Continue prompt |
| 3600 | Page 6, reverse |
| 3840 | Page 5, reverse |
| 4081 | Page 4, reverse |
| 4320 | Page 3, reverse |
| 4560 | Page 2, reverse |
| 4801 | Page 1, reverse |
| 5041 | Page 0, reverse |
| 5281 | Page 0 after Up at first-page boundary |
| 5520 | Page 1 during faster forward navigation |
| 5761 | Page 5 during faster forward navigation |
| 6000 | Page 7 after Down at last-page boundary |
| 6241 | Options, Credits highlighted after Cross |
| 6480 | Options, selection preserved |

Page contents, background, footer prompts, order, backward navigation, end
boundaries, and exit work in these samples. Larger body text/illustration edges
are clearer, but close comparison exposed tiny TM marks reconstructed as TN/TA
on pages 0, 3, and 5. The initial versions of these pages are superseded.

`restore_credits_marks.py` restores four narrowly bounded mark regions from
verified original samples using Lanczos 4x and a two-source-pixel boundary
feather. It substitutes neither fonts nor invented glyphs. This is a fidelity
fallback: tiny marks retain source detail, not newly recovered detail. Revised
full pages were individually viewed offline and staged with before/after hash
checks. Prior files are preserved in `menu-credits-before-mark-restoration`;
revised authoring files are in `menu-credits-mark-candidates`. Native verification
of these changed pages is recorded below when complete. Credits remain pending
until the final fidelity review and regression gates pass.

## Scoring method

`menu-scoring-variants-native` five captures all inspected: 1920 Championship,
2040 Rally, 2160 Elimination, 2280 Rally, 2400 Race Type after Triangle.
Each matching description and gold selection appeared. Original layout and
4:3 proportions retained. Game exit 3 / wrapper exit 0.

`menu-scoring-source-background-native` repeats the same flow with BS candidate
backgrounds disabled. All five captures inspected: 1921 Championship, 2041 Rally,
2160 Elimination, 2280 Rally, 2400 Race Type. Expected normal exit. This is a
source-BS baseline, **not a fully original TMS rendering**: existing TMS pack
remains active. Dark description rectangles and narrow horizontal boundaries
are present with the original BS background too; they are not introduced by
the new BS candidate. All 21 original SCORING.TMS tiles were also individually
viewed; descriptions include darkened clock imagery and separate rows. Do not
generalize the approved track-dial sampling fix to these tiles by assumption.
Accepting each scoring mode onward, wrap behavior, remaining TMS quality and
full race-type variants remain unverified.

## Final checks and current staging

Repaired credits native checks finished normally (game 3 / wrapper 0).
Both captures from `menu-credits-restored-marks-native` were individually viewed:
1800 page 0 and 2400 page 3. The sole capture from
`menu-credits-restored-page5-native`, 2880, was also viewed. Restored marks match
the original low-detail samples without the new TN/TA interpretation. No visible
patch border or changed surrounding layout in these complete-screen captures.
The corrected pages remain isolated candidates pending final family acceptance.

`menu-develop-first-native` finished normally; all three captures were viewed:
61 and 90 black transition states, 120 DEVELOPED BY. The new lettering has a
brighter embossed treatment than the original flat gray. **Rejected and withdrawn**
with verified hashes to `rejected-develop-staged/STARTUP`. Do not restage the
original authoring candidate. Current catalog contains **41**, superseding the
intermediate 42 count above. Startup baseline timing/transition and replacement
quality still need work; black captures alone prove neither success nor defect.

Python syntax checks passed for contour and mark-restoration scripts. Git diff
whitespace check passed (existing line-ending warnings only). Approved binary
hash rechecked unchanged. Disposable-card settings returned to carda.sav/cardb.sav.
No tool sessions from these runs remain active.

## Still pending

Keep the entire all-menu objective and TODO open. Loading remains unsolved;
Save Game/results/standings/awards routes, title/start, all track variants,
foreground coverage and other family-specific gaps are not closed by this pass.

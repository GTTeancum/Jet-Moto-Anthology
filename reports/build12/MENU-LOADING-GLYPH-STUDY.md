# Loading paragraph source-glyph study, 2026-09-25

Loading-only scope and the direction to work with the existing originals remain
in force. This continues the source-guided title/map work. No menu family,
loading screen or paragraph is declared complete; nothing is staged.

## Source annotations

`measure_loading_glyphs.py` labels the actual Joyride paragraph pixels using its
transcription. No replacement text is rendered from a system font. It creates
135 glyph-region records covering 28 distinct characters, source-line review
images and repeated-character source sheets.

The first word boxes were inaccurate. All ten first-pass line images were viewed
sequentially, followed by the e/t/a source sheets. The review exposed clipped or
contaminated examples. Word bounds now come from the source's measured whitespace
runs, checked against each transcribed line's word count. All ten V2 line images
were individually viewed in order. Glyph boundaries inside touching words remain
provisional; do not infer that all 135 are ready for outline fitting.

Current source annotations:
`menu-loading-joyride-glyph-measurements-v2/glyphs.json`.
Do not use V1 annotations for fitting.

## First constrained outline

`fit_loading_e_outline.py` fits a connected bowl, enclosed counter and crossbar
to four reviewed e occurrences: IDs 41, 64, 98 and 117. ID 128 is withheld from
shape fitting; only its translation is fitted afterward. These are the isolated
ends of the/The words. The geometry uses an explicit ellipse/crossbar prior,
not an outline from an external font. Between-sample curves are inferred, so a
good projection error cannot prove exact original typography.

The first large outline and all five source/projection comparisons were
individually inspected. Unlike the rejected unconstrained repeated-word study,
this outline keeps the e counter and crossbar. Training projection RMSE ranges
from 9.69 to 12.11; the held-out value is 12.59. These are diagnostic measurements,
not acceptance gates.

The first full-screen composition made these e letters too gray and replaced
unnecessary background pixels. Both complete screens were individually viewed,
Loading then Continue, along with the paragraph crop.

Measured source paragraph peaks include 232 in A/h/t and 240 in h/m. A second
fit constrains ink intensity to 232, an existing bright source-palette value,
instead of allowing a gray optimum to trade off stroke area against intensity.
This is an authoring color constraint, not proof of the original foreground
material value. The revised isolated outline and held-out comparison were viewed;
its held-out projection RMSE is 14.43. The candidate keeps a visible counter and
crossbar, but its exact curvature and stroke weight remain provisional.

Current fit: `menu-loading-joyride-e-outline-ink232/manifest.json` and its SVG.
Parameter slot 7 is the effective gain (ink minus baseline); it is inactive
during fixed-ink optimization and is normalized when exporting the result.

## Full-screen placement

`compose_loading_e_study.py` places only those five reviewed e instances on the
existing V3 map study. It estimates the background under the old glyph and
retains untouched pixels outside the old/new ink support, including neighboring
h antialiasing and surrounding blueprint detail. It does not reflow lines or
change word positions. Background separation is still an authoring estimate.

Current output: `menu-loading-joyride-e-composition-v2`.
Both complete revised screens were individually inspected, Loading then Continue.
The sharper five letters visibly contrast with the unreconstructed remainder;
this is a mixed authoring study, not a delivered upgraded paragraph.

Scope checks in `scope-check.json` found 2,007 changed pixels in each variant,
all within the five designated glyph boxes. The Loading/Continue difference is
unchanged. No title, map, marker, legend or footer pixels are changed relative to
the input map study. These checks prove edit scope, not typography fidelity.

## Joint h/t/T and complete-word follow-up

`fit_loading_the_words.py` now fits the h shoulder/stems, lowercase t stem,
crossbar and bend, and uppercase T together with the existing e shape. Fitting
whole the/The words avoids clipping a neighboring glyph at a provisional
character boundary. It fits four source words and withholds the final lowercase
the from shape fitting; only that word's translation and background baseline
are fitted afterward. Capital T has only one observed instance here and does
not have an independent held-out validation sample.

The initial five outline images and five original/projection comparisons were
individually inspected in word order 0 through 4. The t showed an artificial
half-opacity seam where its stem, bend and foot primitives met. This was a
renderer artifact, not a feature of the original typography. Do not use those
initial outline renders as artwork.

The final renderer now samples the filled union at higher resolution before
area reduction. This removes internal join seams while retaining antialiasing
on the actual outer boundary. All five revised large word outlines were
individually inspected. Source fitting still uses the smooth optimization
surrogate; a separate `render-projection-check.json` measures the final filled
renderer against the original samples. Neither measurement is font identity
proof or an acceptance gate.

Current outlines: `menu-loading-joyride-the-outline-v2` (five SVGs, inspections,
fit metadata and final-render projection checks). The shapes remain source-guided
geometric hypotheses. In particular, the h stroke weight and inner shoulder
curvature must still be judged in the completed paragraph.

`compose_loading_the_study.py` places the complete five words over the V3 map
study, preserving unmodified pixels outside their old/new ink support. It does
not start from the previous e-only composite, so old diagnostic e placements
are not accidentally layered twice. Current full screens are under
`menu-loading-joyride-the-composition-study`.

The paragraph crop and then both complete screens were individually inspected,
Loading followed by Continue. The new complete words have intact crossbars,
counters and stem connections, with no earlier t seam. They visibly contrast
with the still-coarse surrounding text; this remains an unaccepted mixed
authoring preview, not a delivered paragraph or loading upscale.

Both variants have 6,206 changed pixels relative to the input map study, all
inside the five designated word boxes. Their variant delta is unchanged. No
native run or runtime staging took place. The rest of the paragraph, labels,
terrain, legend, markers, prompts and the other nine tracks remain pending.

## Remaining implementation work

- Review/correct the remaining glyph boundaries before fitting their outlines.
- Fit other characters from source observations, retaining their counters,
  connections, proportions and original placement. The current e prior does
  not establish a complete font or authorize a replacement typeface.
- Assess consistency of weight, color and spacing in a complete paragraph.
- Finish labels, terrain, markers, legend and variant prompts before native
  loading-screen acceptance; the other nine tracks remain pending.

All commands completed. No native test, runtime staging, approved-build change,
release packaging, commit or push occurred in this pass. The goal remains open.

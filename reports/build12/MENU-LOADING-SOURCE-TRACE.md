# Loading reconstruction from available originals, 2026-09-25

Latest user direction: **"This is what you have. Work with it."** Loading screens
remain the sole working scope. Use the existing original assets and continue
reconstruction; the earlier request for higher-resolution artwork is superseded.
This does not establish approval for an unrelated typeface or invented artwork.

## New original source and editable study

Extracted `PICKTRAC/TRACKS0.TMS` into
`menu-loading-lettering-sources/TRACKS0`, with source and PNG SHA-256 provenance.
Records 1 and 12 together contain all ten track names. Their lettering is larger
than the baked loading titles. Record 1 contains JOYRIDE; its original raster
provides a second source for the title's glyph bounds, counters and J descender.
This is a useful source, not proof that every glyph is identical at both sizes.

Individually viewed records 0, 1, 2, 3, 4, 11, 12, 13 and 14. Other dial records
were not needed for this loading investigation and were not inspected. No dial
assets were modified. Also inspected original credits pages 1, 7 and 6, plus
a cropped `The` comparison, as possible larger lettering references only.
No credits asset or typography was copied into a loading screen.

`study_loading_title_source.py` verifies both source PNG hashes and fits the
larger original title to the loading title's position/scale. Its original crop,
projected source-scale crop and contour study were individually viewed. The
simple contour study was too angular; its metric is not an acceptance gate.

`trace_loading_joyride_title.py` creates editable source-guided outlines for
the seven letters. The construction follows the native atlas's stems, counters,
glyph bounds and descending J. Between-sample curves are authoring estimates
and still require comparison in the completed artwork. There is no external
font in this trace. Output includes a vector SVG, a 4x title crop, an enlarged
inspection crop and provenance manifest.

Both versions were individually viewed. V2 corrects the R diagonal's lower
endpoint so it does not touch I. The trace has cleaner curves than the automated
contour study. It remains **an offline title study, not a complete replacement,
not a fidelity acceptance, and not a native-tested loading screen**.

Current artifact:
`menu-loading-joyride-title-trace-v2/joyride-title-source-trace.svg`.
The flat dark-blue diagnostic background is not an authored game background.
No output was staged in Menu4x; all prior rejected OVERV replacements remain out.

## Typeface diagnostics

`identify_loading_lettering.py` compares outline projections to the original
Joyride paragraph. Standard, expanded, Gill and Eras font diagnostics did not
establish a reliable original-font identity. Their generated comparison crops
are not replacements, and none was staged. The first parameter search was
insufficiently initialized; a second pass used multiple starts and width-based
initialization. The Gill pass stopped at a condensed font whose initial width
was outside the old bounds; the bounds were subsequently corrected, but that
pass was not rerun because no result is used in the source-guided title trace.
Do not treat numerical font rankings as typeface identification or approval.

## Next implementation work

### Full-screen composition follow-up

`compose_loading_title_preview.py` now generates both complete 1280x960 Joyride
images as offline authoring previews. **Only the title is reconstructed.** The
paragraph, map, labels, legend and footer are enlarged original source content,
not claimed as quality upgrades. No runtime manifest is generated or staged.

Background samples behind the erased title come from the ten original overview
images at the same coordinates, with exact source identities recorded per pixel.
Eleven source positions have no eligible blue-background observation and use
explicitly recorded nearest-sample placeholders. These remain authoring
uncertainties, not recovered original background detail.

Inspected first-version Loading and Continue individually, in that order.
The color-only erase mask damaged the resort map edge below the title. This
first composition is rejected and superseded; do not stage it.

Restricted the erase mask to the original title's cap rows and J descender,
and retained original full-frame pixels outside the erase/draw support to avoid
crop-resizing seams. Generated V2, then individually inspected Loading and
Continue again in that order. The previously clipped map edge is restored.
The title is now reviewable in context, but its fidelity and background recovery
are not yet accepted. The paragraph still shows the original low-resolution
lettering and the map/legend remain visibly coarse.

Current previews: `menu-loading-joyride-composition-study-v2/OVERV3L-title-preview.png`
and `OVERV3-title-preview.png`. Each is 1280x960. A pixel comparison found no
changes outside the title study rectangle relative to the corresponding enlarged
original. The Loading/Continue difference is retained exactly relative to those
enlarged originals. These checks establish scope/variant preservation only,
not visual quality or native behavior. No new native test was run.

Additional serif-font diagnostics (`menu-loading-outline-identification-serif`)
also failed to establish the paragraph's original typeface. None is used in the
previews. Do not use their numerical scores as authority to replace the font.

### Map-line source reconstruction follow-up

`trace_loading_map.py` separates neutral bright linework in the original Joyride
map and fits splines to the source-derived contours. It explicitly protects the
title, six map labels, start/finish/arrow, checkpoints and grapples. Contours are
saved as editable source-coordinate SVG geometry. No external art or font is
introduced. The terrain fill and protected content remain original-resolution
artwork, so this is still not a complete loading upscale.

Three revisions were generated. In each revision, the complete Loading image
was individually inspected first, followed by the complete Continue image:

- `menu-loading-joyride-map-study`: rejected. Small terrain speckles became false
  white strokes and linework was too heavy. The preliminary curve fit also merged
  two raster components. Do not stage this revision.
- `menu-loading-joyride-map-study-v2`: retained 18 longer contours and constrained
  local color contributions. It removes the speckle reconstruction, but the
  blockwise color correction and abrupt protected-region boundaries are visible.
  Superseded; do not stage.
- `menu-loading-joyride-map-study-v3`: continuous color correction and a tapered
  transition into protected regions remove those hard joins and block artifacts.
  Road/coast boundaries are smoother in the full-screen study. This is a
  **provisional authoring result**, not acceptance of all map artwork or the page.
  The paragraph, map labels, terrain texture, markers, legend and footer remain
  visibly coarse and are not reconstructed by this operation.

V3 scope checks are saved in `scope-check.json`. All 14 protected rectangles are
pixel-identical to the input title-composition preview, in both variants. The
Loading/Continue delta is unchanged. Edits stay within output x=[100,840),
y=[228,796); paragraph and footer are outside this area. Both raw and fitted
selected masks have 18 components and no holes; this aggregate count does not
prove every original map connection. The largest measured contour deviation
from the source-derived polygon is 0.327 source pixels. Local RGB block-mean
error relative to the input preview averages 0.238 on affected blocks, with
99th percentile 2.875 and maximum 5.188. These are scope/color constraints,
not substitutes for visual review or acceptance.

The initial implementation encountered coincident and zero-area contour vertices
at threshold crossings. Consecutive duplicates are removed before spline fitting;
short/degenerate contours stay as original source content in the final composition.
All authoring commands are terminal. No runtime assets were staged or native
test started, because the complete loading reconstruction is still unfinished.

### Remaining implementation work

- Validate and refine title shapes against both original sources; extend the
  source-guided approach to the other nine names once the method is visually sound.
- Reconstruct the Joyride paragraph's letter shapes using the actual source,
  preserving character spacing, line breaks, counters and stroke connections.
- Reconstruct map linework separately, preserving all original connections,
  labels, markers, colors and placement.
- Composite against the original background with explicit source evidence for
  covered pixels. Do not insert the diagnostic flat background into the game.
- Build the complete Joyride Loading/Continue pair before native review. Then
  cover the other tracks and transitions; do not declare the family complete
  from a title crop or one track.

No runtime code, approved build, Build 11, release package, commit or push changed.
The new scripts were executed to generate the inspected artifacts. No native run
was made because there is no full-screen candidate ready for that test yet.

# Overview reconstruction reassessment, 2026-09-25

The all-menu quality goal remains unfinished. No replacement loading artwork
passed this review. Original fallback is a regression rollback, not an upscale.

## Review and withdrawal

Individually inspected all 20 authored overview images: OVERV0 through OVERV9,
each Continue and L/Loading variant. Compared all ten Continue images against
their original extracted TIM images; also inspected both original Joyride variants.
The remaining nine original L images were not separately viewed in this pass.
This is an offline asset review, not native navigation coverage for ten tracks.

| Track / source | Findings in both authored variants |
| --- | --- |
| Cypress Run / SWAMP1/OVERV0 | Irregular paragraph letter shapes; smeared small legend; inconsistent map contour widths. |
| Blackwater Falls / SWAMP2/OVERV1 | Small e/g shapes distorted; thin map labels and construction lines have uneven widths. |
| Suicide Swamp / SWAMP3/OVERV2 | Paragraph strokes irregular; small legend loses definition; line intersections thicken. |
| Joyride / ISLAND1/OVERV3 | Prior user rejection upheld: paragraph, legend and thin map linework distorted. |
| Cliffdiver / ISLAND2/OVERV4 | Wording remains recognizable but glyph shapes fluctuate; small diagram labels wobble. |
| Hammerhead / ISLAND3/OVERV5 | Small text has false serif-like shapes and irregular strokes; bridge/label details distort. |
| Willpower / ALPINE1/OVERV6 | Paragraph shape distortion; thin jump annotations and construction lines vary in weight. |
| Ice Crusher / ALPINE2/OVERV7 | Paragraph glyph damage; dense track-segment outlines become uneven. |
| Snow Blind / ALPINE3/OVERV8 | Paragraph and footer glyph distortion; thin map annotations change weight. |
| Nightmare / DARK/OVERV9 | Small paragraph and legend distorted despite simpler map silhouette. |

Moved the remaining 18 staged PNGs and 18 manifests from isolated Menu4x into
`rejected-overview-loading-staged`, preserving relative paths and checking SHA256
before/after each move. `inventory.json` records all 36 files. The earlier four
Joyride files remain separately preserved in `rejected-joyride-loading-staged`.
Authoring originals remain untouched; do not copy these candidates back.

Catalog now has 32 PNGs and native startup confirms 32 original sources. No
overview replacements remain staged. The general background authoring command
now refuses OVERV TIMs before inference to prevent repeating this failed method.

## Two rejected experiments

`TextureTools/constrain_menu_overview.py` applies iterative local source-sample
constraints to a verified SR prior, with a two-level quantization tolerance.
Joyride output: `menu-overview-constrained-experiment/ISLAND1/OVERV3.png`.
Mean absolute channel error after 4x area reduction fell from 4.857 to 1.358;
99th percentile fell from 36.813 to 2.125. **Visual inspection rejects it:**
warped letters persist and source correction adds halos/embossed edges. These
numbers demonstrate why round-trip fidelity cannot stand in for visual quality.

`TextureTools/experiment_overview_lettering.py` separates original near-white
paragraph coverage from the blue background and reconstructs that mask alone.
Output: `menu-overview-mask-experiment/paragraph.png`. Individually inspected:
malformed e/g shapes and inconsistent strokes remain. Also rejected. No font
substitution or regenerated text was used. Neither experiment was staged or
claimed as native-verified. Scripts are retained as explicitly rejected experiments.

The next approach must address glyph shape/topology and fine diagram geometry,
not simply denoise the same full image, change model strength, or pass a pixel
error threshold. No successful replacement method has been established yet.

## Native fallback verification

`menu-overview-withdrawal-native`: muted, headless, isolated output, disposable
cards, expected game exit 3 / wrapper exit 0. Individually inspected all six
captures in order:

- 1801: Full Season selected on race-type screen, during transition.
- 1920, 2041, 2161, 2280, 2401: original Joyride Continue artwork, complete 4:3
  layout, paragraph, map and footer visible; original low resolution remains.

Trace loads original OVERV3L at frame 1805 and OVERV3 at 1834. These six captures
missed the brief Loading state; trace alone does not prove its visual content.
`menu-overview-original-transition-native` completed with expected exit 3 /
wrapper exit 0. All five captures were inspected individually in order: 1810,
1817 and 1827 show original Loading; 1832 and 1840 show original Continue.
The full map, paragraph, legend and 4:3 layout remain visible across these states.
This verifies the sampled fallback transition; it is neither an every-frame
animation audit nor acceptance of an upscale. Audio was muted and unverified.

Python syntax compilation passed for the three authoring scripts. A negative
command-line check confirmed the general background tool rejects original
Joyride OVERV3 before inference and creates no output. `git diff --check` passed
(existing line-ending warnings only). No new runtime code was changed, so no
runtime rebuild or broad test-suite rerun was needed.

## Previously pending season-quit captures

Individually inspected `menu-season-quit-native` in order:

- 2881: paused Joyride with Resume selected, Quit below it; pause text is coarse.
- 3120: title screen, 1 Player selected.
- 3360: same title screen.

The Down/Cross quit route returns to title and does not reach Save Game.
Save Game remains unverified; do not repeat this route expecting its dialog.

Approved `.build/audit-bin/Release/net10.0/win-x64/JetMoto.dll` still hashes to
`D2C09E1AF287900921AD27FEDE9281FB1CFF724C0044783D577F0D4DD66F3333`.
No runtime build, release packaging, commit or push was performed in this pass.

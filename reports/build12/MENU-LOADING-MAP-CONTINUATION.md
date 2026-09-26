# Loading map continuation, 2026-09-25

Loading-only scope. Two additional paired candidates now exist: Cypress Run
and Blackwater Falls. These are partial map improvements, not finished HD pages.
Original titles, paragraphs, labels, markers, terrain, legend and prompts remain
source-resolution content enlarged for context. No substitute typeface or neural
lettering is introduced.

## Artifacts and authoring

- Cypress: `menu-loading-cypress-map-v2/OVERV0L-map-study.png` and
  `OVERV0-map-study.png`; 20 fitted contours, 63,296 changed output pixels per page.
- Blackwater: `menu-loading-blackwater-map-v1/OVERV1L-map-study.png` and
  `OVERV1-map-study.png`; 26 fitted contours, 44,140 changed output pixels per page.
- Runtime copies: `menu-loading-runtime-candidate/SWAMP1` and `SWAMP2`, also
  staged in the isolated `.build/ui-buoy-bin` Menu4x catalog. Catalog count: 47.

Both final Loading and Continue pages were individually inspected for each track.
Cypress V1 had excessively bright, uneven borders and is superseded, unstaged.
V2 uses a subpixel coverage inset and 70% blending with the original. The same
settings are used for Blackwater. This improves selected edge smoothness while
retaining the original colored artwork and protected labels and markers. Fine
terrain and small text remain visibly coarse; these are not final-quality screens.

The reusable `trace_loading_map.py` accepts explicit reviewed per-track profiles.
Its historical Joyride default is retained. Profiles specify bounds and protected
rectangles; the renderer fails if edits escape them. `package_loading_map.py`
checks original hashes, dimensions, protected pixels, edit bounds and exact signed
Loading/Continue pixel differences. It preflights both files before copying and
refuses to overwrite a different staged candidate. Both pairs passed.

The inset mask's thresholded component count is larger than the original contour
mask's count. This is not proof that all source connections were reconstructed:
the final composition includes the original underneath. Final screen inspection,
not this mask count, determines whether the composited borders remain readable.

Reproduction from the repository root, using the authoring Python environment:

```powershell
.build/menu-authoring-env/Scripts/python.exe TextureTools/trace_loading_map.py --profile TextureTools/loading-cypress-map.json --output reports/build12/NEW-cypress
.build/menu-authoring-env/Scripts/python.exe TextureTools/trace_loading_map.py --profile TextureTools/loading-blackwater-map.json --output reports/build12/NEW-blackwater
.build/menu-authoring-env/Scripts/python.exe TextureTools/package_loading_map.py --profile TextureTools/loading-cypress-map.json --candidate reports/build12/NEW-cypress --stage
.build/menu-authoring-env/Scripts/python.exe TextureTools/package_loading_map.py --profile TextureTools/loading-blackwater-map.json --candidate reports/build12/NEW-blackwater --stage
```

Use new output directories. Omit `--stage` to package reports only.

## Native evidence

The historical `menu-loading-cypress-native` log actually loaded SWAMP2,
Blackwater Falls; it is not Cypress coverage. Cypress remains offline-inspected
and staged but unverified in-game. The known initial track-selection replay cycles
Joyride, Blackwater and Suicide Swamp.

The first new Blackwater test (`menu-loading-blackwater-map-native`, 44 seconds)
ended at vblank 2372, before the replay's selection at 2400, and saved no captures.
It provides catalog-loading evidence only, not loading-screen visual evidence.

The second run (`menu-loading-blackwater-map-native-v2`, 55 seconds) completed
with game exit 3 / wrapper 0. All eight saved captures were individually inspected
in chronological order: 2409 track selection; 2421 and 2436 Loading; 2449, 2464,
2480, 2496 and 2512 Continue. The log records owned candidate submission for
SWAMP2/OVERV1L and SWAMP2/OVERV1. The map, paragraph, labels, markers, legend and
correct variant footer remain present, with no clipping observed. Dark blueprint
tones show native quantization; fine text and terrain remain soft. This verifies
these saved Loading/Continue presentations, not every intermediate animation frame,
race entry or audio. No host input, UI automation or screen capture was used.

The optional original-only authoring path initially exposed a Python variable
shadowing error and was corrected (`source_path` is now distinct from contour
paths). A fresh Blackwater reproduction checks PNG hashes against the inspected
and staged artwork. The newer manifest distinguishes un-inset spline topology
from inset-mask topology; earlier manifests used `splineTopology` for the latter.

## Remaining work

Finish all non-map artwork on these pages and Joyride. Establish Cypress's actual
native route and inspect both states. Seven other track pairs have no new candidate.
The full ten-track upgrade remains open. Audio tests are muted.

Approved audit executable SHA256 was rechecked unchanged:
`D2C09E1AF287900921AD27FEDE9281FB1CFF724C0044783D577F0D4DD66F3333`.
No runtime source, approved build, Build 11, release package, commit or push changed.

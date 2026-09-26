# Loading-screen continuation, 2026-09-25

Scope derives from HANDOFF-LOADSCREENS.md and directly inspected artifacts.
No prior task histories were consulted. No desktop control or capture was used.

## New complete paired candidates

Inspected original SWAMP3/OVERV2 and ISLAND2/OVERV4 separately. Authored distinct
map bounds and protected rectangles for each track; no rectangles were copied
from another track. Both profiles use subpixel inset 0.6 and strength 0.7.

| Track | Profile | Candidate | Changed pixels per variant |
| --- | --- | --- | --- |
| Suicide Swamp | TextureTools/loading-suicide-swamp-map.json | menu-loading-suicide-swamp-map-v1 | 31,796 |
| Cliffdiver | TextureTools/loading-cliffdiver-map.json | menu-loading-cliffdiver-map-v1 | 33,692 |

Candidate paths are relative to this report's directory. Each contains full
1280x960 Loading and Continue pages, editable map SVG, authoring manifest and
scope-check.json. Both complete pages were individually inspected, Loading first,
then Continue. Selected pale borders are smoother; source layout, route connections,
bridge connections, labels, colored markers, paragraphs, legends and prompts remain
present. Borders adjacent to protected areas retain original softness. Cliffdiver's
dense switchback marker cluster remains original. Neither page is final-quality
accepted: small lettering, terrain, title, legend and prompts remain enlarged source
artwork. This is partial map work, not a whole-page HD completion.

Packaging checked original hashes, dimensions, allowed edit area, protected pixels
and exact signed Loading/Continue differences. Both pairs were staged in
menu-loading-runtime-candidate and the isolated ui-buoy-bin Menu4x catalog.
No pre-existing candidate was replaced. Authoring manifests record generation-time
status; scope-check.json and this report record subsequent staging and inspection.

## Suicide Swamp native evidence

Run: menu-loading-suicide-swamp-map-native-v1, 55 seconds, using
menu-loading-right-2-replay.json. Game exit 3; wrapper exit 0.
Catalog count 49 includes unrelated menu assets and is not track coverage.
The log confirms original-owned submissions for SWAMP3/OVERV2L.TIM and
SWAMP3/OVERV2.TIM. All eight saved captures were individually inspected in order:

1. frame-002409.png: Loading.
2. frame-002419.png: Loading.
3. frame-002433.png: Continue.
4. frame-002448.png: Continue.
5. frame-002465.png: Continue.
6. frame-002480.png: Continue.
7. frame-002496.png: Continue.
8. frame-002512.png: Continue.

Map, labels, markers, bridge, paragraph, legend and appropriate footer are visible,
with no observed clipping. Native dark blueprint tones show quantization and text
remains soft. This verifies the saved Loading/Continue presentations, not every
intermediate frame, the selection transition, race entry or audio. Audio was muted.

## Cypress route investigation

The first attempt, menu-loading-cypress-beginner-route-v1, changed difficulty left
once in Options, exited with Start, then moved left three times on the main menu.
It entered two-player selection. Inspected frame-003200.png, showing PLAYER 2;
the run log contains no track overview load. Other saved captures were not reviewed.
This run provides no Cypress loading evidence. The second replay omits the three
main-menu left presses.

## Preservation and remaining work

Approved audit JetMoto.dll SHA256 rechecked unchanged:
D2C09E1AF287900921AD27FEDE9281FB1CFF724C0044783D577F0D4DD66F3333.
No runtime code, approved playable output, Build 11, release package, commit or
push changed. Existing dirty source changes were preserved.

Whole-page reconstruction remains incomplete on all ten tracks. Five tracks now
have partial paired candidates: Joyride, Cypress Run, Blackwater Falls, Suicide
Swamp and Cliffdiver. Five still need candidates. Cliffdiver native coverage and
the Cypress route remain to be established at this point in the report.

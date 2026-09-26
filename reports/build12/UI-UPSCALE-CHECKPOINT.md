# UI Upscale Work In Progress

Latest: [Joyride runtime candidate](MENU-LOADING-RUNTIME-CANDIDATE.md) packages
the source-guided title/map pair and stages both variants in the isolated build.
Catalog is now 43. Five native Loading captures were individually inspected.
This supersedes older statements that every OVERV replacement is absent.
Full loading quality and the other nine tracks remain pending.

## Current focus: Loading screens only

User follow-up: "This is what you have. Work with it." Continue with the existing
originals. [Source-guided loading trace](MENU-LOADING-SOURCE-TRACE.md) records the
new larger original track-name atlas and an editable Joyride title study.
The title study is not a full-screen candidate or native acceptance.

Latest user direction limits current work to loading screens. See
[loading source continuation](MENU-LOADING-SOURCE-CONTINUATION.md).
All ten original Loading images are individually inspected; all 20 originals
match a fresh disc decode exactly. Loading/Continue pairs provide no additional
map or paragraph samples for reconstruction. No replacement is accepted or staged.

## Latest continuation: Credits and scoring

See [credits/scoring evidence](MENU-CREDITS-SCORING-CONTINUATION.md). Eight credits
pages and scoring background authored; credits forward/reverse/boundary/exit
states and three scoring selections individually inspected in native captures.
Tiny credits TM distortion found and repaired with original-sample fallback;
revised pages 0/3/5 were also individually inspected in native captures. DEVELOP
was rejected for added embossed shading and withdrawn after native inspection.
Isolated catalog now 41; all OVERV candidates remain withdrawn. Six contour
experiments also failed visual review. No loading upgrade or all-menu completion.

## Continuation: Overview Family Withdrawn

All 20 authored overview variants were individually reviewed offline. The same
small-lettering failure extends beyond Joyride. Remaining 18 PNGs and manifests
were withdrawn from the isolated catalog with verified hashes; all originals
and rejected authoring artifacts are preserved. Native startup now confirms 32
candidate sources, superseding the earlier 50/52 counts. Both new reconstruction
experiments failed visual inspection and remain unstaged. No loading upscale
has passed. See [detailed reassessment](MENU-OVERVIEW-REASSESSMENT.md).

The three previously unviewed season-quit frames are now inspected: 2881 shows
pause, 3120 and 3360 show title. Quit does not reach Save Game. Six new native
fallback captures were individually inspected; Joyride Continue is restored to
original artwork. Other menu families and Save Game remain pending.

Additional narrow native transition run `menu-overview-original-transition-native`
was fully inspected across its five captured images: 1810/1817/1827 Loading,
1832/1840 Continue. Original map, paragraph, legend and 4:3 layout remain intact.
These are sampled fallback checks, not upscale acceptance or an every-frame audit.
Both runs used muted headless rendering and disposable cards and finished with
expected game exit 3 / wrapper exit 0. Authoring syntax and rejection-guard
checks passed; no runtime rebuild was performed.

## User Rejection: Joyride Loading Artwork

The user rejected the Joyride loading/continue screen shown during the season
navigation test: "That loading screen does not pass. It looks awful".
This supersedes any earlier positive quality assessment of those candidates.
The neural reconstruction distorts the small lettering and map linework; larger
dimensions and successful integration do not satisfy the quality requirement.

Removed both ISLAND1/OVERV3 and OVERV3L candidate PNGs and manifests from the
isolated runtime catalog. Preserved them under rejected-joyride-loading-staged
for comparison; original authoring files and provenance remain untouched.
The next launch falls back to original Joyride artwork pending a replacement.
This fallback is not the requested completed upscale. Do not restage these files.
Reassess the same reconstruction method on every other track overview before
accepting any of them; none of the loading-screen family is complete.

The season-back experiment produced five individually inspected captures, all
showing Joyride Continue; Triangle did not leave that screen. The subsequent
season-quit test completed, but its captures have not been inspected and provide
no accepted Save Game or menu quality evidence. Testing redirected to this
quality rejection before continuing Save Game work.

Scope: menu/HUD lettering, all menu items, and yellow/red buoy edges. Water and
effects remain closed by user acceptance. Build 11 and `.build/audit-bin` are unchanged.

## Current Priority And Style Constraint

On 2026-09-25 the user approved the dial ("Dial looks great") and made ALL menus
the next priority. The expanded per-screen checklist is in `TODO.md`. This is a
quality-only upscale of the original art, not a redesign or new visual style.
Retain the existing typography, colors, composition, layout and animations.
HUD/buoys remain tracked separately; they must not displace the all-menu priority.

Original disc inventory confirms three distinct asset paths requiring coverage:
- TMS/DMD menu artwork in STARTUP, NAVIGATE, PICKTRAC, MISC, STANDING and PRIZES.
- Standalone TIM portraits `NAVIGATE/RIDER00.TIM` through `RIDER19.TIM`, award
  images `PRIZES/PRIZE0.TIM` through `PRIZE8.TIM`, and `MISC/JMICON.TIM`.
- BS backgrounds for title, rider/race/scoring, options/sound/load/save,
  `MISC/CONTROL0.BS` through `CONTROL4.BS`, `PICKTRAC/TRACKS0.BS` through
  `TRACKS4.BS`, and track/overall standings and victory variants.

These are source-file inventory findings, not evidence that their replacements
are implemented or visually accepted. Menu artwork beyond the dial remains open.

## Dedicated Menu Sampling Pass

Implemented categorical-alpha-aware filtering for an explicit allowlist of
dedicated TMS menu banks: title, race/scoring, options/sound/load/save, track
selection, and standings. The existing 4x artwork is unchanged. Mixed rider and
prize model banks are excluded pending per-material review. The approved dial's
UV correction is unchanged; it is not blindly applied to other menu tiles.

`JETMOTO_MENU_FILTER=0` disables only this new menu-bank filtering for controlled
comparison. It does not disable the approved dial repair or alter game assets.

Build succeeded with zero errors. Native asset tests: 135 passed, zero failed,
including menu-bank allowlisting and rejection of gameplay/mixed-model banks.
Native muted captures inspected individually:
- `menus-sampling-native/frame-001000.png`: original title artwork, labels,
  selection color and composition retained; background remains low resolution.
- `menus-sampling-native/frame-002000.png`: original track-selection layout and
  approved dial remain intact. Baked background text remains rough.
- `menu-options-native/frame-001600.png`: correct options menu, highlighted
  difficulty and disabled-row distinction retained; coarse lettering and stray
  dark row fragments still need source/atlas investigation.

These are initial sampling changes and coverage checks, not completed all-menu
upscales. Dedicated backgrounds, portraits and original-alpha edge cleanup are
still outstanding; no menu family has been removed from the TODO.

Controlled options comparison: `menu-options-unfiltered-native/frame-001600.png`
was also inspected individually with `JETMOTO_MENU_FILTER=0`. The same dark row
fragments remain with the new filtering disabled. They are pre-existing, not a
regression introduced by this pass. Their exact source/atlas cause is still
unverified; this comparison does not mark options artwork as finished.

Completed/closed entries were removed from `TODO.md` at the user's request.
Their approval history remains here and in `VALIDATION.md`; the TODO contains
pending work only.

## Track Dial

The original `PICKTRAC/TRACKS0.TMS` dial assets are drawn as rotated 32-pixel
tiles with inclusive 31-pixel UV spans. With high-resolution replacements this
compresses the sampled span and causes repeated discontinuities in bevels/arcs.
The correction is limited to ten verified bank/ordinal/ID entries. It expands
inclusive endpoints to contiguous tile boundaries only for successfully loaded
replacement materials. Missing replacements retain original rendering.

Categorical-alpha-aware filtering smooths edges without interpreting PS1 STP as
ordinary half-opacity. Water/effect materials cannot enable this UI path.

Native muted headless evidence, individually inspected:
- Before: `ui-dial-source/frame-002000.png` (2560x1920, 4:3).
- Rejected asset rebuild: `ui-dial-clean-native/frame-002000.png`. Softer artwork
  and visible shading bands; removed from the isolated output's overrides.
- Corrected sampling, unchanged neural artwork:
  `ui-dial-contiguous-native/frame-002000.png`. The repeated bevel jumps are
  visibly removed; the circular rim, previews and menu proportions remain intact.

Initial attribution solely to neural warping was incomplete. The controlled
comparison establishes the tile-sampling discontinuity as a substantial cause.
The reconstruction script is retained only as a rejected diagnostic experiment.

Rotation evidence: `ui-dial-rotation-native/frame-002060.png` and
`frame-002100.png` were inspected individually. The first shows an intermediate
turn; the second shows Blackwater Falls selected. Arcs/bevels retain clean tile
joins and the menu remains 2560x1920 (4:3). This is two sampled poses, not an
every-frame animation audit or acceptance of all menu artwork.

Release compilation succeeded with two existing nullable warnings, zero errors.
`ui-tile-final-tests.log`: GL45, GL33 and GL21 each 717 passed / 0 failed;
parent 18 passed / 0 failed. The earlier tile-test negative control failed
because its transparent fixture did not expose the UV difference at some
raster scales; a two-axis opaque gradient now tests both correction and
non-UI isolation in normal and rotated orientations.

The approved application/runtime SHA256 values were rechecked and still match
the approved checkpoint. Only `.build/ui-buoy-bin` contains this candidate.

## Still Open

The user rejected the all-menu filtering pass: larger images and smoother
sampling did not establish the requested quality improvement. The title,
track-selection and options captures from that pass are diagnostic baselines,
not accepted upscale proofs. The approved dial correction remains accepted.
All other menu families require source-art quality work and complete-screen
validation, including BS backgrounds and standalone TIM portraits that the
TMS replacement pipeline did not cover. No menu-wide quality completion is claimed.

- HUD font replacement is not integrated. Source load confirmed as
  `ISLAND1/JMFONT.TIM`, caller `800E0918`; glyph emission traced to `800E0618`.
  `build_ui_font.py` is an offline export experiment, not a shipped HUD fix.
- Buoy edge filtering is implemented and synthetic blend checks pass. Native
  `ui-buoy-closeup/race-000980.png` still shows coarse silhouette steps in the
  enlarged yellow buoy. Further source-alpha cleanup and red-buoy inspection
  are required before closing the buoy item.
- Menu text, backgrounds and remaining menu artwork are not universally upgraded.

No release package or new commit is claimed by this checkpoint.

## Source-Quality Restart, 2026-09-25

- Added offline `TextureTools/MenuAssetExport`, referencing the isolated candidate
  assemblies without changing the approved game output. It uses original VLC
  function 800E2694, original quantization/scale tables, and the runtime MDEC.
  It does not initialize the game window, audio, or input.
- Extracted original `STARTUP/TITLE.BS` and `MISC/OPTIONS.BS`, each 640x480.
  Both PNGs individually inspected: correctly arranged original artwork and
  lettering. An attempted 320x240 extraction correctly failed the output-size
  check without creating a PNG. These are sources, not upscale proofs.
- Source PNGs and provenance: `menu-original-assets/STARTUP/TITLE.png` and
  `menu-original-assets/MISC/OPTIONS.png`, each with a `.png.json` manifest.
- Added source-manifest and model-checksum-validated neural background authoring.
  Authoring dependencies are isolated in `.build/menu-authoring-env`.
  Both compact Real-ESRGAN weight files match the existing project's recorded hashes.
- First title candidate: `menu-quality-candidates/STARTUP/TITLE.png`, 2560x1920.
  Individually inspected: cleaner logo/illustration contours, original composition
  retained. Small text and compression artifacts still need scrutiny. This is
  NOT native game output, not deployed, and not accepted as a finished menu.
- Tiled vs single-patch inference check on a seeded 73x67 RGB fixture: maximum
  difference 0.0 in 8-bit units. This tests tile implementation only, not art quality.
- Approved application/runtime hashes rechecked unchanged against the recorded
  approved checkpoint. No water/effects modifications.

Next implementation boundary: preserve loaded BS filename provenance through
the original decode/upload and menu draw/copy paths so the enhanced background
is bound by source ownership, never by searching VRAM or recognizing screen pixels.
Native integration, all-menu source coverage, and complete visual regression
remain open; the persistent all-menu goal is active.

## First Native Background Integration

Added an opt-in `JETMOTO_MENU_BACKGROUNDS=1` candidate path. Original BS files
and authored PNGs are verified against their provenance manifests. Loaded RAM
bytes are revalidated at the original background-decoder boundary. The candidate
is submitted immediately after that original source draw, before subsequent
menu controls; no VRAM matching or screen-wide recognition is used.

`menu-source-trace-options/run.log` confirms original filename, load address,
dimensions and decoder caller for startup/title/options. The first candidate
catalog contains only TITLE.BS; all other backgrounds retain the original.

Native muted/headless run: `menu-background-first-native`, 36 seconds.
All four captures inspected individually:
- `frame-001000.png`: enhanced title artwork, 1 Player highlighted.
- `frame-001201.png`: enhanced title persists, Options highlighted.
- `frame-001400.png` and `frame-001600.png`: original options screen reached;
  no stale title artwork. Coarse options text and row fragments remain unresolved.

The title illustration and logo show the reconstructed contours in actual game
output. This demonstrates integration and two selection states, NOT all-menu
completion or final acceptance of small lettering. Transition endpoints were
checked; intervening transition frames were not exhaustively inspected.

Candidate compilation: zero errors/warnings. Native asset tests: 135 passed,
zero failed (`user-reported-effects-flicker/menu-background-native-tests.log`).
No approved binaries, Build 11 release, or water/effects changed.

User clarification: track loading screens are explicitly in scope, including
every track/variant and original artwork/text/indicators. Added a separate pending
TODO gate; track selection proofs do not count as loading-screen validation.

## Options Lettering And Loading Sources

Options text used enhanced RGB under a nearest-repeated original alpha mask.
`enhance_menu_lettering.py` now reconstructs foreground and occupancy contours
from the original masks using verified SRVGG weights, retaining exact original
two-color palettes and matching geometry between normal/highlight variants.
It does not substitute a font. Candidate 2 includes 28 source-authored UV regions
per palette, derived from OPTIONS.DMD and bound only to verified OPTIONS IDs.

The original atlas has shared rows: for example, row 216 contains shadow from
the preceding label while the Keypad letter pixels begin at 217. The original
Keypad polygon includes row 216. Neural reconstruction alone did not remove the
visible stray row. Isolated per-label crops clear only leading foreign-shadow
rows without modifying the shared atlas or moving the lettering geometry.

Evidence, individually inspected:
- `menu-options-contours-native/frame-001600.png`: neural options background and
  reconstructed atlas, but row fragments still present. Diagnostic, not accepted.
- `menu-options-uv-capture/frame-001600.geometry.json`: actual drawn original
  material UV bounds, including Keypad (0,216)-(126,228).
- `menu-options-isolated-labels-native/frame-001601.png`: isolated source labels;
  stray rows removed, normal/highlight/disabled text and background readable.
- `menu-options-values-native/frame-001600.png`: Professional highlighted.
- `menu-options-values-native/frame-002000.png`: Turbo Off highlighted.
  Both changed-state captures retain clean rows and original placement/colors.

The isolated candidate is updated, not the approved playable output. Options
submenus and all unvisited values still need their own visual checks; this is
not blanket options/all-menu completion. Native tests now 141 passed, zero
failed, including crop ownership, wrong-bank/ID isolation and missing-crop
fallback. Latest candidate build: zero errors/warnings. Approved app/runtime
hashes remain unchanged. `Run-Validation.ps1 -MenuBackgrounds` now explicitly
records/enables background candidates so future tests do not rely on inherited
environment state.

`export_menu_tim.py` extracted 50 standalone source assets into `menu-original-tim`:
20 rider portrait/biography panels, 20 overview/loading variants across ten tracks,
nine prize images and the memory-card icon. No upscale or runtime coverage is
claimed for these extractions. Individually inspected RIDER00 and ISLAND1/OVERV3L:
the latter includes the Joyride map, description and Loading text. Runtime
loading transitions and every other variant remain pending.

## First Native Track Loading Upscale

2026-09-25: source upload tracing established that Joyride uses
`ISLAND1/OVERV3L.TIM` during loading and `ISLAND1/OVERV3.TIM` for the continue
screen. Both are original direct-color 320x240 images, uploaded from source
offset 20 by caller `8014D368` to (0,0). The measured loading interval in
`menu-loading-first-native/run.log` is VBlank 2104-2125. Source tracing now logs
VBlank numbers and source-owned uploads at the original LoadImage callers.

`MenuBackgrounds` now supports this verified track-image upload path. Optional
candidates still require original-disc and authored-PNG checksums, exact loaded
RAM ownership, original TIM dimensions/format, the specific upload caller and
destination. Only the ten original track overview names and their L variants
are eligible; unrelated TIMs cannot use this path. The image draw follows the
original upload, with no display-wide pixel recognition or persistent overlay.

Joyride's two original extractions and reconstructed candidates were inspected
individually. The learned reconstruction improves map outlines, title contours
and lettering without changing the original map, text, colors or layout. Small
letter shapes still inherit limitations of the original low-resolution artwork;
this is not a claim of newly recovered source detail or all-menu acceptance.
Candidates live only under the isolated ui-buoy-bin output's Menu4x/ISLAND1.

Native muted/headless evidence, every listed image inspected individually:
- `menu-loading-original-native/frame-002200.png`: original continue screen,
  candidate feature disabled, same candidate executable and replay. Visible
  coarse lettering and map edges provide the same-output-size comparison.
- `menu-loading-first-native/frame-002201.png` and `frame-002401.png`: reconstructed
  continue screen, full 4:3 composition and original confirmation artwork.
- `menu-loading-first-native/frame-002601.png`, `frame-002800.png`, and
  `frame-003000.png`: native gameplay after confirmation; no stale loading image,
  Hor+ gameplay framing retained. These are transition-endpoint checks, not
  exhaustive gameplay or lighting/water validation.
- `menu-loading-short-state-native/frame-002109.png`, `frame-002116.png`, and
  `frame-002120.png`: actual reconstructed Loading state.
- `menu-loading-short-state-native/frame-002132.png`: matching continue state.
  The short-state run inspected four captured frames, not every intervening frame.

Latest candidate build: zero errors/warnings. Native source tests: 150 passed,
zero failed (`user-reported-effects-flicker/menu-loading-source-tests.log`),
including wrong track/source pairing, unrelated portrait, truncated data,
paletted format, invalid block size and incorrect geometry rejection.
Generated hooks: 48 verified. Approved playable app/runtime SHA256 values were
rechecked and remain unchanged. No Build 11 or lighting/water/effects changes.

Remaining: the other nine tracks' artwork and actual native loading paths,
broader menu family coverage, additional backends/transitions, and cumulative
regression testing. The all-menu goal remains active; no menu family is closed
based solely on this Joyride checkpoint. Nothing packaged or pushed.

## All Loading Artwork Candidate Pass

2026-09-25 continuation: reconstructed the other eighteen original track
overview/loading images using the existing verified neural authoring pipeline.
Both original and candidate images were individually inspected for every track:
Cypress Run, Blackwater Falls, Suicide Swamp, Joyride, Cliffdiver, Hammerhead,
Willpower, Ice Crusher, Snow Blind and Nightmare. All twenty PNGs and their
checksum manifests are staged only in the isolated ui-buoy-bin Menu4x output.
The runtime reports 22 eligible original sources: these twenty plus the existing
title/options BS candidates. This is asset availability, not native coverage.

Observed offline: route silhouettes, annotation placement, descriptions, title
spelling, colors and loading/continue prompt differences remain consistent with
the originals. Map outlines and title edges are visibly reconstructed, not just
enlarged. Small paragraph/legend strokes are rounded by the neural reconstruction
and still need a stricter fidelity pass before final quality acceptance.

New native muted/headless coverage, all six captures individually inspected:
- `menu-loading-cypress-native/frame-002109.png`: Blackwater Falls Loading.
- `menu-loading-cypress-native/frame-002160.png`: Blackwater Falls continue.
  The directory was named before tracing; its name is misleading. The actual
  original source is SWAMP2/OVERV1(L).TIM, NOT Cypress Run. The existing swamp
  replay selects Blackwater Falls; source log verifies uploads at 2106 and 2119.
- `menu-loading-right-2-native/frame-002409.png`: Suicide Swamp Loading.
- `menu-loading-right-2-native/frame-002430.png`: Suicide Swamp continue.
  Verified source SWAMP3/OVERV2(L).TIM, uploads at 2404 and 2418.
- `menu-loading-right-3-native/frame-002409.png`: Joyride Loading.
- `menu-loading-right-3-native/frame-002431.png`: Joyride continue.
  Verified source ISLAND1/OVERV3(L).TIM, uploads at 2404 and 2427.

Full-screen framing, maps, descriptions and changing footer prompts were checked;
no clipping, misplaced image or blank background was observed in these captures.
These tests stop at the continue screen and do not extend gameplay coverage.
The right-3 replay wraps to Joyride: the current test profile only exposes three
selectable tracks. Do not infer coverage of other tracks from repeated dial
inputs. Remaining seven native loading flows need verified unlocked test state
or original progression, without altering user saves or approved playable output.

Native loading-screen coverage is now 3/10 tracks, both variants for each. All
ten tracks have inspected offline candidates, but loading-family completion is
still open, as are all other menu-family and cumulative release gates. No renderer
code changed in this pass; it uses the previously built/tested candidate and its
150-pass source-test checkpoint. No new broad regression claim is made.

## Rider Panels And Race Background Checkpoint

Candidate-only work, 2026-09-25. Lighting and water/effects remain closed.
Authored and individually inspected original/candidate pairs for
NAVIGATE/PICKRIDE.BS and RACETYPE.BS. Installed only in the isolated ui-buoy
output. Native background captures inspected: menu-rider-race-background-native
frames 1351/1400 (Dakota) and menu-race-background-native frames 1600/1650
(Single Race selected). Race selection variants remain unverified.

Rider portrait/biography TIMs use the original asynchronous read path, bypassing
the normal file-load scope. Request identity now comes from func_8013D320 A1;
replacement requires the verified LoadImage caller, original workspace,
destination (39,69), dimensions 576x192, exact source bytes and manifest hashes.
The original upload runs first; the replacement covers only its original panel.
No screen recognition, VRAM matching or full-screen portrait overlay is used.
Generated patch count is 50; isolated build succeeded with zero warnings/errors.

Authored RIDER17 (Dakota) and RIDER18 (Wild Ride), preserving original artwork
and biography content. Original and candidate images individually inspected.
Both are staged only in the isolated Menu4x catalog (26 sources total).
Native asset tests: 159 passed, zero failed, including nine rider identity/header
checks. Log: user-reported-effects-flicker/menu-rider-panel-tests.log.

Muted headless native evidence, each listed capture individually inspected:
- menu-rider-panel-native/frame-001351.png and frame-001401.png: upgraded
  Dakota panel, readable original biography, unobstructed changing bike pose.
- menu-rider-switch-native/frame-001420.png, frame-001450.png and
  frame-001500.png: Wild Ride panel and biography replace Dakota; gauges change,
  original team remains selected, and the preview continues changing pose.
  Source trace proves RIDER17 requested at 1215 and RIDER18 at 1381, with both
  corresponding replacement submissions. No stale Dakota panel was observed.

These are reconstructed image details/edges rather than resolution-only output.
Small lettering still warrants fidelity review across the remaining biographies.
Coverage is 2/20 rider panels; remaining portraits, team transitions, rapid
switches, fallback and navigation-back states are not yet validated. No whole
menu family is closed. Approved playable output and Build 11 were not replaced.

## Complete Portrait Candidate Set

All remaining RIDER00..16 and RIDER19 original PNGs and reconstructed candidates
were individually inspected before staging. Together with RIDER17/18, all twenty
panels now have 4x learned-reconstruction candidates in the isolated Menu4x
catalog. Original comic compositions, nameplates, colors, biography text and
line breaks remain intact in this inspection. Some tiny artwork marks and letter
contours are softened; this is not a claim of recovered original high-resolution
master art. No replacement fonts or invented text were used.

First broad replay, menu-all-riders-native, incorrectly assumed downward team
navigation wraps at Mt. Dew. Source tracing proves it only visited RIDER15..19.
Do not count its twenty captures as twenty distinct rider checks. Captures 1561
(Miko), 1680 (Irons), 1801 (Shannara), and 1921 (unchanged Shannara after down)
were inspected individually; upgraded panels are correctly placed, biography
text remains readable, gauges update, and the preview is not covered. Other
captures in that run were not individually inspected. Combined with prior
Dakota/Wild Ride evidence, native distinct-panel coverage is 5/20 at this point.
The replay was corrected to move up through the remaining three teams; its
results must be checked separately rather than inferred from this run.

Corrected run `menu-all-teams-native` completed muted/headless with expected
smoke termination. All fifteen captures were individually inspected, in order:

| Frame | Rider | Original panel |
| --- | --- | --- |
| 1920 | Harris | RIDER11 |
| 2040 | Chien | RIDER12 |
| 2160 | The Max | RIDER13 |
| 2281 | Gunner | RIDER14 |
| 2400 | Quick Jessie | RIDER10 |
| 2520 | Shirow | RIDER05 |
| 2640 | Stone | RIDER06 |
| 2760 | Blackjack | RIDER07 |
| 2880 | Tetsujin | RIDER08 |
| 3000 | Technician | RIDER09 |
| 3121 | Arroyo | RIDER04 |
| 3240 | Bomber | RIDER00 |
| 3360 | Rhino | RIDER01 |
| 3480 | Mace | RIDER02 |
| 3600 | Masala | RIDER03 |

Source request logs agree with each displayed identity. Portrait, biography,
nameplate and original panel boundaries are correct in every listed capture.
Team lamps and preview liveries change to K2, Axiom and Butterfinger respectively;
gauges vary with rider. No stale panel, misplaced background or covered preview
was observed. Native distinct portrait coverage is now 20/20, combining these
fifteen with the five Mt. Dew riders documented above. This verifies settled
selection states, not every intermediate frame. Rapid input, missing-candidate
fallback, return navigation, and remaining foreground artwork still need checks
before closing the rider-menu family. Other menu families remain open.

Approved `.build/audit-bin` application SHA was rechecked and remains
`D2C09E1AF287900921AD27FEDE9281FB1CFF724C0044783D577F0D4DD66F3333`.
No application or renderer code changed in this asset-and-replay pass; the last
159-pass source-test result remains its code checkpoint, not a fresh regression.

## Controller Diagrams And Sound Background

Extracted verified originals MISC/SOUND.BS and CONTROL0..4.BS at 640x480.
Individually inspected all six originals and all six 4x reconstructed candidates
before staging only into the isolated Menu4x/MISC directory. Original typography,
controller shapes, colors, button mappings and pointer destinations are retained.
No diagram labels were retyped or replaced. Small SELECT/START print remains
limited by source detail. Candidate catalog now contains 50 original sources.

Muted headless replay `menu-controller-sound-replay.json` completed normally.
All eight native captures in `menu-controller-sound-native` were individually
inspected, in sequence:
- 2161: Standard, CONTROL0.
- 2401: Big Hands, CONTROL1, swapped shoulder and accelerator/turbo mappings.
- 2640: Crossover, CONTROL2, changed brake/grapple/accelerator mappings.
- 2880: Brake Happy, CONTROL3.
- 3121: Arcade, CONTROL4, reversed lean directions preserved.
- 3361: returned Options, Keypad 2 highlighted at capture time.
- 3600: Sound & Music Settings, Sound Effects selected, all three sliders visible.
- 3840: Music selected and its slider moved left; other sliders remain unchanged.

Logs verify original-owned submissions for all five controller backgrounds,
Options on return, and Sound. No stale diagram remained on return or sound entry.
Controller diagram framing, labels, pointer lines, footer and background are
visibly improved at native output size. Sound heading/background/footer and
slider tracks are improved, but separate live sound labels still have visibly
jagged contours and need their own TMS reconstruction. Do not close Sound.
Controller accept/persistence, Keypad 2 route, wrap/reverse navigation and sound
cancel/exit/overall-volume states remain unverified. Audio was intentionally
muted; this run makes no auditory validation claim. No menu family is closed.
No renderer/application code or approved playable build changed in this pass.

Additional original-disc audit: 262 BS filenames contain 136 unique byte streams.
Most duplicates are per-biome victory images repeated across three tracks, plus
three shared standings groups. Exact source equality may avoid redundant
authoring later, but does not replace native transition/variant checks.

## Sound Lettering Candidate

Extracted all three original MISC/SOUND.TMS records with per-record provenance
using TextureTools/export_menu_tms.py. Individually inspected each original.
Unlike Options, Sound uses full-color shaded gold/silver lettering and slider
handles, not the two-color mask profile. Do not reuse that Options profile.

Added explicit binary-cutout authoring to enhance_menu_background.py. It rejects
semi-transparent inputs instead of losing their transparency semantics, and the
default opaque path now rejects transparent inputs. Authored and individually
inspected both label atlases in menu-sound-lettering-candidate. Staged only the
two PNGs into the isolated ui-buoy output's Overrides/MISC/SOUND directory.
Original texture record 0 and all approved playable files remain unchanged.

Muted headless menu-sound-lettering-native completed with expected game exit 3.
Individually inspected both complete captures, frame-003600.png then
frame-003840.png, and compared the earlier menu-controller-sound-native 3600
capture. Reconstructed letters have visibly smoother contours rather than
repeated source pixels. Original wording, gold/silver shading, placement,
background, slider handles and footer remain intact. Sound Effects is selected
at 3600; Music is selected at 3840 and its slider has moved left. No black atlas
rectangle or neighboring-label bleed is visible in these two states.

This is partial native evidence, not completion of the Sound menu family.
Overall Volume/Exit highlights, cancel/done transitions, persistence and any
Music Test variant remain unverified. Muted execution provides no audio proof.
No game code changed or new game build was required for this asset-only check.
Lighting and water/effects remain closed and unchanged.

## Sound Exit And Load/Save Background Checkpoint

The previous goal turn made progress through sound atlas authoring and native
comparison. This pass adds remaining common sound selections and two BS assets.

`menu-sound-exit-replay.json`, muted/headless, completed normally. All five
captures in `menu-sound-exit-native` were individually inspected in sequence:
- 2400: Overall Volume gold highlight, its slider moved left.
- 2640: Exit gold highlight, all slider handles unselected.
- 2881: returned Options with Sound and Music selected.
- 3121: reopened Sound; adjusted Overall Volume position retained in-session.
- 3361: Cancel returned to Options without stale sound artwork.
This does not establish cross-process persistence or auditory correctness.
Music Test is not present in this normal menu despite appearing in the source
atlas; its availability and any alternate state still need source investigation.

Extracted and individually inspected original MISC/LOADGAME.BS and SAVEGAME.BS
at 640x480, then reconstructed and inspected both 4x candidates. Staged their PNGs
and provenance only in the isolated Menu4x/MISC directory (catalog now 52).
Memory-card artwork, purple palette, layout, headings and original footer icons
remain intact. Save Game retains its original right-edge black strip; do not
interpret this source feature as a newly introduced crop.

`menu-loadgame-native` completed normally. All three captures inspected:
- 1441: Load Game, Card 1, no Jet Moto games on card.
- 1680: Card 2 selected after Circle, same empty-card message.
- 1920: returned to title via Triangle; no stale load background.
No save was selected, loaded, deleted, formatted or overwritten by the replay.
Load heading/footer/background improve visibly, but live text and background
patches are still from the previous TMS pack. This screen remains incomplete.
Save background is authored/staged but has not been reached in-game yet.

Extracted LOADGAME's nine and SAVEGAME's twelve original TMS records with
provenance. Individually viewed all nine LOADGAME records, not yet SAVEGAME's.
Messages span texture pairs (LOADGAME 3/5 white text, 4/6 gold dialogs);
reconstruct joined text before splitting back to original tile boundaries.
Slots/name-entry glyphs occupy records 7/8. Do not claim these extracted originals
as authored replacements. Next work: live load/save text, patch consistency,
safe isolated test-card fixtures and Save Game/dialog native coverage.

## Joined Load-Menu Message Reconstruction

Extended enhance_menu_background.py with explicit right-source/right-output
arguments for reviewed horizontally split artwork. Both source checksums,
dimensions, same-bank ownership and output non-overwrite checks are enforced.
Reconstruct RGB and binary coverage jointly, then split at the original tile
boundary. Each output records both original inputs in its provenance.

Authored LOADGAME 3/5 white messages and 4/6 gold dialogs as paired candidates in
menu-loadgame-lettering-candidate. Individually viewed all four outputs before
staging into isolated Overrides/MISC/LOADGAME. Verified all four output hashes,
exact 4x dimensions and binary alpha categories. No game/runtime code changed.

Muted headless menu-loadgame-lettering-native completed normally. All three
complete screenshots individually inspected in order: 1440 Card 1 empty,
1680 Card 2 empty, 1921 returned title. Compared against the prior inspected
menu-loadgame-native captures: the long empty-card message now has visibly
reconstructed letter edges, with unchanged wording and no visible join through
ON THIS CARD. Card identifiers, heading, footer and layout remain intact.
No saving, formatting, deletion or loading occurred in this replay.

Gold dialog outputs are authored but not natively verified. Other white messages,
slot lettering/name entry, TMS background-patch consistency, and Save Game remain
open. Individually inspected all twelve original SAVEGAME TMS records this pass;
its additional name-entry footer spans records 0/5, and record 8 includes the
purple IN USE label. Original extraction is not enhancement or native coverage.
The all-menu goal remains active; no screen family was closed by this pass.

## Save-Menu Foreground Candidates And Entry Investigation

Authored and individually inspected nine SAVEGAME TMS candidates in
menu-savegame-lettering-candidate: joined pairs 4/7 (white card messages),
6/9 (gold confirmations), 0/5 (name-entry/card-switch footer), and individual
8/10/11 (IN USE, gold/white EMPTY SLOT and character atlases). Original text,
italic slot labels, purple IN USE color, gold highlights and arrow/button art
remain. Staged these only under the isolated candidate's Overrides/MISC/SAVEGAME.
All are still pending native Save Game/name-entry/dialog validation. No new
native capture was made in this authoring pass, and no acceptance is inferred
from isolated atlas inspection. Background-patch records 1/2/3 remain unchanged.

Source investigation for safe validation:
- Program.Run resolves the supplied CUE before setting current directory to
  AppContext.BaseDirectory; settings and saves therefore belong to that build.
- Candidate settings use relative carda.sav/cardb.sav, both enabled. Runtime's
  LoadMemoryCards uses these paths; MemoryCard.Format flushes immediately, so
  destructive dialog testing must use deliberate disposable card fixtures.
- Original executable load base is 800DD2D0. SAVEGAME.BS string at 800DF4A0 is
  referenced by the original Save Game builder func_8013B950 (8013BB90).
- Menu registration at 80135D94/80135DA0 binds this builder to menu ID 0x67.
  Original references to that ID include 80137900, 80138D30 and 8013C128;
  trace those callers next to establish the normal replay route. Do not force
  arbitrary RAM state or claim a synthetic atlas preview is the actual menu.

Approved playable output, Build 11, lighting and water/effects were not changed.

## Disposable Memory-Card Validation

Added opt-in -DisposableCards to Run-Validation.ps1. Requires an explicit
BuildOutputRoot, refuses reuse of an existing report card directory, redirects
both cards to new files under that report, and restores the original candidate
settings bytes in finally. The game remains muted/headless. This is validation
infrastructure, not a game behavior change or menu completion.

Executed menu-disposable-cards-native using the Load Game replay. Individually
inspected all three complete captures: 1440 Card 1 empty, 1680 Card 2 empty,
1920 returned title. New carda.sav/cardb.sav were created under disposable-cards.
Post-run assertions verified byte-identical settings restoration and unchanged
SHA-256 hashes for both pre-existing candidate memory cards. Game exited with
the expected validation timeout, wrapper completed successfully.

Save route investigation continues: func_80135B8C builds the surrounding menu
graph, registers Save Game as 0x67 and connects it through objects at stack
offsets 3648 and 37A8. References at 8013607C/80136150/80136250/80136360 are
conditional graph connections, not proof that a particular controller button
opens Save. No arbitrary menu/RAM injection was added. Save Game full-screen,
name-entry and confirmation evidence is still missing and remains pending.

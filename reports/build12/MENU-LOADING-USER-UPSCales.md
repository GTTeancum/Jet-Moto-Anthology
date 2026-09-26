# Supplied loading-screen integration — 2026-09-25

Current artwork: `exports/loading-screens-batch-20260925-210134/`.
The user's twenty supplied full-page images supersede the earlier partial map
candidates. No further map-only authoring is active.

## Packaging

All twenty supplied files are 1024x768 opaque RGB. Each was individually viewed,
Loading then Continue, in track order: Cypress Run, Blackwater Falls, Suicide
Swamp, Joyride, Cliffdiver, Hammerhead, Willpower, Ice Crusher, Snow Blind,
Nightmare. Titles, maps, paragraphs and the appropriate prompts are present.

The existing NativeTextureAsset loader requires exactly four times the source
dimensions (1280x960). `TextureTools/package_supplied_loading.py` makes separate
Lanczos-resized RGB runtime copies. It does not change the supplied files, invent
detail, alter lettering or normalize the two variants' artwork. All twenty input
hashes were checked unchanged after staging.

Package and provenance manifests:
`menu-loading-user-upscales-20260925/Textures/Menu4x/`.
Staged destination: `.build/ui-buoy-bin/Release/net10.0/win-x64/Textures/Menu4x/`.
Original disc identities and checksums are retained in every adjacent manifest;
the supplied-file and runtime-file hashes are separately recorded.

The previous ten test overview PNGs and their ten manifests were copied and
hash-verified in `menu-loading-user-upscales-20260925/previous-runtime/` before
any staged file was replaced. The root package manifest inventories that backup.
Older report candidates remain untouched. The isolated catalog now contains
twenty overview PNGs (all ten track pairs), alongside unrelated existing menus.

## Artwork observations

These are the supplied images, not claims of exact original-art fidelity.
Willpower uses an orange start arrow and yellow checkpoint markings. Snow Blind
adds colored route stripes and turn chevrons. Nightmare uses detailed metallic
markers. These differences were preserved. Several start/finish legend symbols
look like chevrons rather than the original checker pattern.

Blackwater Loading and Continue also differ in map shading/detail, notably near
the dam and the lower brown route. Its Continue X icon has an offset/ghosted
appearance already present in the supplied artwork. Those differences remain in
the runtime copies; the swap is not strictly limited to the prompt pixels.

## Native evidence

Muted, headless native runs use process-local replay and disposable cards.
No desktop control or capture is involved. Runs execute one at a time.

Blackwater: `menu-loading-user-blackwater-native-v1`, 53 seconds, game exit 3,
wrapper exit 0. All eight saved captures were inspected individually in order:
2392 and 2401 track selection; 2413 and 2424 Loading; 2436, 2448, 2460 and 2472
Continue. Both SWAMP2/OVERV1L.TIM and SWAMP2/OVERV1.TIM were submitted as owned
replacements. Complete page content and prompts are visible without observed
clipping or transparent cutouts. Dark blueprint colors show native quantization.
The map-detail changes between the two supplied variants are visible on the swap.

Further run results are recorded below after visual inspection. Captures cover
saved states, not every intermediate frame. Race entry and audio are not tested.

## Preservation

No runtime source or binary changes were needed. Approved audit JetMoto.dll SHA256:
`D2C09E1AF287900921AD27FEDE9281FB1CFF724C0044783D577F0D4DD66F3333` (unchanged).
Build 11, approved output and supplied originals remain untouched. No commit,
push or release packaging was performed.

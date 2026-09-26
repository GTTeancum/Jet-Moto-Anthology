# HUD and speedometer checkpoint

The user approved the V4 reconstructed timer digits and buoy appearance:
"Those look good. Get that speedometer bit looking better. It's tacky by comparison".
V1/V2 upscale attempts were rejected and withdrawn. V4 remains the approved
base; the unrelated V5 letter/map experiments were not staged.

## Speedometer replacement

Built-in imagegen restored the original dial crop, producing a clean metal
face, continuous rim, readable 20/40/60/80/100 numerals and clean indicator
sockets. Original dynamic needle, lamps and bar remain game primitives.
`TextureTools/package_speedometer.py` integrates this artwork into the lower
half of each of ten F4AD atlases. The upper half is pixel-identical to approved
V4; all twelve staged buoy assets still match their approved SHA256 hashes.
PNG alpha remains categorical 0/128/255: transparent background, PS1 STP face,
and opaque outer rim. The generated silhouette has small restoration differences
from the source pixels; gameplay sprite dimensions and coordinates are unchanged.

Local output: `reports/build12/speedometer-v1/`, containing the generated
`speedometer-art.png`, ten packaged atlases and their SHA256 manifest.
Staged in `.build/ui-buoy-bin/Release/net10.0/win-x64/Textures/Overrides`.
No release packaging or changes to the approved audit-bin / Build 11.

## Evidence and limits

`speedometer-native-v1`: 110-second headless muted wet-transition replay.
Individually inspected native framebuffer images in order: race-000900.png,
race-001002.png, race-001200.png. All show the restored dial, aligned green
lamps, red needle/bar, readable numerals and translucent face over the moving
water. World scene, rider, buoys and other HUD elements are visible. This is
a sampled visual check on ISLAND1, not a full speed-range, all-track or audio
validation. The existing thin world streaks near a buoy remain visible in the
third capture; this speedometer change does not close buoy edge verification.

HUD source binding tests: 166 passed, 0 failed in `hud-native-tests.log`.
These test source/descriptor provenance and stale binding rejection; they do
not substitute for the inspected native captures. Latest runtime candidate
build completed with 0 warnings and 0 errors before this asset-only change.

## Final generation prompt

Built-in imagegen edit target:
`hud-buoy-source/dashboard-original-detail.png` (original 832x512 crop).

> Edit target: attached original game speedometer texture crop, 832x512. Create a faithful high resolution restoration with EXACTLY the same composition, silhouette, proportions, placements, framing and bottom cropping. This is a replacement texture for a 1996 racing game HUD. Refine pixelated surface to smooth clean dark graphite metal, restrained satin bevel on outer circular rim, smooth grey-silver curved speed strip, crisp tiny white numbers reading exactly 20 40 60 80 100 along the upper curved arc at the same positions. Keep the four dark small indicator lamp sockets at their exact original positions, white horizontal bar in exact position, black circular knob on right and small white-yellow-red arc right above knob in same positions. The bottom of round dial stays cropped. Preserve every detail's positions, no new details. No needle: that is drawn by game. No glowing accents, branding, extra text, extra ticks, extra lamps, new designs, drop shadows outside silhouette, or perspective changes. Matte dark face with subtle smooth metallic gradient. Background pure black. Output same 13:8 aspect ratio, full bleed matching attached crop precisely. Aim elegant source-faithful restoration, no overly glossy plastic.

# Joyride loading runtime candidate

The action-first pass packages the existing source-guided title and V3 map
contours as both Joyride Loading and Continue replacements. The paragraph uses
consistent original lettering; isolated reconstructed words are excluded.

Files: `menu-loading-runtime-candidate/ISLAND1/OVERV3L.png` and `OVERV3.png`,
1280 x 960, each with a runtime source/output checksum manifest. Both are staged
only in `.build/ui-buoy-bin/Release/net10.0/win-x64/Textures/Menu4x/ISLAND1`.
The catalog has 43 entries, including these two loading candidates.

First native run: `menu-loading-joyride-candidate-native`, 33 seconds, headless,
muted, disposable cards, process-local season replay. Game exit 3, wrapper 0.
All five saved captures were individually inspected sequentially: 1810, 1817,
1823, 1827, 1837. Each shows the actual Joyride Loading page, new title and map
lines, original paragraph/labels/markers/legend, and Loading footer in 4:3.
No clipped title or missing map/paragraph was observed in these captures.
The runtime log confirms submission of both owned OVERV sources, but these
five images alone do not verify the Continue presentation.

Limitations: small lettering, terrain, markers, legend, and footer are original
samples enlarged with Lanczos. Title background includes provisional interpolated
pixels. The title and selected map lines are crisper than the remaining image;
map line weight and small-label softness still need work. Native dark grid tones
show quantization. This is a usable test candidate, not final quality acceptance.
Nine other tracks remain pending. No claim of every animation frame inspected.
Audio is muted, so audio behavior is unverified by this check.

Approved audit build SHA256 remains
`D2C09E1AF287900921AD27FEDE9281FB1CFF724C0044783D577F0D4DD66F3333`.
No release, commit, push, or changes to the approved dial/gameplay were made.

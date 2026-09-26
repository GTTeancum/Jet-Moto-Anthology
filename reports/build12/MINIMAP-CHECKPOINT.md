# Crisp minimap follow-up

Requested after the accepted HUD/buoy checkpoint was committed and pushed as
`82a4c15`. The Remaining HUD And Buoys section has been removed from TODO by
user direction. This follow-up changes only the minimap artwork.

`TextureTools/trace_minimaps.py` fits periodic cubic splines to each original
64x64 map's contours. Fit tolerance is 0.30 original pixels RMS, reduced to
0.25 for SWAMP1 to retain its narrow loops. Rendering produces 256x256 hard
cutout textures: no blurred RGB or fractional-opacity halo. Original black
line color (gray for DARK), STP alpha class 128, atlas placement and game
marker coordinates are preserved. Each map's connected components and hole
count are checked against its source. The game still consumes raster textures;
editable cubic-path SVGs are saved alongside them.

All ten resulting map previews were individually inspected. All textures are
256x256 with alpha restricted to 0/128. SHA256 verification confirms that the
ten accepted speedometer/HUD atlases and twelve buoy textures are unchanged.

Local outputs: `reports/build12/minimap-v1/`, including source-keyed PNG/SVG
pairs, opaque diagnostic previews, SHA256 manifest and any previous override
backups. Staged in `.build/ui-buoy-bin/Release/net10.0/win-x64/Textures/Overrides`.
Source texture IDs: DACB, DACF, DAB3, DAE5, DAE9, DAED, 224D, 2256, 2243, DA73.

Reproduction (Pillow, NumPy, SciPy, scikit-image required; current scikit-image
installation is in the local `.build/hud-authoring` authoring environment):

```powershell
python TextureTools/trace_minimaps.py --cue '../Jet Moto/Jet Moto (USA).cue' --output reports/build12/minimap-v1 --stage .build/ui-buoy-bin/Release/net10.0/win-x64/Textures/Overrides
```

Native validation: `minimap-native-v1`, a bounded 110-second headless muted
wet-transition replay. Individually inspected race-000901.png, race-001000.png,
and race-001200.png in sequence. ISLAND1 has smooth, crisp curves over sky,
cloud and beach backgrounds. Fixed red markers and moving race markers remain
aligned with the trace; the other HUD artwork and world remain visible.
All ten atlas variants were inspected offline; only ISLAND1 was checked in
game during this follow-up. Audio and full-track traversal were not tested.
No release packaging or changes to Build 11 / the approved audit-bin.

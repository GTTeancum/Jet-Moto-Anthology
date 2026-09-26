# Continuous HUD numeral outlines

The user identified breaks in the timer and lap-number outlines on Nightmare.
These were present in the authored F4AD atlas, not caused by Nightmare lighting
or texture binding: Pillow's stroked paths left a seam at the first/last vertex
of closed digits and exposed ink at open stroke ends.

`TextureTools/reconstruct_hud_buoys.py` now explicitly renders round joins and
caps for both the silver border and magenta ink. The original fitted numeral
paths, placement and colors are retained. `TextureTools/repair_hud_digits.py`
applies this repair to all ten existing speedometer atlases and verifies that
every pixel outside the digit cells is unchanged. Gauge, punctuation, labels,
boost lamps, minimaps and buoys retain their previous artwork.

Reproduce after packaging the speedometer:

```powershell
python TextureTools/repair_hud_digits.py --source reports/build12/speedometer-v1 --output reports/build12/hud-digit-joins-v1 --stage .build/ui-buoy-bin/Release/net10.0/win-x64/Textures/Overrides
```

The local output manifest records source and repaired SHA256 hashes for all ten
atlases. This supersedes the ten speedometer-v1 atlas hashes; those older hashes
remain the immutable source checkpoint. No runtime code or shader change.

## Verification

All ten atlas preservation assertions passed. The repaired atlas was inspected
at native resolution: closed zero/eight outlines and open stroke ends are
continuous. Native `nightmare-digit-joins-v1` completed its 91-second headless,
muted replay. All five saved captures were individually inspected in sequence:
3600, 3901, 4200, 4501, 4800. Zero's top-left seam is gone; timer, lap counter
and transient position/split numerals have continuous silver borders, including
the open ends of one, two, three and seven. The start grid, moving riders,
advancing timer, road, starfield, minimap and gauge remain visible through the
tested flow. This is sampled Nightmare coverage, not all-track gameplay or
audio verification.

# Road Emission Correction

Status: experimental correction; not accepted or packaged.

The user correctly identified water spray over the road in the previous
race-relative-straight-bay captures (780, 784, 793, 796). Those captures fail
surface correctness. Emitter height relative to the water plane alone was
insufficient: a low road could qualify as water.

Both ballistic spray and persistent foam now veto emission at birth positions
whose surrounding source-height samples contain solid ground at/above water.
Unknown positions outside the source map fail closed. The map is baked from
opaque solid native scenery, explicitly excluding classified water polygons.
This is a conservative rejection test, NOT positive simulation contact.
Low-resolution shore boundaries and overhead structures can over-reject;
missing geometry can still require stronger contact provenance. Dirt, sand,
and mud emission remain unimplemented by this correction.

The validation replay also had a separate pre-race input bug: subtracting the
initial race-start value of -1 made race-only input active before race entry.
The corrected gate explicitly requires a recorded race start, resets between
configurations, rejects unknown clocks, and installs the race callback only
for validation. Previous race-relative runs do not establish replay fidelity.

Verification so far:
- WorldLightingTests: 140 passed, including dry-road rejection for both paths
  and retained open-water particle emission.
- ReplayTests: 7 passed against the actual linked replay implementation.
- Isolated Release build: succeeded, zero warnings/errors.
- Native muted road check: completed (85 seconds, expected application exit 3,
  harness exit 0). All four saved captures inspected individually in order:
  `road-emission-rejection/race-000781.png`, `race-000795.png`,
  `race-000812.png`, `race-000826.png` (HUD 5.8 to 6.5 seconds).
  The prior large white droplets are absent behind the player and visible
  nearby opponents on the road. This is bounded evidence for this road case,
  not inspection of every rendered frame or all terrain transitions.
  Open-water emission retention is currently established only by the CPU
  fixture, not by these road captures. In-game wet/dry transition and swamp
  checks remain required. Water color/detail discontinuities remain visible,
  especially in the last capture; water visual acceptance still fails.

Build 11 and deployed releases have not been overwritten.

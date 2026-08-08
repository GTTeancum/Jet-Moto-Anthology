# Status

**Goal:** Jet Moto (SCUS-94309) recompiled via RecompOne, playable to the point
of completing laps as a rider.

**Last updated:** 2026-08-07

---

## Gates

| # | Gate | State |
|---|------|-------|
| 0 | Recompiler produces C# | **done** — 1859 functions |
| 1 | Port project compiles | **done** — ~6 s clean build |
| 2 | Reaches game code without faulting | **in progress** — boots, GPU up, executing recompiled MIPS |
| 3 | PSYQ SDK calls routed to runtime | not started — 0 of ~50 routed, **the critical path** |
| 4 | First GPU packet / anything on screen | not started |
| 5 | Title screen renders (screenshot-verified) | not started |
| 6 | Menus navigable under scripted input | not started |
| 7 | A race loads | not started |
| 8 | **Lap counter increments** — the goal | not started |

## Where things stand

The port boots. It initializes the OpenGL 3.3 backend, loads the dispatch table,
and executes real recompiled game code several calls deep before faulting. That
is further than a first day usually gets.

Function discovery needed `linearSweep` — entry-point scanning alone missed
functions reached only through function pointers. That took 1462 → 1859.

## Next

1. Clear the remaining unmapped-call faults until execution reaches the main loop.
2. **Name the PSYQ SDK functions.** This is the whole project. RecompOne routes
   SDK calls by string-matching names (`SdkPatches.cs`), and with everything
   named `func_800XXXXX` nothing routes — no rendering, no controller, no CD.
   Approach: signature-match against 1996-era PSYQ library releases.
3. Build the verification harness (screenshot capture + scripted pad input +
   memory assertions) before touching gameplay behaviour.

## Facts worth not rediscovering

- Boot EXE `SCUS_943.09`: text `0x800DD2D0`, size `0xEF000` (978944), entry
  `0x800EC310`, SP `0x801FFFF0`, GP unset.
- No code overlays anywhere on the disc. `QUICKY.PAC` is the one unexamined container.
- Build loop is fast: recompile ~20 s, rebuild ~6 s.

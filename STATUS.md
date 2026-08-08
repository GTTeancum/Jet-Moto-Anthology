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
| 2 | Reaches game code without faulting | **done** — runs indefinitely, no fault |
| 3 | PSYQ SDK calls routed to runtime | **in progress** — 5 of ~50 routed |
| 4 | Game progresses past init (first VSync) | **blocked** — spinning, see below |
| 5 | Title screen renders (frame-dump verified) | not started |
| 6 | Menus navigable under scripted input | not started |
| 7 | A race loads | not started |
| 8 | **Lap counter increments** — the goal | not started |

## Where things stand

Day one got further than expected. The port boots, loads the boot EXE, executes
recompiled MIPS, and no longer crashes: it runs indefinitely without faulting.

Getting there took two fixes. `linearSweep` for functions only reachable through
function pointers (1462 → 1859), and naming five PSYQ SDK functions, which
cleared both the CD read timeouts and a `0x80800000` wild-pointer crash.

**Current blocker.** Not crashing is not the same as progressing. With
`RECOMPONE_LOG=sdk,cd` the port makes no SDK calls at all after `ResetGraph` —
it is stuck in a busy-wait. The likely cause is partial routing: HLE `CdRead`
completes into runtime state while the game's own un-named `CdReadSync` polls
hardware state that never updates. Fix is to name the rest of libcd as a set.

## Next

1. Name the remaining libcd functions — `CdReadSync`, `CdSync`, `CdControl`,
   `CdGetSector`, `CdDataSync`. Start from `func_800E3DF0` and `func_800E4344`,
   both confirmed libcd members.
2. Scale up naming beyond what the string pool gives: PSYQ library signature
   matching for the functions that print nothing.
3. Confirm progress by frame dump (gate 5) once the game reaches its main loop.

## Harness

```bash
python harness/autorun.py --once --timeout 45   # headless run + fault triage
python harness/autorun.py                       # loop, auto-fixing unmapped calls
```

Environment switches added to the local RecompOne fork:

| Variable | Effect |
|---|---|
| `RECOMPONE_HEADLESS=1` | no window, no GL — execution testing only, does not render |
| `RECOMPONE_OFFSCREEN=1` | real GL in a hidden window — renders, never visible on screen |
| `RECOMPONE_DUMP_DIR=<dir>` | dump display buffer as PPM |
| `RECOMPONE_DUMP_EVERY=<n>` | dump interval in frames (default 60) |
| `RECOMPONE_LOG=sdk,cd,bios,gpu,dma,spu,mdec` | per-subsystem tracing (`all` for everything) |

## Facts worth not rediscovering

- Boot EXE `SCUS_943.09`: text `0x800DD2D0`, size `0xEF000` (978944), entry
  `0x800EC310`, SP `0x801FFFF0`, GP unset.
- No code overlays on the disc. `QUICKY.PAC` is the one unexamined container.
- PSYQ debug string pool at `0x800DE130-0x800DE8A8` — the naming goldmine.
- Named so far: `0x800E4B90` CdInit, `0x800E5314` CdRead, `0x800E4074` CdReady,
  `0x800EB6C4` VSync, `0x800E792C` DrawSync, `0x800E748C` ResetGraph,
  `0x800E7834` DrawSyncCallback.
- Build loop: recompile ~20 s, rebuild ~6 s.

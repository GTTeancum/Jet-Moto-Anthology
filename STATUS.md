# Status

**Goal:** Jet Moto (SCUS-94309) recompiled via RecompOne, playable to the point
of completing laps as a rider.

**Last updated:** 2026-08-07

---

## Gates

| # | Gate | State |
|---|------|-------|
| 0 | Recompiler produces C# | **done** — 1863 functions |
| 1 | Port project compiles | **done** — ~6 s clean build |
| 2 | Reaches game code without faulting | **done** |
| 3 | PSYQ SDK calls routed to runtime | **15 routed** — libcd, libcdstream, VSync, DrawSync |
| 4 | Game progresses past init | **done** |
| 5 | Title screen renders | **done** — verified from an offscreen capture |
| 6 | Menus navigable under scripted input | **done** |
| 7 | A race loads | **done** — reaches ISLAND1 track data |
| 8 | **Lap counter increments** — the goal | in progress |

## Where things stand

The port boots, plays its streamed intro, reaches the title screen, accepts
scripted controller input, walks the menus, and loads a track. What remains is
confirming a lap actually completes.

Everything is verified without a human watching: offscreen rendering into a
window parked off-desktop, frames captured via `glReadPixels`, audio forced
silent, controller driven from a script.

## The five runtime bugs found so far

All in RecompOne's runtime, not in the port, and all found by asking what the
game was waiting on rather than by inspection. Details in `DECISIONS.md`.

1. `LibCd.CdInit` returned 0 on success where PSYQ returns 1.
2. `LibCd.CdRead` never set `StatRead`, so callers polling `CdReadSync` for
   status `0x22` retried the same read forever.
3. `LibEtc.VSync(-1)` returned a counter only advanced by the game's own
   `VSync(0)`, deadlocking a boot-time spin. VBlank is now time-driven.
4. BIOS `InitPAD2`/`StartPAD2` were no-ops, so the game received **no
   controller input at all** — from a real pad either, not only a scripted one.
5. Not a bug but a big one: `FrameClock` capped harness runs at ~10 fps.

## Performance

| Change | VSync(0) per 20 s |
|---|---|
| baseline | 62 |
| libcdstream routed | 211 |
| unthrottled | **2016** (~100 fps) |

The apparent "3 fps" was never the recompiled code. It was an unrouted
`StGetNext` spinning `0x800000` times inside another `0x800000`-iteration
retry, and after that a deliberate 60 Hz frame limiter. Stack sampling with
`dotnet-stack` found both; guessing at the memory path found nothing.

## Next

1. Confirm a lap completes — dump RAM across a race and use
   `harness/findcounter.py` to find the lap counter, then assert on it.
2. Name the libgpu public API (`DrawOTag`, `PutDrawEnv`, `PutDispEnv`), which
   print nothing and so need shape-based identification.
3. Check the CueBin warning about reads outside the data track (lba
   34186-34195) before trusting CD-DA music.

## Harness

```bash
python harness/autorun.py --once --timeout 45   # headless run + fault triage
python harness/ppm2png.py harness/captures      # frame dumps -> PNG
python harness/findcounter.py harness/ram       # find lap-counter candidates
```

| Variable | Effect |
|---|---|
| `RECOMPONE_HEADLESS=1` | no window, no GL — execution testing, does not render |
| `RECOMPONE_OFFSCREEN=1` | real window parked off-desktop; renders, never seen |
| `RECOMPONE_DUMP_DIR` / `_EVERY` | presented framebuffer → PPM |
| `RECOMPONE_RAMDUMP_DIR` / `_EVERY` | 2 MB RAM snapshots |
| `RECOMPONE_INPUT` | `frame:buttons;...` or `@file` scripted controller |
| `RECOMPONE_LOG` | `sdk,cd,bios,gpu,dma,spu,mdec` or `all` |
| `RECOMPONE_PEEK` | log addresses and what they point at, once a second |
| `RECOMPONE_TRAP_CDREAD` / `_VSYNC` | throw on the nth call to expose the game-side stack |
| `RECOMPONE_MUTE`, `RECOMPONE_UNTHROTTLE` | forced on for headless/offscreen |

## The fork

`tools/RecompOne/` is gitignored, so runtime fixes live in
`tools/recompone-fork.patch` against upstream `8bd2039`, restored by
`tools/apply-fork.sh`. Every hunk is tagged `[jetmoto-fork]`. No upstream PRs —
that maintainer rejects AI-authored contributions.

## Facts worth not rediscovering

- Boot EXE `SCUS_943.09`: text `0x800DD2D0`, size `0xEF000`, entry `0x800EC310`.
- No code overlays. `QUICKY.PAC` still unexamined.
- PSYQ debug string pool at `0x800DE130-0x800DE8A8`; RCS tags date the SDK to
  late 1995 (`bios.c v1.71`, `sys.c v1.116`, `intr.c v1.73`).
- **Route the public API, never the internals** — internals take different
  arguments. Public libcd wrappers are at `0x800E30F4-0x800E36A8`.
- **Settle ambiguous names from call sites**, not from the internal a wrapper
  calls. `CdRead`/`CdReadSync` and the pad byte order were both wrong until
  checked that way.
- BIOS pad buffers: `InitPAD2(0x801EA0D8, 34, 0x801EA0FC, 34)`. The game reads
  `buf[1] >> 4 == 4` then `(buf[3] | buf[2] << 8) ^ 0xFFFF`.
- Trap-and-read-the-stack beats reading generated MIPS-to-C# by hand.
- Build loop: recompile ~20 s, rebuild ~6 s.

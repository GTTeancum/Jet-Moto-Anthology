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
| 6 | Menus navigable under scripted input | **not verified** — see below |
| 7 | A race loads | **done** — races render in full 3D, screenshot-verified |
| 8 | **Lap counter increments** — the goal | **not met** — see below |

## Where things stand

The port boots, plays its streamed intro, reaches the title screen, accepts
scripted controller input, walks the menus, loads a track, and **runs races in
full 3D**. What remains is asserting on a lap counter.

Everything is verified without a human watching: offscreen rendering into a
window parked off-desktop, frames captured via `glReadPixels`, audio forced
silent, controller driven from a script.

## The runtime bugs found so far

All in RecompOne's runtime, not in the port, and all found by asking what the
game was waiting on rather than by inspection. Details in `DECISIONS.md`.

1. `LibCd.CdInit` returned 0 on success where PSYQ returns 1.
2. `LibCd.CdRead` never set `StatRead`, so callers polling `CdReadSync` for
   status `0x22` retried the same read forever.
3. `LibEtc.VSync(-1)` returned a counter only advanced by the game's own
   `VSync(0)`, deadlocking a boot-time spin. VBlank is now time-driven.
4. BIOS `InitPAD2`/`StartPAD2` were no-ops, so the pad buffers were never
   filled at all. Fixing that makes the buffers correct, but the game still
   does not act on them — see gate 6.
5. Not a bug but a big one: `FrameClock` capped harness runs at ~10 fps.

## Performance

| Change | VSync(0) per 20 s |
|---|---|
| baseline | 62 |
| libcdstream routed | 211 |
| unthrottled | **2016** (~100 fps) |

Offscreen rendering *was* ~9 fps against ~55 headless. That was the CueBin log
flood, not the renderer: with it silenced, offscreen runs at several hundred
fps and visual verification is cheap.

The apparent "3 fps" was never the recompiled code. It was an unrouted
`StGetNext` spinning `0x800000` times inside another `0x800000`-iteration
retry, and after that a deliberate 60 Hz frame limiter. Stack sampling with
`dotnet-stack` found both; guessing at the memory path found nothing.

## Gate 6: the input pipeline is correct; the menu never reads it

Earlier this was marked done because `NAVIGATE/PICKRIDE.BS` loaded after
scripted presses. That was the **attract sequence** cycling menus by itself.

The input path has now been traced end to end with hooks on the game's own
functions, and **every stage is correct**:

| stage | address | result |
|---|---|---|
| BIOS pad buffer | `0x801EA0D8` | `0xFFBF4100` with Cross held |
| parsed button word | `gp+0x70` | `0x00004000` = Cross |
| edge ("newly pressed") | `gp+0x6C` | `0x00004000` on the press frame, `0` while held |
| game's own query | `func_800EF9D8(id, mode)` | returns **1** for the pressed button |

So input works end to end and the game *acknowledges* it. `func_800EF26C` and
`func_800EF2AC` are dead code — an earlier note claiming "nothing consumes the
edge word" was hooking those, not the live path (`func_800EF9D8` ->
`func_800EF4CC`).

**Button id map**, established by pressing one button at a time and reading
which id the game reports a hit for:

| id | button | id | button |
|---|---|---|---|
| `0x00` | Triangle | `0x0A` | Down |
| `0x01` | Cross | `0x0B` | Up |
| `0x02` | Square | `0x0E` | Left |
| `0x03` | Circle | `0x0F` | Right |
| `0x06` | *any button* | `0x10` | Select |
| `0x08` | R1 | `0x14` | Start |

**What is still wrong:** despite every press being received and acknowledged,
no menu transition happens. Cross, Start, Up, Left and Right all register and
none moves the highlight off `1 PLAYER` or loads a new screen. One press *does*
suppress the attract demo — after a press the demo stops starting — so input
reaches game state, just not the menu.

**Where it actually stands** (an earlier note here blamed state-machine
dispatch; that was wrong and is corrected below).

The real title handler is `func_801370B4`. It loads `STARTUP\TITLE.BS` and
calls `func_8013C05C`, a generic menu driver, passing the option handlers
(`0x80137130`, `0x80137160`, and `0x801377F8` = Options). **Both are dispatched
and run.**

`func_80134FBC` / `func_801395B8` / `func_8013963C` are *not* the title — they
are the controller-config screen (`MISC\CONTROL%d.BS`). Their never being
reached is expected, not a defect. That invalidated the previous "the title
state is never dispatched" conclusion.

Logging the caller (`RA`) of every input query shows two pollers running every
frame:

- `RA=0x8014E9FC..0x8014EBE8` — sweeps ids `0x00`-`0x15` with mode 0. This is
  the menu's input snapshot, and it returns hits for pressed buttons.
- `RA=0x801352E4/0x801352F8` — ids `0x0A`/`0x08` with mode 2, inside
  `func_801352B4`.

So the menu polls input every frame and receives correct answers, and still
does not transition. The defect is in what the menu does with the answer, not
in obtaining it.

**The menu tick is understood.** `func_80134D14(struct)` runs per frame:

```
func_8014E9CC(struct)          # sweep: sets [struct+0x14] = -1, then queries
                               # each button id with mode = [struct+0x10];
                               # a hit writes the button index to [struct+0x14]
if ([struct+0x14] >= 0) func_80134C2C(struct, index)   # act on it
if ([struct+0x84] > 0)  [struct+0x84]--                # startup lockout
```

Observed live with `struct = 0x807FE740`: the lockout counts 299 -> 0 correctly
(~5s at 60 Hz) and then stays 0, so it is not blocking. But `[struct+0x14]`
stays **-1 at every tick**, including during a press — so the sweep is not
recording a hit even though the same sweep's query sites *do* return hits when
logged directly.

That is the contradiction to resolve next: either `func_80134D14` belongs to a
screen other than the title (it is dispatched, but so are a dozen handlers), or
its queries and the observed hits happen in different frames. The cheap way to
settle it is to log `[struct+0x10]` (the mode the sweep passes) and the tick's
own query results together, rather than inferring across two runs.

The attract loop at real-time pacing is: title ~30s, demo race ~11.5s, repeat.

## Gate 8: not met, and why the earlier result was wrong

An automated check reported `LAP CONFIRMED` twice. **Neither result holds up**,
and the reason matters more than the claim did.

The attract demo restarts roughly every 25 seconds. Frame-difference analysis
across a 2931-frame capture shows the pattern plainly: alternating stretches of
zero change (the static title) and ~60% change (a demo race), each race lasting
about 6000 presented frames at ~244 fps. **A lap cannot complete in 25 seconds**,
so the counter increments being observed are demo restarts resetting state, not
laps. Two runs of the same test disagreed with each other — 38s/90s in one,
both increments in the same instant in another, then frozen for 470s — which is
what finally exposed it.

What is genuinely established: races render, with motion, and riders move. What
is *not* established: that a full race runs to a completed lap.

The blocker is menu navigation. The attract demo starts on its own a few
seconds after the title appears, and a press during a demo only returns to the
title, so a fixed-timing script bounces between the two forever.
`harness/find-menu-route.py` searches the timing space and scores each candidate
by whether it reaches a *sustained* race — track data loaded with no `TITLE.BS`
reload afterwards.

## Gameplay confirmed

Screenshots from an offscreen run show real gameplay: AI riders in the DARK
track underpass, and an alpine start line with the starting gantry lit green,
the full pack, spray particles and sponsor banners. GTE transforms, texturing
and the display list all work.

Left alone the game runs its **attract demo**, cycling title -> AI race ->
title. The demo races are genuine AI racing but only run ~25 seconds before
restarting, so they cannot demonstrate a completed lap.

RAM sampling during a race shows 20-41% of memory changing between snapshots,
consistent with a live simulation.

## How it got unstuck

The complete track set loads — `.FLR` collision, `.CAM` camera, `.TPT`,
`.TMS` textures, `.DMD` models, `VCORE.VAB` sound bank, overview map. Nineteen secondary entry points had to be added to `functions[]`, each found by
running until it faulted. `RECOMPONE_COLLECT_UNMAPPED=1` turns an unmapped call
into a logged skip rather than a crash, so a whole run's worth surfaces at once
instead of one per recompile — discovery only, since behaviour past the first
skip is not trustworthy. A normal run now survives its full timeout with zero
exceptions.

Three things were masking progress:

- After the intro finished streaming, nothing bounded `LibCdStream`, so it
  walked off the end of the disc forever (observed at lba 1.4 million) and
  `CueBin` printed a warning per sector. Megabytes of console I/O were stalling
  the process. Both fixed.
- RAM snapshots looked static between samples, which suggested nothing was
  happening. That was misleading: the run had crashed, and static menu screens
  legitimately change only ~25 bytes per sample anyway.
- The scripted press sequence interacts badly with the attract demo. Presses
  during a demo only return to the title, so a fixed-timing script can bounce
  between title and demo indefinitely. This is still unsolved; see gate 8.

## Next

1. Get a *sustained* race via `harness/find-menu-route.py`, then re-run
   `harness/verify-lap.py` against it. The lap-counter array is already located
   (`0x801744B4`, stride `0x84`); what is missing is a race long enough to use it.
2. Name the libgpu public API (`DrawOTag`, `PutDrawEnv`, `PutDispEnv`), which
   print nothing and so need shape-based identification.
3. Decide whether "a rider completes laps" is satisfied by the AI, or whether
   the player bike must be driven — the current script leaves the player idle.

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

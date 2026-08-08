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
| 2 | Reaches game code without faulting | **done** |
| 3 | PSYQ SDK calls routed to runtime | **11 routed** — full public libcd, VSync, DrawSync |
| 4 | Game progresses past init | **done** — full startup sequence, main loop running |
| 5 | Anything renders | **done** — DEVELOPED BY screen, capture automated |
| 6 | Menus navigable under scripted input | not started |
| 7 | A race loads | not started |
| 8 | **Lap counter increments** — the goal | not started |

## Where things stand

The port boots and executes the entire startup sequence, loading and MDEC-decoding
`SCEAPRES.BS` (Sony licence), `PROFILES.INI`, `SISAPROD.BS` (SingleTrac), and
`DEVELOP.BS`, then settles into a steady `VSync(0)` frame loop. MDEC decode is
verifiably correct — 1200 macroblocks and 153600 words out for a 640×480 image,
which is exactly right — and the result is DMA'd out on channel 1 and uploaded
to the GPU on channel 2.

Getting here took three genuine bug fixes in RecompOne's runtime, all found by
tracing what the game was waiting on rather than by guesswork. They are
documented in `DECISIONS.md`.

## Current blocker: libgpu's command queue never drains

The port renders — the `DEVELOPED BY` startup screen displays correctly, and
frame capture is automated (`RECOMPONE_OFFSCREEN` + `RECOMPONE_DUMP_DIR`,
nothing appears on screen). It then hangs before the title screen and loads no
further files.

Located exactly, via `RECOMPONE_TRAP_VSYNC`:

```
func_8011A840 -> func_80135280 -> func_80134DA4 -> func_8013F51C
  -> LoadImage(0x800E7B54) -> func_800E9578 -> func_800E9E04 -> VSync(-1)
```

`func_800E9578` is libgpu's 64-entry ring buffer. It computes
`(head+1)&0x3F == tail`, finds the queue full, and waits. Queue globals are
`head=0x8016BCA4`, `tail=0x8016BCA8`. The drain function is
`func_800E986C` (the only writer of `tail` on the normal path, at `0x800E9A80`),
and the wait loop **does** call it every iteration — it runs but never advances
the tail.

Ruled out so far:

- GPUSTAT bit 26 (ready to receive command) — correctly set by the runtime.
- GPU DMA raising no IRQ — correct. The game writes `DICR=0x00900000`, enabling
  only channel 4 (SPU), so channel 2 completion is not meant to interrupt.
- VBlank IRQ 0 never firing — **was** true, now fixed and verified reaching the
  game's handler at `0x800F0A20`.
- DMA CHCR Start bit left set — not the case; `PSMemory` already clears bit 24
  before running the transfer.
- The game's own `DrawSync` being needed to drain the queue — tested un-routed,
  made no difference.

Next thing to try: single-step `func_800E986C` and find which condition makes
it bail before `0x800E9A80`. It reads a hardware register via a pointer at
`0x8016BC80` and tests bit `0x01000000`, and another via `0x8016BC74` testing
`0x04000000` — identify both registers and confirm the runtime models them.

## Earlier open question (resolved)

Frame dumps came back entirely black. The answer was option 2: rendering works,
and the *capture* was blind, because with the HLE backend the frame never lands
in `gpu.Vram` — `GlCore.Present` goes straight to the screen, and `ReadVram`
returns empty even when the window is visibly rendering.

Capture now takes `glReadPixels` on the default framebuffer after `DoRender`,
which reflects exactly what is displayed. Verification is fully automated from
here on.

## Next

1. Unblock the libgpu queue (see above) — everything else waits on this.
2. Name the rest of the libgpu public API. `DrawOTag`, `PutDrawEnv` and
   `PutDispEnv` print nothing, so they need shape-based identification rather
   than string cross-reference; the band `0x800E7300-0x800E7E00` holds the
   already-named entry points.
3. Scale naming past the debug-string anchors via PSYQ signature matching.
4. Scripted pad input + memory assertions once menus are reachable.

## Performance concern

At present the port runs about **3 frames per second**, measured headless with
no GL at all, so the cost is in the recompiled CPU code rather than rendering.
Not worth chasing until the game actually progresses, but 3 fps is nowhere near
playable and it will have to be dealt with before the lap goal is meaningful.
Every generated function carries `MethodImplOptions.NoInlining`, and every
memory access goes through an `IMemory` interface call — those are the first
two things to look at.

## Harness

```bash
python harness/autorun.py --once --timeout 45   # headless run + fault triage
python harness/ppm2png.py harness/captures      # frame dumps -> PNG
```

| Variable | Effect |
|---|---|
| `RECOMPONE_HEADLESS=1` | no window, no GL — execution testing only, does not render |
| `RECOMPONE_OFFSCREEN=1` | real window parked off-desktop; renders, never seen |
| `RECOMPONE_DUMP_DIR` / `_EVERY` | display buffer → PPM |
| `RECOMPONE_LOG=sdk,cd,bios,gpu,dma,spu,mdec` | per-subsystem tracing (`all`) |
| `RECOMPONE_TRAP_CDREAD=<n>` | throw on the nth CdRead to expose the game-side call stack |

## Facts worth not rediscovering

- Boot EXE `SCUS_943.09`: text `0x800DD2D0`, size `0xEF000`, entry `0x800EC310`,
  SP `0x801FFFF0`.
- No code overlays. `QUICKY.PAC` still unexamined.
- PSYQ debug string pool at `0x800DE130-0x800DE8A8`; RCS tags date the SDK to
  late 1995 (`bios.c v1.71`, `sys.c v1.116`, `intr.c v1.73`).
- **Route the public API, never the internals** — internals take different
  arguments. Public libcd wrappers live at `0x800E30F4-0x800E36A8`.
- The game touches memory above 2 MB (`0x807Fxxxx` buffers), and the original
  crash was at exactly `0x80800000`, the 8 MB boundary. Worth understanding if
  wild pointers reappear.
- Build loop: recompile ~20 s, rebuild ~6 s.

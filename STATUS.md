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
| 5 | Anything renders | **unknown — needs one windowed run** |
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

## The one open question

Frame dumps come back **entirely black** — VRAM reads back 0% non-zero across
the whole 1024×512 buffer, even though decode and upload both succeed. Two
possibilities, and they are not distinguishable from here:

1. Rendering genuinely fails, and nothing reaches VRAM.
2. Rendering works, and only the *offscreen readback* is blind, because with
   the HLE backend active the frame lives in GL rather than in `gpu.Vram`.

Option 2 is quite plausible: `GlCore.Present` bypasses VRAM entirely. **One
windowed run settles it**, which is the single thing worth a human glance right
now:

```bash
dotnet JetMoto/bin/Release/net10.0/JetMoto.dll "JetMotoPS1image/Jet Moto (USA).cue"
```

If the licence screens appear, rendering is fine and only the capture path
needs fixing — which unblocks fully automated verification from there on.

## Next

1. Settle the render question above.
2. Name the libgpu public API the same way libcd was done, so `DrawOTag`,
   `PutDrawEnv` and `PutDispEnv` route to the runtime.
3. Scale naming past the debug-string anchors via PSYQ signature matching.
4. Scripted pad input + memory assertions once something is on screen.

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

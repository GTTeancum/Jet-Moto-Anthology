# Status

Two ports, one shared RecompOne fork (`tools/recompone-fork.patch`).

| Game | State |
|------|-------|
| **Jet Moto** (SCUS-94309) | **done** — a full 3-lap race played end to end, 2026-08-09 |
| **Jet Moto 2** (SCUS-94167) | **playable** — boots, menus, controls, races; 2026-08-09 |

## Loose files

Either game will boot from an extracted tree of loose files instead of a
bin/cue image, and prefers it when one is present. Extract with:

```bash
python tools/extract-disc.py --cue "JetMotoPS1image/Jet Moto (USA).cue" --out JetMoto_loose
```

The folder *is* the disc root — the executable and `SYSTEM.CNF` sit at the top
exactly where the disc had them, so it browses like the disc:

```
JetMoto_loose/
  SCUS_943.09        the executable, at the root
  SYSTEM.CNF
  ALPINE1/ STARTUP/ MISC/ PICKTRAC/ ...
  cdaudio/*.ogg      the soundtrack, one file per CD-DA track
  .disc/disc.json    manifest: tracks, files, sector map
  .disc/structure.bin  the sectors belonging to no file
```

Bookkeeping lives under `.disc/` rather than at the top so nothing that is not
disc content sits in the disc root.

The disc is still addressed by sector everywhere above the CD layer -- by the
game's own ISO reader, by overlay loading, by the recompiler config -- so the
tree is not just a folder of files: the runtime rebuilds a byte-faithful view of
the data track from it, at the original LBAs. `--verify` proves that, comparing
every sector against the image:

```bash
python tools/extract-disc.py --cue "JetMoto2_PS1image/Jet Moto 2 (v1.1).cue" --out JetMoto2_loose --verify
```

Both discs currently verify 100% identical (34186/34186 and 59103/59103).

Turning the redbook tracks into ogg takes the soundtrack from ~450 MB of raw
PCM to ~45 MB, and it plays: the runtime had never produced CD audio at all, so
this is music neither port had before. Files whose ISO entry points at a CD-DA
track rather than at data -- Jet Moto 2's `.DA` files each start exactly on an
audio track -- are recorded as references and served by the soundtrack.

`RECOMPONE_DISC=<path>` forces a specific disc of either kind;
`RECOMPONE_DISC_PREFER=cue` keeps the image even when loose files exist.

---

## Jet Moto 2

Boots through the logos and FMVs to the title screen, the menus respond to the
pad, and Single Track races load and play with a live HUD, lap counter and
speedometer.

```bash
dotnet JetMoto2/bin/Release/net10.0/JetMoto2.dll "JetMoto2_PS1image/Jet Moto 2 (v1.1).cue"
```

What it needed, none of which Jet Moto 1 did (details in `DECISIONS.md`):

- **The libcd HLE turned off.** Jet Moto 2 drives the CD hardware itself, and
  running both layers left the drive in two half-states. Only `VSync` and
  `DrawSync` stay routed.
- **Four CD controller fixes** in the runtime: nothing drove the sector
  streamer, the drive ignored Setmode's 2340-byte sector size, IRQ 2 was never
  delivered to the CPU, and the request-data bit rewound the sector FIFO.
- **21 overlays registered** — the shell at 0x800C10B8 and all 20 per-track
  overlays at a shared 0x801020B8, both recovered from call targets since the
  files carry no PS-X EXE header.
- **A SIO0 controller port**, which the runtime did not model at all, plus the
  `SysEnqIntRP` interrupt chain, which was stored and never walked. The game
  ships its own pad driver and needs both.

Verified under scripted pad input: the menu selection moves with left/right,
Cross confirms through Choose Race Type and the track briefing, and the race
itself runs — timer counting, lap counter, minimap and speedometer all live,
across several different tracks over a long run, with no crashes and no
unmapped calls.

Not machine-verified: a completed lap. The harness can hold the throttle but
cannot steer, so finishing a lap needs a person at the controls — the same way
Jet Moto 1's three-lap race was confirmed.

Known issue: a garbled sprite blob follows the rider in-race, most likely a
particle or spray effect. Cosmetic; the race is otherwise correct.

---

## Jet Moto

**Goal:** Jet Moto (SCUS-94309) recompiled via RecompOne, playable to the point
of completing laps as a rider.

**Reached 2026-08-09.** A full 3-lap race was played end to end — no crashes, no
unmapped calls, working controls.

---

## Gates

| # | Gate | State |
|---|------|-------|
| 0 | Recompiler produces C# | **done** — 1878 functions |
| 1 | Port project compiles | **done** — ~6 s clean build |
| 2 | Reaches game code without faulting | **done** |
| 3 | PSYQ SDK calls routed to runtime | **done** — 15 routed: libcd, libcdstream, VSync, DrawSync |
| 4 | Game progresses past init | **done** |
| 5 | Title screen renders | **done** |
| 6 | Menus navigable | **done** |
| 7 | A race loads | **done** |
| 8 | **A rider completes laps** | **done** — 3-lap race played through |

## How to run it

```bash
dotnet JetMoto/bin/Release/net10.0/JetMoto.dll "JetMotoPS1image/Jet Moto (USA).cue"
```

`RECOMPONE_FRAME_DIVIDER=2` gives 30 Hz pacing, matching the original.

Default keys: arrows = D-pad, `Z` Cross, `X` Circle, `A` Square, `S` Triangle,
`Enter` Start, `Q`/`W` L1/R1.

A log is written to `jetmoto.log` beside the binary, flushed per line so a crash
still leaves a record; the previous run is kept as `jetmoto.prev.log`.

## The bugs that mattered

All in RecompOne's runtime rather than in the port, and all found by asking what
the game was waiting on. Full reasoning in `DECISIONS.md`.

1. `LibCd.CdInit` returned 0 on success where PSYQ returns 1.
2. `LibCd.CdRead` never set `StatRead`, so callers polling `CdReadSync` for
   status `0x22` retried the same read forever.
3. `LibEtc.VSync(-1)` returned a counter only advanced by the game's own
   `VSync(0)`, deadlocking a boot-time spin.
4. BIOS `InitPAD2`/`StartPAD2` were no-ops, so the pad buffers were never filled
   — the game received no controller input at all, from a real pad as much as a
   scripted one.
5. **The pad buffer's two button bytes were swapped.** The game builds its word
   as `(buf[3] | buf[2] << 8)`, which reads as though `buf[3]` holds
   SELECT..LEFT. It does not — the game's word is byte-swapped relative to the
   standard layout, so every button landed 8 bits from where it belonged and
   Square acted as Left. Found by pressing keys and reporting the symptom, after
   a long and fruitless instrumentation-led search.
6. `LibCdStream` walked off the end of the disc forever once the intro finished,
   and `CueBin` printed a warning per sector — megabytes of console I/O that
   stalled the process.
7. Pacing: a forced vsync tick fired every 64 polls while normal frames poll ~7
   times, so the game's clock ran at close to double speed.

## Rendering

Dithering is removed outright. `Gpu.EffectiveDither` is `_dither &&
DitherEnabled` with `DitherEnabled` a `const false`, so it constant-folds away in
both the GL backend (`vDither` always 0, shader dither table unreachable) and all
four software-rasteriser sites. `GPUSTAT` bit 9 still reports what the game
requested.

The 15-bit quantisation in `quant5` is untouched, so gradients that dither used
to mask now band. Dropping that for 24-bit output is a one-line change if smooth
gradients are wanted.

## Performance

Several hundred fps offscreen with full 3D, ~100 fps headless — far above
requirement. `RECOMPONE_FRAME_DIVIDER=2` throttles to the original's 30 Hz.

## Known loose ends

- **Sound is unverified in detail.** It plays, but music and effects have never
  been checked for correctness. The 13 Red Book tracks are no longer unexercised:
  with a loose disc they are ogg files and the game plays them (the attract
  sequence starts track 10), which is music this port previously never produced.
- **`QUICKY.PAC`** in `PICKTRAC` has never been examined.
- **Diagnostic scaffolding** in `JetMoto/patches/InputProbe.cs` is unwired but
  still present.
- **libgpu is only partly routed.** `DrawOTag`, `PutDrawEnv` and `PutDispEnv`
  print nothing so were never identified; the game's own copies run instead. That
  works, so it is not urgent.
- Only one track and race type have been played through.

## Method notes worth keeping

- **Route the public API, never the internals** — internals take different
  arguments.
- **Settle ambiguous layouts from behaviour, not from reading the code.** The
  `CdRead`/`CdReadSync` swap and the pad byte order were both got wrong by
  careful reading and right by observation.
- **Trap and read the stack** beats reading generated MIPS-to-C# by hand.
- **Anchor measurements on events the program produces**, not on wall-clock
  guesses about when it will be ready.
- Several confident diagnoses here were wrong and were caught only by the next
  measurement. A cheap end-to-end test beats a clever inference.

## The fork

`tools/RecompOne/` is gitignored, so runtime fixes live in
`tools/recompone-fork.patch` against upstream `8bd2039`, restored by
`tools/apply-fork.sh`. Every hunk is tagged `[jetmoto-fork]`. No upstream PRs —
that maintainer rejects AI-authored contributions.

## Facts worth not rediscovering

- Boot EXE `SCUS_943.09`: text `0x800DD2D0`, size `0xEF000`, entry `0x800EC310`.
- No code overlays anywhere on the disc.
- PSYQ debug string pool at `0x800DE130-0x800DE8A8`; RCS tags date the SDK to
  late 1995 (`bios.c v1.71`, `sys.c v1.116`, `intr.c v1.73`).
- Public libcd wrappers live at `0x800E30F4-0x800E36A8`.
- BIOS pad buffers: `InitPAD2(0x801EA0D8, 34, 0x801EA0FC, 34)`.
- Lap counters: `0x801744B4`, stride `0x84`, one per rider.
- Build loop: recompile ~20 s, rebuild ~6 s.

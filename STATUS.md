# Status

Three ports, one shared RecompOne fork (`tools/recompone-fork.patch`).

| Game | State |
|------|-------|
| **Jet Moto** (SCUS-94309) | **done** — a full 3-lap race played end to end, 2026-08-09 |
| **Jet Moto 2** (SCUS-94167) | **playable** — boots, menus, controls, races; 2026-08-09 |
| **Jet Moto 3** (SCUS-94555) | **playable** — boots, menus, controls, races at a locked 60 fps; 2026-08-15 |


## 2026-08-17 — Correction: the capture harness was the problem

`harness/jm3-ontrack.txt` does not drive the bike. It wedges it against terrain
in the first seconds and holds it there. On the capture this investigation ran
against, the HUD reads `8:39.76` on lap `1/3` and the picture barely changes over
225 game-frames. The camera was clipped into a wall for the whole run.

A camera inside geometry renders exactly like a broken renderer: culled
backfaces, world visible through surfaces, nonsensical silhouettes. Those frames
were reported as evidence of missing polygons. They were not evidence of
anything.

Retracted as a result:

- "The dark wedges are the river." Drawn from stuck-camera frames. A wireframe
  of the same viewpoint shows continuous rock and no water in frame. Open again.
- Every missing-polygon claim sourced from `jm3-ontrack.txt`, in both directions.

Replaced by `harness/jm3-demo.txt`: zero input, let the title screen time out
into the game's own attract demo. The game drives, along the developers' racing
line, with the camera where they put it. Captures from it look correct — bike and
rider well-formed, terrain continuous, sky and HUD intact, lap and place counters
advancing — with some frames around demo crashes still to be explained.

What is measured and still stands: `dropped=0`, `stretched=61` against
`poly=118692`, no exceptions across a 420 s run, and a continuous quad mesh under
the wireframe. The `[gte]` and `[otz]` counters read zero throughout and are not
wired in this build; they are not evidence.


## Jet Moto 3

A different studio and a different engine, and it needed more of the runtime
than either predecessor. It boots through both logo movies, the legal screen and
the attract movie, into the shell; the menus respond to the pad; a race loads
and runs at 35–60 fps with the rider, the bike, the track, the gates, the
banners, a live lap timer, lap counter, position, minimap and speedometer.

```bash
dotnet JetMoto3/bin/Release/net10.0/JetMoto3.dll "Jet Moto 3 (USA).cue"
```

- Recompiles: 1534 functions plus `SHELL.BIN` as an overlay at 0x800DBB28, an
  address recovered by scoring candidates against the 14 calls the resident
  executable makes above its own end and against the overlay's own string pool.
  The game confirmed it by passing exactly that address as its load buffer.
- libcd and libstr routed. Jet Moto 3 wants the opposite treatment to Jet Moto 2:
  it uses the bulk `CdRead` API and its `CD_ready` re-enters itself from inside
  the interrupt, which deadlocks on the hardware path.
- Its own SIO pad driver runs inside the vblank interrupt chain.

### What it took, and what was wrong before

The previous version of this file said Jet Moto 3 was blocked on movie decode
speed. That was wrong. Five bugs, all interrupt or presentation timing; the full
account is in `DECISIONS.md`.

| Bug | What it looked like |
|---|---|
| Interrupts nested — hardware masks them inside a handler, this did not | a vblank fired inside the pad driver, the nested copy took the SIO byte, the outer copy spun forever. It hung at a different point every run, so it read as flakiness |
| Interrupt handlers had no stack of their own | Jet Moto 3 parks SP at the scratchpad base for a hot routine; a handler taken there pushed below the scratchpad, into nothing |
| The GPU DMA completion interrupt fired inside the write that started the transfer | libgpu restarted the same ordering table until the stack ran out |
| Both the enqueue path and the DMA handler drained libgpu's queue | the read index walked past the write index into unfilled slots — a call through a null pointer |
| `LibCdStream.Streaming` meant "a ring is configured", and the vblank rescue presented one frame in four | the whole game ran at a few frames a second. This is what the old entry was measuring and calling a decode problem |

Ten branch targets also had to be named in the config: the game tail-jumps into
the middle of neighbouring functions and the detector does not make those
addresses functions, so the dispatch landed on nothing.

Two instruments did all the work and are worth keeping:

- `RECOMPONE_TRAP_STALL=<seconds>` throws from the memory path once the game has
  gone that long without calling `VSync`, unwinding the recompiled stack with a
  sample of the addresses the spin was reading. `RECOMPONE_TRAP_AFTER=<seconds>`
  is the same thing on a timer, for a hang that keeps calling `VSync`.
- `RECOMPONE_FPS=1` reports presented frames, time spent presenting, and what
  the GPU was asked to draw. "Nothing is on screen" has two very different
  causes, and a primitive count separates them in one run.

### Performance

Locked **60 fps** in racing at **640x480 internal**, including the busiest
scenes measured, at 137,000 polygons a second with under 3 per cent of wall time
inside the presenter.

The GL backend defaults to 4x internal resolution, a sixteenfold fill-rate cost,
and that was holding the busiest scenes to **21 fps with two thirds of every
second in the presenter** -- not the game and not the recompiled code. 1x fixes
the frame rate but 320x240 upscaled is a hard sell on a modern display, so the
scales were measured rather than argued about:

| | 1x (320x240) | 2x (640x480) | 4x (1280x960) |
|---|---|---|---|
| busiest scene | 60 fps | **60 fps** | 21 fps |
| polygons/second there | 139,000 | 137,000 | 45,000 |
| time in present | 2% | 2.8% | 65% |

2x is free relative to 1x and is the default. Note the shape of that curve: 2x
to 4x is four times the fill but twenty-four times the presenter cost, so the
top setting is not simply "a bit heavier". `RECOMPONE_RES_SCALE=1|2|4` overrides
it and the in-game display settings still switch it.

### The dark wedges: not a defect

They are the river. Devil's Canyon is a river track -- the game's own loading
screen calls it "the raging Apache River... serene lakes, and wondrous rapids" --
and the dark blue-black masses lying between the sandbars are the water.

Established against a reference, finally. `RECOMPONE_SWAP_FILE=<from>=<to>`
redirects a CD file lookup, so pointing the boot logo at
`/DATA/FMV/TRKSEL/CANYON.STR` makes the intro player decode the game's own
Devil's Canyon track preview -- developer-rendered footage of this exact track,
through the game's own decoder, with no menu navigation to script. It shows the
same canyon rock above the same large dark water below. Sampled: the reference
river reads (2,2,72), (3,0,62), (2,1,33); this port's dark areas read (48,48,56),
(24,32,56), (24,32,48). Both deep blue, and the reference is lossy MDEC at a
different exposure.

The mechanism, for the record: those pixels are painted by a backdrop quad
(page (512,256), full-page UVs, neutral vertex colour) that carries the canyon
panorama with its water band, and no terrain polygon covers them because none is
submitted there. All of which is correct for water.

**The cost of not having a reference.** A day went into this on the assumption
that a large flat dark region had to be wrong. It was scenery. The check that
settled it -- play the game's own footage of the track and look -- was available
from the first hour and cost about twenty minutes once attempted. Anything that
looks like a rendering defect in a game nobody here has played should be
compared against the game's own art before it is treated as a bug.

Along the way, and worth keeping:

- The GPU primitive counters, the pixel watcher and the colour hunt
  (`RECOMPONE_WATCH_PIXEL`, `RECOMPONE_HUNT_COLOUR`) that identified the exact
  primitive.
- Headless frame capture, so investigating never needs a window again.
- 24-bit display capture, without which every movie frame is coloured noise.
- Two tests that could not fail, both of which I read as evidence before
  noticing: flat shading (cannot separate a dark polygon from a hole showing
  something dark) and a magenta back-buffer marker (overwritten by the game's own
  black clear before anything drew).


### Other known issues

- Not machine-verified: a completed lap. The harness holds the throttle but
  cannot follow a racing line.
- The intro runs about 90 seconds before the shell. Start skips the logos.
- Six branch targets past the end of the text segment are unregistered; they are
  linear sweep reading data as code, and unreachable.

---

## The shipped executables

`dist/` holds the three single-file builds (`JetMoto.exe`, `JetMoto2.exe`,
`JetMoto3.exe` — the same binary under three names, each reading its own file
name to know which game it is). They are gitignored; the release is built with:

```bash
dotnet publish Launcher/JetMotoLauncher.csproj -c Release -r win-x64 --self-contained
```

**A fresh install used to sit there doing nothing**, including the Jet Moto and
Jet Moto 2 downloads already published. `Entry.Run` calls `WaitForValidDisc`
before it opens anything, and that spins until the *runtime's* configured disc
path is a real file — the launcher only ever passed the disc to `Entry.Run` and
never set it in the runtime's configuration. Every development run had a
`settings.json` from an earlier session with the path already in it, which is
why this never showed up locally. The launcher now sets it.

Verified after the fix: `JetMoto3.exe --disc "Jet Moto 3 (USA).cue"` on an empty
folder recompiles the disc in 11 s, boots, plays the intro, and reaches a race
under scripted input with no faults over four minutes.

---

---

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

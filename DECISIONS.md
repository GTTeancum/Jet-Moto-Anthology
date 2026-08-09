# Decision log

Standing rule: I do not block on judgment calls. I pick, log here with the
reasoning, and continue. Anything here can be reversed cheaply — every entry
corresponds to committed work.

---

### 2026-08-07 — Point `cue` at the existing dump rather than copying it

The wiki's suggested layout wants a `disc/` folder inside the port project. The
dump is 400 MB and already sits in `JetMotoPS1image/`. Config points there
directly. Avoids a pointless second copy; `disc/` stays gitignored anyway in
case a future step needs a rebuilt image.

### 2026-08-07 — No overlays in the config

Walked the ISO9660 tree: the only executable is the boot EXE `SCUS_943.09`
(text `0x800DD2D0`, `0xEF000` bytes, entry `0x800EC310`). Everything else is
data — `.TMS`/`.DMD`/`.TPT`/`.FLR`/`.CAM` track geometry, `.TIM` textures,
`.VAB` banks, `.BS` MDEC stills, `.DA` streamed XA, plus 13 Red Book audio
tracks. So `"overlays": []`. This removes the single fiddliest part of a
RecompOne port. **Revisit** if a code blob turns up inside the `.DA` or
`.PAC` containers — `QUICKY.PAC` in particular is unexamined.

### 2026-08-07 — Do not set `linearSweep` yet

The wiki recommends `linearSweep: true` when no decompilation exists. It turned
out not to be needed for the first pass: with no symbols at all, RecompOne falls
back to `FunctionDetector.DetectFromScan` walking from the entry point, which
found 1462 functions and resolved 27 jump tables. `linearSweep` is documented as
liable to swallow data as code, so it stays off until there is evidence of
functions being missed — i.e. until a call lands on an address with no emitted
function.

### 2026-08-07 — Name SDK functions by cross-referencing the debug string pool

Jet Moto has no decompilation, so the plan was PSYQ library signature matching.
It turned out not to be necessary as a first move: the retail build kept the
PSYQ debug string pool at `0x800DE130-0x800DE8A8` (`ResetGraph(%d)...`,
`DrawSync(%d)...`, `VSync: timeout`, `CD_init:`, `CD_read`, `CD_ready`,
`CD timeout: `). Each string is printed by exactly the library function that
bears its name, so reconstructing `lui`/`addiu` address pairs and mapping the
reference site back to its enclosing function yields ground-truth names for
free. `harness/`-adjacent scratch tooling did this in one pass.

Signature matching is still the plan for the silent majority of the SDK — this
only names functions that happen to print something.

### 2026-08-07 — Partial per-library routing is unsafe; route whole libraries

Naming 5 functions (`CdInit`, `CdRead`, `CdReady`, `VSync`, `DrawSync`) cleared
the CD timeouts and the `0x80800000` crash, and the port went from crashing in
under a second to running indefinitely. But tracing (`RECOMPONE_LOG=sdk,cd`)
shows it makes **no SDK calls at all** after `ResetGraph` — it is spinning, not
progressing.

Diagnosis: routing splits state. HLE `CdRead` completes into runtime-side
state, while the game's own un-named `CdReadSync`/`CdSync` keep polling the
hardware state the HLE never touches, so the poll never satisfies. **Rule going
forward: name a library's functions as a set, not piecemeal.** The immediate
job is the rest of libcd — `CdReadSync`, `CdSync`, `CdControl`, `CdGetSector`,
`CdDataSync`. `func_800E3DF0` and `func_800E4344` are known libcd members
(they reference `"CD timeout: "`) and are the first candidates.

### 2026-08-07 — Headless vs offscreen are different things

`RECOMPONE_HEADLESS=1` creates no window and no GL context. Nothing rasterizes
and, more importantly, nothing pumps frames — so it is only useful for
execution-progress testing, never for "does it render".

`RECOMPONE_OFFSCREEN=1` creates a real GL context in a window parked outside
the desktop (see the `IsVisible=false` entry at the end — the first attempt
hid the window and got no rendering at all). Nothing ever appears on the
user's screen (their stated requirement) but rendering is real and frames can
be dumped. Frame dumping is
`RECOMPONE_DUMP_DIR` + `RECOMPONE_DUMP_EVERY`, written as binary PPM to avoid
adding an image-encoder dependency to the fork.

### 2026-08-07 — Suppress recompiler-output warnings in the port csproj

`NoWarn=CS0164;CS0219;CS0162;CS1717` and `Nullable=disable`. These fire on
machine-emitted code in `generated/main.cs` (unused labels, self-assignments,
unreachable branches) and would bury real diagnostics from hand-written patch
code. Nothing we author lives in `generated/`.

### 2026-08-07 — Three real bugs in RecompOne's runtime, not in the port

Worth recording because the instinct on a recomp is to assume the fault is in
your own naming or config. In all three cases the port was right and the
runtime was wrong, and each was found by asking "what is the game waiting on?"
rather than by inspection.

1. `LibCd.CdInit` returned 0 on success while `CdReset` returned 1. PSYQ's
   `CdInit()` returns 1. The game printed its own "CdInit: Init failed"
   (`0x800DE9D8`) and took a degraded boot path.
2. `LibCd.CdRead` performed the read but never set `StatRead`. Callers poll
   `CdReadSync` until the status byte is `0x22` as proof the read occurred;
   the `ReadN` command path already did this, so the omission was inconsistent
   within the same file.
3. `LibEtc.VSync(-1)` returned a counter advanced only by the game's own
   `VSync(0)` calls. On hardware VBlank advances it. A boot-time
   `while (VSync(-1) < target)` spin therefore never terminated.

None of these can go upstream — the maintainer rejects AI-authored PRs — so
they live in `tools/RecompOne/`, each tagged `[jetmoto-fork]` so they survive
a re-clone by grep.

### 2026-08-07 — Locate loops with a trap, not by reading

`RECOMPONE_TRAP_CDREAD=<n>` throws on the nth `CdRead`, and the resulting .NET
exception carries the full recompiled game-side call stack. That found the
retry loop in seconds after a fair amount of wasted time reading generated
MIPS-to-C# by hand and guessing at which function owned the loop. Generalise
this: when the question is "who is calling this", trap and read the stack.

### 2026-08-07 — IsVisible=false does not render

The first offscreen implementation created the window with `IsVisible=false`.
VRAM read back all zero. A window parked at `(-4000,-4000)` is a real window to
the driver, still never appears on the user's desktop, and is the right way to
get "headless" rendering here.

### 2026-08-07 — Two more runtime bugs: InitPAD2 and the frame limiter

**BIOS `InitPAD2`/`StartPAD2` were no-ops.** `_padBuf` was only ever assigned
by `PAD_init2` (B(15)), which Jet Moto never calls. The consequence is worth
stating plainly: the game received **no controller input at all**, from a real
pad as much as from a scripted one. Now both 34-byte buffers are recorded and
filled.

The byte order was settled from the game's own parser rather than from docs.
`func_800EF098` tests `buf[1] >> 4 == 4` for a digital pad and then forms
`(buf[3] | buf[2] << 8) ^ 0xFFFF`, so `buf[3]` looks like the low button byte.

> **Superseded 2026-08-09 — this conclusion was wrong.** That reading is exactly
> backwards. The real BIOS buffer holds SELECT..LEFT first, so the game's word is
> byte-swapped relative to the standard layout. With the order above, every
> button arrived 8 bits from where it belonged and Square acted as Left. The
> reasoning felt airtight and was not; it was corrected by a person pressing keys
> and reporting the symptom. See the 2026-08-09 entry.

**`FrameClock` capped harness runs at 60 Hz** and, in practice, ~10 fps.
Unthrottling is now the default for headless and offscreen runs, which are
harness runs by definition. The vsync counter stays wall-clock based so
anything the game times off `VSync(-1)` still takes the same real time.

### 2026-08-07 — Profile before optimising; the guess was wrong

The port looked like it ran at 3 fps, and the obvious suspects in `PSMemory`
were real: a hardware division on every memory access, a per-byte write-tracking
loop, byte-by-byte word assembly. All were fixed. It made **no measurable
difference**, because the actual cost was an unrouted `StGetNext` spinning
`0x800000` times inside another `0x800000`-iteration retry, and after that a
deliberate frame limiter.

`dotnet-stack` found both in minutes by sampling — 100% of samples in the spin,
then 4 of 4 asleep in `Throttle`. The `PSMemory` work was kept because it is
correct and will matter once real gameplay runs, but it should not have been
done first.

### 2026-08-07 — The fork is a patch, not a checkout

`tools/RecompOne/` is gitignored, which meant every runtime fix lived only in an
untracked working copy and would have been lost on a re-clone. They now live in
`tools/recompone-fork.patch` against a pinned upstream revision, restored by
`tools/apply-fork.sh`. Regenerate the patch whenever the fork changes.

### 2026-08-07 — Why linear sweep misses these, and why one-at-a-time is right

The race code kept faulting on `unmapped call` at addresses like `0x8010AB44`,
and the instinct was to bulk-harvest them rather than grind one per run. Two
attempts at that failed, and the reason is worth recording.

They are not in a static table: searching the EXE for the literal address finds
nothing, because the dispatch table is built in RAM at runtime
(`s2 = *(base + idx*4); jalr s2`). Harvesting pointer-shaped words from a RAM
snapshot produced 375 "targets", but the first several were string-pool
addresses — the filter cannot tell code from data, and guessing wrong emits
junk functions.

They are also not findable by prologue scanning. Every `addiu sp, sp, -N` in
the failing regions is *already* emitted. `FunctionDetector.LinearSweep` skips
any address covered by an existing function (`f.Start <= addr < f.End`), so
these are **secondary entry points inside functions the detector merged**.
Nothing static distinguishes them from ordinary mid-function labels; only the
fact that the game calls them does.

So an explicit `functions[]` entry per address — exactly what `autorun.py`
adds — is the correct fix, and discovering them by running until each one
faults is the only sound way to find them. It is cheap: an iteration is about
25 seconds once the log flood is gone.

### 2026-08-07 — The lap counter is a per-rider array at 0x801744B4, stride 0x84

Found by diffing RAM snapshots across a race rather than by reverse
engineering the HUD, which is what `RamDump` + `harness/findcounter.py` were
built for.

```
0x801744B4  [0,0,0,0,0,0,0,1,1,1,1,1,1,1]   rider 0
0x80174538  [0,0,0,0,0,0,0,1,1,1,1,1,1,1]   rider 1
0x801745BC  [0,0,0,0,0,0,0,1,1,1,1,1,1,1]   rider 2
0x80174640  [0,0,0,0,0,0,0,0,1,1,1,1,1,1]   rider 3
0x801746C4  [0,0,0,0,0,0,0,0,0,0,0,0,1,1]   rider 4
```

The staggering is the tell: a shared frame counter or timer would move in
lockstep, but these increment at different moments, which is what a field of
racers crossing the line at different times looks like. Stride `0x84` is the
rider struct size.

`0x80173C74` also looks lap-like but resets (…2,2,2,1,1…), so it is probably a
displayed or leader-relative lap that restarts with the attract loop. Not the
one to assert on.


### 2026-08-07 — Anchor the lap test on a race start, not on a settle window

The first automated lap check reported `LAP CONFIRMED: 0 -> 2 in 29s`. It was
wrong, and the ways it was wrong are worth keeping.

1. **It baselined on the first read.** Before the race initialises the counter
   addresses hold stale values, so "0" was not a real starting lap count and
   the jump to 2 was not two laps.
2. **It watched a single address.** Tracing the whole array showed individual
   slots *decreasing* — one went 2 back to 1. The array is sorted by race
   standings, so a slot holds "the lap count of whoever is in Nth place" and
   values swap on overtakes. Only the maximum across the array tracks progress.
3. **The second attempt still self-deceived.** Rejecting increments faster than
   a plausible lap sounds right, but the interval was measured from the moment
   of baselining, so a genuine increment just after the settle window was
   rejected with `dt = 0`. A fixed 90s settle also started *after* the first lap
   had already happened.

The test now waits for the array to read all-zero, which is an unambiguous
race-start marker, and measures from there. **The general lesson: anchor a
measurement on an event the program actually produces, not on a wall-clock
guess about when it will be ready.**

### 2026-08-09 — Goal reached, and what actually got it there

A full 3-lap race was played end to end with working controls, no crashes and no
unmapped calls. That is the definition of done set at the start.

The last blocker was not a deep one. The pad buffer's two button bytes were
swapped, so every button arrived 8 bits from where it belonged and Square acted
as Left. It was found in seconds by a person pressing keys and describing the
symptom, after hours of instrumentation had produced four confident and wrong
diagnoses in a row: a stuck debounce counter, dead-code consumers, a
state-machine dispatch failure, and the wrong title function.

The pattern in every one of those: inferring across separate runs instead of
running one cheap end-to-end test. The byte order had even been "verified" once
by reading the game's own parser arithmetic, which is exactly the kind of
careful reasoning that feels conclusive and is not.

**Rule for next time: when something has an observable end-to-end symptom, get
the symptom first.** Instrumentation is for narrowing a search once the
behaviour is known, not for establishing what the behaviour is.

---

## Jet Moto 2 (SCUS-94167)

### 2026-08-09 — Same engine, almost none of the same problems

"Pretty much the same engine" turned out to be true of the game and false of
everything the port depends on. Jet Moto 1 reaches the disc through the PSYQ
libcd HLE and the controller through the BIOS pad calls. Jet Moto 2 reaches
both through **the hardware**, and the runtime's hardware models were the parts
nobody had exercised.

Four bugs in RecompOne's CD controller, all only reachable from the hardware
path:

1. `AdvanceStreaming()` existed and **nothing ever called it**, so `ReadN`
   delivered exactly one sector and stalled. The game timed out and reissued
   the read forever.
2. `ReadNextSector` always returned the 2048-byte user area, ignoring Setmode
   bit 5. Jet Moto 2 boots asking for whole 2340-byte sectors, so every field
   it parsed was 12 bytes off and its own ISO reader never found `CD001`.
3. The interrupt flags were raised but **IRQ 2 was never delivered to the CPU**.
   A game that does its CD work from the interrupt handler therefore never
   asked for the data — zero DMA transfers in a whole run.
4. Writing the request-data bit rewound the sector FIFO. The game pulls the
   12-byte header in one DMA and the 2048-byte payload in a second, so it got
   the header twice and never the payload.

**The decision that unlocked all four: stop routing libcd.** Jet Moto 1 needed
the HLE; for Jet Moto 2 the HLE and the game's own libcd were two half-states
of one drive, which is why the boot read looped at a stale position no matter
which layer was "fixed". Only `VSync` and `DrawSync` stay routed.

### 2026-08-09 — Overlays, and a base address recovered from call targets

Jet Moto 2 is overlay-based: `/BIN/JM2SHELL.BIN` is the shell, and each of the
20 `/BIN/<track>.BIN` files is a per-track code overlay. None has a PS-X EXE
header, so the load addresses had to be inferred.

- **Shell, 0x800C10B8.** Scored candidate bases by how many `jal` targets land
  on a function prologue and how many `lui`/`addiu` constants land on a string
  in the pool at file offset 0. The winner led both by a wide margin, and the
  runtime confirmed it within 2 KB: the dispatcher only commits an overlay when
  a write lands in its first page, and the game's DMA did exactly that.
- **Tracks, 0x801020B8 — all of them.** The resident executable calls five
  fixed addresses in the gap below its own base, which only makes sense if
  every track overlay loads at one address and exports the same entry points.
  Matching those five targets against prologues in three different track files
  agreed on 0x801020B8; Nebulous puts 11 of its 12 internal calls on a prologue
  there and at most 3 anywhere else.

Overlay activation had to be added to the hardware path too — it was wired only
into the libcd HLE. **Arm on the LBA a read starts at, never on each streamed
sector**: `ICEBERG.BIN` spans sectors 489-491 and `ISLAND1.BIN` begins at 491,
so announcing every sector loaded Island 1's code over the Iceberg track that
was actually being read, and the game jumped into the wrong overlay.

### 2026-08-09 — The controller: a dead interrupt chain and a missing device

Jet Moto 2 never calls a BIOS pad function — `PadInit` is dead code in the
retail build, referenced by nothing. It ships **its own SIO driver**, and two
separate gaps kept it from ever running.

1. **`SysEnqIntRP` stored the handler chain and nothing walked it.** Only
   libetc's callback table was dispatched, so a library installing itself the
   BIOS way was silently dead. Jet Moto 2 registers one chain element at
   priority 2; with the walk added, its driver woke up and touched SIO for the
   first time.
2. **There was no SIO0 device at all.** 0x1F801040-4F fell through to the
   generic register array, so a write went nowhere and a read returned the last
   value written.

Finding the driver needed the runtime, not static analysis: it keeps the SIO
base in a *data word*, so scanning for `lui`/`addiu` pairs that build
0x1F801040 found nothing in any module, in main, shell or overlays. A one-shot
stack dump on the first SIO write named it immediately.

The subtle part was the acknowledge. The driver waits on I_STAT bit 7 for the
*previous* byte, and clears that same bit at the end of every byte it sends.
Latching the acknowledge immediately meant the driver's own clear wiped it, the
next byte's wait timed out, and every packet died two bytes in — reported as
"no controller". On hardware the pad answers ~100us later, i.e. *after* the
clear. **Deferring the latch by one observation** is what made the full
`01 42 00 00 00` exchange complete; the driver then went on to probe with 0x43,
which is a pad it believes in.

Two dead ends worth recording, both from guessing at timing instead of reading
the driver: raising IRQ7 from inside the register write (too early — the state
machine advances only after that write returns, so every packet restarted at
its first byte), and synthesising I_STAT bit 7 around the chain walk (the
cleanup cleared the bit and acknowledged the transfer on the game's behalf).

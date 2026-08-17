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

---

### 2026-08-09 — Loose files, and why a folder of files is not enough

Both ports now prefer an extracted tree over a bin/cue image. The obvious
implementation -- dump the ISO to a folder and serve files by name -- would have
worked for Jet Moto 1 and failed completely for Jet Moto 2, because almost
nothing above the CD layer asks for a *file*. The game's own ISO reader, overlay
activation, `CdSearchFile`, and the recompiler's overlay config all address the
disc by **sector**.

So the loose tree keeps the original LBAs and the runtime rebuilds the data
track from three sources: file contents from the loose files, the sectors
belonging to no file (volume descriptors, path tables, directory records) from a
small `structure.bin` — 43 sectors for Jet Moto 1, 90 for Jet Moto 2 — and a
synthesised blank sector for everything else. Sector headers are generated, so
`Setmode`'s 2340-byte whole-sector view works exactly as it does off the image.

**One seam, at `CueBin`.** Every consumer already reached the disc through it,
so backing its members with a `LooseDisc` made loose files work everywhere at
once rather than teaching each caller about a second disc format.

`--verify` compares every reconstructed sector against the image and both discs
come back 100% identical. That check is the whole reason to trust this: "the
files are all there" is not the property that matters.

Two things the extraction had to learn from the data rather than from
assumption:

- **Files that are not files.** Jet Moto 2's `.DA` entries sit past the end of
  the data track — `CANYON.DA` begins exactly at audio track 2. They name the
  redbook tracks so the game can find them by filename. The first extraction
  wrote sixteen zero-byte files and looked fine; they are now recorded as track
  references and answered by the soundtrack.
- **Form 2 sectors.** XA payloads are 2324 bytes and cannot survive a 2048-byte
  view, so any file containing one is stored as whole 2352-byte sectors and
  served back verbatim.

CD audio is a genuine gain rather than a port of existing behaviour: the runtime
accepted `CdlPlay` and produced no samples, so both games had always run without
music. It needed wiring in two places, because the two games reach the drive
differently — `CdController` for Jet Moto 2's hardware path, and the libcd HLE
for Jet Moto 1's.


---

## Jet Moto 3 (SCUS-94555)

### 2026-08-13 — A third engine, and four more runtime gaps

Pacific Coast Power & Light rather than SingleTrac, so nothing about the first
two games' layout carried over. What carried over was the method: the retail
build kept the PSYQ debug string pool, so the SDK entry points fell out of it
again.

**Route the public entry, not the printer.** 0x800810C0 prints "CdRead: retry"
and is *not* CdRead -- it is the internal CD_read. Routing it handed the HLE the
internal's arguments, which read a sector count of zero, so SHELL.BIN never
loaded and the shell's jump tables were still zeroed RAM. The two functions game
code actually calls are 0x80081448 and 0x800815E4. The old lesson, relearned:
the string tells you which function prints it, never which one is public.

**Jet Moto 3 wants the opposite of Jet Moto 2.** Jet Moto 2 drives the CD
hardware and routing libcd broke it. Jet Moto 3 uses the bulk CdRead API and its
CD_ready re-enters itself from inside the interrupt while waiting for the next
sector -- which on the hardware path cannot arrive until the handler returns and
acknowledges. Routing libcd takes the interrupt out of the loop entirely. There
is no single right answer for a game; there is only what its libcd does.

Four gaps in the runtime, all of them things neither earlier game exercised:

1. **Sector filtering was stored and never applied.** Setfilter's file and
   channel were recorded and reported but every sector still went to the CPU,
   including XA audio. Fatal for a game streaming .STR movies.
2. **I_STAT was raised only around the interrupt-chain walk**, then dropped
   before the callback-table handler ran -- so libcd saw no CD interrupt
   pending, never acknowledged it, and the drive stalled with the flag stuck
   set. My own regression from the Jet Moto 2 pad work.
3. **VBlank only fired when the game called VSync.** Both earlier games poll
   VSync(-1) constantly so this was invisible. Jet Moto 3's boot loop spins on a
   byte its VBlank handler clears and calls nothing at all while waiting.
4. **The display only updated on VSync(0).** A PS1 scans VRAM out continuously
   and a game need never call it; Jet Moto 3's movie player syncs through its
   VBlank handler instead.

Gaps 3 and 4 are pumped from the memory path, because reads are the only thing
such a loop does. The first attempt did that unconditionally and **broke Jet
Moto 1** -- injecting interrupts underneath a game that is driving its own
vblank is both unnecessary and destabilising. It is now gated on eight fields of
silence from the game, so it is a rescue for starved loops and invisible to
everything else. Both earlier ports were re-verified rendering correctly
afterwards.

### Still blocked

The movie decodes and never appears. Geometry is right, the display is enabled
and in 24-bit mode, the HLE GPU backend is active -- but its VRAM texture never
receives the blit. Worth noting for whoever picks this up: naming ResetGraph,
LoadImage, ClearImage, StoreImage and MoveImage in the config does nothing.
SdkPatches only reimplements DrawSync and VSync out of libgpu; Jet Moto 1's
config names the others as documentation and they have never been routed.


### 2026-08-14 — Jet Moto 3: a picture, but not a playable one

The blocker from last session is solved. The movie was decoding into VRAM
correctly all along -- all 20 strips of every frame -- and being thrown away,
because the GL backend's 24-bit present path never produced a picture and the
software VRAM shadow it could have fallen back to was deliberately not written
under the HLE backend. Mirroring CPU-to-VRAM uploads into the shadow and
presenting 24-bit from there put the Pacific Coast Power & Light logo on screen.
The GL 24-bit path itself is still broken and is now bypassed rather than fixed.

Then the frame rate. Profiling put 100% of samples inside `LibCdStream.StGetNext`:
the movie player spins there up to eight million times waiting for a frame, and
the stream feeder was paced to the drive's exact sector rate. A 320x240 movie
needs about 150 sectors a second and a 2x drive delivers exactly 150, so there
was no headroom at all -- any jitter left the ring empty and the player burnt its
whole stall timeout before each frame. The ring's own busy flags are already
backpressure, so the pacer bought nothing; removing it took delivered frames from
77 to 242 in the same wall time and got the boot as far as the second movie.

That was not enough. Presentation, not decoding, is now the limit at ~2 fps, and
I did not isolate where the time goes. **Jet Moto 3 is not playable and I am not
going to claim otherwise.** The game will not advance past its intro, and
skipping the movie does not work either -- with the ring reported empty and the
.STR files reported absent it retries rather than taking a missing-file path.

Two things worth knowing for whoever continues:

- A game can spin in a wait loop that touches no memory and calls nothing but a
  routed SDK function. The vblank clock added last session hangs off memory
  reads, which is useless there; it is now also pumped from `StGetNext`. Any
  other spin of that shape will need the same treatment.
- The vblank rescue must stay gated. Pumping it unconditionally broke Jet Moto 1
  outright, and an eight-field gate throttled Jet Moto 3's intro to two frames a
  second. It sits at two fields, which is narrow enough for a game that calls
  VSync once a frame and wide enough that a game polling it several times a
  frame never triggers it.


### 2026-08-14 — Siloing, and the limit of the movie work

The first thing this session should have had, and did not: **per-game
isolation**. Changes made for Jet Moto 3 were landing on all three ports, which
is exactly how a vblank fix broke Jet Moto 1 outright. `GameQuirks` now holds the
three behaviours Jet Moto 3 needs -- a vblank clock for loops that call nothing,
24-bit output from the software VRAM shadow, and an unpaced stream feeder -- all
defaulting to false. Jet Moto 1 and 2 opt into none of them and run the code they
ran before any of this existed. That should have been the shape from the start;
a shared runtime with three disagreeing consumers needs it.

Siloing immediately paid for itself. The vblank rescue had been gated on "the
game has not called VSync for two fields" purely to protect the other two ports.
Once the quirk made that protection unnecessary the gate came off, and vblanks
went from 300 to 5900 in the same wall time -- the boot sequence then reached the
second movie for the first time.

It still is not playable, and the table in STATUS.md lists every avenue tried.
The short version: decode and blit manage two or three frames a second against
the fifteen the player wants, and every attempt to close that gap either broke
rendering or was ignored by the game. Skipping does not work either -- the player
retries when starved, and stubbing it out leaves the game rendering nothing at
all, which says the movie has side effects the rest of the boot depends on.

What is left is real work on the movie pipeline itself: where MDEC decode and the
twenty-strip VRAM blit actually spend their time. I did not isolate that, and
without it the rest -- menus, controls, racing, packaging -- cannot start.


### 2026-08-14 (later) — Jet Moto 3 was never a movie-decode problem

The previous entry concluded that the intro was too slow to decode and that the
movie pipeline needed real work. That was wrong, and it was wrong because the
measurements were taken from outside. Three bugs, all interrupt timing, all
found by asking the program what it was waiting on:

**A hang trap.** `RECOMPONE_TRAP_STALL=<seconds>` throws from the memory path
once the game has gone that long without calling `VSync`. The exception unwinds
the recompiled call stack, so a hang arrives named instead of guessed at. It
carries a one-in-64 sample of recently-read addresses, which is what turned
"stuck somewhere in the shell" into "spinning on `JOY_STAT` bit 1". Everything
below came from that one instrument; none of it was reachable by reading code.

**Interrupts nested.** Hardware masks interrupts while an exception handler
runs. Nothing here did, and it mattered because the vblank rescue fires from
the memory path — so a handler that reads memory could take a second vblank
inside the first one. Jet Moto 3's pad driver runs in the vblank chain and walks
the SIO port a byte at a time; the nested copy consumed the byte and the outer
copy spun on a receive flag that would never be set again. That was the stall
that made every run look nondeterministic: it fired at a different point each
time, sometimes before the first movie, sometimes after the second. Nested
requests are now recorded and taken as the handler unwinds, which is what the
hardware does with an interrupt that arrives while masked. IRQ 7 stays exempt —
the controller acknowledge is drained a byte at a time from inside the call, and
that nesting is deliberate.

**The GPU DMA interrupt fired inside the write that started the transfer.**
Real transfers take microseconds; the handler runs long after the code that
kicked it has returned. Raising it inline re-entered libgpu before the request
it had just started was marked in flight, so the handler started the same
ordering table again — and again, until the stack ran out.

**Even deferred, the libgpu queue desynchronised.** It is drained from two
places: after every enqueue, and from the DMA-done handler. When the transfer
completes inside the register write, both drains can run over the same entry,
and the read index walks past the write index into slots nobody filled — a call
through a null function pointer. Suppressing the interrupt for the GPU channel
leaves one drain, which is correct here precisely because the transfer really is
already finished.

With those three, the intro plays to the end, the legal screen renders, and the
game loads its shell and starts the attract movie.

**A fourth, cheaper one:** `LibCdStream.Streaming` was `_active`, which a game
sets once and need never clear. Jet Moto 3 leaves the ring configured for the
whole session, so the reduced streaming vblank rate applied to the menus and to
racing as well — the game ran at a few frames a second everywhere, which is what
"the port is slow" had actually been measuring. It is `_active && _reading` now.

**Process note, learned the hard way:** `tools/apply-fork.sh` resets the
`tools/RecompOne/` checkout to the pinned upstream commit and re-applies the
committed patch. Running it with uncommitted runtime work in the tree destroys
that work. Regenerate the patch instead, and stage untracked files first or the
new files are silently missing from it:

```bash
cd tools/RecompOne && git add -A -- . && git diff --cached HEAD > ../recompone-fork.patch && git reset -q
```


### 2026-08-14 (later still) — present on the game's swap, and a camera that fooled me

Two things after Jet Moto 3 reached its first race.

**Presenting on a clock shows half-drawn frames.** The vblank rescue presented
on a timer, because Jet Moto 3 never calls `VSync(0)` and there was nothing else
to hang presentation off. A timer slow enough not to tear held the game to 15
fps; a timer fast enough for 60 sometimes caught a frame mid-draw, which showed
as a band of the other buffer along the top of the screen. The display origin
moving is the buffer swap, so that is the moment a finished frame exists: the
presenter now watches for it, and falls back to a timer only for a screen that
never swaps.

**A fixed camera is not a rendering bug.** Most of a session went into "the bike
and the near track are not drawn", from screenshots that showed only distant
scenery with a live HUD. They were the out-of-bounds camera: the scripted input
can hold the throttle but cannot follow a racing line, so it drove into the
water within seconds every time, and the game showed a static scenic view while
the rider respawned. The GPU primitive counter added to `RECOMPONE_FPS` is what
settled it — a hundred polygons a frame in that state, twelve hundred once the
bike was actually on the track. The lesson is the one already in STATUS.md and
it still had to be relearned: measure the thing, do not infer it from a picture.


### 2026-08-14 (evening) — the frame rate was the renderer's resolution

Jet Moto 3's busiest racing scenes ran at 21 fps with two thirds of every second
inside the presenter. I had been reading that as the port being slow. It was the
GL backend's 4x internal resolution — a sixteenfold fill-rate cost, on by
default. At 1x the same scenes hold 60 fps at 139,000 polygons a second with
presentation down to 2 per cent of wall time.

1x is not a compromise here: it is the resolution the console rendered at, so
for a port meant to match retail it is the correct default. It is a per-game
quirk, and the in-game display settings still switch it.

The lesson is an old one in a new place. Every earlier measurement of "how fast
is this" was taken with the presenter counted but never questioned, and the
counter that finally exposed it (`RECOMPONE_FPS=1`, reporting time *inside*
present alongside the frame count) had been added for a different investigation
entirely.

**The dark wedges on the track are not solved.** The elimination table lives in
STATUS.md. Two things worth carrying forward:

- Forcing NCLIP to never cull draws every polygon the game submits, and the
  wedges survive that. So they are not geometry this port is dropping — which
  rules out winding, culling and the extent rule in one measurement, and is the
  single most useful thing anyone can know before picking this up again.
- Twice I concluded from a screenshot that the wedges were "textured polygons
  sampling black", on the strength of flat-shading turning the whole scene one
  grey. That test cannot distinguish a dark polygon from a hole showing a dark
  polygon behind it, and I read it as evidence twice before noticing. A test
  that cannot fail is not evidence.

**And a self-inflicted one:** the texture-page and CLUT census I added for that
investigation was a locked dictionary write per textured polygon, tens of
thousands a second, running on every game's hot path with no way to turn it off.
Instrumentation that is always on is a feature, and needs to be costed like one.


### 2026-08-15 — the wedges, and a correction

Still not solved. What changed is that one of the things I told the user was
established turns out not to be, and that is worth more than another hypothesis.

I marked the back buffer magenta before each frame, saw no magenta survive
anywhere, and concluded the wedges were drawn polygons rather than holes. Jet
Moto 3 clears every frame with an untextured black 512x240 rectangle -- GP0(0x60),
not a fill, which is why the fill logger never saw it -- and that clear
overwrote the marker before any game geometry drew. The test could not have
produced magenta whatever the answer was. It proved nothing.

That is the second time in this investigation I read a test that cannot fail as
evidence; the first was flat shading, which cannot distinguish a dark polygon
from a hole with something dark behind it either. Both times the mistake had the
same shape: I checked whether the result was *consistent* with my hypothesis
instead of asking what the test would have shown if the hypothesis were false.

Genuinely new, and worth having:

- The frame clear is a black rectangle primitive, so a hole shows pure black.
  The wedges measure (0,8,24). They are not the clear showing through.
- The display is 512x240 in some phases and 320x240 in others, double buffered
  at VRAM y=0 and y=240. Two pixel-watch runs looked in the wrong place because
  I assumed 320 throughout.
- One VRAM-to-VRAM blit in an entire race, and no CPU upload lands in the
  framebuffer during one. Neither paints the wedges.
- `RECOMPONE_WATCH_PIXEL` logs every primitive that writes a given VRAM pixel
  with its full state, and is validated: 400 writes captured on a pixel known to
  be painted. Pointed at a wedge pixel, with the display mode read first rather
  than assumed, it should end this in one run.

Stopping here rather than spending another cycle: the loop is four minutes per
attempt and the last three attempts failed on targeting rather than on the
hypothesis, which is a sign to hand over a working instrument instead of more
guesses.


### 2026-08-15 (later) — the wedges were the river

Not a bug. Devil's Canyon is a river track, and the dark masses on the track are
the water. The game's own loading screen says so in as many words: "the raging
Apache River... serene lakes, and wondrous rapids".

What finally settled it was getting a reference, which I had said for hours I
lacked and never went after. `RECOMPONE_SWAP_FILE=<from>=<to>` redirects a CD
file lookup; pointing the boot logo at `/DATA/FMV/TRKSEL/CANYON.STR` makes the
intro player decode the game's own track preview for this exact track. Developer
footage, through the game's decoder, no menus to navigate. It shows the same
canyon rock over the same large dark water. The reference river samples
(2,2,72), (3,0,62), (2,1,33); this port's dark regions (48,48,56), (24,32,56),
(24,32,48).

The lesson is not subtle. A day went into proving a rendering defect existed,
against scenery, because I assumed a large flat dark region had to be wrong in a
game I have never seen running. The check that settled it was available from the
first hour and took twenty minutes. **Before treating something as a rendering
bug, compare it against the game's own art.** The disc is full of authored
reference -- loading screens, track previews, attract footage -- and all of it
decodes through machinery this port already has.

Two secondary lessons, both about tests that cannot fail:

- Flat shading cannot separate "a dark polygon" from "a hole showing something
  dark behind it". I read it as evidence for the first, twice.
- A magenta back-buffer marker cannot survive the game's own black clear
  rectangle, which runs before anything else draws. I read its absence as proof
  the wedges were drawn geometry.

Both times the error was checking whether the result was consistent with the
hypothesis rather than asking what the test would show if the hypothesis were
false.

## 2026-08-17 — The missing-geometry investigation was run against a wedged bike

**Decision:** `harness/jm3-ontrack.txt` is retired as an evidence source. Visual
judgements about Jet Moto 3 are made from `harness/jm3-demo.txt` (attract mode)
or from a viewpoint whose validity is independently established.

**What happened.** `jm3-ontrack.txt` holds throttle and taps steering on a fixed
schedule. That does not drive a Jet Moto bike. It wedges it against terrain in
the first seconds and holds it there for the rest of the run. Measured on the
capture used for days of this investigation: at dump 360 the HUD reads `8:39.76`
on lap `1/3`, and the mean luma change between dumps 225 game-frames apart is
about 5/255. The bike is stationary against a wall with the camera clipped into
or against geometry.

A camera inside geometry renders exactly like a broken renderer. Backfaces are
culled, so surfaces vanish; the world behind them is visible through the gap;
silhouettes make no sense. Every one of those symptoms was reported, in good
faith, as evidence of missing polygons in the port — including the frames handed
to the user.

**What this invalidates.**

- The "dark wedges are the river" conclusion. It was drawn from stuck-camera
  frames. A wireframe capture of the same viewpoint shows continuous rock mesh
  and no water anywhere in frame. Not established either way; treat as open.
- Every "missing polygon" and "nonsensical geometry" claim sourced from
  `jm3-ontrack.txt` captures, in both directions — the claims that something was
  broken and the claims that nothing was.

**What survives.** The wireframe overlay itself is sound and produced the one
solid negative result here: over the visible terrain in the captured frame the
mesh is continuous, a regular grid of quads split into triangles, with no gaps.
Whatever is or is not wrong, that surface was not full of holes. The GPU
counters also stand: `dropped=0`, and `stretched=61` against `poly=118692`.

**Cost.** Days. The root error is methodological and worth stating plainly: I
built a capture harness, never verified that it produced a valid viewpoint, and
then spent the entire investigation instrumenting the renderer to explain
artefacts that the harness was creating. The check that would have caught it —
read the lap timer on my own screenshot — was available in every single frame.

## 2026-08-17 — "Oversized triangles overdraw the terrain" is NOT proven

Stated to the user with more confidence than the evidence carried. Three tests
were run to prove it. All three were invalidated by setup errors of mine, not by
the game:

1. **Suppression A/B** (`RECOMPONE_SKIP_BIG=250`, compare frame 0410 with and
   without). Invalid: the two runs were not the same moment or even the same
   track -- baseline `0:50.86`, suppressed `0:48.80`, different minimaps. The
   claim that the attract demo is deterministic by frame index is wrong; it was
   based on two runs happening to land on the same track.
2. **Pixel watcher** at (60,180). Invalid: that coordinate is inside the 64x64
   HUD speed gauge. The log contains the black clear and the gauge, and no
   terrain primitive at all.
3. **Self-targeting colour hunt** (`RECOMPONE_HUNT_COLOUR`), added specifically
   to stop me choosing coordinates by hand. Produced zero `[pixel]` lines across
   a 400 s run. The hunt path did not fire; cause not yet established.

**What is actually established:** one wireframe frame (`0:50.86`) in which the
corrupted region carries long red oversized-triangle edges and almost no green
mesh, while correct terrain in the same frame is dense green. That is
suggestive. It is one frame and it is my interpretation of it. It is not proof,
and the user was right to refuse it.

**What is still true and measured:** the defect is real and reproducible in
attract mode, on a viewpoint that cannot be blamed on the input harness. The
software rasterizer's span-drop is now counted (360/sample, previously reported
as 0 because `DroppedSpan` was only wired in the GL path). Both silent rejection
paths were ruled out as the cause of the holes via `RECOMPONE_MARK_DROP`.

**The pattern to stop repeating.** Five consecutive investigations here have
died on instrumentation defects rather than findings: a trap clock that never
started, a counter wired to the wrong render path, a viewpoint from a wedged
bike, a watched pixel under the HUD, and a colour hunt that never fired. Each
cost a full run. Before the next probe, verify it fires on a case with a known
answer -- the primitive trap was only trusted after it was made to fire at a
5 s gate first.

## 2026-08-17 — PCSX-Redux as the reference renderer

The user approved installing a reference PS1 emulator, which is what
OPEN-QUESTIONS.md asked for. PCSX-Redux was chosen because it has Lua scripting,
a GDB server, memory watchpoints and a VRAM viewer, so the same attract demo can
be driven and dumped on a schedule and diffed frame-for-frame against ours.
Cloned to `tools/pcsx-redux/` and gitignored, same treatment as
`tools/RecompOne/`.

**Three local build fixes.** The checkout is gitignored, so a re-clone loses
these. Recorded here so they survive.

1. **NuGet packages are not restored by a plain build.** These are
   packages.config projects, so `-t:restore` alone is not enough:
   `MSBuild vsprojects/pcsx-redux.sln -t:restore -p:RestorePackagesConfig=true`.
2. **The repo targets platform toolset v145**, newer than VS2022's v143. Build
   with `-p:PlatformToolset=v143`. Retargeting the projects on disk would work
   too but the override keeps the tree clean.
3. **`src/core/isoffi.lua` exceeds MSVC's C2026 string literal limit** at 18169
   bytes. The file is embedded as one raw string via `#include` inside
   `static const char* isoFFI = ( ... );`, using a trick where the first line
   reads as `--lualoader, R"EOF(--` — a C++ pre-decrement plus comma operator in
   C++, and a comment in Lua. Fixed by inserting the same trick at the blank
   line 178 to split it into two adjacent literals, which C++ concatenates:
   `-- )EOF" R"EOF(--`. The Lua bytes are unchanged; the inserted line is a Lua
   comment.

Do not build the `.vcxproj` files directly. Include paths use `$(SolutionDir)`,
which is empty outside a solution build, and every include fails. Build the
`.sln`.

**Also note for future build checks:** piping MSBuild through `tail` makes `$?`
report tail's status, not MSBuild's. Two builds here were read as succeeding
when they had failed.

## 2026-08-17 — OpenBIOS built; reference capture not yet dumping frames

OpenBIOS builds and works. PCSX-Redux boots Jet Moto 3 from the disc: CD-ROM ID
`SCUS94555`, label `JM3_FINAL1V4`, `OpenBIOS detected (87c3ec0f)`.

**Toolchain version matters.** MIPS GCC 16.2.0 fails to link the `shell`
subproject: newer binutils rejects the same linker script passed twice, and
`common.mk` passes both `nooverlay.ld` and `shell.ld`. GCC **12.2.0** builds it
clean. Install with `./mips.ps1 install 12.2.0` (no leading `v`, the script adds
it). Use the `install` command, never `self-install` -- the latter is the only
path that calls `Add-Path` and writes the user's persistent PATH. `install`
stays inside `tools/pcsx-redux/`.

**Run flags that matter.** `-no-ui` segfaults with the GL backend (no window, no
GL context), so headless requires `-softgpu`. Without `-dynarec` the CPU runs
`Interpreted`, which is the slowdown this emulator is known for. Lua `print` is
unavailable inside an event listener and errors surface only as "Error in event
listener" with no detail -- use `PCSX.log`, and read the GUI's Lua console,
which showed the real message (`unfinished string`) when `-logfile` did not.

**Where it stands:** `harness/jm3-refcap.lua` loads without error and its
`[refcap] armed` line appears, but zero frames are written and no failure is
logged. The listener is either not firing or `takeScreenShot` is failing inside
a `pcall` whose false branch returns silently -- a blind spot built into the
script, and the same class of mistake as every other dead probe in this
investigation. Fix that logging first, before anything else.

## 2026-08-17 — RETRACTION: the saturation fix is dead code for Jet Moto 3

Reported to the user as "one real bug found and fixed", with the claim that sky,
rock, sand and riders now render at the reference's brightness. **That claim was
wrong** and is withdrawn.

The user asked for visual proof, which is what exposed it.

`RECOMPONE_MARK_WRAP=<threshold>` paints any modulated pixel above the threshold
bright green, inside the same `if (!raw)` block the fix lives in. Results over
full demo runs:

- threshold 255 (the real wrap point): 0 marked pixels in 397 frames.
- threshold 100: 0 marked pixels in 129 frames.
- threshold 1, which must mark essentially every modulated pixel: **0 marked
  pixels in 260 frames.**

The binary was confirmed current (DLL newer than source) before trusting this.
So the `!raw` branch never executes for textured triangles in this game -- Jet
Moto 3 draws its terrain with raw textures, which take no vertex-colour
modulation at all. The wrapping was a genuine defect in the code and the
saturation is correct hardware behaviour, so the change is kept, but **it fixes
nothing here.**

**The brightness improvement was not real either.** Mean luma 78.3 -> 89.3 was
measured across `/tmp/demo` versus `/tmp/fixed`, two different runs. Attract mode
cycles tracks, so those were different scenes. This is the same track-cycling
trap already documented twice in this file, and I walked into it a third time
while trying to demonstrate a fix.

**What actually still stands, and it is not nothing:**

- Nothing is undrawn. `RECOMPONE_MARK_UNDRAWN` over 44 frames that contain the
  dark regions: zero magenta pixels in every one, including a frame that is 90%
  dark region. The back buffer is fully painted every frame.
- The reference renderer works and shows the correct appearance.
- The old harness never drove the bike, and `dropped=0` was measuring the wrong
  render path.

**The lesson, stated plainly because it keeps recurring.** Every probe must be
proven to fire before a null is believed -- that rule was already written down,
and it caught this one. But a *positive* result needs the same treatment: I
verified the fix by eyeballing two captures and computing a statistic across
them, without checking they showed the same scene. A fix is not demonstrated
until the code path it touches is shown to execute.

## 2026-08-17 — UN-RETRACT: the saturation fix is live. The retraction was my bug.

The previous entry retracted the saturation fix on the grounds that
`RECOMPONE_MARK_WRAP` never fired. **That retraction was wrong and is itself
withdrawn.**

The probe writes its marker as `(0,255,0)`, but 15-bit packing sends 255 through
`>> 3` to 31 and back to **248**. The dumped PPMs can never contain `(0,255,0)`.
I scanned for a colour that cannot exist. Rescanning the same captures for
`(0,248,0)`:

| threshold | frames marked | sampled px |
|-----------|---------------|------------|
| 255 (real wrap point) | 75 of 413 | 3851 |
| 100 | 115 of 261 | 378213 |
| 1 | 118 of 260 | 780515 |

So the modulation product does exceed 255, the pre-fix code did wrap those to
black, and the fix corrects real pixels. It is not dead code.

**But the scale matters, and this is the part to keep straight.** Sampling every
third pixel in both axes covers a ninth of the frame, so 3851 marks is roughly
34000 real pixels spread over 75 frames -- a few hundred per affected frame. The
fix is real and minor. It does **not** explain the large dark regions, which run
from 16% to 90% of a frame. The claim that it made sky, rock and sand match the
reference stays withdrawn; that comparison was two different scenes.

## 2026-08-17 — What actually paints the dark regions

`RECOMPONE_LOG_COLOUR=3104`, now deduplicating by primitive so its cap is not
spent on 40 copies of the first painter. Distinct painters of 0x0C20:

- **24x textured triangles, `colour=(128,128,128)`, `page=(512,256)`,
  `clut=(768,387)`, 8-bit.** Neutral modulation means output equals the texel,
  so the sampled texture data is itself dark navy.
- **14x untextured flat triangles, `colour=(0,13,25)`.** `To15(0,13,25)` is
  exactly 3104. The game specifies this colour itself.
- 2x full-screen textured backdrop rects, `page=(320,0)`, `clut=(768,385)`.

The untextured ones are the game's own choice of colour and we reproduce it
faithfully; they are only wrong if the reference shows them brighter. The
textured ones are the real suspect: if the texture page or CLUT address is off,
we sample dark texels where the art is bright.

**Next step, and it is now a direct comparison rather than a guess:** dump VRAM
page (512,256) with CLUT (768,387) from our runtime and from PCSX-Redux and put
them side by side. Same texture, same CLUT, two renderers. If ours is dark and
the reference is not, the addressing is wrong and that is the bug.

## 2026-08-17 — FOUND: 0x0C20 is the game's clear colour, drawn as a polygon

Area accounting on `RECOMPONE_LOG_COLOUR=3104` (counting matching pixels per
primitive, not just first sighting) names the painter beyond argument. The top
four primitives, 11% each and 44% together:

```
tri tex=False colour=(0,13,25) xy=(0,0)(320,0)(0,240)
tri tex=False colour=(0,13,25) xy=(320,0)(0,240)(320,240)
tri tex=False colour=(0,13,25) xy=(0,240)(320,240)(0,480)
tri tex=False colour=(0,13,25) xy=(320,240)(0,480)(320,480)
```

Two full-screen quads, one per framebuffer, each split into two triangles,
flat-filled with (0,13,25). `To15(0,13,25)` is exactly 3104 = 0x0C20.

**That is the screen clear, and the game does it with a polygon rather than a
fill rectangle.** Which explains the fill trace finding exactly one fill in 400
seconds, a result that was recorded as odd and never followed up.

**So the dark regions are bare background.** They are the clear colour showing
through in places where no geometry was drawn on top. Geometry really is missing
there. The user said so from the first screenshot and was right.

## RETRACTION: "nothing is undrawn" was wrong

The `MARK_UNDRAWN` result -- zero magenta across 44 frames containing the dark
regions, including one 90% dark -- proves nothing. The marker is written to the
back buffer at flip time, and the game's very first primitive each frame is a
full-screen clear quad that paints over all of it. The magenta could never
survive regardless of what geometry did or did not draw.

This is the *same* objection I raised against `MARK_UNDRAWN` early on, dismissed
after the fill trace showed only one fill rectangle. The dismissal was wrong
because I assumed a clear must be a fill rectangle. It is a polygon here.

**A working version of that probe** must distinguish "painted only by the clear
quad" from "painted by real geometry". Tag the clear primitive -- it is
identifiable by being untextured, flat, colour (0,13,25), and covering the whole
framebuffer -- and count pixels whose last writer was that primitive. Those
pixels are the hole, measured directly.

That number, per frame, against the reference, is the remaining task.

## 2026-08-17 — The defect, quantified: up to 49% of the frame is bare clear

`RECOMPONE_HOLES=1` tags the screen-clear quad and tracks, per pixel, whether
the clear is still the last thing that wrote it at end of frame. Those pixels
are holes, measured directly. The clear is identified **geometrically** -- an
untextured primitive whose bounding box covers at least 95% of the draw area --
rather than by colour, because the clear colour differs between tracks and a
colour key would silently miss most of them.

In-race sample, one line per 60 frames:

```
t=249.7  49%  (38061/76800)
t=250.7  33%
t=251.7  46%
t=252.7  16%
t=253.7  28%
t=254.7  48%
t=255.7  10%
t=256.7   0%  (458/76800)
t=257.7   0%  (0/76800)
t=258.7   2%
```

Nearly half the frame, in the worst samples, is background that nothing drew
over. It swings between 0% and 49% within a couple of seconds. This is the first
number in this whole investigation that measures the reported defect directly
instead of standing in for it.

**Caveat to settle before drawing conclusions from it.** Some clear-owned area
may be legitimate: if a frame's backdrop quad is not drawn, sky would read as a
hole and be counted here. The backdrop is a real primitive (page (512,256), the
cloud panorama), so it should cover sky, and frames reaching 0% show full
coverage is achievable. But confirm what the 49% frames actually look like, and
what the same measurement gives on PCSX-Redux, before treating 49% as entirely
defect.

The reference comparison is now cheap and meaningful: run the same probe idea
against the emulator, or simply check whether reference frames of a comparable
scene show any background at all.

### The caveat is settled: it is not sky, it is the entire 3D scene

Rendering the worst frame from the holes run (frame-0210, 94% bare clear)
answers it outright. The HUD draws perfectly -- lap timer 0:58.00, lap 1/3, 1st
place, speed 88, minimap, the "PRESS START" prompt. The **entire 3D world is
absent**: no terrain, no sky backdrop, no bike, nothing.

So the clear-owned area is not legitimate unpainted sky. And the defect is not
"some polygons are missing". Whole frames render with no 3D content at all,
while other frames in the same second render 16%, 33% or 0% bare. The game is
simulating correctly throughout -- the timer advances and the speedometer reads
88.

That reframes the target. The 2D HUD path works every frame. The 3D scene path
intermittently produces nothing. A per-polygon explanation cannot produce a frame
that is 94% empty with a perfect HUD; something is dropping the whole 3D
submission.

**Where to look, in order:**

1. The ordering table handoff. These games build an OT in one buffer while
   drawing the other. If the buffers are swapped wrongly, or the OT is cleared
   before it is drawn, a frame renders empty while the HUD -- typically
   submitted separately or later -- still appears.
2. The `DrawOTag` DMA itself: a linked-list transfer that terminates early, or
   is skipped, loses the whole chain. `GpuCounters.Polys` per frame would show
   this immediately -- a near-zero frame among normal ones.

The cheapest next probe is per-frame primitive counts alongside the holes
percentage. If polygon submission collapses on the empty frames, the fault is
upstream in the OT or its DMA; if the polygons are submitted but not drawn, it
is in the rasteriser's handling of them.

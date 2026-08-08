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
`(buf[3] | buf[2] << 8) ^ 0xFFFF`, so `buf[3]` is the low button byte. A first
attempt had bytes 2 and 3 swapped, which produced a plausible-looking buffer
the game silently ignored — the same class of mistake as `CdRead`/`CdReadSync`.
**Generalise: when a name or layout is ambiguous, the call site decides it.**

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

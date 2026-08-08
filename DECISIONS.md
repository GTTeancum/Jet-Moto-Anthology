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

`RECOMPONE_OFFSCREEN=1` creates a real GL context in a window with
`IsVisible=false`. Nothing ever appears on the user's screen (their stated
requirement) but rendering is real and frames can be dumped. Frame dumping is
`RECOMPONE_DUMP_DIR` + `RECOMPONE_DUMP_EVERY`, written as binary PPM to avoid
adding an image-encoder dependency to the fork.

### 2026-08-07 — Suppress recompiler-output warnings in the port csproj

`NoWarn=CS0164;CS0219;CS0162;CS1717` and `Nullable=disable`. These fire on
machine-emitted code in `generated/main.cs` (unused labels, self-assignments,
unreachable branches) and would bury real diagnostics from hand-written patch
code. Nothing we author lives in `generated/`.

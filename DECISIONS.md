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

### 2026-08-07 — Suppress recompiler-output warnings in the port csproj

`NoWarn=CS0164;CS0219;CS0162;CS1717` and `Nullable=disable`. These fire on
machine-emitted code in `generated/main.cs` (unused labels, self-assignments,
unreachable branches) and would bury real diagnostics from hand-written patch
code. Nothing we author lives in `generated/`.

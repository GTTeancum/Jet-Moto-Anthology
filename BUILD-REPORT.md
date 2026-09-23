# Jet Moto / RecompOne — Build 01 validation report

## Result

The supplied USA executable was statically recompiled into C# and compiled using the supplied .NET SDK and offline dependencies. The patched build booted in the scratch environment and **visibly rendered Jet Moto's title menu**, including the 1 PLAYER, HEAD TO HEAD, LOAD GAME, and OPTIONS choices. See `reports/title-menu.png` and `reports/boot-06.log`.

This establishes code generation, compilation, disc access, substantial startup progress, and title-menu rendering. It does **not** establish playable races, working controls, correct audio, functional memory-card saves, or long-session stability. The final observed run was a bounded 90-second smoke test, ending intentionally with exit code 3. No race was played. Audio was directed to a null backend, not listened to.

The Windows build script publishes a self-contained `win-x64` application using the user's existing Windows SDK. It has been reviewed but not executed on Windows here. Separate Windows-targeted C# compilation is documented in the supplied Windows build logs; that scratch cross-check is framework-dependent and omits the native application host, because the uploads contain a Linux SDK rather than the Windows SDK's apphost pack. It is not a Windows execution test or a test of the final Windows `.exe` launcher.

## Exact inputs

- RecompOne commit: `d81dec8c9622fdcd0865d73588a3baa8d3c3a605`.
- Wiki snapshot: `df292b0b076302c53d7c878f772c0b368ef77306`.
- .NET SDK: `10.0.401`; scratch runtime: `10.0.12`.
- All three uploaded package checksums matched `UPLOAD-CHECKSUMS.sha256`.
- Offline feed: 47 packages. The Windows build script rechecks their hashes before restore.
- Game boot file: `SCUS_943.09`, 980,992 bytes, with 978,944 bytes loaded after the PS-X EXE header.
- Boot SHA-256: `f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48`.
- Entry point: `0x800EC310`; load address: `0x800DD2D0`; initial stack: `0x801FFFF0`.
- CUE layout: 14 tracks. Full original file hashes are in `reports/disc-manifest.json`.

The disc probe found one PS-X executable and no separate raw-code overlays. That is a probe result, not proof that every possible embedded or dynamically generated code path is supported. Automatic function discovery and linear sweeping are not substitutes for gameplay testing.

## Generated project

The final generation reported **1,874 functions**, 32 functions with jump tables, 702 jump-table entries, and **36 runtime replacements**. There is one generated main dispatch table. The generated sources, game entry wrapper, runtime launcher, and corrected function map are included in this package.

The automatic configuration named many SDK functions but missed CD routines in this executable. Fourteen additional names were assigned after instruction/call-site review; thirteen enable additional built-in runtime replacements, while `CdPosToInt` is only a clarified name. All address changes are listed in `reports/manual-sdk-map.json`.

Evidence used for those assignments included:

- `0x800E319C` and `0x800E31BC`: thin wrappers calling the low-level sync/ready bodies at `0x800E3DF0` and `0x800E4074`, whose diagnostic strings identify `CD_sync` and `CD_ready`.
- `0x800E31DC`, `0x800E31F4`, and `0x800E320C`: callback setters, cross-referenced to their use in sync, ready, and completed-read dispatch respectively.
- `0x800E3224`, `0x800E336C`, and `0x800E34A8`: command paths distinguish ordinary control, result-less fast control, and blocking control with completion-status testing.
- `0x800E36A8` and `0x800E3688`: high-level read/read-sync wrappers. The lower-level read at `0x800E521C` uses a different return convention and was **not** blindly replaced with the high-level API.
- `0x800E5A84`: pathname/file-search behavior and explicit `CdSearchFile` diagnostic strings; `0x800E382C` has an exact masked position-conversion signature.

No arbitrary successful return was substituted for the startup waits, and the dispatcher was not switched to tolerant mode.

## Runtime fixes and regression evidence

### 1. Completed bulk-read status

The original HLE `CdRead` copied the requested sectors but left `_lastResult` reporting the previous command. Jet Moto's reader at `0x800EDE08` calls `CdReadSync` and checks its result byte at `0x800EDE64` against `0x22` (motor + read). A stale `0x02` caused repeated reads of `STARTUP/SCEAPRES.BS`.

The patch publishes the read status **after** the requested bytes have been copied, then marks completion and dispatches the existing read callback. It does not claim success before performing the transfer.

The regression test locates the actual startup file, performs the HLE read, compares every file byte with the original filesystem result, and checks `CdReadSync`'s return and response byte. Those checks pass.

### 2. Command completion versus data-ready events

The original runtime assigned a data-ready interrupt into the same `_lastIntr` latch read by `CdSync`. Its `PumpReady` path could therefore change a completed command's return value from 2 to 1 while servicing a data callback. Jet Moto's helper at `0x800EE034` waits for the command-complete result 2.

The patch keeps data-ready/data-end events in the data-event path rather than overwriting the command-completion latch. Existing callback delivery remains in place. A regression test registers a real dispatch callback, starts reading, calls `CdSync`, checks that the callback ran, and verifies that the command-complete result remains 2.

That test **failed before this patch and passed after it**. The failing and passing logs are both included. The subsequent boot progressed beyond the intro stream and loaded the title assets.

### 3. Bring-up diagnostics and launcher

Generated function entries and backedges now provide optional execution breadcrumbs and cooperative stop checks. The runtime patch adds a bounded recent-function ring; it is not a call-stack reconstruction or an exact atomic CPU snapshot. The launcher writes a heartbeat and state file every five seconds, catches/report failures, validates the disc revision, and supports bounded smoke tests.

The CLI-selected disc is saved before host initialization. This avoids a first-launch disc-picker dialog remaining open after a valid path has been supplied. This launcher change does not modify the game binary.

## Tests actually performed

- Uploaded package hash verification and local dependency restoration.
- RecompOne compiler build and automatic disc/function analysis.
- Actual C# generation with the corrected map and instrumented emitter.
- Linux x64 game compilation and real execution under Xvfb with Mesa llvmpipe OpenGL 4.5.
- Disc validator accepted the supplied CUE/boot hash.
- CD byte-for-byte and command/data-interrupt regression tests passed.
- Bounded 40-second and 90-second boot runs exited through the requested smoke deadline, rather than an observed runtime exception.
- Final 90-second test rendered the title menu. Frame/interrupt counters continued advancing; that alone is not a frame-rate or playability benchmark.
- Windows-target restore and C# compilation/publish were checked separately with the same source/dependencies and an explicit no-apphost, framework-dependent configuration. Consult its logs for the exact command outcome.

The original runtime has two nullable-reference warnings in its CD/SPU debug panels on a clean build. Incremental builds can report zero warnings. No claim is made that these pre-existing warnings were corrected.

## Next validation point

Build and launch this package on the user's Windows PC, check menu input and first-race loading, then return `Collect-Logs.cmd`'s ZIP. Keep the same pinned input kit. A future failure should be diagnosed from the generated address breadcrumbs and actual runtime logs, not hidden by skipping further calls.

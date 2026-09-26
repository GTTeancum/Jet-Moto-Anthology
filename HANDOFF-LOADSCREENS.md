# HD loading screens — active handoff

Updated 2026-09-25. Repository:
`D:\Programming\GitHub\Jet-Moto-Recomp\JetMoto-RecompOne-Build12`

## Context boundary — mandatory

**Do not import previous chat information.** Do not retrieve, read, summarize,
merge, or rely on earlier chats, task histories, chat summaries, or remembered
conversation details. Do not use task-history tools to reconstruct context.
Do not carry forward instructions, approvals, assumptions, acceptance claims,
or additional scope from another chat.

This handoff carries the mandate for continuation. Use it together with the
current user's instructions, applicable repository instructions, and directly
verified source files, assets and test evidence. Historical reports may be used
to locate or verify technical artifacts; they must not import previous-chat
directions or expand this mandate. `HANDOFF-MENU-UPSCALE.md` is a historical
document, not the active handoff. If historical material conflicts with this
handoff, keep this handoff's scope and verify technical facts directly.

## Mandate

Continue the HD texture upgrade, **specifically loading screens**. Work toward
all ten tracks and both Loading and Continue variants using the available
original artwork. Make concrete improvements to complete screen candidates,
package them in the isolated test build, and inspect actual rendered results.
Do not spend the continuation on isolated letter studies.

Preserve original art style, typography, colors, layout, proportions, map
connections, markers and variant prompts. No unrelated replacement fonts,
invented artwork or distorted neural lettering. Enlarging an original alone
does not establish an HD quality upgrade. Partial map improvements must remain
identified as partial; do not call the ten-track task finished.

Keep other menu families, lighting and water/effects outside this work. Preserve
the approved track dial, 4:3 menus, Hor+ gameplay and existing cumulative fixes.
Do not overwrite Build 11 or the approved playable output. Preserve the dirty
working tree. No release packaging, commit or push is part of this handoff.

## Computer-control boundary

Never activate Computer Use, desktop capture, Codex capture, UI automation,
screen takeover, remote input, or OS keyboard/mouse/controller input.
Use files, logs, source and non-interactive terminal commands. Native game
captures and process-local replay in a muted headless game are permitted.
Do not run two validation processes against the same candidate at once.

## Current concrete state

The isolated Menu4x catalog has **47 entries**, confirmed by the latest native
startup. This count includes unrelated pre-existing menu assets and is not a
loading-screen coverage count.

| Track | Current partial candidate | Verification |
| --- | --- | --- |
| Joyride | Source-guided title and selected map lines; original small text | Existing pair staged; earlier report records Loading inspection. No new Joyride run in this continuation. |
| Cypress Run | Selected map borders, original text and markers | Both offline pages inspected; staged. Native route and both in-game states remain unverified. |
| Blackwater Falls | Selected map borders, original text and markers | Both offline pages inspected; new native Loading and Continue captures inspected. |
| Other seven tracks | No new candidate from this continuation | Pending |

No whole loading page is final-quality accepted. Small lettering, terrain,
labels, markers, legend, prompts and most titles still retain enlarged original
samples. Native dark blueprint tones show quantization. Do not reinstate
quarantined rejected artwork from `menu-quality-candidates` or rejected folders.

### Current files

All paths below are relative to the repository root.

- Cypress source: `reports/build12/menu-original-tim/SWAMP1/OVERV0.png` and
  `OVERV0L.png`, with original provenance sidecars.
- Cypress candidate: `reports/build12/menu-loading-cypress-map-v2/`.
  V1 had excessive bright/uneven borders and is superseded; use V2 only.
- Blackwater source: `reports/build12/menu-original-tim/SWAMP2/OVERV1.png` and
  `OVERV1L.png`, with original provenance sidecars.
- Blackwater candidate: `reports/build12/menu-loading-blackwater-map-v1/`.
- Packaged pairs: `reports/build12/menu-loading-runtime-candidate/`, under
  `SWAMP1`, `SWAMP2`, and existing `ISLAND1`.
- Isolated runtime staging:
  `.build/ui-buoy-bin/Release/net10.0/win-x64/Textures/Menu4x/`.
- Current authoring tools: `TextureTools/trace_loading_map.py`,
  `TextureTools/loading-cypress-map.json`,
  `TextureTools/loading-blackwater-map.json`,
  `TextureTools/package_loading_map.py`.
- Technical evidence: `reports/build12/MENU-LOADING-MAP-CONTINUATION.md`.
- Pending scope: `reports/build12/TODO.md`; only its loading items are active.

The map tool accepts per-track source bounds and protected rectangles. Optional
`--preview` allows an existing title composition; without it, verified originals
are used. Cypress and Blackwater use subpixel coverage inset 0.6 and blending
strength 0.7. Both packaging checks passed: original hashes, 1280x960 dimensions,
protected pixels, edit bounds, and exact signed Loading/Continue differences.
Cypress changes 63,296 output pixels per page; Blackwater changes 44,140.
These measurements establish scope, not visual acceptance.

The optional original-only path exposed variable shadowing during testing;
that is fixed by keeping `source_path` distinct from contour paths. A fresh
Blackwater reproduction produced identical PNG hashes to the inspected/staged
pair. Reproduction output: `menu-loading-blackwater-repro-check-v2`.
The failed preceding reproduction directory contains incomplete diagnostics.

### Latest native evidence

`reports/build12/menu-loading-blackwater-map-native-v2/` completed at 55 seconds,
game exit 3 / wrapper exit 0. All eight saved captures were individually inspected
in this order:

1. `frame-002409.png`: Blackwater track selection.
2. `frame-002421.png`: Blackwater Loading.
3. `frame-002436.png`: Blackwater Loading.
4. `frame-002449.png`: Blackwater Continue.
5. `frame-002464.png`: Blackwater Continue.
6. `frame-002480.png`: Blackwater Continue.
7. `frame-002496.png`: Blackwater Continue.
8. `frame-002512.png`: Blackwater Continue.

Map, paragraph, labels, markers, legend and variant footer were present without
observed clipping. The runtime log confirms owned submissions for SWAMP2/OVERV1L
and SWAMP2/OVERV1. This covers the saved states, not every intermediate frame,
race entry or audio. Audio was muted.

The first 44-second run ended at vblank 2372 before selection at 2400 and saved
no captures. Do not cite it as visual evidence. The older directory named
`menu-loading-cypress-native` actually loaded SWAMP2/Blackwater; it is not
Cypress coverage. The known initial replay cycles Joyride, Blackwater and
Suicide Swamp. Establish Cypress's actual route without assuming its availability.

## Next work

1. Continue substantive loading-screen artwork improvements using current
   originals and complete paired candidates. Extend reviewed map profiles to
   remaining tracks where appropriate; source geometry and protected regions
   must be inspected per track, not copied blindly.
2. Address remaining page artwork while preserving original lettering and
   composition. Do not treat map-only results as complete pages.
3. Establish and verify Cypress's native Loading/Continue route. Test additional
   tracks and both variants as candidates become ready.
4. Inspect full-screen output and relevant transitions. If asked to inspect every
   frame, inspect every frame sequentially. Successful rendering, nonzero frames
   or passing metrics alone cannot establish quality.
5. Keep pending work and evidence accurate. Explicitly distinguish offline
   inspection, staging, native state coverage and final quality acceptance.

## Commands and locations

Run commands from the repository root. Python:
`.build/menu-authoring-env/Scripts/python.exe`.

Create new output directories; the authoring tool refuses reuse:

```powershell
.build/menu-authoring-env/Scripts/python.exe TextureTools/trace_loading_map.py --profile TextureTools/loading-blackwater-map.json --output reports/build12/NEW-blackwater
.build/menu-authoring-env/Scripts/python.exe TextureTools/package_loading_map.py --profile TextureTools/loading-blackwater-map.json --candidate reports/build12/NEW-blackwater --stage
```

The packager refuses to overwrite a different existing candidate. Review new
artwork first and preserve any replaced test candidate deliberately. Omitting
`--stage` packages into reports only.

Native verification example; use a new run name:

```powershell
$env:JETMOTO_TRACE_MENU_SOURCES='1'
& 'C:/Users/smmel/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe' -NoProfile -File Run-Validation.ps1 -Name NEW-blackwater-native -Seconds 55 -Replay reports/build12/menu-loading-blackwater-map-replay.json -BuildOutputRoot .build/ui-buoy-bin -DisposableCards -WorldWake -MenuBackgrounds -Capture -CaptureStart 2408 -CaptureEnd 2521 -CaptureEvery 16
```

The wrapper uses headless/mute/no-dialogs and disposable cards, restoring settings
afterward. Leave CPU-heavy authoring out of native runs. Inspect actual filenames;
capture frames can differ from requested numbers.

Approved output: `.build/audit-bin/Release/net10.0/win-x64/`.
Rechecked JetMoto.dll SHA256:
`D2C09E1AF287900921AD27FEDE9281FB1CFF724C0044783D577F0D4DD66F3333`.
It is unchanged. No validation process remains active at handoff.

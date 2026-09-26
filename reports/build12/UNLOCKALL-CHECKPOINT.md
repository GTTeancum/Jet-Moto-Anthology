# Session-only all-track access

`JetMoto.exe --unlockall` makes all ten track dial slots available for the
current process. Default launches retain original track access. Current race
difficulty is preserved. No saved unlock flags, championship records or memory
card buffers are patched; ordinary game saving still behaves normally.

For this checkout, double-click `Play-VisualQA.cmd`. It starts the rebuilt
`.build/ui-buoy-bin/Release/net10.0/win-x64/JetMoto.exe` with the original CUE,
`--unlockall`, current menu artwork and world wake enabled. It clears inherited
headless/mute/replay/capture-directory settings for manual play. It has not been
launched interactively by the agent; all verification uses headless captures.

## Implementation

`JetMoto/TrackAccess.cs` and two source-anchored generated hooks:

- After the original availability read at 0x8014048C in func_8014046C,
  return 1 for dial slots 0..9 when enabled. The original query reads a four
  difficulty by ten slot byte table at 0x8018A9BC. Out-of-range slots and
  disabled mode retain the original result.
- At the track selector's artwork-variant read at 0x8013EF64, request original
  TRACKS3 artwork when enabled so newly selectable slots have their thumbnails.
  This changes only the local artwork request; it does not change difficulty.

No original-disc modifications or save generation. `--help` and README describe
the flag. `Run-Validation.ps1 -UnlockAll` forwards it and records the option.
`TrackAccessTests` is included in `Build-Windows.ps1 -RunTests`.

## Verification

- 165 checks pass across all four source difficulty rows, enabled/disabled and
  re-disabled sessions, out-of-range inputs, and artwork variant scoping.
- 54 generated hooks verified. Final candidate build: 0 warnings, 0 errors.
- Native `unlockall-sweep-v2`: all twelve captures individually inspected in
  sequence (1900, 1980, 2160, 2341, 2520, 2700, 2880, 3060, 3240, 3420,
  3600, 3780). The dial reaches Joyride, Hammerhead, Cypress Run, Blackwater
  Falls, Suicide Swamp, Nightmare, Willpower, Ice Crusher, Snow Blind and
  Cliffdiver, then returns to Joyride. This check preceded the artwork-only
  follow-up; its blank formerly locked thumbnails motivated that refinement.
- All 42 staged HUD, boost, minimap and buoy override hashes survived rebuild.
- Final Nightmare launch: all nine saved captures individually inspected in
  sequence (2900, 3000, 3300, 3600, 3901, 4200, 4500, 4801, 5100).
  All ten thumbnail wedges are populated; Nightmare loads into its starfield
  race with riders, road, countdown, advancing timer and moving minimap markers.
  Both memory-card SHA256 hashes remained unchanged after the completed run.
  The user subsequently identified broken numeral outlines in this footage;
  track access verification does not establish HUD artwork quality. See the
  separate numeral repair checkpoint for that follow-up.

The initial sweep attempt exposed a PowerShell single-string splatting issue
in the test wrapper. It exited with argument error before boot. The wrapper
now explicitly uses a string array; v2 completed at its expected smoke deadline.
No all-track full-race, audio or championship completion claim is made.

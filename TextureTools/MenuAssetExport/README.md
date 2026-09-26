# Original Menu Background Extraction

Offline authoring tool, not a game launcher or a replacement renderer. It uses
the supported original executable's VLC routine and the game's runtime MDEC
decoder. It does not initialize a window, audio, or interactive input.

Build after the isolated candidate exists in `.build/ui-buoy-bin`:

```powershell
dotnet run --project TextureTools/MenuAssetExport -c Release -- `
  "D:\path\Jet Moto (USA).cue" STARTUP/TITLE.BS `
  reports/build12/menu-original-assets/STARTUP/TITLE.png 640 480
```

Dimensions must be established from the original menu path. The tool checks
that decoded macroblock count exactly matches the requested dimensions. It
does not infer aspect ratio from pixel count. Both TITLE.BS and OPTIONS.BS
were extracted at 640x480 and individually inspected as correctly arranged.

The executable revision and original MDEC table headers are checked. Output
has original RGB555 precision expanded by eight, matching native texture
modulation. Each PNG receives a source-hash manifest. Existing outputs are
never overwritten. No extracted art or model weights are committed.

`enhance_menu_background.py` accepts these source manifests, verifies source
and model checksums, and produces an explicitly unaccepted neural candidate.
It neither installs candidates nor substitutes typography. Runtime binding,
complete-screen fidelity, and navigation checks are separate acceptance gates.

The enhancement tool also accepts standalone TIM extraction manifests from
`export_menu_tim.py`. Current native loading-screen integration is restricted to
the ten original track overview names and their L variants, direct-color TIMs
at 320x240, and the verified original upload caller. Joyride's two variants are
the first inspected native candidates; this does not establish coverage for
the other tracks or rider/prize TIMs.

# Jet Moto / RecompOne — Build 07 validation report

Date: 2026-09-21. This report describes work performed on the final enhanced pack in this turn. Earlier reports are retained as historical records, not as new test evidence.

## Result

A complete original-ID texture pack and cumulative buildable source update were produced. **1,390 images were processed through trained general-purpose 4x super-resolution networks.** Sixteen uniform-color swatches/masks were intentionally replicated without inventing detail. The final set is **1,406 RGBA PNGs from all 34 catalogued original TMS banks**, each exactly four times the original width and height. It is not Build06's Lanczos enlargement relabeled as a neural result.

The uploaded checkpoints and the original 1x game textures were actually loaded, and CPU inference actually ran. The complete batch finished in **662.26 seconds**, reusing identical results for **551 unique original images**. Final manifest hashes, processing profiles and the authoring environment are included. Runtime never uses those offline duplicate hashes to identify a texture.

## Visual processing and limits of the models

Both selected networks are general-purpose models; no anime checkpoint, style-transfer model, face enhancer, text prompt or diffusion redraw was used. The general compact model is mixed with its weak-denoise companion at a 0.25 denoising weight. Most artwork uses that conservative combination. Selected environment textures use the full RRDB model after comparison and review of candidate surfaces.

The final per-image method counts are:

| Method | Pack entries | Unique original inputs |
| --- | ---: | ---: |
| General x4v3, 0.25 denoising weight | 1,232 | 438 |
| Selected RRDB environment surfaces | 158 | 98 |
| Constant visible color retained | 16 | 15 |
| Total | 1,406 | 551 |

The decoder reads original TMS/TIM data, not the old enlarged PNGs. Inference uses full source images with 36-pixel original-resolution context padding. Reviewed opaque surfaces may use wrapping on an axis when the source edge continuity test supports it; other inputs use reflected context. Broad color drift is corrected against the source with an 8-high-resolution-pixel Gaussian correction. This is a low-frequency color correction, not a replacement of the neural detail with an interpolated image.

Original alpha classes 0/128/255 are preserved **exactly**, including their four-times-upscaled pixel footprint. The PS1 semi-transparency category is not mistaken for ordinary half-opacity. This avoids damaging material semantics, but it deliberately does not invent smoother leaf silhouettes or change alpha-mask geometry. Uniform swatches preserve exact original RGBA values. Duplicate originals produce byte-identical PNGs even across different bank/texture IDs.

The included `reports/build07/texture-comparison.png` compares final Build07 files directly against the old Build06 filtered files at the **same actual 4x output-pixel scale**. It shows stone, a road surface, a sponsor image and foliage. There is no enlargement of only one column and no illustrative AI screenshot. `comparison-sources.json` identifies the exact images. Selected comparisons and candidate contact sheets were visually reviewed; every single output has not received an exhaustive artist review.

Fine texture detail is inferred, not recovered with certainty from an unavailable high-resolution original. Small lettering and very thin features remain source-limited. More visible detail can also make distant surfaces shimmer: no new mipmapping or anisotropic-filtering stage was introduced.

## Cumulative source audit

All **21 upstream overlays** are byte-for-byte identical to Build06 and match the cumulative patch manifest. All **11 game-project files** were compared with Build06. The only differences are the displayed build label in Program.cs and the project version changing from 0.6.0 to 0.7.0. Generated original functions, native texture hooks, projection, renderer, audio and physics code were not otherwise modified.

Therefore the bundle retains the CD/startup fixes, adjacent-disc GUI launcher and safe deployment, permanently disabled renderer dithering, true Hor+ 16:9 gameplay, 4:3 pillarboxed menus and pause, expanded visibility/sky behavior, perspective-correct texture mapping, subpixel projection, and original DMD/TMS material-ID texture replacement including close-range subdivision. Eleven generated hooks were checked and already present; no new hook application was needed.

Evidence: `cumulative-source-audit.json`, `generated-hooks.log`, unchanged `upstream-patches/patch-manifest.json`.

## Tests actually run

| Check | Observed result |
| --- | --- |
| Complete Python pack audit | 1,406/1,406 files passed original bank/ID path, exact dimensions, output SHA-256, original source fingerprint and categorical alpha checks |
| Original alpha correspondence | **187,508,736** output alpha pixels match the exact 4x original footprint |
| Identical-original consistency | All 551 unique original groups produce byte-identical replacements within each group |
| Neural-vs-filtered control | Every one of the 1,390 neural outputs differs in visible RGB pixels from the old filtered pack; this is a provenance check, not an objective quality score |
| Actual runtime PNG loader | **1,406/1,406** loaded through `NativeTextureAsset.GetTexture`; exact scale, decoded size, object-cache reuse and original alpha comparison passed |
| Native asset regressions | **149 passed, 0 failed**, including all original TMS banks |
| OpenGL pixel regressions | **999 passed, 0 failed**: 333 each on Gl45, Gl33 and Gl21 at the covered internal resolutions |
| Static renderer checks | 13 checks plus 3 successful isolated graphics suites; summary 16 passed, 0 failed |
| Perspective / precision | **56 passed, 0 failed** |
| Widescreen / visibility | **108 passed, 0 failed** |
| Adjacent-disc launcher | **14 passed** |
| CD regression | **11 assertions passed** against the uploaded original disc |
| Generated hook integrity | **11 verified**, newly applied 0 |
| Linux final compilation | Passed, 0 errors; final incremental build reported 0 warnings |
| Windows-targeted managed publish | Passed, `win-x64`, framework-dependent without a Windows apphost; managed PE GUI subsystem 2 |
| Published pack verification | Both Linux and Windows-targeted output trees contain all 1,406 exact final PNG hashes and the final manifest |

A clean earlier Linux project compilation in this turn emitted two existing nullable warnings from the upstream debug UI (`SpuViewerPanel` and `CdDebugPanel`); these are not new Build07 changes. The final incremental game build reported no warnings. Neither result is a claim of native Windows execution.

The three model hashes matched the user-uploaded collector metadata. The source ZIP, dependency ZIP and Linux SDK matched the upload SHA-256 list, and all 47 offline NuGet packages matched the packager's package manifest before they were used. The model checks establish integrity of this upload, **not independent publisher signatures**.

Full logs and machine-readable pack/audit/publish reports are under `reports/build07`. Software/backend: Linux x64, .NET SDK 10.0.401, runtime 10.0.12; Mesa llvmpipe OpenGL, CPU-only PyTorch 2.10.0. The game itself does not depend on PyTorch.

## Actual final-pack game sessions

Two sessions used the freshly compiled final game and exact new PNGs, **with no CUE argument**. The launcher found the verified CUE and all 14 BIN tracks beside the executable. Scratch links to the already extracted supplied tracks were used; no disc bytes were patched. Audio output used the null backend and was not listened to.

**Session 01: 360 seconds.** Title and attract rendering, rider/race/track menus and Joyride track introduction were observed. Island and swamp assets were loaded during attract presentation. This is not claimed as a completed player-driven race. The run ended cooperatively at its requested deadline with exit 3.

**Session 02: 240 seconds.** Title, rider selection, single-race selection, Joyride track introduction and grid were traversed. Keyboard-controlled driving was observed, followed by a confirmed pillarboxed pause menu and a confirmed return to expanded 16:9 gameplay. Images show actual rendered output; the driving route went into the shoreline area and triggered the game's normal track-boundary message. No full lap, complete race or multiplayer session is claimed. This run also ended at its requested deadline with exit 3; that code is a test timeout, not a crash or playability certificate.

Final session 01 counters:

```text
native[bound=897712,resolved=1612619,png=190,draw-fallback=0] material[seen=1353347,linked=1309179,unmapped=44168,packets=897712,bad=0,deferred=9106,subdivided=9106]
legacy VRAM matcher: calls=0 untextured=0 rejected-size=0 rejected-gpudirty=0 hashed=0 memo-hits=0 tiles=0
```

Final session 02 counters:

```text
native[bound=745069,resolved=1360021,png=125,draw-fallback=0] material[seen=1217243,linked=1092048,unmapped=125195,packets=745069,bad=0,deferred=6845,subdivided=6845]
legacy VRAM matcher: calls=0 untextured=0 rejected-size=0 rejected-gpudirty=0 hashed=0 memo-hits=0 tiles=0
```

The final gameplay session loaded **125** replacement PNGs through the real binding path, with **6,845** subdivided source primitives, **zero** bad native packets and **zero** native draw-fallback counts. The counters for unmapped material visits are not all zero: unsupported/unidentified material paths retain the original mapping. No claim is made that every draw in the game received a replacement. Both sessions reported zero legacy VRAM matcher calls. Scene-list limit hits stayed zero in these logged sessions; this is not an exhaustive every-track edge-culling guarantee.

Useful images: `run02-grid.png`, `joyride-driving-confirmed.png`, `joyride-pause-confirmed.png`, `joyride-resume-confirmed.png`, `rider-selection.png`, `texture-comparison.png`. Screenshots that were captured before an attempted pause had actually taken effect were not retained as pause evidence.

## Installation paths and preservation

**Existing Build06:** `Install-Textures.cmd` performs a texture-only update at the existing deployment directory. The script checks the installed build, requires the game to be closed, checks each source PNG, copies to a fresh stage, verifies the copied hashes, then switches the built-in pack directory. The prior built-in pack is retained in a timestamped backup. Matching `Textures/Overrides` files retain priority and are never removed. The EXE, DLLs, disc, saves and settings are not changed by this fast path; the old binary therefore still displays its old build label.

**Full cumulative build:** `Build-Windows.cmd` uses the original input kit, applies all cumulative patches and deploys version 0.7.0 plus the final PNGs. No intermediate build or new model/SDK collector is needed. This path is appropriate for older installations or a complete rebuild.

Both paths target `D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto`. The complete source package is included, but a native Windows EXE is not generated in this Linux scratch. The optional texture installer and existing Windows build/deployment scripts were statically reviewed; they were **not executed in native Windows** here. The end user does not install Python or weights and the running game does not perform neural inference.

## Remaining limitations

This does not remaster movies, .BS backgrounds, every standalone TIM or every dynamic palette path. Alpha silhouettes remain exact original pixel footprints; thin edges may therefore remain blocky even when their RGB texture is enhanced. Missing or untrusted material IDs keep the original image. The original art direction is retained as the processing target, but generated fine detail and tiny lettering cannot be guaranteed pixel-semantically faithful without manual art review.

Native Windows execution, the user's GPU driver, every location of every track, audio quality and full multiplayer gameplay remain untested here. No claims of photorealistic redesign, higher polygon count or a complete hand-authored remaster are made.

## Reproduction and provenance

`TextureTools/README.md` documents optional offline authoring with the original supplied disc/assets and the three model files. `enhance_native_pack.py`, `neural_models.py` and `enhancement-profiles.json` contain the actual pipeline used. The exact model/source/profile/output hashes are recorded in the manifest. The retained `build_native_pack.py` is explicitly the legacy decoder/comparison tool; its Lanczos authoring function was not used to create the new neural outputs.

Pinned RecompOne revision: `d81dec8c9622fdcd0865d73588a3baa8d3c3a605`. Original executable SHA-256: `f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48`. Model weights, SDK binaries, NuGet caches, disc images, font files and generated native binaries are excluded from this release ZIP.

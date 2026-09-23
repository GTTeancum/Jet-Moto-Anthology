# Build 03 validation report

Date: 2026-09-21

## Scope

Permanent removal of renderer-added dithering. No user-facing enable/disable option is added. The Windows application remains a GUI EXE, discovers the adjacent CUE, and deploys to `D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\` with its accompanying libraries. The Build 02 deployment code is unchanged and does not clear the destination or copy over disc images, memory cards, or settings.

The user reported that Build 02 runs. They could not test audio because their system was muted. This update leaves audio code unchanged and makes no audio-success claim.

## Renderer changes

- `GpuHleForward.cs`: retain the emulated draw-mode/GPUSTAT state in the existing GPU logic, but forward `Dither = false` to the graphics backend.
- `GlCore.cs`: remove dither selection for triangles and lines, remove the internal packed dither flag and its vertex parameters, and force `GL_DITHER` off during initialization and every flush. The empty-batch flush also disables it for presentation and transfers, even if another GL user changes state.
- `GlShaders.cs`: remove the dither varying, lookup matrix, and quantization-offset branches from both GLSL 330 and GLSL 120 primitive shaders. All three shipped OpenGL backends use these shared paths.
- Launcher version is 0.3.0 / Build 03; the diagnostic log explicitly identifies permanent no-dither rendering.

The original five-bit-per-channel color conversion remains. This is not a true-color patch, texture filter, asset edit, or audio change. Any pattern already stored in a texture or video frame remains part of that image.

## Executed validation

1. All three uploaded archive SHA-256 checksums matched the upload manifest. Offline package restoration used the uploaded SDK 10.0.401 and local NuGet feed. No newer upstream source was substituted.
2. The full generated Jet Moto game and patched runtime built for Linux in Release configuration. The successful final build reports zero warnings and zero errors; the preceding clean runtime compilation emitted the same two upstream nullable debug-panel warnings seen in the unpatched baseline.
3. The full project published for `win-x64` in framework-dependent, no-apphost mode. Its managed PE subsystem is 2, Windows GUI. This verifies Windows-targeted managed compilation, not a native Windows launch. The user-side script continues to publish the actual self-contained EXE using the existing Windows SDK.
4. Thirteen non-graphics assertions passed: four shader contracts, guest GPUSTAT readback/forwarding for four successive draw-mode changes (eight checks), and a vertex-packing test covering all 16 combinations of relevant primitive flags.
5. Actual renderer tests passed on Gl45, Gl33, and Gl21, each in a separate process. Each backend completed 96 assertions across 1x, 2x, and 4x resolution scales: initialization and real shader compilation/linking, host GL state, before/after dither-request pixel equality, nonempty uniform output, presentation, and GL error checks. Tested shapes are Gouraud triangles, modulated textured triangles, raw-textured triangles, rectangles, lines, external-image triangles, and semitransparent triangles. Each pixel comparison covers the full 64x64 VRAM region. The total is 288 graphics assertions plus 13 non-graphics assertions and three child-process success checks, all passed.
6. The same test code built against the unmodified pinned renderer failed as expected: Gl33 baseline had 24 passing and 85 failing assertions, including changed pixels and spatial color patterns when dithering was requested. This establishes that the tests detect the removed behavior rather than accepting blank frames.
7. All 14 existing launcher regression tests passed again. They use the exact unchanged disc-selection implementation.
8. All generated game functions, configuration/function maps, previous CD and execution-tracing patches, disc locator, deployment copier, and launch/log collection CMD scripts were compared byte-for-byte with Build 02. The new overlay hashes match the manifest and the compiled vendor copy. Details are in `reports/build03/preservation-and-integrity.txt`.

## Test environment and limitations

Linux scratch, Xvfb, Mesa 25.0.7-2 llvmpipe (LLVM 19.1.7), OpenGL 4.5 core for the Gl45/Gl33 paths and OpenGL 4.5 compatibility for the Gl21 path. The Gl21 tests compile/run the actual GLSL 120 fallback shaders and backend; this is not a test on physical OpenGL 2.1-only hardware. Resolution scales were 1x, 2x, and 4x, not every intermediate scale.

An initial harness that changed GL context profiles in one process terminated with a native segmentation fault after its modern-backend checks. The harness was corrected to isolate backends in child processes; the final full run exited 0 with all checks passing. No gameplay runtime change was made in response to that test-harness failure.

No native Windows execution, Windows PowerShell build/deployment execution, audio listening, or new in-game/race playthrough was performed in this update. The earlier screenshots and boot logs are historical Build 01/02 evidence, not screenshots of Build 03. No new disc read tests were necessary for this rendering-only patch; the CD implementation and generated code are byte-identical to the previously tested versions.

Raw Build 03 build/test evidence is in `reports/build03/`. The older reports remain untouched for provenance. Scratch-only offline signature/revocation settings were not added to the Windows build script.

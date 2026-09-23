# Build 09: authored spray/roost replacement

`replace_effects.py` creates the NEW five-sprite artwork from original palette
anchors. It does not upscale the old sprite shapes. It requires Python 3.10+,
Pillow, NumPy and SciPy for optional offline authoring only. The player needs none
of these tools and no neural weights for these effects.

First export original 1x PNGs with `build_native_pack.py` (see its `--help`). Then:

```text
python replace_effects.py --originals originals --base-pack Native4x-Test --output new-effects-pack
python verify_neural_pack.py --pack new-effects-pack --originals originals --report audit.json
```

The output must not already exist. All 1,406 PNGs are carried forward, with only
15 verified effect entries replaced; 15 matching `.png.material.json` files enable
smooth coverage. The original dimension+RGBA fingerprint must match before art is
created. Original disc/model files and base pack are never overwritten.

Effect sprites are 256x256 RGBA. Alpha is coverage (0..255), not native STP. The
runtime accepts it only for its explicit bank/record/ID allowlist plus an exact
sidecar. Keep PNG and sidecar together, including for a custom Override. Other
textures and older sidecar-free categorical overrides keep the old behavior.

The deterministic authoring seeds, original palette anchors and output hashes
are in `pack-manifest.json`. No model weights or successful image-generation
output were used for this effect-art pass. Earlier neural authoring instructions
below still apply to the UNCHANGED environment/rider pack, not to the new effects.

---

# Build08 rider/bike texture refinement

The game loads the finished PNGs. These are optional offline authoring tools, not runtime dependencies.

Build08 changes 44 original bank/ID entries corresponding to four rider/moto atlases. The remaining 1,362 PNGs are byte-identical to Build07. Source images are the original 128x256 RGBA atlases; outputs are 512x1024. A full general-purpose RealESRGAN_x4plus network runs four reflected/rotated passes. No anime, face enhancement, diffusion, or artistic restyling is used. Two jacket decals use clearer artwork already present on the original disc: Mountain Dew and AXIOM. The other two atlases receive the stronger neural detail pass without invented logos. Original categorical alpha and native IDs remain unchanged.

## Reproduce the focused pass

Requires Python 3.10+, PyTorch with `weights_only=True`, NumPy, Pillow and SciPy, plus the already supplied official model checkpoints. Do not use an already enlarged PNG directory as the original source.

```powershell
python .\TextureTools\refine_riders.py `
  --cue "D:\Games\Jet Moto (USA)\Jet Moto (USA).cue" `
  --base-pack "D:\PreviousBuild07\Textures\Native4x-Test" `
  --weights "D:\JetMoto-Neural-Models" `
  --output "D:\JetMoto-Rider-Pack08" --threads 3
```

The output directory must be new or empty; the original disc and base pack are never modified. An original 1x PNG tree can be supplied with `--originals` instead of `--cue`. `rider-decal-profiles.json` records exact source crops and original atlas placements. Runtime texture lookup does not use the author's duplicate-image hashes: it still uses the original DMD material, TMS bank, record ordinal and texture ID.

The full pack is included ready to load. Inferred texture detail is not a guaranteed recovery of lost source art. The highest original model remains 136 source polygons, not a newly sculpted mesh.

---

## Retained Build07 full-pack authoring notes

# Texture authoring tools — Build 07

Normal users do not need Python, PyTorch, a GPU, or model weights. All final 4x PNGs are included and load through the existing native bank/material/texture-ID integration.

## Reproduce the neural pack

Dependencies used in scratch: Python 3.13, PyTorch 2.10.0 CPU, NumPy, Pillow and SciPy. The tool uses float32 inference. See the build report for recorded versions. Another hardware/library stack can change final rounding slightly.

Extract the previously collected three-model ZIP to a local folder. Then, from the Build07 folder:

```powershell
python .\TextureTools\enhance_native_pack.py `
    --cue 'D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\Jet Moto (USA).cue' `
    --weights 'D:\TextureWork\JetMoto-Neural-Models' `
    --output 'D:\TextureWork\JetMoto-Neural4x' `
    --originals 'D:\TextureWork\JetMoto-Original1x'
```

`--assets-root` is an alternative to `--cue` for a complete original extracted tree. The tool verifies all 34 TMS fingerprints and, with a CUE, the game executable revision. Output must be new/empty; originals and enhanced output cannot be the same folder. No source file is modified. `--device cuda` is optional on a suitably installed authoring machine; CPU is the default.

## Actual processing

The reviewed surface profile uses the full-size **RealESRGAN_x4plus** model for road, rock, wood and other selected environmental textures. Other nonconstant artwork uses **realesr-general-x4v3**, parameter-interpolated with its weak-denoise companion at denoise strength **0.25**. These are general models, not anime models. No face enhancer, text prompt, generative redesign or style-transfer stage is used.

Only broad color drift is corrected against the source after neural inference. This is not a blend of the final image with the old Lanczos output: learned high-resolution edges and detail remain. Fully constant-color swatches retain their exact colors rather than acquire invented detail.

Transparent RGB is extended from occupied pixels before inference to avoid feeding arbitrary black backgrounds into cutouts; it is hidden again afterward. Alpha 0/128/255 is copied exactly at 4x with nearest replication. Alpha 128 remains the PS1 semi-transparency category, not ordinary 50% coverage. No antialiased alpha, UV repositioning, texture-ID renaming, or atlas rearrangement occurs.

Inference uses full images with 36 original pixels of surrounding context. Reviewed opaque surfaces whose opposite edges are already numerically continuous receive wrapped context on the relevant axis; others use reflection. This is an authoring heuristic, not knowledge of every native material's repeat flags or a guarantee that every pre-existing texture seam is removed.

Identical decoded originals share one processed result. SHA-256 values in `enhancement-profiles.json` are **offline processing/deduplication keys only**. The runtime still uses original DMD/TMS material identity, bank path, ordinal and texture ID. No VRAM dumping or runtime pixel matching is added.

Every output entry records original ID/dimensions, output SHA-256, method, alpha policy and context choice. `pack-manifest.json` is written only after the complete 1,406-image pass succeeds. Interrupted output has no completed manifest and must not be installed as a complete release.

## Legacy filtered generator

`build_native_pack.py` remains for decoding TMS files and reproducing the old Build06 filtered comparison. Its `upscale()` routine is still Lanczos and is **not** the neural workflow. Use `enhance_native_pack.py` above for the actual Build07 quality pass.

## What this does not promise

Neural detail is an estimate guided by the low-resolution original, not recovered original high-resolution artwork. Very small text, thin edges, and animated or unmapped assets still need visual review. Movies, .BS backgrounds and unsupported material paths are not universally remastered. This update does not add mipmaps, anisotropic filtering or new dynamic palette handling.

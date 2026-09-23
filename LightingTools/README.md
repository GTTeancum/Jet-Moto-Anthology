# Original-geometry lighting data authoring

Optional OFFLINE tool. Normal Windows builds already contain the completed data and do not require Python. Requirements: Python 3.10+, NumPy and Pillow.

From the cumulative build folder:

```powershell
python .\LightingTools\bake_world.py `
    --cue "D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto\Jet Moto (USA).cue" `
    --pack ".\Textures\Native4x-Test" `
    --output "D:\TextureWork\JetMoto-Lighting-Rebuilt"
```

Output must be new or empty. The original executable and original TMS bank checksums are verified. Disc files are read-only. `native_graph.py` traverses the original static DMD hierarchy, retaining translation, rotation, scale, original instance and LOD/animation identities. It does not generate or edit game geometry. `native_material.py` links original polygon materials to original TMS records, not live VRAM or rendered pixel colors.

Ten track banks produce a 1024x1024 PNG height DATA map and a compressed JSON original-geometry receiver catalog. PNG RG encodes 16-bit height, B marks occupied map samples, A is opaque; these are not new texture artwork. Static near-detail solids provide the top-height approximation. All original geometry variants provide conservative receiver candidates. No dynamic rider bodies or sprite cutouts are baked into the height data.

`water-identities.json` lists reviewed original water/surface IDs by bank. Classification requires the actual source material identity and an approximately horizontal original polygon. A native LOD family can additionally identify its untextured variants when a corresponding near child is entirely water-material geometry. This is offline source-graph classification, not a runtime screen color threshold. Mesh/frame/transform provenance is checked again when the game emits a command.

The runtime compares the original DMD hash and the height PNG hash before activating a scene. For world positioning it reconstructs the native view transform from an unambiguous static original instance and validates additional instances against that view. The original camera can include projection scaling; its true inverse is required. World-space water sampling does not depend on the post-processed framebuffer or display dimensions.

The bounded heightfield has intentional limitations: one upper height per XY sample; finite resolution and ray-march distance; no full scene reflection, depth-buffer ambient-occlusion pass, translucent caster model or dynamic actor shadows. Existing rider shadows remain. Unknown receivers keep original rendering.

Two complete independent runs of the packaged authoring script reproduced all delivered PNG DATA, geometry gzip and catalog bytes exactly in the tested environment. `reports/build11/lighting-data-audit.json` records their fingerprints and original source checks. Identical data are already deployed by the normal build; regenerate only when deliberately working on the source integration.

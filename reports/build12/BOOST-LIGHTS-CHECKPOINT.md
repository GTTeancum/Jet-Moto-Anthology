# Boost indicator restoration

The user identified the green HUD lamps as boost availability indicators and
requested one final upscale. Each lamp is the original 8x8 DAD3 texture, with
its visible 5x5 region at (0,3)-(5,8). The game draws each remaining boost over
the dial's dark sockets.

Built-in imagegen created a crisp circular green glass lens with a restrained
highlight and darker edge. `TextureTools/package_boost_lights.py` packages it
at 32x32, fitting the visible lens exactly inside the original occupied bounds
scaled fourfold. Opaque alpha remains categorical 0/255. Original sprite
positioning and boost-count behavior are untouched.

Output: `reports/build12/boost-lights-v1/`, containing `boost-lens-art.png`,
ten source-keyed DAD3 PNGs and a SHA256 manifest. These are staged in
`.build/ui-buoy-bin/Release/net10.0/win-x64/Textures/Overrides`.
All ten replacements pass dimensions/alpha checks. SHA256 comparisons confirm
the prior 32 speedometer/HUD, minimap and buoy assets remain unchanged.

Validation replay: `boost-lights-replay.json`. Uses the game's process-local
replay input to accelerate and press Triangle (Turbo in the original controller
screen) at race frames 900, 1100, 1300, 1500, 1700 and 1900. No host input is generated.

First native run `boost-lights-native-v1` used the first four pulses only.
All five saved race captures were inspected in sequence: 000801, 001001,
001200, 001400, 001600. These show four, four, three, two and one available
boost respectively, with clean round green lenses and dark sockets after use.
The first pulse did not consume a boost on that route; a second replay adds
later pulses to verify depletion. First run ended at its expected 125-second
smoke deadline (exit 3); this deadline alone is not visual acceptance evidence.
`hud-detail.png` is an unscaled crop of race-001001, not a mockup.

Second native run `boost-lights-depleted-v1`: individually inspected all three
captures in sequence, race-001802, race-001951 and race-002101. All show zero
available boosts and four dark sockets, with no residual green artwork.
Together the two runs visually cover counts 4, 3, 2, 1 and 0. The screenshots
also show the surrounding gameplay, backgrounds, minimap and other HUD art.
Native visual coverage is ISLAND1; all ten banks receive the identical lens
at their original DAD3 identities. Audio and refill-on-next-lap were not tested.

## Final prompt

Built-in imagegen reference: `boost-source/boost-detail.png`, nearest-neighbor
inspection enlargement of original ISLAND1 DAD3.

> Use case: precise-object-edit. Restore the attached tiny green boost indicator into a clean high resolution GAME HUD SPRITE. Make one circular luminous green glass indicator lens, viewed straight-on, perfectly circular. Original color is vivid lime green with a small pale green highlight upper left, emerald dark edge lower right. A restrained smooth convex glass surface, vivid readable bright green center, subtle internal gradient and one clean small highlight; no light rays, fuzzy glow, cast shadow, metal bezel, words, symbols, surrounding housing or embellishments. The attached pixelated sprite is the color and material reference, not its square pixel edges. Deliver a single centered circular lens occupying 90 percent of square canvas with genuine transparent background outside the circle. Crisp cutout boundary. It will appear about 20 pixels wide over a black dial, so keep shading simple, strong and clear, avoid excessive microdetail. No extra objects.

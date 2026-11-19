# Framework reference-test art profile — proposed

This is an art test under `examples/modern-city-demo`, not a separately
commissioned game. No settings or artwork approval has been recorded.

| Choice | Proposed value |
| --- | --- |
| Artifact filename prefix | `framework-city-reference` — a test prefix, not a game name |
| World representation | Continuous space in meters; no imposed street/block grid |
| Look | Crisp 16-bit-style pixel art (`pixel_mid`) |
| Camera | Straight-down orthographic; consistent object/ground projection |
| Production asset scale | 32 pixels per meter |
| Facing coverage | Decide per object; static structures need only their declared views |
| Animation | Hybrid: measured frames and separated moving components |
| Image generator | Codex's built-in image generation tool |
| Native consumer | Godot |
| Palette | No fixed palette; maintain the approved reference style across the set |

The reference test will retain the full buildings → individual districts → whole
city overview → reconciled map → sprites/tiles → native TileMapLayer sequence.
Overviews and detail images have their own measured map calibration; the production
asset scale does not force one enormous full-city image. Every required instance,
substructure, surrounding area and connection must remain accounted for.

For this test only, propose terrain presentation cells of 0.5 meters (16 production
pixels), and sampled component animation at no more than 0.05 seconds between
approved poses, with every frame transition included. These are test inputs,
not framework-wide defaults. Continuous curve fidelity between samples and
game-specific interaction behavior are separate capabilities.

Accepting this profile selects these art-test choices only. It does **not** approve
future images, mockups, sprites, cleanup evidence, atlases or a completed city.
Those remain exact-artifact review gates under canonical GameArts.

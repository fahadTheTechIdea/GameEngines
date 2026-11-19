# Mixed independent world domains

This reusable framework input combines three structurally different domains:

- Terrain: two required encounter destinations, optional drainage, an occupied
  retained lake, wind-derived humidity, dry-land regions and a generated
  land-only destination network. The network bends around the lake.
- Interior: six source-sized rooms with six required fixtures, generated
  branching topology, physical walls, five openings and qualified local paths.
- Progression: three geometry-free states in an independent logical coordinate
  space. No terrain, footprints, scene or art is required for this domain.

`NetworkRuleId` references an exact outdoor network destination. `InteriorConnectionId`
references a generated room-side opening. Explicit bidirectional transitions
connect the domains without merging local coordinates. The complete journey
requires physical network segments, room passages, both transitions and a logical
link, in both directions. All 19 nodes remain in the declared program.

The engine accepts this definition through `GenerateDomains` or
`generate-world-domains`; the resulting directory contains every local metadata
packet and a root index. `MetadataDomainGenerationSession` supports transactional
edits and replayable source-history checkpoints. The qualified counterfactual
raises the retained lake from level 1 to 1.5, regenerates the outdoor network and
reuses unchanged interior/progression stages without moving or dropping content.

This is technical framework qualification. It does not implement game-specific
progression effects, physical portal meshes, continuous 3D connectors, transition
runtime behavior, production artwork or Godot. World-scale and wider expressive
range gates remain open in the master plan.

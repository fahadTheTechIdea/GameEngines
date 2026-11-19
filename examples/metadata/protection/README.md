# Protected gameplay spaces

This engine qualification fixture retains two gates, four boundary markers, a
resource and four additional approach markers. The source phase declares a
node-relative interaction area and a one-meter-wide sightline between offset
gate anchors. It requires neither roads nor generated regions. The final phase
must preserve the source rules and every required physical member.

`recipe.json` places the view at y=5. `moved-view.json` changes it to y=6 while all
physical members remain fixed. `blocked-view.json` places it at y=16, across
required markers, and must reject with `METADATA_CONTEXT:exclusion:view` before
publishing output. These are controlled test coordinates, not a finished city.

Run through the normal `generate-metadata` command with `catalog.json` and a new
output directory. The generated schematic shows the exact protected polygons in
orange. [Verification](../../../docs/evidence/2026-10-27-world-protection-verification.json)
records actual CLI execution, replay, browser geometry and independent checks.
The engine contract is documented in [World metadata](../../../docs/WORLD_METADATA.md#protected-gameplay-areas-and-sightline-envelopes).

This is tagged 2D keep-clear geometry. 3D visibility, terrain occlusion, NPC
behavior, broad scale/variety, artwork and native Godot remain separate gates.

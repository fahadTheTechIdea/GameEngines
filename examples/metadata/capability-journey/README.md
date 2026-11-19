# Capability-gated journey reference

This framework example authors five generic locations under one reusable program.
The engine must visit the key location, return through the same junction with a
new capability, enter the vault, acquire a second capability and reach the exit.
Both capabilities are required at the final gate. Every reachable state must
permit return to the entry. These are game-authored logical constraints, not a
particular game's inventory system or a physical map.

`recipe.json` and `catalog.json` are the actual input pair. The current qualified
packet is `generated/progression-audit-public/baseline`; use `generate-metadata`
with these inputs and a new destination to regenerate it. The optional schematic
checks and shows the saved witness; C# owns all state exploration and return
qualification. No art, mockup or Godot is required.

Capabilities are persistent and nonconsumable. Gates apply in both directions of
a bidirectional link; model asymmetric travel as separate directed connections.
The solver does not model consuming keys, numeric inventory, combat, timers or
runtime interaction. Broader world generation remains unfinished.

`upper-recipe.json` uses the same catalog and exercises the supported conjunction
of 16 persistent capabilities. Its qualified packet is
`generated/progression-audit-public/upper`. Both public packets exactly match SDK
output. The current 320-case matrix additionally tests five milestone profiles
against grants, gates, initial abilities, travel direction and return policy.
Unknown nested fields are rejected through the ordinary strict `ReadAsync` and
CLI input boundary, including misspelled return requirements and unsupported
consumable-key declarations. These source constraints are never silently ignored.
See [S03.4 qualification](../../../docs/world-generation/stages/s03-gameplay-topology-and-regions.md)
and [exact evidence](../../../docs/evidence/2026-11-01-progression-acceptance-verification.json).

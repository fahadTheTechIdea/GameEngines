# Geometry-derived facing for game-world content

These are reusable framework inputs for the same C# generation/session APIs.
They contain no game-specific branch, artwork or engine runtime dependency.

`MetadataPopulationGrammar.Facing` derives each required member's heading from
its actual path or boundary. `MetadataPlacementBinding.RotationDegrees` is the
additional source-local offset. Zero aligns local +X to the selected direction;
+90° rotates it toward world +Y. The engine rotates the complete source polygon,
reservation and access ports and then applies the ordinary constraints.

| Input | Declared behavior |
| --- | --- |
| authored | Bent-path content follows its incoming segment; boundary content faces inward. |
| offset | Same positions and sources; a 90° source-local offset changes path-facing geometry. |
| clockwise | Clockwise boundary winding still gives the intended inside direction. |
| nested / translated | Two complete nested programs reference their accepted parent parts; translating the frame preserves headings. |
| reservation-tangent | Objects align tangentially around an actual accepted source reservation. |
| region | Required objects face the filled side of a generated region outline. |
| hole-tangent | Tangential objects follow a hole loop; the witness retains its reversed filled-side normal. |
| split | An explicitly disconnected allocation retains both components; the selected loop supplies heading and position. |
| route / reverse-route | Content follows the actual accepted route in its selected direction, including bends. |
| negative-reservation | Outward-facing asymmetric source clearance cannot satisfy the original separation; refuses. |
| negative-hole | Filled-side outward access extends beyond the domain; refuses. |

The two refusal inputs retain the same source dimensions and prescribed positions
as their tangential counterparts. They show why a developer must choose a feasible
layout/facing combination; the generator does not shrink sources, reduce counts,
increase budgets or silently repair the requested world.

The directional archetype is a distinct 0.6 × 0.2 m catalog source with a
0.7 × 0.3 m reservation and a positive-X entrance/approach. Existing source-world
archetypes stay unchanged. Eleven complete packets retain 114 nodes, 103 physical
sources and 64 oriented members. These small technical declarations qualify the
geometry contract, not complete game-world composition or visual quality.

The public API remains `WorldMetadataGenerator.Generate` / `GenerateFilesAsync`.
Inspect `PlacementPlan.PopulationOrientations` for purpose, source, tangent,
filled-side normal, angular offset and effective rotation per node. Expanded root
and nested placement bindings expose each effective `RotationDegrees`. Native
read/publication/session replay reconstructs all geometry; JavaScript consumes it.

The planar arc-length helpers also expose `PathPoses` / `BoundaryPoses`. Exact
bends use incoming tangents. Facing is optional; sockets and interior-region
grammars reject it, and inside/outside modes require a boundary. A consumer cannot
reorient accepted source members. This does not supply sprite-facing selection,
continuous offset curves, animation or Godot integration.

[API contract](../../../docs/WORLD_METADATA.md#geometry-derived-population-facing)
and [source-qualified verification](../../../docs/evidence/2026-10-30-population-facing-verification.json).

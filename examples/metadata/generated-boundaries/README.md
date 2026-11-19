# Source-derived surroundings

These framework examples arrange declared catalog members around accepted source
geometry. They introduce no game/genre switch, copied boundary polygon, random
scatter, artwork or Godot dependency.

| Example | Declared source | Required result |
| --- | --- | --- |
| nested | First source child in each of two actual parent programs | 15 nodes, 14 physical sources, 8 surrounding members |
| translated | Same complete programs with translated world/root/parent frames | Same counts; surroundings translate exactly 3m |
| wider | Same complete programs with wider source footprints | Same counts; boundary positions rederive from the new source |
| footprint | Actual source-part footprint | 6 nodes, 5 physical sources, 4 surrounding members |
| reservation | Actual declared clearance reservation | Same counts; no footprint substitution |
| region | Generated region outline | Same counts; complete footprints remain in the region |
| hole | Explicit inner loop of the generated region | Same counts; inside means the filled region side |
| allocation | Generated local allocation outline | Same counts; complete footprints remain in that allocation |
| disconnected-allocation | Explicitly owned split allocation in a C-shaped region | Same counts; both source loops remain; loop selection is explicit |

Run through the existing `WorldMetadataGenerator` API or
`generate-metadata <recipe.json> <catalog.json> <new-output-directory>` CLI.
Every source/callback/edit/publication example retains its complete catalog,
physical source dimensions and generation budgets.

## Reusable source selection

`MetadataPopulationGrammar.GeneratedBoundary` supplies a
`MetadataGeneratedBoundaryPattern`. `Footprint` and `Reservation` sources use an
explicit `NodeId`, the actual program parent when selectors are null, or a declared
parent `PartId` plus `PartIndex`. The source must already exist in the accepted
population source phase. Reservation selection fails if no reservation was declared.

`Region` uses `RegionId`; `Allocation` also uses `AllocationId`. Source outlines
retain holes and components. Multiple loops require an explicit `LoopIndex`.
Loops are canonical counterclockwise polygons starting at their lexicographically
smallest vertex, sorted by that vertex and area. Exact collinear subdivisions are
removed without moving vertices. Start-edge indices address this canonical order.
Unresolved endpoint topology, duplicate edges and unsupported complexity refuse.

Spacing, start distance, span and offset are explicit design rules. Samples use
the shared arclength primitive; an exact bend uses the incoming edge normal.
`WorldBoundarySide.Inside`/`Outside` select a side. A hole reverses the primitive
side so that region-inside remains on filled land. These are sampled segment
normals, not a continuous mitered offset curve. No wrap or capacity fallback exists.
Complete footprint, terrain, parent, source-port and route constraints still apply.
Generated positions remain world-coordinate witnesses, even in repeated local frames.

## Disconnected ownership

The last case declares `WorldLocalProgram.AllowDisconnected=true` for one ordinary
power-diagram program. The shared component owner retains every clipped cell and
boundary, sums actual area and checks the seed and original 80m² minimum. Its east
allocation has two 48m² pieces, totaling 96m². Connected ownership remains the
default; it refuses this source rather than dropping an island. Area-balanced
intent and regional seed policies remain unchanged. Ownership does not create
physical connectivity, roads, bridges or traversal between components.

## Evidence and limits

Placement stores `PopulationBoundaries` with source identity/hash, canonical loop,
hole/side, purpose, member IDs and world positions. Native replay rederives them
from the actual source. The JavaScript consumer checks saved arithmetic, binding
selectors and node positions; the C# owner remains the full source/replay authority.

Owner/member spacing guards capture only their dependent source poses. A relative
conflict with the same owner, or between two members following that owner, fails
without redesigning unrelated source content. Other failure kinds keep their
applicable existing whole-assignment proof. No arbitrary relocation or changed
source geometry repairs a contradiction.

Qualification covers all nine complete cases, exact source preservation, edits,
rollback, cancellation, publication/read/checkpoint replay, tamper, scoped clauses
and independent geometry. It does not create per-item sprite facings, fences as
art, automatic content counts, arbitrary region topology, full world density,
game-design acceptance or native runtime behavior. Godot and artwork remain deferred.

[Exact commands, source hashes and remaining gates](../../../docs/evidence/2026-10-30-generated-boundaries-verification.json).

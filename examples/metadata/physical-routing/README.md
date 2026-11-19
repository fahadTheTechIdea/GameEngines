# New routing through a generated physical world

This framework example extends the richer-density terrain-demand program with a
new typed journey between existing destinations. The engine retains all six
compounds, 241 environmental members, 270 total nodes and 269 physical members
(257 solids). All nineteen source routes remain exact. A twentieth journey is
searched against every solid with the existing candidate/edge/work limits.

`recipe.json` and `catalog.json` are actual reusable inputs. Generate a new packet
with `generate-metadata`; the current packet is `generated/physical-routing-accepted`.
The world remains `PartialScope`: it is a connected physical reference program,
not a complete game, full terrain coverage or owner visual acceptance.

Placed solids now form a separate immutable query layer. The terrain definition
keeps its authored masks; metadata persistence retains actual solids in the
access/placement plans. Query consumers must retain the query object or pass both
its base definition and physical obstacles. The same owner checks points,
segments, full-width passages, areas, interiors and corridors.

Candidate proposals can exceed a search graph's capacity. A checked seed route
supplies an upper cost bound. Lowest-cost mode retains every eligible proposed
feature that can improve the incumbent, or refuses if that complete set exceeds
the original candidate cap. Every obstacle stays in collision tests. First-feasible
mode makes no minimum-cost claim. `CandidateAudit` exposes retained/pruned/probe
counts and the bound; C# replays the search, while JavaScript checks saved facts.

Separate primitive stress cases qualify 257/512/1,024 solids under the default
256-candidate and 20-million-work limits, with independent full-feature Dijkstra
and capsule/rectangle clearance oracles. These are bounded observations, not
universal world-scale latency or continuous-path optimality claims. Terrain-mask,
combined-vertex, physical-site, candidate, edge and work ceilings remain explicit.
Automatic whole-world intent/composition, broader strategies and visual acceptance
remain open. Godot is deferred.

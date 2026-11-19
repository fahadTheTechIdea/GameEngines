# Terrain-selected regional networks

These are framework verification inputs. Four complete source programs share a
terrain domain; each retains one compound and its two required modules. Authored
ownership, per-instance origins and individual overland links are absent. The
engine derives ownership/local programs, resolves exact named ports, then uses
the shared terrain-aware network solver to choose feasible connections.

Inputs:

- `request.json`: connected sparse backbone with terrain-kind costs of1.
- `cost-request.json`: bog travel cost changes to9. The elevation plane, complete
  local source packets and50-million-work network limit remain exact. The selected
  macro graph changes.
- `blocked-request.json`: a hard barrier blocks a required pair. Generation
  returns the actual failure witness and publishes no partial world.
- `composed-request.json`: staged transitions, a separate terrain-selected network
  and physical ownership feedback operate together in one complete world.

Use the public C# owners:

```csharp
var definition = new MetadataRegionalOwnershipPlanner().Plan(request);
var world = new MetadataRegionalWorldGenerator().Generate(definition);
```

A game can prohibit a direct link while allowing an indirect journey. For
example, when adapting the input above, set the network rule's options to:

```csharp
var options = rule.Options with
{
    ForbiddenConnections = [new("arrival", "grove")]
};
```

The destination IDs come from that rule, and the restriction applies in both
directions. Regional preflight rejects a declaration if its allowed graph cannot
meet the requested connectivity or single-connection-failure policy; a graph
that passes must still pass terrain and physical routing.

The CLI route is `generate-derived-regional-world <request.json>
<new-output-directory>`. `MetadataRegionalGenerationSession` accepts source
edits; a cost-only network change reuses all four exact local packets. Saved
restore, failed-edit preservation and exact rollback are verified.

`NetworkRules` binds named destination IDs to real region/node/port endpoints and
declares purpose/scale, traversal/search, required pairs and connectivity policy.
It supports multiple destinations in one region and independent channels with
different kinds. `Topology` supplies staged progression under its own kind.
The document's `NetworkPlan` retains candidate attempts, actual costs, selected
forward/reverse paths, connectivity proof and mappings to ordinary regional
routes. No image, artwork approval, game name or Godot runtime is required.

This adapter currently allows1..4network rules,2..32destinations per rule and a
combined128regional links in shared2D space. This fixture's connected backbone
without reuse is minimum over its evaluated feasible candidate graph. It does not prove a
global continuous optimum or automatically revise source/anchor choices when a
network fails. Scale labels do not infer infrastructure hierarchy. Crossing paths
do not create automatic multi-level junctions. Full world design, broad variety/
scale, selective locks and independent streaming remain unfinished.

See the [contract](../../../docs/WORLD_METADATA.md#terrain-selected-regional-networks)
and [current evidence, commands and retained failures](../../../docs/evidence/2026-10-31-regional-networks-verification.json).

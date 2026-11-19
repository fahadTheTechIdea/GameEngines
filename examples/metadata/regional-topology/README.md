# Procedural regional connections

This is an engine-independent framework fixture, not a game. It has six named
regional destinations, each with one complete compound and two required modules.
Ownership and terrain-qualified origins are derived from source rules. No city,
street grid, mockup, artwork or Godot project is required.

`request.json` declares two topology stages: early sites lead through a required
pass, then a later site leads to the goal. The shared topology owner generates
all seven regional links, including two extra loops, within degree four. The
normal terrain/physical route owner connects exact named compound ports in both
directions. Stage members are available branches; only declared transitions are
mandatory. Source kinds and purposes belong to the developer's recipe.

The same public C# flow handles authored or generated regional links:

```csharp
var definition = new MetadataRegionalOwnershipPlanner().Plan(request);
var world = new MetadataRegionalWorldGenerator().Generate(definition);
```

The public CLI equivalent is `generate-derived-regional-world request.json
<new-output-directory>`. `changed-request.json` changes only the topology seed.
The qualified result changes macro branches and routes while retaining exact
local packets. `MetadataRegionalGenerationSession` also accepts this source edit,
persists history, refuses invalid edits and supports an exact source rollback.

Inspect `Definition.TopologyPlan` for original inputs, logical destinations,
stage membership, generated edges, progression proof and real endpoint bindings.
The original `Connections` input is empty in this fixture. Adding authored links
is supported when their kinds are not owned by a topology rule. The generated
world and its inspector contain every original physical local member.

This regional adapter currently supports2..16 named destinations, one per bound
region, in a shared2D surface. A transition proof applies to its declared channel;
other authored channels can provide other journeys. Infeasible graph demand,
invalid ports, terrain/solid route failure and limits reject rather than drop
links. It does not choose different graph seeds to repair terrain, generate road
surface meshes, prove a complete playable game or establish broad genre coverage.

See the [API contract](../../../docs/WORLD_METADATA.md#procedural-connections-between-regional-destinations)
and [qualified commands, hashes and limits](../../../docs/evidence/2026-10-31-regional-topology-verification.json).

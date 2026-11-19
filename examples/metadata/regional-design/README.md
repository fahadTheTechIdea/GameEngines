# Regional design composition

This framework example selects between two complete programs for one of four
regions. The first generates successfully but has the wrong required role; the
second satisfies the declared role and physical-member requirement. The engine
derives regional ownership and selects terrain-aware, two-way connections among
the four complete programs. No specific game, artwork or Godot project is needed.

From the repository root:

```text
dotnet run --project src/WorldCreator/WorldCreator.Cli -- generate-regional-design examples/metadata/regional-design/request.json <new-output-directory>
dotnet run --project src/WorldCreator/WorldCreator.Cli -- verify-regional-design <new-output-directory>
```

The output includes the design source and both attempts in `world-design.json`,
the complete regional world, its child packets and diagnostic schematic. The
design demonstrates selection over explicitly supplied alternatives; it does not
generate game-specific purposes or guarantee that a player's entire world is
complete. Each candidate retains its own declared scope and existing work limits.

`semantic-request.json` demonstrates a second case: an alternative program uses
`sites-0` instead of `compounds-0`. A declared `compound` role resolves that
program's actual network endpoint and keeps its authored access port. A prior
option lacking the role is recorded as rejected. Substitute this filename in the
same commands above to run that case.

`semantic-root-request.json` also changes the alternative program's root ID.
The same role binding resolves its new `new-region-program/sites-0` endpoint;
the other regions and their complete physical connections remain required.

`semantic-progression-request.json` extends this composition to gameplay roles.
Starts, goals and milestones select exact node kinds/archetypes instead of
depending on one generated root path. The first complete program lacks the start
role and is rejected; the second has a different root and part path and satisfies
the progression. All eight physical modules, four compound parents and three
bidirectional regional routes remain required. Use the same generate/verify
commands with this filename. `world-design.json` records each role binding and
the exact-node path. A logical local entry link is explicitly separate from the
three qualified physical regional connections.

`seeded-standard-request.json` and `seeded-wide-request.json` have the same
two equally preferred program options and differ in `SelectionSeed`. These
particular seeds choose different source-sized compound footprints, while all
four regions and their two-way connections remain required. Generate each into
a separate new output directory to inspect the two world schematics. Other seeds
may select the same option; the seed does not change terrain by itself.

`world-seed-a-request.json` and `world-seed-b-request.json` use a `SeedPlan`
to vary authored terrain detail, ownership partition choices and program
selection from one seed. Both inputs retain the same terrain settings, content
requirements, physical feedback and feasible-path routing policy; only the seed
differs. Generate each into a separate directory and use `verify-regional-design`
to replay it. A different seed can be infeasible or yield a similar result.

`world-seed-lowest-cost-request.json` uses the world-B input with the network
route objective changed to `LowestCost`. It retains the same terrain and work
limits. The engine's lowest-cost claim is relative to its bounded candidate
graph, not a claim of a continuous globally optimal route.

For editable design history, use `region-session start-designed <design.json>
<new-output-directory>`, then `region-session edit-designed
<generation-session.json> <design-edit.json> <new-output-directory>`.
The edit contains `Id`, `Purpose`, `SourceRuleId`, `ExpectedDefinitionSha256`,
`ExpectedWorldSha256`, `ExpectedDesignSha256`, and `Source` (the complete revised design request).
Use the accepted definition hash from `world-regions.json` and world hash from
the checkpoint; its accepted report also supplies `DesignSourceSha256` for
`ExpectedDesignSha256`. The checkpoint retains the original authored seed and each
accepted design revision, and regenerates them when restored.

The public SDK uses the same `MetadataRegionalGenerationSession`:

```csharp
var session = await MetadataRegionalGenerationSession.StartDesignedAsync(generator, initialDesign, reporting, token);
var before = session.Snapshot;
var edit = new MetadataRegionalDesignEditRequest("new-seed", "Revise the world seed", "game:world-edit",
    before.World.DefinitionSha256, before.Report.WorldSha256, before.DesignSourceSha256, revisedDesign);
await session.ApplyDesignAndPublishAsync(edit, newOutputDirectory, token);
```

`session.Snapshot.DesignSource` returns the accepted authored design. Failed,
stale or cancelled revisions retain the accepted state. All generation and
publication still use the existing engine owners and finite limits.

`ProtectionChanges` can add or release the existing regional input/location
protections during a design edit. Active protections participate in candidate
acceptance: the engine can reject a conflicting program and select a later
compatible one. `session.Snapshot.Report.DesignAttempts` records that sequence,
including the reason for each rejected choice. A source with no compatible
declared option fails and preserves the accepted session. Restoring a checkpoint
recomputes the choices with the protection policy active at each edit.

Protected region origins also constrain each candidate's regional planning.
Changing a design seed can therefore regenerate terrain, ownership and complete
programs around an accepted fixed destination. Its effective ownership source
records the pin while `Snapshot.DesignSource` retains the unmodified authored
request. Explicit contradictory pins are rejected; all normal terrain, source
size, movement and work constraints still apply.

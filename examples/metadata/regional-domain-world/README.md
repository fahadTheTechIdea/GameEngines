# Regional world with an independent domain

This framework fixture joins four terrain-qualified regions to a separate
logical domain. A required journey follows the generated regional network,
uses the explicit portal and returns. The regional route geometry and the
independent domain use their existing generation owners; the portal represents
a game-defined transition and does not create a physical connector.

From the repository root, the CLI can generate a new output directory:

```powershell
dotnet run --project src/WorldCreator/WorldCreator.Cli -- generate-regional-domain-world examples/metadata/regional-domain-world/definition.json generated/regional-domain-example
```

Open `index.html` in that output for the regional and domain inspectors. The
composition's `world.json` retains the regional route evidence associated with
domain journey steps. Read it using `MetadataRegionalDomainWorldGenerator.ReadAsync`
to replay the source and verify the composition. This is a bounded framework
fixture, not a complete game or production artwork.

`marker-definition.json` is a second source with two outdoor destinations. Its
regional path connects positioned non-solid markers without structure ports,
then an explicit portal connects to an independent logical domain. The domain
endpoint selector is `UseNodePosition`; terrain and occupancy checks still apply.

`designed-definition.json` supplies a regional design request with two authored
program options. The first has only a logical local link and cannot satisfy the
domain journey; the second supplies a qualified physical route. The generated
index and `selection.json` show the rejection and the selected option. Pass
this file to the same CLI command to generate that variant.

The procedural regional examples use the existing `RegionalDesign` source route with a single
complete program combination and explicit eight-module/four-parent requirements.
They retain the original ownership request, without embedding a generated
ownership receipt in an authoring file. Generation derives and verifies current
receipts; historical outputs still reject stale work or geometry. The required
physical contents, floor transitions and journeys are unchanged.

`floors-definition.json` joins a four-region network to two generated physical
floors. Its journey uses regional route geometry, local room passages and
explicit entrance and stairs transitions, with a required return. The inspector
distinguishes physical route evidence from game-defined domain changes; it
does not create a 3D entrance or stairs.

`progression-definition.json` adds a domain-qualified gameplay rule to the
two-floor example. Reaching the first floor grants a capability that the stairs
transition requires; the world also retains the separate physical round-trip
journey. The capability check is logical and does not implement an inventory or
runtime interaction.

`semantic-progression-definition.json` selects the regional entry by kind and
archetype. A candidate lacking that role is rejected before accepting the next
complete program. The world keeps four regions, two physical floors, the key
before the gated stairs and the separate physical round trip. Progression
results retain the source role-to-node binding; path witnesses use exact domain
and node IDs. Missing or ambiguous roles are never guessed. Substitute this
filename in the existing CLI command to generate the framework example.

`capability-journey-definition.json` adds the same kind of key requirement to
the qualified physical journey itself. It records when the journey acquires the
key and proves it has the key before the stairs transition and on its return.
The world inspector shows the capability state alongside the journey steps.

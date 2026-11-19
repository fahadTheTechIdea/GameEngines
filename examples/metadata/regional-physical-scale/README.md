# Complete regional physical-content scale example

This framework fixture declares eight resource regions, each with 128 solid
objects and their full reserved interaction space. The C# engine expands all
1,032 metadata nodes, positions every 1m × 1m body and 1.5m × 1.5m reservation,
and qualifies an outward/return journey between the outer region destinations.
The authored repeated line is this fixture's layout; ownership does not impose
streets or a gameplay grid. This supplies declared resource content and travel,
not a complete game or general world-design acceptance.

Use the ordinary engine APIs with `definition.json`:

```csharp
var definition = await WorldMetadataGenerator
    .ReadAsync<MetadataRegionalWorldDefinition>(definitionPath, token);
var engine = new MetadataRegionalWorldGenerator();
var world = engine.Generate(definition, token);
await engine.PublishAsync(world, newOutputDirectory, token);
```

The CLI's existing `generate-regional-world` entry uses the same generation,
source replay, reporting and publication owners. No game setup, images, special
catalog provider or Godot dependency is required for this metadata input.

Regional collision qualification indexes each node's complete source envelope,
including reservations/features/approaches. Strictly separated envelopes can
skip exact collision checks. Touching/overlapping candidates retain exact polygon
tests and original first-conflict order. All required content and original work,
node, surface, site and port limits remain hard. Dense overlapping envelopes can
still exhaust the bounded pair work; this is not unlimited streaming support.

The saved qualification checks 1,024 solids and reservations, all 4,096 body
vertices, 458,752 cross-region reservation pairs and both full-width directions.
The current default-budget packet is exactly equal between SDK and CLI. Earlier
Core produces the same geometry with a higher comparison-only budget but exceeds
the normal 20-million regional budget. Browser review was NOT RUN; art and Godot
remain deferred. See [S08](../../../docs/world-generation/stages/s08-deterministic-chunks-and-persistence.md)
and [the precise evidence scope](../../../docs/evidence/2026-11-01-regional-physical-scale-verification.json).

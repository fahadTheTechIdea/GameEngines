# Independent terrain windows

This framework example defines a ridge, a crossing valley, a level platform and
seeded detail on a fractional, negative-origin lattice. It is reusable terrain
input, with no required city, game genre, artwork or runtime.

`recipe.json` retains the complete source definition. `requests.json` asks for
four vertex windows sharing their boundary vertices, with a one-cell halo.
Windows contain original global indices and row-major values. They deliberately
do not masquerade as heightfields with a translated sampling origin.

Use the public Core APIs:

```csharp
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

var recipe = await WorldMetadataGenerator.ReadAsync<WorldTerrainRecipe>(
    "examples/metadata/terrain-windows/recipe.json");
var requests = await WorldMetadataGenerator.ReadAsync<WorldTerrainWindowRequest[]>(
    "examples/metadata/terrain-windows/requests.json");
var terrain = new WorldTerrainGenerator();
var first = terrain.GenerateWindow(recipe, requests[0]);

// This enumerates one generated window at a time. Complete assembly explicitly
// allocates the final field and refuses any missing samples.
var field = terrain.AssembleWindows(recipe,
    requests.Reverse().Select(request => terrain.GenerateWindow(recipe, request)));
var query = new WorldHeightFieldQuery(field);
```

Each window records its source recipe digest, implementation module identity,
versioned algorithm, request, actual halo range and charged sample work. Saved
single windows read through `ReadAsync<WorldTerrainWindow>` replay automatically;
`ReplayWindow` returns a detached verified window. Changed values, stale builds,
different sources, malformed requests and exhausted work refuse explicitly.
These hashes establish consistency, not trusted external authorship.

Both source dimensions retain the existing 2–513 bound. Individual requests
compute only their selected samples, including halo work. Assembly accepts at
most 4,096 windows and uses the smaller of its explicit work bound and the source
recipe's bound. Matching overlaps are represented once. Request order cannot
alter the complete reconstructed field. Cancellation never returns a partial
successful field. Only requested height values are evaluated during independent
generation; a valid window alone does not certify every unrequested height.

Whole-field `Generate` uses the same global-coordinate height evaluator and
retains prior field/world output exactly. An assembled field works with existing
height and surface consumers.

For a terrain-aware regional journey, `regional-surface.json`, `region.json` and
`regional-route-request.json` declare the source ridge, allowed region, endpoints,
actor radius, slope/cost rules and finite search budget. They are game-authored
inputs, independent of a city or grid layout:

```csharp
var surface = await WorldMetadataGenerator.ReadAsync<WorldSurfaceDefinition>(
    "examples/metadata/terrain-windows/regional-surface.json");
var region = await WorldMetadataGenerator.ReadAsync<WorldCreator.Core.Geometry.WorldPolygon>(
    "examples/metadata/terrain-windows/region.json");
var request = await WorldMetadataGenerator.ReadAsync<WorldRouteRequest>(
    "examples/metadata/terrain-windows/regional-route-request.json");
var local = WorldSurfaceQuery.CreateRegional(surface, region);
var journey = new WorldSurfaceRoutePlanner().Plan(local, request);
```

This regional query stores only its original-lattice window and halo. Height,
gradient, full-width routes, costs and work match a full-field regional query.
`local.HeightField.Geometry` describes the global lattice;
`local.HeightField.CompleteField` is null for window storage. The window variant's
`Snapshot` is also null if the region is wholly outside the sampled field;
authored patches and global provenance remain valid. Unknown samples refuse.
`RestrictTo` reuses validated immutable queries without full-field regeneration
or cloning. Supplied complete fields still receive full validation.

The normal constructor and metadata publication retain complete fields by default.
Explicit `TerrainCapture` now supports persisted bounded terrain and metadata
session edits through the same surface owner; see the
[capture example](../terrain-capture/README.md). Independent content chunks,
globally coupled partial drainage and runtime streaming remain open. Storage
chunk boundaries remain distinct from world layout and terrain sample windows.

[API contract](../../../docs/WORLD_METADATA.md#independent-terrain-windows),
[S08 plan](../../../docs/world-generation/stages/s08-deterministic-chunks-and-persistence.md)
and [qualified evidence](../../../docs/evidence/2026-10-31-terrain-window-verification.json).
The regional query/path increment has [separate current evidence](../../../docs/evidence/2026-10-31-regional-window-verification.json).

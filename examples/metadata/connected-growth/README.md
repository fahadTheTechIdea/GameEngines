# Connected physical world programs

These framework examples grow five complete expedition bases from one arrival
anchor and a generated content graph. No per-base positions, world grid, artwork
or Godot project are required. Each base retains its chamfered compound footprint,
solid shelter, work yard, clearance reservations and nested supply cache.

The shared C# engine derives placement candidates from connected neighbors' exact
source envelopes, a gap and ordered local directions (20°, 85°, 155°, 225°, 295°).
The existing search checks terrain, complete nested placement, access and routing
before accepting the assignment. Each base's work area also grows from its
shelter, using its own bounded local grammar. These local directions do not
impose global streets or a world lattice.

- `inland`: 21 nodes, 20 physical footprints, five generated regional connections,
  five arrival links and five shelter-to-yard links. All fifteen travel
  destinations belong to one physical graph. Caches are solid content, not
  navigation destinations.
- `inlet`: adds non-traversable water without changing content, topology or sizes.
  Placement and paths change while all members remain.
- `larger-content`: changes shelter width from 4m to 5m. The same source-sized
  yard moves to retain its gap; no source geometry is scaled down to fit.

Use the existing `generate-metadata <recipe.json> <catalog.json> <new-output-dir>`
command. Current qualified packets are under `generated/connected-growth-qualified`.
Each includes the world, inputs, summary and JavaScript schematic. A
[technical comparison](../../../generated/connected-growth-review/comparison.png)
plots the same consumer geometry; it is not production artwork or a browser test.

Regional routes declare a 10-million-unit search budget per direction; local and
arrival routes each declare 2 million. The unchanged aggregate cap is 200 million.
An earlier 5-million regional budget failed explicitly on the inlet case; the
qualified longest leg uses 8,643,889 units. Its failure evidence is preserved.

Verification covers independent source-size/containment/support-gap checks,
backtracking, connected port approaches, route clearance, terrain and size edits,
saved-plan replay, dependency invalidation, finite failure and actual CLI exports.
See [evidence](../../../docs/evidence/2026-10-28-connected-growth-verification.json).

This is a bounded physical-generation increment. Counts, catalog content,
compound envelopes, local grammars and intended access remain game-authored.
Nested-stage declarations are still authored per parent. Automatic interior
topology/walls, broader scale and genre diversity, runtime streaming and final
whole-world acceptance remain open. Godot is deferred.

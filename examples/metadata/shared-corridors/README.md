# Shared physical passages

Diagnostic fixture for the generic surface pipeline, not a proposed town or complete world.
Five authored destinations and four bidirectional connections yield eight directed journeys,
four unique physical segments and five junctions. Two horizontal segments are shared by
distinct logical connections; the crossing explicitly permits sixteen directed transitions.

`recipe.json` opts into `Corridors.Groups` by connection kind. `catalog.json` declares the
destinations and relationships. No district, block, parcel or city generator is involved.
The existing CLI `generate-metadata` exports the packet to a new directory; the generated
viewer draws saved segments once and shows the saved junction envelopes in amber.

The focused test also exercises branched and rotated negative-coordinate inputs, one-way
directions, incompatible profiles, unsupported levels, terrain hazards, step discontinuities,
ambiguous numerical intersections, network routes and placement feedback. Read/export replay
refuses altered geometry. See [verification](../../../docs/evidence/2026-10-27-world-corridors-verification.json).

This fixture consolidates coincident paths and does not request failure tolerance.
The shared engine separately supports opt-in route reuse, service capacity and
[single-segment failure checks](../physical-resilience/README.md). This fixture
does not certify those options, elevated crossings, vehicle turning or artwork.

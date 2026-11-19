# Source-domain content programs

Framework inputs for purposeful content in actual generated surroundings. No
mockup, artwork, game setup or Godot project is required. The existing C# metadata
generator owns both source generation and content placement.

| Inputs | Declared result |
| --- | --- |
| `base.recipe.json` / `base.catalog.json` | Two complete 80m² modules, four surroundings features per module, eight buffer members, four work-area members, preserved forward/reverse journeys; 15 nodes. |
| `more.recipe.json` / `more.catalog.json` | Three identical source modules, six buffer members and two work-area members per module; 28 nodes. The same reusable program selects actual parents. |
| `source-edit.recipe.json` / `source-edit.catalog.json` | Both source bodies grow to 88m² with matching source entrances/reservations. All required content and surroundings remain; no source is resized to fit. |
| `reservation.recipe.json` / `reservation.catalog.json` | An unchanged 100m² activity body with two required 4m² objects in its declared exterior reservation. The body's required open fraction remains satisfied. |

Use the current CLI's `generate-metadata <recipe> <catalog> <new-output-dir>` or
`WorldMetadataGenerator.Generate/GenerateFilesAsync`. Outputs include source
inputs, the complete metadata document, ground plan, physical accounting and a
JavaScript/SVG schematic. Output directories must be new. Open `schematic.html`
through the normal local preview and enable **Population strata** to inspect
source masks and required anchors; select objects for source provenance.

`PopulationSource` generates the original modules, site features, material masks
and journeys first. The reusable nested program selects every actual `site`
parent. Its buffer binding consumes that parent's `GreenBuffer`, follows the
selected feature edge and orients each unchanged source object along it. Its
equipment binding partitions that parent's covered `work-floor` material through
the shared connected-area/interior search. Every whole body or reservation must
fit the selected source mask; existing source paths remain exact and unobstructed.

The source sequence is explicitly aligned with its frontage. A world-horizontal
sequence at 57° produces a real protected-journey conflict and is retained as a
refusal control. It is not relocated automatically. Band counts, spacing and
catalog counts are explicit constraints and must agree. Missing domains, gaps,
oversized objects, invalid selectors and exhausted work refuse rather than losing
members or substituting another domain.

Exterior children explicitly request `ParentBoundary: Reservation`. Ordinary
nested stages still use the parent's body. A reservation is not solid geometry,
finished surroundings or a declaration that the whole world is populated. Final
ground/interior requirements are measured even when conservative preflight cannot
prove their outcome.

These small inputs qualify reusable source-domain composition. They do not
demonstrate a complete professional game world, automatic terrain-specific site
grammar, coupled conflict repair, independent content streaming, artwork or
gameplay interaction. The full master-plan/design gate remains open; Godot and
artwork remain deferred.

[API and scope](../../../docs/WORLD_METADATA.md#content-from-actual-site-and-ground-domains)
and [exact verification](../../../docs/evidence/2026-10-31-source-domains-verification.json).

# Population follows generated terrain routes

The recipe declares a base phase with seven required physical members and their
interaction routes. The complete phase adds four required markers by referencing
the generated approach connection. It supplies no copied route coordinates.
All eleven physical members retain their catalog footprints and reservations.

```text
generate-metadata recipe.json catalog.json <new-initial-directory>
generate-metadata moved-rock.json catalog.json <new-moved-directory>
```

Retained packets are `generated/route-population-initial` and
`generated/route-population-moved`. Moving the rock face changes the generated
route and all four derived marker locations. Other members keep their exact
positions and source geometry. Each final packet retains its exact source paths.

`blocked-passage.json` places the detail too close to the source route. Its full
reservation intersects the protected passage, so generation rejects with
`METADATA_POPULATION_PASSAGE_BLOCKED` in placement feedback and publishes nothing.
It cannot move the base road, resize a source footprint or drop required content.

The session API caches the explicit source phase and records its allocation
dependency. Detail-only edits reuse the source; source-terrain edits rerun it.
Checkpoint restoration replays both phases and their edit history.

[Evidence](../../../docs/evidence/2026-10-27-world-route-population-verification.json)
covers the API, actual CLI, independent geometry, JavaScript and browser outputs.
This is a bounded engine qualification, not the final city. Network-generated
sources, recursive phases, generated-region population, sightlines, variation,
scale, artwork and native Godot acceptance remain open.

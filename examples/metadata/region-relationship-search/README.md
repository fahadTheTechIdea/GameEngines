# Relationships drive generated regions

This reusable metadata example keeps three complete compound programs and their
travel requirements while constraining the distance between two required locations.
It has no city, artwork or Godot dependency.

`recipe.json` and `catalog.json` request all 11 nodes and 9 exact physical sources.
The required `alpha`/`beta` region anchors must be at most 49 meters apart. The
first unconstrained cut does not meet that rule; the C# region owner reconsiders
the declared cut alternatives before committing the layout. The accepted anchors
are about 33.49 meters apart. A retained terrain void, source dimensions, minimum
areas/clearances, six local and four network travel directions remain required.

`changed-recipe.json` allows separation up to 70 meters. Normal deterministic
selection then chooses a different valid layout with anchors about 50.00 meters
apart. Suitable area remains 7,904 m² and every declared physical member remains.
The engine calculates the geometry; the JavaScript schematic inspects saved data.

Use the existing `WorldMetadataGenerator`/`MetadataGenerationSession` APIs or
`generate-metadata`/`metadata-session` commands. An impossible relationship rejects
generation/publication; a failed edit preserves the previously accepted world.
Checkpoint restore replays the actual source history and selected region designs.

The shared region owner also handles relationships between occupied components
and protected whole-component seed regions. A later component can reject an earlier
cut and cause bounded reconsideration. Existing `RequiredAssignment` is enforced
against canonical intent order and current component indices. Exact fixed-point
controls remain available through `RequiredPosition`; every point/area/clearance
and declared density constraint continues to apply.

Search is limited to declared grammars and budgets. A component's cut-attempt cap,
component assignment/state caps and shared aggregate work bound remain hard.
Finite exhaustion does not establish that no possible continuous layout exists.
This is a framework fixture, not whole-world design or every-game acceptance.

See the [API contract](../../../docs/WORLD_METADATA.md#terrain-derived-required-regions-and-placement-anchors)
and [source-qualified evidence](../../../docs/evidence/2026-11-01-region-relation-search-verification.json).
Browser DOM/render and live-agent behavior remain unqualified in this increment;
broader design/variety/scale work remains open. Godot/artwork are deferred.

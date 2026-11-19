# Shared interfaces drive local allocation

This engine-independent example generates two complete local program areas under
a required shared-boundary span. It uses the same catalog/recipe, local allocation,
placement and route owners as the other metadata examples. No city, artwork or
Godot is required.

`recipe.json` and `catalog.json` retain seven declared nodes: the root, two physical
structures and four station metadata children. The stations have no invented
physical dimensions. Each structure retains its 14×6m source polygon and 16×12m
reservation. The domain is 40×30m, with all source boundaries declared consistently.

The required interface is at least 35m. The first principal-axis split offers 30m,
so the declared search selects an alternative with a 40m shared boundary. Both
source-sized structures fit their allocations, and both directions of the explicit
physical interface route remain required. A semantic boundary is not itself a route.

`changed-recipe.json` lowers the minimum span to 20m. Normal deterministic selection
then chooses the other layout with a 30m interface. The structures move, their
dimensions/reservations remain exact, and all station children and travel remain.
The total suitable area stays 1,200m².

Use `WorldMetadataGenerator`/`MetadataGenerationSession` or the existing
`generate-metadata`/`metadata-session` CLI commands. Checkpoints replay the actual
source and selected cuts. An impossible interface refuses publication and a failed
edit preserves the accepted world. JavaScript inspects saved engine data.

The same local owner qualifies weighted minimum areas and can reconsider an earlier
area cut when a later or nested layout fails a geometric constraint. Existing
attempt/work/cell/depth bounds remain hard. Only declared grammars are searched;
finite exhaustion is not proof that every possible continuous layout is impossible.

[Contract](../../../docs/WORLD_METADATA.md#terrain-derived-required-program-spaces)
and [qualification](../../../docs/evidence/2026-11-01-local-constraint-search-verification.json)
cover same/cross-layout interfaces, a nested fixed anchor, the32-layout boundary,
independent source/reservation/path checks, prior/current original-program controls,
public SDK/CLI parity and failure preservation. Browser rendering/live-agent and
broader professional design/variety/scale gates remain open. Godot/artwork deferred.

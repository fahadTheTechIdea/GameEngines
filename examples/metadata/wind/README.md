# Automatic environmental derivation reference

This framework example declares two required dry regions, two destinations, a
bidirectional water-capable crossing, generated occupied water and optional wind
moisture. Its three nodes are technical reference markers, not a complete game.
No game setup, artwork, mockup or runtime is required.

Recipe `WindMoisture` supplies the existing C# wind grammar's controls.
`Environment` declares authored expressions referring to generated sampled IDs
(`weather-humidity` here). It contains no precomputed wind arrays.
The generator runs:

1. Original `surface` → `drainage` → `water` → effective `water-surface`.
2. `wind-moisture` derives from that effective surface and the wind definition.
3. `environment-input` combines authored expressions/scalars with generated ones.
4. Regions, allocation, suitability, routes and chunks consume the effective
   environment through the existing pipeline and dependency graph.

Without water, wind depends on the ordinary surface. Without wind, no wind stage
is introduced. The authored recipe remains the recipe-hash and checkpoint source
of truth. A generation-local recipe value supplies the effective environment to
existing consumers; it does not overwrite saved inputs.

`WorldMetadataDocument.WindMoisturePlan` stores the original environment plus the
core derivation, including source surface, controls, fields, node witnesses and
work. `EnvironmentPlan.WindMoistureId` binds that authority to the effective field
definition. World validation replays derivation and compares the complete source
surface and field composition. Orphan markers, stale sources, altered samples or
inconsistent fields refuse. Atomic export writes `wind-moisture-plan.json` and
summary counts; strict standalone reads replay the receipt. When the recipe is
available during publication, its wind definition and authored environment must
also match. Mismatch refuses before creating a publication directory. Dictionary
ordering is canonicalized without changing authored numerical expressions.

Wind reversal changes actual usable regions while preserving the required
destinations and crossing. Wind, evaporation, water-width and terrain edits equal
clean generation; unchanged content/base inputs reuse their cache stages.
Authored-formula edits reuse wind derivation and rebuild the effective
environment. Invalid or cancelled proposals leave accepted state/cache intact.
Publication/read-back and complete source-history checkpoints replay.

The larger nested program also runs through automatic recipe stages. A wind
edit recomputes the population source, terrain-funded quantities and final world,
retaining all seven complete compounds, 16 landscape members and 82 directed
paths (59 nodes/58 physical members). Source dimensions, the protected 1.8m ridge,
25000m² minimum and work budgets remain unchanged. Its edited result equals clean
generation and its published original-input checkpoint restores exactly.

The schematic retains actual engine geometry and saved inputs. JavaScript checks
do not replace C# numerical/digest replay or establish browser/design acceptance.
Document-only replay proves receipt consistency; it is not a signature or proof
of unavailable original author inputs. The actual recipe/checkpoint provides
that additional binding where present.

See [verification](../../../docs/evidence/2026-10-29-wind-metadata-verification.json),
[core grammar and limits](../../terrain/environment/README.md), and
[S02 tracker](../../../docs/world-generation/stages/s02-terrain-and-spatial-domains.md).
Lake geometry, wider environment/domain/scale/design acceptance, artwork and
Godot remain open or deferred.

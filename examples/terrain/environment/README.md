# Sampled environmental layers and optional wind moisture

These are framework inputs and technical references, independent of any game,
art profile or runtime. `flat.json`, `ridge-east.json`, `ridge-west.json` and
`inland-dry.json` are replayable `WorldWindMoisturePlan` snapshots. They preserve
source terrain and declare their actual controls, samples, fields and work.

`WorldSampledScalarField` stores typed values, units, purpose and source rule.
Up to eight layers (2,105,352 aggregate values) may enter
`WorldEnvironmentDefinition.SampledFields`. Use
`WorldFieldTerm(WorldFieldInput.SampledField, coefficient, fieldId)` in the
existing field dependency graph. Sampled IDs and expression IDs are separate
namespaces selected explicitly by the input type. Units remain authored; no
automatic dimensional conversion is implied.

The scalar origin, steps, rows and columns must exactly match the existing
terrain heightfield. Both use the same continuous triangle interpolation,
diagonal ownership and analytic gradients. A different frame refuses until
joint subdivision exists; no sparse probes certify whole footprints.
Outside scalar coverage fails explicitly, even if a terrain overlay extends
there. Ground masks still make environment values unavailable. Arrays and
definitions are snapshotted, validated and bounded; cancellation is preserved.

The existing clamp, correlated expression, footprint, region, density and exact
site-placement owners consume those same values. A center point can be dry while
a whole footprint fails for an off-center wet pocket. The five-by-five test's
six adjacent triangle thresholds independently predict 14.08 square meters of
dry support from a 16-square-meter domain.

`WorldWindMoisturePlanner.Plan(surface, definition)` optionally generates humidity
and rainfall scalar layers. Its contract deliberately exposes game controls:
wind vector, upwind-boundary humidity, baseline condensation, condensation per
positive elevation rise and evaporation by arbitrary authored terrain kind.
No ocean or terrain edit is required. The planner uses underlying heightfield
elevations; authored overlay elevation planes do not become atmospheric relief.
Overlay kinds may supply declared evaporation at sampled positions.

Air covers the complete terrain sampling rectangle, including positions outside
the playable boundary or over ground voids. Ground exclusions do not become
airflow walls. This explicit atmospheric extent lets scalar triangles remain
complete where an irregular playable boundary clips them. Consumers still use
the actual ground boundary and exclusions. There is no barrier/volume airflow
capability in this grammar.

Processing follows strictly increasing wind projection through the six native
triangle neighbors. Strictly upstream outputs are mixed by directional weights.
With no upstream neighbor, the declared boundary humidity enters. Evaporation is
capped at remaining humidity capacity; condensation removes a declared fraction,
increased by positive rise above the weighted upwind elevation. Each saved node
balances incoming plus accepted evaporation against rainfall plus outgoing.
These are normalized game values; averaging/branching is not physical global
atmospheric mass conservation. There are no seasons, weather dynamics or
terrestrial-climate plausibility claims.

Call `Validate(plan)` or strict `ReadAsync<WorldWindMoisturePlan>` to replay all
source/control/field/witness data. `Apply(environment, plan)` validates then adds
the two generated sampled layers to the existing environmental definition.
The environment must declare expressions that consume the intended IDs
(`<definition-id>-humidity` and `<definition-id>-rainfall`). Duplicate IDs refuse.
The planner owns calculations; JavaScript only displays saved engine metadata.

**Current integration:** metadata accepts scalar snapshots as explicit authored
inputs or derives them automatically through recipe `WindMoisture`. The latter
uses the effective occupied surface, separate authored/effective environments,
source-bound replay receipts and dependency-aware edit/rollback/checkpoint flow.
See the [automatic recipe example](../../metadata/wind/README.md). Direct core
callers still own supplying new fields when they change their own controls;
saved scalar provenance alone is not a derivation receipt.

The larger reference uses derived humidity in landscape density, retains all
seven complete compounds, 16 landscape members and 82 directed paths, and
publishes/replays 59 nodes/58 physical members with the original ridge and
budgets. Independent barycentric integration matches its saved area and mass.
The existing 59/60-node dry-world lifecycle also passes.

See [verification evidence](../../../docs/evidence/2026-10-29-environment-wind-verification.json)
and [S02 tracker](../../../docs/world-generation/stages/s02-terrain-and-spatial-domains.md).
Lake levels, broader environmental controls, domain variety/scale/design
acceptance, production artwork and Godot remain open or deferred.

Research concept: [Mapgen4's wind-ordered rainfall](https://github.com/redblobgames/mapgen4/blob/main/map.ts)
(checked 2026-10-29). This implementation is an independently written grammar
with its own controls and shared native terrain triangulation; no external code
or dependency was installed.

# Bound generation controls

This framework example declares three integer generation controls: `actors`,
`demand` and `need`. The first controls two repeated child groups, the second
controls charge capacity, and the third controls a complete power-supply program.
The baseline generates 11 semantic nodes: four entities, three charge suppliers,
three power modules and the root. Values 3, 13 and 10 generate 15 nodes through
the same catalog. This is a topology-only program, not a physical game world.

`IWorldMetadataGenerator.DescribeParameters(recipe, catalog)` returns the catalog
locations, roles, capacity IDs and units of all declared controls. The example
also has an uninstantiated `inactive` archetype with `future` and `future-demand`
bindings. These are valid declarations for optional content; describing a binding
does not mean that content ran in this generation.

Supplied names are case-sensitive. A name that no catalog binding declares fails
with `METADATA_PARAMETER_UNBOUND` before publication. An active binding whose value
is absent still fails with `METADATA_PARAMETER_MISSING`. Arbitrary game facts stay
in archetype/connection `Properties`; the sample entity's `world-size` property is
preserved as game metadata and is not advertised as a generation control.

Use the existing `generate-metadata` or reported metadata-session workflow with
these inputs. No art profile, game setup or Godot project is required. Input,
count, work and cancellation limits retain their existing owners.

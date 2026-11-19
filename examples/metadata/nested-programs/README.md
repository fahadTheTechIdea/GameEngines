# Reusable nested physical programs

Two program declarations replace the fully authored expedition example's ten
nested placement stages and five repeated local growth declarations. The engine
selects actual parents by archetype inside a scope, binds their actual catalog
parts, and instantiates the existing placement pipeline. Identifiers are derived
from program and parent identity; no identifier strings are substituted.

| Input | Bases | Metadata nodes | Physical members | Routed connections | Program instances |
| --- | ---: | ---: | ---: | ---: | ---: |
| `inland` | 5 | 21 | 20 | 15 | 10 |
| `inlet` | 5 | 21 | 20 | 15 | 10 |
| `seven-bases` | 7 | 29 | 28 | 21 | 14 |
| `double-storage` | 5 | 26 | 25 | 15 | 10 |

The five- and seven-base inputs use identical program declarations; the
`base-count` parameter drives the extra complete parents and contents. Existing
instance IDs remain stable. The storage program also binds every repeated cache
part; the doubled-storage example declares a shelf-row pattern with two available
positions. It does not resize or omit cache bodies. C# checks exercise this same
row program at one and two caches, scoped selection, mixed explicit/program
ownership, and growth without access, routes, artwork or a registry.

Generate through the existing `generate-metadata <recipe.json> <catalog.json>
<new-output-dir>` command. Qualified packets are under
`generated/nested-programs-qualification`; each includes JSON, summary and the
JavaScript schematic. `TemplateDefinition.Programs` retains the reusable inputs;
`ProgramInstances` records parent/child/stage provenance; `Definition.NestedStages`
contains the concrete derived bindings.

The profile explicitly allows 512 route candidates per direction. The preserved
256-candidate seven-base input failed at that limit. Work, geometry, clearance,
mandatory counts and route counts were not relaxed. Regional work remains capped
at ten million per direction and the existing aggregate cap remains unchanged.

See [verification](../../../docs/evidence/2026-10-28-nested-programs-verification.json).
These are bounded framework fixtures, not a completed game or a claim of arbitrary
world scale. Programs do not invent missing catalog parts, choose larger parent
envelopes, create physical interior topology, or provide streaming. Godot is deferred.

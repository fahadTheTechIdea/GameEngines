# Reusable metadata examples

Start with the [public-Core SDK workflow](../worldcreator-workflow/README.md)
for generation → publication → source edit → checkpoint restore. It demonstrates
distinct physical and logical programs, plus regional edits and composed domains,
without artwork, an AI process or Godot. The [API contract](../../docs/WORLD_METADATA.md)
owns supported inputs, geometry and constraints; the [roadmap](../../docs/ROADMAP.md)
owns qualification and remaining professional-generator work.

Choose the source contract needed by the game:

| Need | Existing example |
| --- | --- |
| Reuse one complete program across multiple regions | [Regional blueprint](regional-blueprint/README.md) |
| Varied reusable recipe/catalog programs | [Generation study](generation-study/README.md) |
| Nested source-sized content and surroundings | [Generic surroundings](generic-surroundings/README.md) |
| Catalog-owned static assemblies and moving-component identities | [Component assemblies](component-assemblies/README.md) |
| Required resource progression, protected spaces and eligible detail variation | [Protected gameplay population](protected-gameplay-population/README.md) |
| Hold a complete physical program fixed across four terrains | [Terrain connection comparison](terrain-routes/complete-program/README.md) |
| Generated rooms, walls, openings and interior journeys | [Generated interiors](generated-interiors/README.md) |
| Outdoor and independent interior domains with explicit transitions | [Domain journeys](domain-journeys/README.md) |
| Regional terrain edits and dependency reuse | [Regional overlay invalidation](regional-overlay-invalidation/README.md) |
| Add/release protections through accepted edit history | [Regional protection changes](regional-protection-changes/README.md) |
| Keep chosen destinations while their surrounding regions change | [Fixed region destinations](protected-region-partitions/README.md) |
| Generate regions under declared location-distance relationships | [Relationship-driven regions](region-relationship-search/README.md) |
| Generate local program areas under required shared boundaries | [Local interface search](local-interface-search/README.md) |
| Bounded region demand and resident reuse | [Regional demand generation](regional-demand-generation/README.md) |

These are game-authored declarations, not engine genre switches. Preserve required
members, exact physical source dimensions and explicit remaining scope. Passing a
fixture is not evidence of complete world design or arbitrary-scale support.

## Minimal hierarchy examples

The original pairs below have a `.catalog.json` of reusable archetypes and a
`.recipe.json` selecting root, counts, scope and coordinate space. They demonstrate
small metadata declarations through the same `WorldMetadataGenerator`.

| Example | Declared hierarchy | Nodes | Connections | Coordinates |
| --- | --- | ---: | ---: | --- |
| `town` | Settlement → 3 districts → 5 locations each | 19 | 14 | Topological |
| `dungeon` | Dungeon → 2 floors → 6 rooms each | 15 | 12 | Topological; includes deep stairs link |
| `platformer` | Campaign → 4 levels → 3 sections each | 17 | 11 | Parent-relative x/y; forward links |
| `space` | Universe → 2 systems → 3 planets each | 9 | 7 | Parent-relative x/y/z |
| `city` | City → 6 districts → sites and compound facilities | 35 | 33 | JavaScript schematic layout; world positions remain unset |

These are minimal technical recipes, not finished game maps or full setting catalogs. A game can add arbitrary kinds and nested constituent programs to its own catalog. Scaling counts does not establish whole-world coverage or solve physical placement.

[Generic surroundings](generic-surroundings/README.md) demonstrates exact
game-authored source footprints with required access and surrounding-area rules,
including repeated and nested structures, without a registered city/building catalog.

[Fenced compound](fenced-compound/README.md) composes 30 panels and 31 posts into
a complete physical boundary with a width-qualified gate journey, using ordinary
catalog parts and generated-boundary/facing rules.

The city pair adds `Properties.schematic` hints for JavaScript diagram presentation. It includes residential, civic, market, production, leisure and education districts; constituent structures within compound facilities remain separate selectable nodes. Coverage is explicitly partial. All new metadata exports package `schematic.html` and `schematic.js`; the actual city demonstration is in `generated/city-schematic/`, with an exported `city-schematic.svg`.

The [metadata API documentation](../../docs/WORLD_METADATA.md) describes constraints, link patterns, optional coordinates, safe export and limitations. Generated demonstration packets live under ignored `generated/metadata-{example}/`, each with a review and complete JSON.

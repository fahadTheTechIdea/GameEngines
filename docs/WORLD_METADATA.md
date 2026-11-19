# WorldCreator metadata

## Catalog-owned component roles

An archetype may declare `Component`, a `MetadataComponentDefinition` containing
`Role` (`StaticAssembly` or `MovingPart`), `Purpose` and `SourceRuleId`. Expansion
copies it to each actual node; it does not infer roles from names or kinds.
A moving part requires a parent node. Invalid role/purpose/provenance rejects
generation and document validation, and complete source replay rejects missing
or changed component facts. Catalog edits update every instance transactionally.
The [component example](../examples/metadata/component-assemblies/README.md)
contains two assemblies and all four required parts without an art dependency.

This is component identity metadata. It does not execute motion, certify animated
swept clearance or alter the declared static footprint/ports/surroundings.
Runtime motion and artwork are downstream capabilities and remain deferred.

## Generation input boundaries

Recipe and catalog JSON root members must match `WorldMetadataRecipe` and
`WorldMetadataCatalog`. Unknown members reject during loading, before generation
or publication. Rendering settings such as `RenderTilePixels` belong to the
downstream presentation profile; they are not generation controls. Game-authored
parameters and properties use their declared bags and validated consumers.
Physical source dimensions and allocation geometry remain in the explicit world
coordinate unit. Terrain samples, optional local allocation cells and render
tiles have separate contracts.

## Inspecting saved connections

In a generated schematic, select a physical route with the pointer or Tab and
Enter/Space. The inspector shows its actual connection, direction, endpoints,
ports, source rule, traversal profile, search limits, cost components and saved
diagnostics. Generated networks retain their network rule/options/analysis;
interior routes retain the declared opening. Shared physical segments offer a
journey selector so each owning connection and return direction can be inspected
without drawing duplicate physical paths. Endpoint and route-focus controls use
the saved geometry, including within shared-coordinate regional packages.

Layer controls and version-1 palette files affect presentation only. Separate
coordinate domains/floors open their own planar maps; topology-only outputs are
labeled as diagrams. Free 3D/volume projection is unsupported. The inspector
accepts at most 5000 nodes and 20000 connections, preserves the working view on
invalid imports and retains all source nodes while decluttering labels. It does
not search paths, relocate content or certify unrecorded alternatives.
[Supported-core inspection verification](evidence/2026-11-02-s09-core-gate-verification.json).

## Placement evaluation reuse in editing sessions

`MetadataGenerationSession`, regional sessions and domain sessions expose optional
`PlacementExecutions` separately from world metadata. Each row identifies a
world/stage/demand and complete-input hash, with `Computed`, `Current`, `Accepted`,
`ComputedWork` and `ReusedWork`. These count pure candidate geometry evaluations;
they are not elapsed times or counts of completed assignment searches.

Private accepted-session entries retain qualified terrain, footprint and
reservation results, including ordinary geometry rejection. Dependencies include
the normalized surface and physical obstacles, environmental/sampled fields,
source demand geometry, placement boundary and selected region/allocation shape
and profile. Terrain/field keys are currently conservative global dependencies.
Candidate generation, current relations/nogoods/pose controls, access, contacts,
separation, backtracking and complete-assignment feasibility still run normally.
Reused evaluations charge their original logical work at the candidate boundary.
Geometry, candidate keys, scope headers and execution reports share the existing
128 MiB serialized-cache cap; original placement/search limits remain unchanged.

Returned geometry is cloned. Failed/canceled proposals cannot replace accepted
state. A whole-allocation hit emits no evaluation rows and preserves private
entries for the next edit. Checkpoint reload replays source/history; serialized
reports cannot install geometry caches. The reusable coastal example includes
`placement-edit.json` for a session started from its roadless recipe and catalog.
[Verification](evidence/2026-11-02-placement-evaluation-verification.json).

## Reusable regional program blueprints

`MetadataRegionalBlueprint` is a version-1 authoring input for repeated regional
programs. Its `Template` is an ordinary `MetadataRegionalOwnershipRequest` whose
`Regions` collection supplies one to sixteen named program prototypes. Each of
the blueprint's two to sixteen `Regions` binds an explicit final region ID,
purpose, source rule and `ProgramId` to one prototype. Optional `Weight`,
`MinimumArea` and `MinimumClearance` override only that instance. All other
prototype fields—including physical sources, placement anchors and pins—are
inherited exactly. Connections and network endpoints must refer to final region
IDs; local node/port IDs remain those of the selected program.

`MetadataRegionalOwnershipPlanner.ExpandBlueprint` snapshots the input, validates
bounded IDs, references and overrides, and returns the normal expanded ownership
request in canonical region-ID order. Unknown or duplicate IDs and invalid
overrides fail rather than dropping a region. The normal planner still validates
terrain, all complete program contents, connections and budgets. Call `Plan` on
the expanded request, or use `generate-regional-blueprint <blueprint.json>
<new-output-dir>` for generation and normal publication. The published world
contains and replays the **expanded request**; retain the blueprint separately
for editing. Blueprint expansion does not rewrite endpoints or propagate future
changes to existing worlds. See the [framework example](../examples/metadata/regional-blueprint/README.md).

## Regional world design from authored program alternatives

`MetadataRegionalDesignGenerator` accepts a `MetadataRegionalDesignRequest`:
an existing regional ownership template, alternative complete recipe/catalog
pairs for named regions, and explicit node-kind count requirements. The engine
tries combinations by increasing sum of declared option priorities, then by
the number of option-rank steps. It explores changes across slots before deeper
changes in a single slot. Equal priorities use option IDs unless `SelectionSeed`
is provided; the seed reproducibly orders equal-priority options and ties.
It never overrides an explicit priority. Different seeds may select the same
result, and `SelectionSeed` does not implicitly change other generator stages.
An option can change its
recipe ID if all affected external network endpoints have semantic bindings and
no explicit or topology endpoint still points into the original program.
For each combination the existing engine derives ownership, generates all regions
and their terrain-qualified connections, then checks the actual generated nodes.
Set a requirement's `Physical` flag to count only nodes with physical access sites.

An optional requirement `Scope` applies the count independently to every
generated parent of `ParentKind` in the specified region. `MinimumParents` and
`MaximumParents` require a nonempty bounded set of parents; `DirectChildrenOnly`
chooses direct children instead of all descendants. Each parent must meet the
requirement's `Minimum` and `Maximum`, so a complete sibling cannot hide another
parent's missing parts. The receipt lists each parent ID, its count and result;
the aggregate count is their sum and can count a node more than once when parent
scopes are nested. The optional `MaximumRequirementWork` bounds evaluation across
all candidate attempts (default five million; maximum 200 million) and is valid
when a scoped regional requirement or any world requirement is declared. `Physical` applies to the
counted descendants, not to the parent. The engine uses declared kinds; it does
not invent a factory, club, city or other game-specific catalog.

An optional `ParentArchetypeId` on the scope narrows the selected parents of
that kind; optional `NodeArchetypeId` on the requirement narrows the counted
parts of the requested kind. Both must be game-authored catalog IDs. An absent
matching parent still fails its required parent-count range, and another
archetype cannot substitute for a required part.

`WorldRequirements` optionally declares counts over all generated regions.
Without a scope, `Minimum` and `Maximum` bound the total matching nodes across
regions. With a scope, every matching parent in every region must satisfy the
part count, and `MinimumParents`/`MaximumParents` bound the number of parents
across the whole world. A region may contain no matching parents; the global
minimum still prevents vacuous success. Each attempt records `WorldCounts`
with counts and parent witnesses attributed to their region. `Physical`, kind,
archetype and direct-child filters have the same meaning as for regional
requirements. An explicitly scoped count sums per-parent counts, so nested
matching parents may count a descendant more than once. Regional and world
requirement IDs share a namespace, and a request may contain either or both.
The same requirement-work budget covers every world and scoped-regional check
across candidate attempts; a budget failure does not select a partial world.

`WorldProgression` optionally declares directed gameplay progression across the
composed regional graph. A node reference contains `RegionId` and `NodeId`, so
the same catalog-generated ID in two regions refers to two different nodes.
Alternatively set `NodeId` to the empty string and provide `Selector` with an
exact `NodeKind` and optional `ArchetypeId`, using the same selector contract as
regional network endpoints. The planner resolves starts, goals, milestones and
capability grants against the complete candidate's actual nodes. Each selector
must match exactly one node in its declared region; it never chooses the first
match or infers a replacement for an explicit ID. A reference cannot combine an
ID and a selector. Output paths contain resolved IDs, and each result's optional
`Bindings` retains every distinct selector source and its resolved node.

Missing or ambiguous selectors and distinct landmark/grant references resolving
to the same node reject that candidate. Malformed selectors remain invalid
declarations; missing explicit IDs/links retain their explicit-reference failure
behavior. `MetadataRegionalProgressionSelectorException` exposes the rule,
reference and actual matches to SDK callers. Resolution is charged against the
existing per-rule progression work and shared fifty-million-work aggregate.
Already-executed resolution for all rules is reserved before graph traversal;
no second unmetered progression stage or increased limit is introduced.

Each rule declares a start, goals, selected connection kinds, ordered mandatory
milestones and a maximum work bound. Optional persistent capabilities declare
region-qualified grants and links requiring capabilities; a link reference has
its owning `RegionId` for a local link or a null `RegionId` for a generated
cross-region connection. The existing progression planner checks directed
reachability, mandatory milestones, optional return and state limits using local
metadata connections and generated regional connections. The selected attempt
records region-qualified node and link witnesses. A missing endpoint or gate,
malformed rule, cancellation or exhausted work/state budget fails the request;
an observed unreachable goal, milestone bypass/order failure or unavailable
return rejects that candidate. Limits apply per rule and complete candidate;
`MaximumAttempts` bounds candidate runs. Publication and read replay the
selection and progression witness from source. This is a logical graph check:
local metadata connections may not have physical routes, and matching logical
endpoints do not establish a physically continuous actor journey.
Rule identities, declared regions, distinct landmarks and connection kinds,
capability declarations and bounds are validated before candidate generation.
An existing region with a missing generated node or link is rejected when the
candidate graph is resolved; invalid declarations do not become search failures.

Domain progression nodes support the same `Selector` with their `DomainId`.
The domain adapter delegates to this same projection/planner and retains
domain-qualified `Bindings`; independent floors keep separate identities even
when their local node IDs repeat. Semantic roles do not create a physical
passage or relax the separately qualified domain journey. Runnable sources are
[regional alternatives](../examples/metadata/regional-design/semantic-progression-request.json)
and [regions with floors](../examples/metadata/regional-domain-world/semantic-progression-definition.json).

`WorldJourneys` separately requires a physically connected route between two
region/node references with optional named access ports. A named port uses its
approach position and enforces width; without a port the node must have a 2D
position and cannot be a solid site. The engine uses directed, already-generated local and regional route segments
whose connection kinds and complete movement-profile identities match the
declared profile. Independent directed routes retain separate states and can
transfer only at the same region/node/port identity (or the same non-solid
node anchor when the port is omitted), when their declared route endpoint
exactly equals that anchor's position. A shared coordinate,
containment, logical connection or crossing does not authorize a transfer.
An optional return uses the actual reverse directed paths. A candidate whose
journey is unreachable is rejected; invalid source, missing/too-narrow ports,
work exhaustion and cancellation fail. Each rule has a work limit, a 65,536
vertex and 262,144 edge limit, and stores the exact segment witnesses. The
designer replays the witnesses during publication and read. These checks use
the generation owner's existing route qualification; the standalone planner
assumes its supplied regional document was authenticated by that owner. Routes
without a matching declared endpoint identity remain isolated. An explicit local `CorridorPlan`
can additionally connect matching-profile route tracks through its qualified,
directed segments and recorded junction transitions. Corridor steps identify
their region, group, segment and direction, and refer to a source route that
uses that directed segment; `SegmentIndex` is -1 for a corridor segment rather
than an index into the unsplit source polyline. Only original route endpoints connect route tracks
to a corridor journey. Interior routes join at their room-center identities;
the interior owner's declared entrance links join their actual outside node or
named outside port. Every interior route must have its corresponding saved
opening. The
graph is conservative and does not find all geometrically possible paths.
Optional `RequiredMilestones` lists distinct region/node references with optional ports in
mandatory order. Each port must be reachable; removing its exact graph vertex
must make the goal and the next milestone unreachable. The saved forward path
therefore visits every milestone in order, and the result records their resolved
positions. A visit means reaching the declared node/port state, not merely crossing its
coordinates on an unrelated route. These checks cover the selected route graph,
not unrepresented terrain or other gameplay movement. The optional return path
is checked independently and need not visit the milestones in reverse order.
Milestone search uses the same per-journey work and graph limits; infeasible
milestones reject a candidate, while malformed or coincident endpoints fail.

The engine accepts the first combination satisfying every requirement. It records
the tested selections, failed requirements or observed spatial failures, and the
world hash. An explicit attempt limit differs from exhausting all alternatives;
invalid input and route/work-budget failures are not interpreted as infeasibility.
Each candidate uses the template's own existing work limits; the design attempt
limit bounds the number of whole candidate runs, not their aggregate work.

`PublishAsync` writes the source and attempt receipt together with the regional
world; `ReadAsync` regenerates and verifies both. Each operation performs one
complete design-source replay, compares the full receipt and world, and retains
the existing dependency and atomic-publication checks. Standalone regional reads
and publications retain their independent regional validation. The CLI equivalents are
`generate-regional-design <request.json> <new-output-dir>` and
`verify-regional-design <output-dir>`. This composes developer-authored programs;
it does not invent roles or content, optimize all feasible worlds, or certify a
playable game. Coverage remains that declared by each selected recipe.

An optional `SeedPlan` provides one world seed with explicit `TerrainDetail`,
`OwnershipPartition` and `ProgramSelection` switches. For each selected layer,
the engine derives an independent stable seed from the world seed, design ID and
layer name, records it in the receipt, and passes it to the existing generator.
Terrain variation requires authored nonzero terrain detail; partition variation
requires at least two authored axis, bend or bend-position choices; program
variation requires at least two equally preferred options. `ProgramSelection` cannot be combined with a
separate `SelectionSeed`. Seeds do not relax constraints and do not guarantee a
distinct or feasible result. The plan never changes unselected layers.

When the game has one complete regional program rather than alternative program
slots, a meaningful `SeedPlan` can be used with empty `Slots` and `Requirements`.
The same single-candidate design can apply real content, progression or journey
requirements without inventing a program slot. A request with neither a seed
plan nor such requirements is invalid. The seed still must target actual enabled
variation; `ProgramSelection` requires real equal-priority alternatives.

`MetadataRegionalDesignGenerator.FromSeededBlueprint(blueprint, seedPlan)` expands
the reusable program blueprint into a seed-only design request, then the normal
design generator and publisher create and replay the full world. The helper does
not infer program alternatives and refuses `ProgramSelection`. The CLI form is
`generate-regional-seeded-blueprint <blueprint.json> <seed-plan.json> <new-output-dir>`;
`verify-regional-design` replays its result. The receipt retains the expanded
source and derived layer seeds; keep the editable blueprint and seed-plan files
separately. See the [seeded blueprint example](../examples/metadata/regional-blueprint/README.md).

`MetadataRegionalBlueprintDesign` additionally allows design slots to refer to
named programs in the blueprint. Each slot identifies a final `RegionId`; each
option declares its own `Id`, `Priority` and blueprint `ProgramId`. By default
the option reuses the program's recipe and catalog, while the instance retains
its regional policy. Set `UseProgramPolicy` to true to select that program's
weight, minimum area and clearance, suitability profile, placement anchor,
capacity, spatial context and pinned anchor as a single policy. Optional fields
replace the instance's values even when null. The instance's ID, purpose and
source rule remain unchanged. This explicit choice can supersede instance
weight/area/clearance overrides. Unknown programs are refused.

Direct `MetadataRegionalDesignOption` callers may provide the same complete
policy through its `Policy` property. A missing policy retains the instance's
policy. Numeric policy bounds are checked before candidate search; selected
policies must also pass the ordinary ownership planner's validation. The saved
design receipt includes the expanded option policy and is replayed with it.

`MetadataRegionalDesignGenerator.GenerateBlueprint` expands these references,
generates the ordinary `MetadataRegionalDesignRequest`, and embeds the editable
blueprint design and its content hash in the design receipt. Publication and
read replay check the hash and check that expanding the retained blueprint
reproduces the saved expanded request. Existing endpoint bindings,
requirements, world journeys/progression, seeds and work limits retain their
normal validation and replay. A changed recipe root still requires the normal
semantic endpoint bindings. Use `GenerateBlueprint` with `PublishAsync`, or
`generate-regional-blueprint-design <design.json> <new-output-dir>`, followed
by `verify-regional-design`. The receipt's `BlueprintSource` is a structured
snapshot, not the original file's formatting. `ExpandBlueprintDesign` remains
available for consumers deliberately generating an expanded design directly;
that direct route cannot retain the original blueprint automatically.

For multiple regions with the same alternatives, define `OptionSets` as named
lists of blueprint design options and set a slot's `OptionSetId` to the desired
set. A slot must have exactly one of inline `Options` or `OptionSetId`. Each
reference expands into that region's own normal design slot: seeded selection,
semantic bindings, constraints and policy application still operate separately
per region. Set IDs must be unique and referenced; unknown, duplicate or unused
sets and mixed inline/reference slots are refused. Sets and slots are bounded to
16, with at most 16 options per set. Blueprint generation retains the original
option sets in the published receipt as well as the expanded design.

Optional `Bindings` select regional-network destinations by `NetworkRuleId`,
`DestinationId`, game-defined `NodeKind` and optional `ArchetypeId`. For each
candidate, the engine analyzes the destination's region, requires one matching
node, and binds its ID while retaining the destination's declared access port.
It checks the same unique node in the generated world. Missing or ambiguous roles
reject that candidate; the attempt receipt records the resolved endpoints and
analysis work. `MaximumBindingWork` bounds analysis across all attempts; it is
invalid without bindings. The default is five million work units.

Binding currently requires a program that can be analyzed before regional
ownership; terrain-dependent counts, unresolved program composition and an
already materialized source world are explicitly unsupported for bound regions.
Fixed endpoint declarations remain available for those cases. A role binding
does not choose an access port or invent a physical connection.

## Protecting generated positions in a session

The C# `MetadataGenerationSession` constructor and `StartAsync` accept optional
`protectedPositions` entries (`MetadataPositionProtection`). Each names an
existing positioned node, a unique protection ID, purpose and source. At session
creation the engine captures the node's identity, parent, coordinate space and
exact coordinates. Each edit generates a complete candidate; if a protected node
is missing, changes identity or moves, the edit fails and the accepted world is
left intact. Positions and protection definitions survive checkpoint replay.

These are fixed for the lifetime of the session and are not placement constraints:
the planner does not search for a new solution that keeps them. Position locks do
not freeze footprints, routes, contents or surrounding terrain. Use separate
input protections and normal gameplay/physical constraints where those are needed.
An unpositioned node cannot be protected this way.

For the CLI, pass an additional JSON array of `MetadataPositionProtection`
entries between the existing input-protection file and output directory in
`metadata-session start`. Later `metadata-session edit` calls replay the saved
checkpoint and enforce the captured positions automatically.

## Semantic landmarks for topology

`WorldMetadataRecipe.TopologyIntents` declares staged topology using game-authored
node kinds for its start and ordered exits. Each intent contains a `Template`
(`MetadataTopologyRule`), `StartKind`, `ExitKinds` in stage order and an optional
`MaximumSelectionWork`. The template's `ScopeNodeId` is explicit; its
`StartNodeId` and each stage's `ExitNodeId` must be empty strings. Each kind must
occur exactly once below that scope. Missing or ambiguous landmarks fail rather
than guessing or dropping content. Resolution follows catalog expansion and feeds
the existing topology, progression and optional physical-routing stages. The
resolved rules are saved in the topology plan; the recipe retains the intent.

Intents can coexist with `TopologyRules`, subject to the combined 16-rule limit
and existing graph constraints. Selection has per-intent and aggregate work
limits. This does not infer gameplay goals or design a whole world from a prompt.

## Portable source and population programs

Optional `MetadataPopulationSource.Context` explicitly binds a source template to
the caller's actual surface and origin. `PlacementAnchorIds` names the root
anchors to inherit. The source template must leave `Surface` and `Origin` unbound,
use the same two-axis space/unit and declare relations to those missing anchors.
Source-authored overrides, duplicate/conflicting/absent anchors and nested sources
refuse. No context is inherited without this declaration. The existing population
owner binds captured inputs before source generation and charges anchor binding
to the existing coordination ledger. All original source paths and bodies remain
subject to preservation and ordinary physical validation.

Optional `Context.UsePlacementBoundary=true` additionally inherits the caller's
actual placement boundary into the source root request and source `Regions` mask.
The source must declare `Regions` with an unbound boundary. Authored source masks
and consumer region/layout/demand/adaptation overrides refuse. The source may own
`RegionDemands` and local layouts: automatic `AreaBalanced`, or explicit relative
`RegionFrame` for other strategies. The consumer inherits the exact accepted
source `RegionPlan` and `LocalLayoutPlan` definition; it cannot reselect habitat.
Source and consumer retain the same qualified spatial plans. Binding geometry is charged to the same work ledger;
the omitted/false flag preserves the existing context contract.

A terrain-only source may omit `Placement` when `PlacementAnchorIds` is empty.
It still explicitly inherits the caller's actual surface/origin and, when
requested, its boundary into the source habitat. It cannot request placement
anchors without a source placement owner or override inherited geometry. No
source building or empty placement stage is fabricated. A boundary-bound consumer
retains the source's exact qualified `RegionPlan`; it does not introduce another
independent region choice after source counts were derived.

When source reconsideration produces a version-3 coordination receipt, optional
`SourceBindingWork` records the work before its first attempt (omitted when zero).
It remains inside the original `MaximumWork`; binding work plus all attempt work
equals `Work`. Native replay authenticates it and the viewer checks that equation.

This makes one complete template usable inside generated regional ownership: the
regional owner derives terrain-qualified program anchors and the normal generator
passes that context into its source phase. Templates contain local design grammars,
not copied positions from a previously generated world. Direct generation of the
same bound template produces the identical complete regional packet. Existing
independent region requests, source replay, sessions and public CLI use this path.

`AnalyzeProgram` now also retains one static population source as `SourceProgram`.
Both phases use the common catalog expansion owner. Shared source members, source
footprints, access definitions, relative layouts, authored connections and registered
definitions must remain preserved in the full consumer program. Analysis retains
counts/choices/provenance without solving spatial constraints. Source/consumer
work and dependencies remain bounded. Regional capacity measures the complete
root envelope and ordinary generation still places every nested source and
content member, validates required open ground and qualifies all journeys.

The connected framework control keeps three complete programs on 336×224m of
shared terrain: 48 nodes/45 physical sources at baseline, 87/84 when source/content
counts increase. A 560m² exclusion remains unavailable; compounds retain their 85%
minimum open-ground requirement, unchanged 0.4m actor diameter and 1.5m module
entrances. Source-size/terrain edits, one-region content edits, exact unrelated
packet reuse, checkpoint restore, API/CLI equality and refusal preservation pass.
The inspector checks source-analysis and inherited-anchor binding while showing
actual source/content polygons and paths.

[Reusable inputs](../examples/metadata/portable-population-world/README.md) and
[qualification/limits](evidence/2026-10-31-portable-population-verification.json)
cover this complete declared composition. It is not broad world-design acceptance,
automatic terrain-dependent count/composition repair, arbitrary irregular site
grammar, independent content streaming, artwork or gameplay integration. All
original 63 tasks/13 requirements remain open; Godot/artwork stay deferred.

## Content from actual site and ground domains

`MetadataPopulationGrammar.Domain` selects exactly one actual source domain:
`SiteFeature` selects an exact parent's feature kind and `SourceIndex` in ordinal
feature-ID order; `GroundMaterial` selects covered cells of the named material.
`NodeId` explicitly selects an owner; when omitted inside a nested program it uses
that actual parent. Root material selection spans the world. Parent material
selection retains cells with winning ground-source provenance for that parent.
No nearest source, empty position or default material is inferred. The existing
`PopulationSource` phase generates and validates that source first.

Declare exactly one `Bands` or `Area` pattern. Bands reuse the shared connected-span
sampler, preserve every declared member and refuse gaps or insufficient length.
Optional `DirectionEdgeIndex` follows the actual selected feature edge plus the
declared direction; `Facing` can orient members along/against/left/right of that
direction. Area uses the existing connected mass-partition/interior search and
requires an explicit suitability `Profile`; it requires source environmental
fields. Area facings and ignored band profiles are rejected. Declared counts and
band counts must agree; the engine never truncates demand to fit.

`MetadataPlacementPlan.PopulationDomains` stores the definition, exact source IDs,
owner, geometry fingerprint, source mask, resolved bands/area plan and world
anchors for every required member. Parent-local anchor translation uses the same
placement owner. The whole source body, or its containing reservation when
declared, must fit the selected mask. Existing terrain suitability, spacing,
entrances and preserved source-journey clearances remain hard. Failed prescribed
content is rejected; it is not scattered elsewhere. Native generation/read/session
replay rederives all traces and geometry.

`MetadataNestedPlacement.ParentBoundary` and reusable
`MetadataNestedProgram.ParentBoundary` can explicitly select `Reservation`.
The default remains `Footprint`. Reservation scope requires an actual parent
reservation; it does not enlarge the body, manufacture clearance or imply solid
occupancy. It allows exterior child structures, props or other authored content
within that original source envelope. Conservative capacity preflight only sums
guaranteed body containment; final measured ground/interior requirements remain
mandatory even when preflight cannot prove a shortfall.

The Population strata inspector layer shows source masks and required anchors.
Selecting an actual object exposes parent, selector and source/rule provenance.
Overlays allow clicks through to objects. JavaScript inspects saved source geometry
and decisions; it does not run a second generation algorithm. The source masks are
computational vector domains, not new semantic regions or gameplay grids.

[Reusable inputs](../examples/metadata/source-domain-programs/README.md) and
[bounded qualification](evidence/2026-10-31-source-domains-verification.json)
cover repeated/rotated/count/source edits, full source paths, missing/gap/oversized
refusals, reservation/open-ground controls, saved replay and rollback. This does
not certify full world content/design, arbitrary site grammars, automatic conflict
repair, gameplay interaction, artwork or runtime streaming. Godot stays deferred.

## Source-derived ground cover

Optional `WorldMetadataRecipe.GroundCover` runs the shared
`MetadataGroundCoverPlanner` after physical allocation/corridor realization and
before chunk indexing. Materials declare ID, kind, purpose and source rule.
Rules bind those materials to actual original terrain kinds, physical archetype
footprints/reservations, site-feature kinds, semantic regions, local allocations
or realized corridor groups. Rules declare priority and minimum source matches.
Unmatched required selectors and conflicting top-priority materials refuse;
agreeing top-priority sources all remain traceable. Registered sources use their
unchanged resolved geometry. No raster, scatter or vacant-cell fill is used.

The common area arrangement returns exact vector cells and accounts for every
bounded-domain area as covered, excluded or unresolved. Original excluded terrain
stays excluded even beneath material rules. Missing actual terrain stays unresolved;
overlays cannot invent terrain coverage. `RequireComplete` refuses unresolved cells;
explicit partial scope retains them and their diagnostics. Material-domain completeness is separate from the authored metadata `Coverage` scope; inspect the ground plan totals rather than inferring it from that enum. A missing material rule
is not silently replaced with grass or another default. Native physical sources,
movement widths, access, routes and terrain are unchanged by material generation.

`GroundCoverPlan` stores full source polygons, node/parent/rule/material lineage,
match counts, exact cells, area totals, context fingerprint and charged work.
`GroundCoverId` requires its receipt. Native document validation rederives the plan;
ordinary generation, public file commands and source-history sessions replay it.
Material edits can reuse placement while invalidating the ground-cover stage.
`MetadataGroundCoverPlanner(registry, layouts).Plan` captures and validates an
existing source through its installed dependencies before deriving the same plan.

The inspector's separate Ground cover layer supports visibility, solo/opacity and
cell selection for source provenance. Continuous compound SVG fills use the saved
cells directly; transparent per-cell targets preserve exact coordinates without
showing computational subdivision seams. JavaScript does not solve or move geometry.
Ground material colors are schematic presentation, not production artwork.

This capability is bounded to two-coordinate surfaces with a finite boundary.
Default limits are1024 resolved material sources,4096 cells and20M work; source and
result transport remains16MiB/depth32. Existing common area edge/event limits still
apply and refuse unsupported combined complexity. No limits are raised implicitly.
Ground plans remain shared dependencies of current chunk packets; independent
ground-cell streaming is not claimed. Material coverage does not certify complete
content, planting, interactions, physical packing, artwork or world-design approval.

Use [the authored controls](../examples/metadata/source-ground-cover/README.md).
See [bounded qualification](evidence/2026-10-31-ground-cover-verification.json).

## Unplaced program analysis and regional capacity

`IWorldMetadataGenerator.AnalyzeProgram(recipe, catalog, maximumWork, token)` uses
the same catalog expansion as `Generate`. It captures inputs before callbacks,
retains nested required members, source links, parameter/capacity/variation/mix
choices, relative layouts, source definitions and exact installed dependencies.
An unbound origin remains unbound. An authored origin can retain authored positions;
analysis does not solve placement, terrain, route or service constraints. Additional
network links are a downstream generation product. Analysis is not a world.

Analysis requires versioned installed extensions for persistent provenance, unlike
ordinary direct generation's supported unversioned layout route. One static
`PopulationSource` is retained as `SourceProgram`, with source/consumer preservation
checks. Optional `TerrainDemands` can now prepare the declared source habitat and
measure actual quantities before the same unplaced consumer expansion. Physical
`ProgramComposition` choices and already-placed `SourceWorld` inputs still refuse
explicitly. Input/result size is bounded at16MiB/depth32; work, node and
connection limits and cancellation remain enforced. A capacity/mix choice with no
feasible physical placement still fails ordinary generation; analysis cannot
certify physical feasibility or silently select a spatial alternative.

`MetadataProgramAnalysis.TerrainAnalysis` retains `HabitatSha256`, the prepared
`MetadataTerrainDemandSource`, `PreparationWork` and the exact measured demand
`Plan`. Source preparation uses the generator's shared terrain/water/wind inputs,
then the existing region, source-capacity and fixed local-layout owners. It does
not search building poses, routes or services. Source geometry, region masks,
environmental fields, allocation cells and catalog identity bind the habitat hash.
The original source recipe/catalog hashes remain explicit in `SourceProgram` and
the analysis plan. Actual generation may reconsider physical source poses; its
selected source recipe hash is retained separately. Habitat geometry and every
measured result/definition/work value must still match exactly. This is measured
program equivalence, not permission to substitute terrain or physical content.

Inherited context must already have a concrete caller surface, origin and named
anchors. An independent declared source habitat does not require a consumer
origin. Source layout adaptation now qualifies the actual source through the
existing generator and measures its accepted allocation. Bound consumer intent
regions are analyzed after content through the normal region-demand/choice owners.
Explicit regional coupling can resolve actual habitat,
declared counts and complete program choices through the joint policy below;
analysis never treats an unknown regional habitat as the entire world or guesses
its counts. Source-stage limits stay in force; reported source preparation and
measured demand work are charged to the existing analysis budget. Terrain recipe
generation and local source owners retain their own declared limits as well.

Root envelope measurement can expand actual part membership without creating
population positions. The existing expansion and footprint owners preserve
declared region/allocation, source dimensions and site reservations; nested members
are not double-counted. This unplaced mode does not assign a footprint facing;
an unresolved generated facing refuses explicitly. Ordinary placement still uses
the complete spatial grammar and validates every member and path.

`MetadataRegionalProgramRegion.Capacity` optionally declares purpose, source rule
and `MaximumCoverage` in(0,1]. `MetadataRegionalOwnershipPlanner(generator).Plan`
analyzes the complete unbound template through that installed generator and resolves
every expanded root placement envelope through the common footprint owner. Nested
sources remain in the analysis; only root envelopes contribute region area:

```
RequiredArea = max(MinimumArea, sum(root envelope area) / MaximumCoverage)
RequiredClearance = max(MinimumClearance, largest root envelope radius)
EffectiveWeight = Weight * RequiredArea
```

The envelope is the exact reservation when declared, otherwise the source polygon.
Offsets, rotation, registered variant clearance and selected site surroundings use
the existing resolver. Weight is an authored proportional bias for a measured
region. Regions without `Capacity` keep their explicit area, clearance and weight;
use intentional comparable weights when mixing the two policies. No implicit game
population, standard coverage or realism target is inferred.

The saved ownership `ProgramDemands` records analyses, whole measured sources and
effective bounds. Native preparation rederives this proof and checks it exactly.
Full program generation checks analyzed members, source links, choices and
dependencies, then qualifies complete physical placement and journeys. Sessions
replay source edits and retain accepted state after refusal. Area and circular
clearance are capacity bounds, not packing or complete ground-content proof.

### Terrain-qualified ownership partitions

Ownership partition selection now feeds each candidate leaf through the existing
terrain/component/intent owner before accepting the division. It uses the actual
program profile, suitable-area requirement, complete-source clearance bound and
optional pinned origin. A weighted division with sufficient outer area can still
lack usable terrain; that failure drives the existing
`Pattern.PartitionSearch.AxisOffsetsDegrees` alternatives. No grid, arbitrary
point scattering, source resizing, relaxed counts or invented connection is used.

`OwnershipPlan.PartitionQualification` retains ordered `Attempts` and final
`SelectedAttempts` ordinals. Each attempt records semantic region ID, exact
stratum hash, whole ownership polygon, query request, qualification status,
selected anchor-plan hash/intent witness or actual failed-intent witness,
rejection and work. `Qualified` means that one tentative leaf passed its terrain
query; only `SelectedAttempts` identifies final ownership. Earlier qualified
leaves may have been discarded when another leaf forced an ancestor to change.
The inspector distinguishes this diagnostic history from placed content.

The common area owner invokes a trusted internal, leaf-local qualifier. Its
geometry `Partition.Work` and cut-attempt work exclude external terrain-query
work. `PartitionQualification.Work` sums all such queries and boundary extraction,
including rejected candidates; the regional planning budget charges them once.
Selected base anchors are reused. Physical component-assignment retries qualify
the same deterministic ownership with unbound component choices before applying
their exact requested assignment, preserving the component-context invariant.

Only an actual unsatisfied terrain-intent query triggers this new reconsideration.
Malformed inputs, geometry-resolution failures, cancellation, work/attempt limits
and witness/transport bounds retain their meanings. Diagnostics are bounded by
65,536 candidate/polygon-vertex references and the current JSON size limit.
Failed all-alternative searches preserve the original terrain/pin failure and
attach the actual qualification and cut-attempt history. They publish no partial
world. Source generation, read/export and session restore rederive the full proof.

All complete local content, access, occupancy and local/global journeys still
undergo ordinary physical qualification after partition selection. The optional
physical-feedback owner below can then reconsider declared ownership axes after
a packing or required-travel failure. Neither search optimizes arbitrary
continuous boundaries or certifies professional-world variety and scale.
Godot/artwork remain deferred. See the
[reusable reference](../examples/metadata/terrain-qualified-partitions/README.md).

Optional ownership request `Coupling(Purpose, SourceRuleId, MaximumIterations)`
resolves terrain-dependent counts with regional capacity. It requires at least one
terrain-demand program and an explicit `Capacity` for each such program.
`MaximumIterations` defaults to16 and must be1..32; all attempts and initialization
share the request's unchanged `MaximumWork`. First expand catalog minima solely
for provisional capacity. Then partition, bind actual terrain/context through the
same regional recipe owner used by generation, measure exact habitat counts and
complete root envelopes, and recompute requirements. Accept only when the exact
required-area/clearance/weight vector reproduces itself. There is no relaxed-count,
source-size reduction or physical-failure fallback.

`OwnershipPlan.CouplingPlan` retains initialization work and ordered attempts:
input/output requirement hashes, actual ownership/origin hash, measured member
counts, analysis/habitat hashes, per-attempt work and stable status. Final
`ProgramDemands` contains the complete actual bound analyses. Initialization plus
all attempt work equals ownership work. Repeated nonstable states, iteration
exhaustion and infeasibility refuse; completed attempts are attached to the
exception's `RegionalCoupling` data. Native preparation rederives the whole
request/trace before generation, read or publication. The viewer checks bounded
structural bindings; it does not authenticate native hashes. Finite iteration
does not guarantee convergence for every request. Physical placement, source
preservation, open-ground requirements and all declared journeys remain mandatory
subsequent gates. Source composition/layout-adaptation analysis and broad world
design still have their existing explicit limitations.

[Reusable coupled reference](../examples/metadata/coupled-population-world/README.md),
[current qualification](evidence/2026-10-31-regional-coupling-verification.json).

`MetadataRegionalOwnershipRequest.PhysicalFeedback` optionally declares purpose,
source rule and `MaximumAttempts` (default8, range1..32). It requires declared
regional connections or `NetworkRules`. The same regional owner fully generates a detached
candidate, checks analyzed content, source preservation, required open ground,
cross-region occupancy, approaches and every local/regional path. A typed
disconnected/trace-rejected regional connection or unresolved regional network
rejects a complete component assignment. A bounded breadth-first frontier changes
one qualified component at a time, prioritizing failed-link participants, while
retaining other choices. It can subsequently combine changes in several regions.
A locally viable component is never pruned because another endpoint failed.
The existing intent owner binds each full vector through `RequiredAssignment`;
component domains, environment and ownership must remain identical within one
ownership context. Once its complete component frontier is spent, declared
`Pattern.PartitionSearch.AxisOffsetsDegrees` can generate other complete cut
assignments. Semantic keys `(FirstMember, Members)` identify every internal cut
in the balanced region tree; offsets are evaluated relative to each actual
parent. A bounded breadth-first frontier may change a child cut while retaining
the root and protected destinations. New masks create a fresh component frontier;
no previous component ordinal or anchor pose is transferred. No source
dimensions, counts, widths, requirements or search caps are relaxed.

`OwnershipPlan.PhysicalFeedbackPlan.Attempts` records candidate hashes, selected
components/anchors, planning and physical-qualification work, failure owner,
connection/status/diagnostic and accepted complete geometry hash. Later attempts
retain exact `RequestedComponents` and `ContextOrdinal`; network failures retain
their full failed candidate plan. Pose-dependent placement failures retain an
attributed `PlacementFailure`. Source-only impossibility remains available to
program pruning and is not retried as a geometry problem.
`PhysicalFeedbackPlan.Contexts` retains each requested/selected partition vector,
exact masks and ownership digest, and its own `ComponentSearch` with qualified
domains, context digests, work and truncation. A rejected planning context has no
candidate, geometry or component assignment: its attempt has `PlanningRejected`,
actual `PartitionFailure` terrain witnesses and zero physical-qualification work.
Existing count
coupling may run inside each attempt. Its own initialization/iteration work then
equals that attempt's `PlanningWork`. Ownership work equals `PartitionSearchWork`
plus every context's `ComponentSearch.Work` and all attempt planning and
qualification ledgers. These ledgers account for reported
placement, source/consumer coordination, access, route and global assembly work;
environmental/content stages keep their original separate local limits. This is
not a CPU-time or all-stage work claim. Full generation is not published from a
probe. Public generation/read/export still rederive the source and whole feedback
trace, and requalify the complete world. Registered dependency drift refuses.

The same semantic partition frontier participates in coupled terrain/count/
complete-program search. Joint attempts retain `RequestedPartitions`,
`SelectedPartitions` and rejected planning witnesses; partition enumeration is
included once in `JointPlan.EnumerationWork`. Count transitions keep their cut
bindings until remeasurement converges. Terrain-derived quantities follow the
unchanged authored habitat equation; fixed required content is not dropped.

`METADATA_REGIONAL_PHYSICAL_PARTITIONS_EXHAUSTED` means the declared finite
partition/component frontier is spent. Attempt/work/geometry/route-candidate
limits remain distinct hard failures and are never treated as semantic retries.
Without multiple declared axes, component exhaustion retains its own diagnostic.
Exact quarter-turn cut vectors avoid trigonometric residual slivers; other angles
are not rounded. See the [reusable inputs](../examples/metadata/physical-ownership-redesign/README.md)
and [bounded verification](evidence/2026-10-31-physical-ownership-redesign-verification.json).

For explicit feedback, the route owner can first produce an exact terrain-domain
disconnection witness through shared area/convex-face/component owners. It uses
all sampled terrain kinds and authored exclusions, with generated solids included
in the permissive domain. Distinct components can prove impossibility for a
positive-width actor; connected centers prove no route feasibility. Zero-radius
actors receive no proof from this positive-width test. Normal routes still check
every actual solid, cost, grade and actor clearance. The proof uses the existing
4096-face bound and ordinary work limits. Geometry-resolution or budget failures
are not reclassified as disconnection or retried. Missing component alternatives,
attempt exhaustion, malformed inputs and cancellation remain explicit failures.

The current policy revises finite qualified terrain-component assignments for
regional connection/network failures. Exhausted complete assignments and attempt
limits remain distinct failures; malformed input, budgets and cancellation are
never candidate retries. General local packing repair, broader source
composition/adaptation, partition redesign, continuous anchor optimization and complete-world design remain
separate open work. [Reference input](../examples/metadata/regional-physical-feedback/README.md),
[qualification](evidence/2026-10-31-regional-physical-feedback-verification.json).

Optional `Recipe.ProgramComposition.RequiredRanks` binds an exact complete ranked
selection prefix. The existing mix/region chooser interprets ranks; zero means
the first logical choice at that decision. The prefix contains at most4096ranks
in0..63 and resolves exactly every requested independent selection. An empty
vector is valid only for a boundary-inheriting consumer with a declared and
actually qualified upstream composition. Its consumer receipt has zero selections
and zero mix/region work; the upstream receipt owns real source choices/work.
Source-free or otherwise unused empty policies still refuse. Invalid,
incomplete or extra ranks refuse. Bound generation attempts that selection once;
a physical failure becomes a typed selected-program constraint failure, with the
original physical cause retained. It cannot silently advance to a different mix.
Native document replay uses the bound source. Existing unbound physical
composition retains its ranked search behavior and original limits.

`AnalyzeProgram` supports a bound mix prefix through the same catalog expansion
and ranked chooser used by generation. It retains every nested member, capacity,
source dimension, relative layout and dependency without inventing placement.
Unbound composition still refuses at this single-analysis boundary. Consumer
intent-region decisions now use actual spatial selection after catalog content.
`RegionAnalysis` retains the exact selected plan, optional source-demand plan and
preparation work. A full bound rank vector includes these region decisions, and
regional physical qualification compares the resulting region/demand plans. An
explicitly inherited source habitat is retained exactly, without another consumer
choice. Source adaptation keeps its existing limits. The existing public5Manalysis budget is unchanged.

Optional `MetadataRegionalOwnershipRequest.ProgramComposition` declares purpose,
source rule and `MaximumAttempts` (default16, range1..64). At least one region
must have an unbound recipe composition policy and explicit capacity. The regional
owner lazily asks the shared chooser for complete candidates, binds each selected
prefix, measures that program's actual root envelopes, derives regional space and
physically generates every required body and journey. It accepts the first fully
qualified candidate in deterministic preference order. Original capacity, mix,
child-count, price, source-size and geometry constraints remain mandatory.

Only typed placement/spatial/region-intent infeasibility or the shared area's
existing semantic constraint classification advances choices. Malformed data,
dependency changes, cancellation and search/work budgets propagate. A pose-
independent `NestedSourceEnvelope` witness can exclude only the owning program
choice across other regional choices. Other failures retain the complete joint
choice search. No source is resized to repair a mismatch.

`OwnershipPlan.CompositionPlan` retains enumeration work and each complete
candidate's selected region/rank/analysis identity, candidate hash when available,
work, failure owner/reason, invariant source witness and accepted geometry hash.
Enumeration plus candidate work equals aggregate ownership work under the
unchanged request cap. Registered dependencies are checked across attempts.
`PhysicalFeedback` may qualify component alternatives inside a candidate; its work
then equals that composition attempt's work. All public reads/exports replay the
original unbound source and complete selection trace. JavaScript consumes the saved
choices and checks bounded structural relations; native replay authenticates them.

Without `Coupling`, terrain-dependent regional mix requests still refuse unresolved
habitat context. Direct surfaced recipes now have bounded region-choice and
source-adaptation analysis below. Automatic ownership now admits nested recipe
Regions/LocalLayouts only through explicit `SpatialContext` and the joint policy
described below; uncontextualized overrides still refuse.
General redesign, selective locks, streaming and complete-world design remain open. [Reusable input](../examples/metadata/regional-program-choices/README.md),
[qualification](evidence/2026-10-31-regional-program-choices-verification.json).

### Spatial choices and accepted adapted source habitats

On a concrete surfaced recipe, bound `AnalyzeProgram` and the existing internal
choice frontier expand complete content before selecting intent regions. They use
the same region-demand calculator and selector as ordinary generation. Exact full
ranks include region alternatives in that order. A content-dependent impossible
region may advance the preceding mix; `RejectedAnalyses` retains actual ranks,
mix allocations, reason and work on regional choice receipts. Hard work/attempt,
cache/receipt, malformed input, dependency and cancellation failures propagate.
The analysis retains source metadata coordinates and does not place consumer bodies.

When the terrain-demand source declares `LayoutAdaptation`, the same generator
qualifies its complete world before measurement. `TerrainAnalysis.SourceAdaptation`
retains actual rejected/accepted attempts, and `SourceAllocationWork` is charged
once inside preparation. The accepted `LocalLayoutPlan` binds habitat identity
and exact quantities. Source stage limits stay unchanged; reported work is not an
all-stage CPU bound. One captured mix frontier reuses only its immutable upstream
habitat, under the existing cache-byte cap and dependency checks.

A consumer without its own adaptation policy must declare the exact original
source layout template. Generation then uses that source's accepted definition;
a different template refuses `METADATA_POPULATION_SOURCE_LAYOUT_CHANGED`. The
source's bodies, access, allocation and journeys remain protected. This is source
preservation, not permission to redesign it for new content. Native source replay
qualifies exact geometry; JavaScript validates bounded structural bindings.

Four technical controls retain47nodes/34bodies/36directed paths;440m² and430m²
accepted allocations fund three/two discoveries with original0.4m footprints.
Two source edits restore exactly, including original world bytes on rollback.
These checks do not close broad world design or automatic nested ownership.
Automatic ownership now supports boundary-free nested templates through explicit
`SpatialContext` below. Concrete upstream source composition is qualified in the
next section; nested source/context integration and general joint redesign remain open.
[Reusable example](../examples/metadata/adapted-source-habitat/README.md) and
[evidence](evidence/2026-10-31-spatial-program-analysis-verification.json).

### Qualified upstream complete-program ownership

A concrete `PopulationSource.Recipe` may use the existing `ProgramComposition`
policy. The same C# generator qualifies its actual complete world before consumer
analysis expands. `SourceProgram.QualifiedComposition` retains the original
source recipe/catalog and every failed/accepted choice; public source identities
refer to that original qualified source. Internal bound analysis remeasures its
actual complete selections. Unknown source spatial context refuses
`METADATA_PROGRAM_ANALYSIS_SOURCE_CONTEXT_UNRESOLVED`; global terrain is not a
substitute for an unbound source.

Current source mix inputs must match the accepted source request, including
supplies, demand, minimum count and limits. Its sealed exclusions replay through
the same mix owner. Source choices consume no consumer rank. Missing/changed
programs refuse `METADATA_POPULATION_SOURCE_PROGRAM_UNUSED`/`PROGRAM_CHANGED`;
accepted members, source dimensions and entrances cannot be resized or dropped.
The same ownership applies to explicit frozen `Placement.SourceWorld` generation;
public program analysis still refuses inline source documents.

When upstream composition selected a terrain region, the consumer must retain its
original region template and derived demand. The exact accepted source region is
inherited without another consumer choice; conflicting changes refuse
`METADATA_POPULATION_SOURCE_REGIONS_CHANGED`. Accepted adapted layout inheritance
uses the existing source-layout owner. Downstream quantities measure actual
selected habitat and allocations. A consumer composition policy owns only newly
added mix/region choices, with its own bounded receipt.

One captured consumer frontier reuses an immutable qualified source and habitat
under existing cache-byte limits. Source dependency identity is checked before
reuse; callback mutation/cancellation, source failures and work/cache limits stay
hard. Source generation retains its original stage limits; reported qualification
work is not an all-stage CPU/latency guarantee. Native reads replay original source
composition, geometry and consumer derivation. JavaScript checks exact inherited
mix/region facts, original composition structure and accepted habitat bindings;
it does not authenticate hashes or certify world/game design.

Four controls retain48nodes/32bodies/28directed paths and all10discoveries, including
two source-allocation quantities and a physically selected second terrain
component. Original source sites remain14×6m with16×8m reservations; rejected
100m alternatives remain in the catalog. Source edits, failed-edit preservation,
two-edit history restore and exact rollback pass. Nested `SpatialContext` combines population sources/terrain demands through the
source-owned habitat contract below. [Reusable input](../examples/metadata/source-program-ownership/README.md)
and [evidence](evidence/2026-10-31-source-program-ownership-verification.json).

### Ownership-relative nested spatial programs

Optional `MetadataRegionalProgramRegion.SpatialContext` declares purpose and source
rule through `MetadataRegionalSpatialContext`. Its reusable recipe has no surface
or origin, a `Regions` request with null boundary, no authored seeds and positive
terrain intents. The regional owner binds that boundary to the exact computed
ownership polygon. `RegionDemands` measures complete source envelopes normally.
Local layouts either use automatic `AreaBalanced` program intents or an explicit
`RegionFrame` so authored anchors/cuts/grids resolve from actual selected regions.
Strategies are declared by content; no city/genre switch or mandatory grid is added.

This context requires the existing regional `Coupling`, `ProgramComposition` and
per-region `Capacity` policies, plus a recipe composition policy for actual region
decisions. Initial complete catalog analysis uses `RegionSelectionDeferred=true`;
its ranks contain only actual catalog decisions, possibly an empty vector when
the region is the only decision. It does not invent a region, seed or full-world
habitat. This internal initialization state cannot be accepted as a generated world.

The existing joint search then reanalyzes each actual ownership candidate,
including region demand and complete region-choice ranks. Stable requirements and
full exact bindings must pass ordinary physical generation, all local/regional
journeys and native replay. Accepted analyses/choices cannot remain deferred.
Failures retain original source/trace, and the existing work/attempt/depth/cache
limits remain hard. Local/environment owners retain their independent limits;
reported regional work is not an all-stage CPU or latency bound.

Authored metadata coordinates from declared relative layout grammars remain valid;
the viewer verifies their source declaration and exact actual value. Non-root
physical sources remain unplaced in analysis. JavaScript checks explicit context,
boundaries, deferred/final state and geometry receipts, while native replay
authenticates the entire source derivation.

Source boundary/seed overrides, missing context/coupling/composition/capacity,
and unframed authored local layouts refuse. `PopulationSource`/`TerrainDemands`
now compose through explicit source-owned boundary context in the next section.
Independent consumer habitat overrides still refuse.

Five complete controls each retain33nodes/27bodies, nine semantic terrain regions,
nine local allocations and34directed paths. Source-size, protected terrain,
ownership orientation and complete mixed-program alternatives change actual
design without removing required members. Source edits and exact rollback retain
full native history. These are technical controls, not complete world-design or
genre/scale acceptance. [Reusable example](../examples/metadata/nested-ownership-world/README.md)
and [evidence](evidence/2026-10-31-nested-ownership-verification.json).

### Source-owned nested habitats in computed ownership

A `MetadataRegionalSpatialContext` may use a recipe whose `PopulationSource`
declares `Context.UsePlacementBoundary=true`. The source owns boundary-free,
seed-free positive terrain intents, source region-demand calculations, framed or
area local layouts and its own complete `ProgramComposition`. Consumer `Regions`,
`LocalLayouts`, `RegionDemands` and `LayoutAdaptation` must be absent. Existing
regional coupling/composition/capacity plus a consumer composition policy remain
required, even when that consumer has no independent choices.

Initial joint measurement expands complete catalog programs under declared
minima with `RegionSelectionDeferred=true`. Unknown source qualification is not
invented: this provisional analysis has no `SourceProgram` or accepted habitat.
At actual computed ownership, the population owner binds source surface/origin,
placement boundary and nested region boundary. The existing generator qualifies
that full source, retains rejected/accepted choices, derives local layouts and
measures exact habitat counts. Only added consumer choices consume consumer ranks.
Source-only accepted consumers use exact empty vectors and honest zero-selection
receipts backed by actual upstream composition. Initial and final ranks may differ.

The joint owner converges complete demand/choice state before ordinary physical
qualification and full native source replay. A provisional input may already have
stable area/count/rank requirements; it still cannot stand in for actual source
qualification. Accepted output analyses and choices cannot remain deferred. Every
original source member, dimension, entrance, module offset and local/world journey
remains protected. No independent consumer habitat redesign, source shrinking,
required-count truncation or empty-position fallback is introduced.

Shared-face region outlines preserve source cell coordinates/areas. The existing
qualified shared-face predicate also supplies endpoint identity, preventing
independent clip/interpolation roundoff from leaving an open boundary. Boundary
sampling remains explicit: `UseLongestEdge=true` chooses the initial canonical
edge by greatest length and lowest index on a tie. It conflicts with nonzero
`StartEdgeIndex`; spacing/inset/span and every physical constraint remain mandatory.
It is not a continuous inset-curve solver or permission to skip invalid samples.

Five controls retain184nodes/164bodies,30nested regions/30allocations,74discoveries
and110directed journeys. Lower authored source coverage grows actual allocation
areas and discovery counts from21to32. Source-only, mixed upstream programs,
protected terrain and ownership rotation retain complete source geometry.
Two source edits/history restore exact original world bytes. Broader joint redesign,
full purposeful world-design variety/scale, selective locks and streaming remain
open. [Reusable input](../examples/metadata/nested-source-habitats/README.md) and
[evidence](evidence/2026-10-31-nested-source-habitats-verification.json).

### Joint habitat, exact counts, complete programs and physical qualification

Declaring both regional `Coupling` and `ProgramComposition` selects the joint
path in `MetadataRegionalOwnershipPlanner`. It uses the existing source analysis,
ranked mix chooser, source-envelope measurement, ownership/intent allocation and
complete physical generator. It has no city/genre switch or mockup prerequisite.

Catalog minima initialize complete program alternatives. Every candidate region
is then analyzed against its actual shared surface, origin and owned boundary.
The complete choice frontier is rebuilt for that habitat: a changed terrain count
can add or remove nested mix decisions. Exact complete rank bindings and measured
area/clearance/weights must converge together before physical acceptance. No
minima-only prefix is treated as a complete later program. All required bodies,
access, grounds, local and regional journeys still undergo normal generation.

The shared source-fit owner can reject a necessary local envelope mismatch before
allocation. A proved static parent/child failure prunes only matching node/type/
structure geometry under the same source bindings. Quantity annotations and
world translations do not change that geometric proof. Dynamic facings, connected
growth and layout adaptation do not receive this early static proof; their actual
physical checks remain. Other typed physical/area/region failures retain finite
backtracking. Malformed declarations, dependency drift, cancellation and exhausted
budgets propagate. A cycle rejects that branch, not every possible world.

`OwnershipPlan.JointPlan` replaces the separate composition/coupling/physical
receipts for this combined path. Its ordered attempts retain parent links,
iteration, input/output identities and complete choices, measured demands and
habitat hashes, component exclusions/selections, candidate/ownership/geometry
hashes, work and actual failures. `EnumerationWork + sum(Attempts.Work)` equals
aggregate ownership work. Existing regional composition attempt limits (1..64),
coupling branch depth (1..32), optional physical qualification limit (1..32),
individual stage limits and the original request work cap remain mandatory.
Reaching one branch's depth limit can leave other candidates to inspect; later
frontier exhaustion is a hard budget failure, not proof of universal infeasibility.

The declared physical feedback policy can reject a complete component assignment
and reconsider declared partition axes with fresh component contexts, then
reanalyze the consequences through the same joint search. Complete physical
qualification remains mandatory without this optional repair policy. Native
generation/read/export and source-history restoration rederive the original
unbound request and complete trace; JavaScript only checks saved structural facts.

The shared program-choice cursor advances past a descendant only when its exact
cached request domain is proven exhausted. A solved single-offer domain with one
permitted count has one count vector and can establish that fact immediately.
Analysis and generation use the same prefix-advance owner. Different requests,
unknown alternatives and changed source/terrain contexts are not pruned. Existing
analysis, mix-work, selection, cache, receipt and region-work caps still apply.

Terrain-derived `MetadataPart.ProgramMix` counts bind an exact solver count.
`MetadataProgramMixRequest.ExactCount` must equal `MinimumCount` and remain within
the existing 128-member request bound and authored offer/count/cost/capacity caps.
The original rule is retained. The solver cannot add surplus members to satisfy
capacity; impossible exact supply refuses. Terrain and mix receipts cross-check
the same count on native validation and inspection. Manual count parameters remain
conflicting quantity authorities on a terrain part. A `Capacity` rule instead uses
the terrain-derived count exactly and validates the actual selected sources against
the required capacity; it cannot increase that count to resolve a shortfall.

For an explicitly chosen capacity-feasible combination, a game can use the
`ProgramMix` rule's own capacity demands and offers with the terrain count. The
solver selects among authored complete sources within the declared cost, count and
work limits; it does not add members. This is a separate authored selection policy
from seeded `Variation`, and declaring both on the same part is rejected. A failed
seeded variation is not silently replaced by a program mix.

One captured analysis frontier reuses its choice-independent upstream habitat
measurement under the existing composition cache-byte cap. Source/catalog capture,
dependency checks, source-analysis work and cancellation remain active. This does
not share mutable caches between worlds or claim an all-stage CPU/latency bound.
Physical source-retry exhaustion also preserves the last complete consumer choice,
the actual physical cause and charged placement work; it is not mislabeled as a
required-prefix mismatch after rollback of provisional choices.

[Reusable joint input](../examples/metadata/joint-terrain-world/README.md),
[bounded qualification and remaining gates](evidence/2026-10-31-regional-joint-verification.json).

Use [the framework examples](../examples/metadata/derived-regions/README.md), including
`request-program-capacity.json` and `request-larger-program.json`, with the existing
`generate-derived-regional-world` command. No game setup, image or Godot is needed.
See [bounded qualification](evidence/2026-10-31-program-ownership-demand-verification.json).

## Direct input ownership

`IWorldMetadataGenerator.Generate` and `DescribeParameters` capture a detached
recipe/catalog before invoking registered layout validation or generation. Layout
callbacks may change their caller's original collections without changing the
captured counts, hierarchy, metadata properties, coordinates, remaining scope or
provenance hashes. Managed sessions already capture their inputs; both routes use
the same generation algorithms. Direct layouts still do not require incremental
dependency identity; session/replay dependencies keep their existing measured
identity requirement.

Callers must not write to collections concurrently while capture is reading them.
This is input ownership, not isolation from arbitrary extension code or a promise
of deterministic unversioned extensions. The internal typed copy preserves NaN
and infinity for rejection by semantic validators; it does not allow them in
published worlds or change the strict file-reader policy.

## Population refusal causes

Automatic source/population coordination retains its original placement causes.
If a revised source has no feasible assignment after a population refusal, the
outer `MetadataPlacementInfeasibleException.InnerException` is an
`AggregateException`: its first cause is the last population placement failure,
and its second is the revised-source placement failure. Their original structured
`PlacementFailures`, conflict pairs and separation witnesses remain available.
Exhausting `MaximumFeasibilityAttempts` retains the last population failure as its
direct inner cause. Existing outer reason codes, coordination attempts, work limits
and transactional state behavior remain unchanged. This does not turn a finite
search refusal into proof of geometric impossibility.

The [fenced compound](../examples/metadata/fenced-compound/README.md) exercises both
branches with complete physical parts and a declared gate journey. Its boundary
uses ordinary catalog solids and the existing generated-boundary/facing grammar;
the standalone site-boundary helper is an independent geometry oracle in that
qualification, not a new automatic metadata compiler.

## Prescribed population scale and work

The current prescribed/physical-access limit remains 1,024 sites. The existing
placement owner indexes incident relationships and builds a conservative bounds
hierarchy for immutable prescribed options. Bounds contain both the actual
reservation/footprint and the anchor. Strict box-distance separation can omit a
branch; touching/equality and possible interactions reach the original exact
polygon predicate in original demand order. This index changes no coordinates or
layout grammar. Dynamic search/growth/provisional candidates retain their owner.

Access validation prepares one immutable positioned set per stage assignment and
checks only pairs with a possible solid/port interaction through the same opening/
blocking predicate. Every physical source is still classified and validated; body
collision, source containment, clearance and terrain remain independent constraints.
Index construction, relevant relationships and access work share the original
finite/cancellable work budgets. Work counts intentionally reflect the new work.

The [scale examples](../examples/metadata/population-scale/README.md) retain complete
256/512/1,024-member populations and a changed-density source counterfactual.
Generation/read/publication/session replay recompute the current result; no saved
index is trusted. A larger physical declaration explicitly refuses rather than
truncating content. Chunk references remain finite indexing of full source
geometry, not independent world streaming.

## Reported developer session lifecycle

`MetadataGenerationSession.StartAsync(generator, recipe, catalog, reporting, ...)`
creates a session using `MetadataSessionReporting` and the existing shared
`WorldOperationReporter`. Initial generation supports optional
`IProgress<MetadataStageExecution>`; those records describe completed/reused stages,
not overall percentage or a continuous heartbeat. Core generation remains the
same engine algorithm. Caller inputs/protections detach before reporting callbacks.
Generation is synchronous CPU work; the host chooses its background scheduling.

Reported sessions await `ApplyAsync`, `ApplyAndPublishAsync` and `PublishAsync`.
Synchronous `Apply` is rejected on a reported session. Pure constructor/algorithm
APIs remain available for hosts owning their reporting boundary separately.
`RestoreAsync(..., reporting: ...)` installs host services again after verified
source-history replay; service objects are never checkpoint data. Restore records
one restore action rather than inventing fresh actions for historical edits.

Each action emits `world.metadata.session.<create|edit|edit-publish|publish|restore>`
with `started`, `succeeded`, `failed` or `cancelled` suffixes. The shared event's
optional `Context` holds source/world/checkpoint identity, proposed input hashes,
accepted edit, stage/reason attribution and executed/reused counts as applicable.
Context snapshots before any channel writes and is bounded to 32 entries,
64-character ASCII keys and 2,048-character values. Runtime actor/correlation
labels are host-supplied, bounded, and do not authenticate a human. Messages omit
raw input bodies and exception text; original exceptions remain available to callers.

The CLI records completion after its command/publisher returns, before terminal
report delivery. A later delivery error uses `.reporting.failed` with outcome
`committed-reporting-failed` and the actual `outputPath` when output exists, or
`completed-reporting-failed` for a completed read-only operation. It keeps a
nonzero exit code for the delivery error, exposes the recoverable result/path
and does not add a false failed/cancelled generation or repeat the command.
Precommit failures keep their normal failed/cancelled outcome. Raw algorithms
and file helpers remain available to hosts owning the reporting boundary;
choose reported sessions or the CLI for the integrated lifecycle.

The same session exclusion spans awaited reporting and mutation/publication.
Before commit, cancellation/failure preserves accepted state and existing output.
After the actual state/directory commit, terminal reporting uses no cancelled
caller token. A success-sink failure throws `MetadataSessionReportingException`
with `Committed=true`, the recoverable `Session` and optional committed
`OutputDirectory`; it does not report a failed world or repeat the action. Creation
and restore commit an in-memory session; publication commits a directory. A failed
operation and failed failure-report sink are retained together in an aggregate.

The `metadata-session` CLI uses this lifecycle with its existing reporter and
command correlation. Its outer boundary distinguishes committed reporting faults
from failed generation. Other pure/optional domain boundaries remain separately
scoped; this is not a claim of complete low-level reporting coverage.

See the [runnable public SDK example](../examples/worldcreator-workflow/README.md)
and [commands/evidence](evidence/2026-10-30-session-workflow-verification.json).

## Geometry-derived population facing

Set optional `MetadataPopulationGrammar.Facing` on a path, boundary, generated
route, generated boundary, region-band or terrain-contour binding. `Along` follows the exact selected path or contour segment, or the declared region-band direction;
`Against`, `Left` and `Right` add 180°, +90° and -90°. Boundary-only `Inside` and
`Outside` use polygon winding; generated hole loops use the region's filled side.
The binding's existing `RotationDegrees` becomes the source-local angular offset.
Local +X maps to the selected heading plus that offset: 0° points world +X and
+90° points world +Y. Effective rotations are normalized to [-180°, 180°).

`Facing=null` retains the fixed binding rotation. Sockets and area population
reject a facing declaration; paths, contours and region bands reject
`Inside`/`Outside`. Contours use each sampled segment's tangent, including at
bends and when reversed. Existing
source-world members cannot be reoriented by a consumer. An exact bend uses its
incoming tangent, matching the existing normal and arclength position rule. A
generated reverse route uses the actual saved reverse path, which need not be a
reversal of the forward path. There is no finite-difference heading estimate.

The common footprint/access owners rotate the complete original polygon,
reservation, site features and access ports before normal placement/terrain/
containment/passage qualification. Facing can make an otherwise placeable source
fail. The engine retains source dimensions, counts and constraints and refuses
that declaration; it does not resize content or move its prescribed anchor.

`MetadataPlacementPlan.PopulationOrientations` stores member IDs, purpose/source,
selected mode, original angular offset, tangent headings, optional filled-side
normal and effective rotations. Expanded placement bindings contain the effective
rotation for each node. Native source/publication/checkpoint replay reconstructs
the complete result. JavaScript checks the saved path tangent and rotation against
each effective binding; native replay remains the complete source authority.
Author edits against the saved source recipe or session snapshot; expanded bindings
are resolved output with effective rotations.
`WorldPopulationGeometry.PathPoses` and `BoundaryPoses` expose the shared planar
sampling result for geometry consumers.

See the [reusable facing examples](../examples/metadata/population-facing/README.md)
and [qualification/remaining limits](evidence/2026-10-30-population-facing-verification.json).
This supplies geometric orientation metadata; sprite-facing selection, animation
and Godot remain separate downstream work.

## Source-derived boundary population

`MetadataPopulationGrammar.GeneratedBoundary` references an accepted source
footprint, declared reservation, region outline or allocation outline through
`MetadataGeneratedBoundaryPattern`. A physical source selects `NodeId`, its actual
program parent, or a parent `PartId`/`PartIndex`. Region/allocation selectors use
their existing IDs. Multiple loops require explicit selection. Canonical ordering
and exact collinear reduction preserve the boundary geometry; unresolved topology
and bounded complexity fail explicitly.

Optional `UseLongestEdge=true` chooses the initial canonical edge from actual
accepted source geometry. Equal lengths keep the lowest canonical index. Nonzero
explicit `StartEdgeIndex` conflicts with this policy. The resolved pattern records
the actual edge; native replay and the viewer verify it. This chooses the start,
not a new boundary, inset curve or implicit member capacity.

The shared `WorldBoundaryPattern` now declares `WorldBoundarySide`. Sampling uses
arclength and incoming-edge normals. Generated region hole loops reverse the
primitive side so region-inside means filled land. There is no continuous offset
curve, wrap, arbitrary placement or implicit capacity repair. Existing terrain,
full-footprint, containment, source access and passage constraints remain mandatory.
Inside-region/allocation bindings also retain the full matching placement scope.

`MetadataPlacementPlan.PopulationBoundaries` records exact source/hash, selected
loop, hole/side, member identity/purpose and world-coordinate positions. The native
owner replays the complete source. JavaScript checks saved sampling arithmetic,
selector binding and node positions. Repeated source frames and source-size edits
rederive positions through the current generation/session owners.

Ordinary `WorldLocalProgram.AllowDisconnected=true` explicitly lets one program
own all clipped components. Seed membership and the original minimum apply to the
whole retained union. Connected ownership remains default; regional seed and
area-balanced intent policies remain unchanged. Disconnected ownership provides
no bridge, road or gameplay connectivity.

See the [nine reusable examples](../examples/metadata/generated-boundaries/README.md)
and [source-qualified evidence](evidence/2026-10-30-generated-boundaries-verification.json).

## Measured generation studies

`MetadataGenerationStudy` evaluates 1–128 declared `MetadataStudyCase` snapshots
through the current engine. `MetadataStudyDefinition` declares purpose/source,
comparison cohorts, 1–64 histogram bins and finite measurement work. Generation
budgets remain in each unchanged recipe. `RunAsync` returns the report;
`PublishAsync` uses the existing atomic publication owner for complete study input,
report and successful world packets. `study-metadata` exposes the same API.
Known generator/dependency refusals remain per-case outcomes. Cancellation,
unexpected exceptions, changed dependencies and publication/reporting failures
before completion abort explicitly. No rejected sample receives a world or
substitute recipe.

After a study result is complete, reporting failure throws
`MetadataStudyReportingException` with its exact `Report`, `CorrelationId`,
`Completed=true` and optional committed `OutputDirectory`. `Committed` is true
only for a published result. Recover that result instead of rerunning the study;
no failed/cancelled generation event is fabricated after completion. Terminal
delivery uses an uncancelled token and sends one final completion notification.
Late cancellation cannot turn a completed result into a cancelled study. A
precommit operation failure plus failure-reporting failure preserves both errors
in the existing aggregate exception.

Metrics cover explicit graph shape, distinct-neighbor branching, actual directed
path detour, source-solid footprint coverage and contextual counts. Missing
measurements stay unavailable. Source-solid coverage excludes generated walls,
corridor surfaces, terrain and reservations; it is not total built density.
Histograms preserve rejection/availability denominators and group matching declared
spaces/units. Fingerprints identify exact output structure/geometry/graph repeats;
they do not certify perceptual diversity. Time and memory are process-scoped
observations, not isolated benchmarks.

Study source uses the existing strict/partitioned JSON owner and 256MiB complete
source limit. Bounded snapshotting precedes callbacks. The optional study API
does not alter generation algorithms, invent seed behavior, choose a genre, start
a setup wizard or invoke artwork/Godot. See the
[complete reusable corpus](../examples/metadata/generation-study/README.md) and
[source-qualified evidence](evidence/2026-10-30-generation-study-verification.json).

## Authored JSON integrity

`WorldJson.CreateOptions` rejects duplicate property names through .NET 10 native
controls. This includes nested objects and escaped-equivalent names, inline files
and complete partitioned bodies. The storage manifest parser uses the same native
rule; content hashes do not make an ambiguous body acceptable. Names remain
case-sensitive. Typed catalog property bags also pass the native shape check
before expansion; their existing count/name/64KiB limits and cancellation apply.
Unknown and required contract fields retain their current validation. No historical
input format or duplicate-name preference is provided.

See [bounded contract and CLI evidence](evidence/2026-10-30-core-audit-verification.json).
This qualifies the current shared metadata boundary, not every optional visual
consumer or a whole-game runtime.

## Catalog-bound generation parameters

Generation controls in `WorldMetadataRecipe.Parameters` are integer values bound
by the catalog's `MetadataPart.CountParameter`,
`MetadataCapacityRule.QuantityParameter` or
`MetadataProgramDemand.QuantityParameter`. Names are case-sensitive. A supplied
name with no declaration anywhere in that catalog refuses with
`METADATA_PARAMETER_UNBOUND:<name>` before world generation/publication. The error
retains sorted `UnboundParameters` and `DeclaredParameters`. An active declaration
whose value is missing retains `METADATA_PARAMETER_MISSING:<name>`.

`IWorldMetadataGenerator.DescribeParameters(recipe, catalog, token)` returns
immutable, canonically ordered `MetadataParameterBinding` records with the name,
archetype/part location, role and any capacity ID/unit. This uses the same input
validation owner and does not expand a world. An otherwise valid recipe with an
empty parameter dictionary can inspect declarations before supplying values.
Static descriptions include unused archetypes, zero-count branches and variation/
program alternatives. A declaration does not claim that branch executed, and
capacity rounding can preserve a count while the requested quantity changes.

Declared inactive parameters remain legal for reusable catalog configurations.
Arbitrary descriptive/game data belongs in archetype/connection `Properties`,
where it is preserved without claiming a generation effect. Layout `Parameters`
remain the separate strategy-specific inputs of `MetadataLayout`. Existing value,
catalog size, count and cancellation limits retain their owners. See the
[bound-parameter example](../examples/metadata/parameter-bindings/README.md).

## Physical source accounting

Every metadata publication now writes `physical-accounting.json`, adds its status,
areas and geometry digest to `summary.json`, and shows a separate table in
`review.html`. The world document, recipe/catalog identities, source geometry and
placement results are unchanged. Domain collections publish one report per child
coordinate space; they do not sum areas across independent domains.

`MetadataPhysicalAccountingPlanner.Measure` measures projected 2D source polygons
through the existing `WorldSurfaceAreaQuery` arrangement. Physical allocations and
clearance envelopes come from the shared access/allocation owner. Sources retain
node identity, purpose, source rule, footprint hash and optional clearance hash.
The measured set is `AccessPlan.Sites` plus their associated placement clearance
envelopes. Generated walls, terrain and corridor surfaces remain with their own
planners; this report does not aggregate all compiled collision/render geometry.
The geometry digest binds that selected source set, coordinate space and boundary;
recipe/catalog hashes remain separate. This is not catalog approval, collision or
whole-world design validation. Use a generated or validated world as its source.

| Field | Meaning |
| --- | --- |
| `GrossDomainArea` | Finite outer surface boundary, including terrain/water/exclusions. |
| `SolidUnionArea` | Union of physical footprints explicitly classified solid. |
| `ReservationUnionArea` | Union of reservation-role footprints and separately declared placement clearance envelopes. |
| `PhysicalFootprintUnionArea` | Union of all physical source footprints, without enlarging solid bodies to their clearances. |
| `CombinedUnionArea` | Union of physical footprints plus clearance envelopes. |
| `AllocatedSuitableArea` / `UnclaimedSuitableArea` | Saved region allocation, reported separately from physical footprints. |
| `DeclaredGroundUnionArea` / `OpenGroundUnionArea` | Optional explicitly declared ground-use source union, and that union minus solid source footprints. Unmarked reservations and clearances do not supply ground-use intent. |
| `InteriorCoverage` | Optional per-generated-interior floor union, generated wall union and clear floor after subtracting walls plus all solid source footprints. Bound to the assembly's exact room sources; the search-domain boundary is not constructed floor. |

`InteriorCoverage` retains each interior rule ID, scope, purpose, source rule,
sorted room-node IDs and every wall's ID, owning room and footprint hash. It
references the existing access-plan room polygons and interior-plan wall polygons;
it creates no new layout. The accounting geometry digest includes those assembly
bindings and wall sources. `Validate` recomputes the measurements and rejects a
changed report/source. Missing or mismatched room ownership, unclassified physical
sources and walls outside their owning room refuse. Generation/read replay still
belongs to the interior planner; accounting consumes generated or validated data.

For each assembly, `FloorUnionArea` counts overlapping room polygons once.
`WallUnionArea` counts overlapping generated wall strips once. `ClearFloorArea`
is room-floor union minus the union of **all** generated interior walls and solid
source footprints in that coordinate domain, including wall/solid overlaps and
walls belonging to another assembly. It does
not subtract movement-radius envelopes, water or terrain exclusions and is not a
traversability certificate. It is distinct from a declared ground-use minimum,
whose existing contract subtracts solid source footprints only. An absent
interior plan omits this measurement; generic reservations never imply floors.
Independent coordinate domains and overlapping assemblies are not additive.
Publication includes the measurements in `physical-accounting.json`,
`summary.json` and `review.html`, with the same bounded accounting budget and
unmeasured/failure semantics as other categories.

An interior rule can require constructed floor capacity with an optional
`FloorRequirement`, for example:

```json
"FloorRequirement": { "MinimumClearArea": 335, "MinimumClearFraction": 0.9 }
```

Both minimums must pass. Area uses squared world units; the fraction uses the
assembly's exact room-floor union, never the available search-domain boundary.
Values must be finite, area nonnegative and fraction between zero and one.
Omitting this declaration preserves existing generation and serialized world
data. It does not impose a global density rule or require an interior in other
world types. Native interior feasibility, generation and saved-plan replay enforce
the requirement through the same accounting owner and existing shared work ledger.
Reports retain `Requirement` and `MinimumSatisfied` only when declared. Numerical
acceptance tolerances are 1e-8 area and 1e-9 fraction. Full-width route validation,
terrain suitability and runtime traversal remain separate requirements.

Before placement search, the accounting owner can prove a source-capacity
shortfall for supported convex room programs. It reuses the interior owner's
closed wall geometry and subtracts at most opening-width × wall-thickness per
incident doorway (entrance allowances are conservative). It selects at most one
mutually separated descendant solid stage per room; overlapping stages,
clearances and reservation wrappers are never added as solid bodies. Fixed source
areas, disjoint-room and wall/solid constraints, bounded containment depth and
conservative numerical/contact allowances yield an upper bound on possible clear
area/fraction. Missing support for a proof leaves ordinary exact feasibility in
force; the bound is a necessary condition, not a layout generator or acceptance.

An early refusal reports `METADATA_PLACEMENT_UNRESOLVED:INTERIOR_FLOOR_CAPACITY`
and a typed `InteriorFloorCapacityWitness`, with every room/source-stage member,
wall/opening allowances, uncertainty and conservative maximum. Exact assembled
failure reports `METADATA_INTERIOR_FLOOR_MINIMUM_UNSATISFIED` and a typed
`InteriorFloorObservation`. Placement preserves the original constraint exception
through final failure/attempt-limit errors. Neither failure silently drops members,
resizes sources, raises budgets or replaces the accepted session/output.
The reusable [full-world example](../examples/metadata/terrain-level-world/floor-intent/README.md)
demonstrates accepted terrain/minimum edits and an insufficient-floor refusal.

Area values use squared `Space.Unit`. The categories overlap and are **not
additive**. A parent reservation is counted once in a union, while every source
still appears in the inventory. Terrain patches and paths do not inflate physical
object footprint coverage. Neither allocation nor any area ratio certifies a
complete game world; declared purposes and remaining scope stay explicit.

`Measured`, `NotApplicable` and `Unmeasured` are accounting states, separate from
`MetadataCoverage`. Geometry-free worlds, including zero-axis topology, have no
invented zero physical area. An absent access/classification plan, unsupported coordinates or
an unbounded surface retain an explicit reason and nullable measurements. Specific
arrangement/work exhaustion after input snapshotting also returns `Unmeasured`
with no partial category areas. Invalid input, input-snapshot budget exhaustion
and cancellation still refuse. Default work is 20M units; the API accepts an
explicit 1..200M bound, without changing generation budgets. Existing arrangement
edge/event limits remain in force; source polygons are never resized or sampled
away to obtain a measurement.

For saved reports, read through the normal bounded JSON boundary and call
`planner.Validate(report, verifiedWorld)`. It recomputes the report with its saved
work bound; changed sources, purposes, roles, clearances or reported values reject
as `METADATA_ACCOUNTING_SOURCE_MISMATCH`. Loading a report alone does not validate
its source world. [Current implementation and independent geometry evidence](evidence/2026-10-30-physical-accounting-verification.json).

### Explicit ground-use intent

A catalog `PhysicalFootprint` may declare optional `GroundUse`:

```json
"GroundUse": {
  "Kind": "courtyard",
  "Purpose": "Shared activity and circulation ground",
  "SourceRuleId": "catalog:courtyard-ground",
  "MinimumOpenFraction": 0.8
}
```

The source must have reservation occupancy and retain its exact physical polygon;
its separate clearance envelope does not enlarge the ground use. The kind is
game-authored. No ground-use declaration is inferred from an ordinary parent
reservation, region allocation, empty map position or unspecified occupancy.
Every declared instance must have a physical source. A world with such declarations
must classify all physical sites; unspecified sites cannot silently count as open.

The existing `MetadataPhysicalAccountingPlanner` computes each use's source area
and open fraction by subtracting the union of **solid source footprints**. Crossing
and overlapping solids count once. Observations retain source identity, exact
footprint hash, the declaration and whether the minimum is satisfied. Overlapping
use polygons also count once in the two union measurements. The geometry digest
binds the declaration as well as the selected source geometry.

Required declarations are checked during `Generate`, domain/session generation,
document validation and publication. A failed minimum rejects with
`METADATA_GROUND_USE_MINIMUM_UNSATISFIED:<node>`; an unmeasured required declaration
rejects instead of treating unknown area as success. `Measure` itself retains
failed observations for diagnostics. Invalid declarations, missing physical
sources and invalid occupancy refuse explicitly. Fractions are finite 0..1,
with a 1e-9 fraction tolerance. Existing accounting work and arrangement limits
and cancellation remain in effect. Reports expose no partial ground measurements
after work exhaustion. Failed edits retain the previous accepted packet.

Contained programs also receive a source-capacity preflight before their candidate
search. The same accounting owner uses resolved source polygons and validated nested
placement ownership to bound how much solid area must lie inside a ground source.
It combines solid members across branches whose common placement stages prove
separation and whose complete paths remain footprint-contained. A solid ancestor
is counted once, without adding its contained descendants. Reservation-scoped
escape paths remain unproven. It never adds unrelated or potentially overlapping
branches, or counts reservation/clearance area as solid. Rotation preserves source area. The bound includes numeric,
boundary-contact and maximum containment-depth uncertainty; near-boundary cases
without a strict proof continue to exact geometry evaluation.

A proved shortfall raises `METADATA_PLACEMENT_UNRESOLVED:GROUND_USE_CAPACITY:<node>`.
`error.Data["GroundUseCapacityWitness"]` contains a typed
`MetadataGroundUseCapacityWitness`: source node/kind/rule, nested stage, all counted
solid IDs/areas, ground area, uncertainty, maximum possible open fraction, required
minimum and unit. `SourceStageIds` attributes all contributing stages when more
than one stage supplies the counted solids; single-stage witnesses omit it.
Work is charged to the existing placement budget. The existing
pose-invariant failure protocol prevents source coordination from prohibiting
positions that cannot alter the shortfall. Work exhaustion and cancellation remain
distinct failures. This is an early proof of selected impossible requests, not a
certificate that every other layout fits or meets its ground minimum. Final exact
accounting remains mandatory. See [capacity qualification](evidence/2026-10-30-ground-capacity-verification.json).

Joint regional program analysis applies this same necessary proof to fixed catalog
sources before terrain allocation. Unknown branches remain unproven. A typed
`GroundUseCapacity` witness appears in the invariant choice; its pruning key
binds contributing source and containment paths, their geometry and occupancy,
and the placement template. Explicit pose-controlled rotations are honored by
source-envelope checks. Full placement and world qualification remain required.

The schematic site inspector now exposes each existing catalog ground-use kind,
purpose, source rule and minimum fraction. It labels this as a declared requirement,
not an area measurement or movement certificate. Ordinary reservation sites instead
state that ground-use intent is undeclared. The JavaScript reader validates the
declaration's text, finite fraction, unit and reserved-source binding; it does not
calculate a new layout or infer a missing use. Native `physical-accounting.json`
and the publication's `review.html` retain measured source areas and outcomes.
See [current core and inspector qualification](evidence/2026-10-30-core-inspector-verification.json).

This is a game-authored source-area constraint, not total walkable floor area or
an automatic placement-repair rule. Generated walls, terrain exclusions, water,
actor clearance, path connectivity and runtime movement remain with their existing
owners. Ground-use intent neither completes missing content nor closes remaining
scope. Worlds may leave this optional capability undeclared. See the complete
[terrain/level example](../examples/metadata/terrain-level-world/ground-use/README.md)
and [qualification](evidence/2026-10-30-ground-use-verification.json).

## Independent domain and floor inspection

The [multi-level framework example](../examples/metadata/multi-level-world/README.md)
qualifies three16-room domains with repeated local IDs, measured doorway-bound
transitions, a complete first-to-last journey/return and a larger middle-level
fixture source edit. All99 nodes/96 physical sources survive create, publication,
clean comparison and checkpoint replay; unchanged domains stay exact. Domain
identity is not inferred from an identical world hash. Available field/search
bounds are distinct from the actual floor polygons selected by
`InteriorPlan.Results.RoomNodeIds` and `AccessPlan.Sites.Footprint`; do not treat
all query-domain ground as assembled interior floor. This does not create3D
connectors or remove the per-stage searched-demand cap. [Evidence](evidence/2026-10-30-multi-level-world-verification.json).

Domain collections retain one schematic page per independent coordinate domain.
Each child now has a **Coordinate domain** selector, explicit transition list and
root collection link. Selection opens a canonical sibling page; it never merges
coordinates or infers floor elevations. The same local world can appear in two
distinct domains, including floors with identical node IDs and positions.

The C# publisher derives a version-1 `domain-data` display receipt with collection
and definition identity, current domain, each entry's world/recipe/catalog
identity, canonical sibling path and declared directed transitions. The browser
checks bounded structure, current source identity and the actual child-page path
before displaying controls. These checks do not replace full C# collection replay
or provide signature/authenticity verification. Unknown versions, changed current
sources, copied-floor receipts on the wrong page and unsafe targets refuse.
Source purpose text uses normal JSON/HTML encoding and DOM text nodes.

Standalone exports embed `null`; successful manual metadata load clears collection
context. SVG metadata records a `Domain` reference containing collection,
definition digest and current domain, or `null` when standalone. Navigation does
not create continuous 3D geometry, runtime scene transitions or a physical volume
viewer. [Qualification](evidence/2026-10-30-domain-navigation-verification.json).

## Semantic schematic view and palette

The current inspector uses the engine's `LayerPlan` exclusively for semantic
membership. It checks exact declared/unassigned node and logical-link inventories
before replacing a working view. Content filtering does not infer inheritance
from a parent, kind or renderer group. Physical terrain, route/wall geometry and
other context remain unfiltered, as the UI reports; use renderer controls to
hide those groups. Semantic membership does not represent floors or execution.

Palette files are presentation-only UTF-8 JSON, at most 64 KiB:

```json
{"Version":1,"LayerColors":{"encounters":"#9238b1"}}
```

Keys must name declared semantic layers; colors require six hexadecimal digits.
Unknown versions, extra top-level fields, unknown IDs and unsafe colors refuse
before replacing the current palette. Palette changes affect ordinary node base
fills and logical-link strokes. Eligibility/failure footprints and markers,
capacity colors and terrain ramps retain their diagnostic meaning. Save/load,
reset and a new successful world load never rewrite source world metadata.
SVG metadata records `Palette`, `SemanticFilter` and renderer `LayerView`; all
source shapes/labels/links stay in the SVG, including explicitly hidden members.
Current DOM filtering is independent of generation and introduces no layout or
world-design algorithm. [Qualification](evidence/2026-10-30-semantic-inspection-verification.json).

## Schematic layer availability and detail

Each loaded world enables only layers with rendered content. Unavailable layers
are unchecked/disabled with an explanation; loading another world restores its
available defaults. Geometry-free worlds show logical connections immediately.
Labels are prioritized by selected item and hierarchy, culled for screen overlap,
and revealed as zoom/focus allows. No node or physical source geometry is removed.
The label count reports visible/eligible labels; every node remains selectable.
Destination markers are capped at five screen pixels. Full SVG export restores
source label visibility, font sizes and marker radii rather than exporting the
screen-detail filter. [Current browser/export evidence](evidence/2026-10-30-schematic-capabilities-verification.json).

Physical `engine-*` coordinate views share the same meter-scale zoom bound,
including a focused `engine-surface` footprint. Geometry-free diagrams keep their
own diagram-unit zoom bound. The inspection-layer selector and layer-opacity
slider have stable accessible names. Live review of a complete 1,019-node packet
covers three domains and actual SVG output with all 1,014 source footprints and
128 directed routes preserved. This is technical inspection rather than owner
world-design acceptance. [Evidence](evidence/2026-10-30-browser-inspection-verification.json).

## Intrinsic nested source fit and resumable hierarchy

The placement owner resolves source polygons/reservations/site features once per
invocation and reuses them across provisional parent poses. Before search, a
nested source envelope is projected against the parent/local boundary on axis
and edge-normal directions. If a required width exceeds the available width
beyond conservative containment/operand uncertainty, no translation can repair
it: `METADATA_PLACEMENT_UNRESOLVED:NESTED_SOURCE_ENVELOPE_TOO_LARGE` attributes the
node, parent and source rule. Fixed rotations and reservations participate.

This is a necessary impossibility test, not a fit certificate. Uncertain or
non-rejected shapes still pass all ordinary full geometry, terrain, access and
nested checks. No dimension/count/limit changes occur. A pose-invariant failure
does not generate source-pose nogoods; higher design/composition owners can
reconsider declared program alternatives. Existing accepted sessions remain
unchanged on refusal.

The shared placement search yields provisional assignments with detached site,
diagnostic and conflict snapshots. Nested stages use an explicit controller on
the heap; descendant or final feedback rejection resumes the owning search.
Suspended enumerators are disposed on completion, exhaustion and exceptions.
Original stage/state/work limits and complete programs remain unchanged.
All 85/169/493/577-node scale programs now generate/reload, including 128 child
stages in the largest. [Current evidence](evidence/2026-10-30-heap-stages-verification.json).
The previously recorded process-stack defect is resolved; broader core/design
qualification remains open.

## Provisional structure-to-structure relations

Ordinary search now uses the same analytic distance owner when a relation endpoint
is a structure whose position is selected during search. Links work in either
declared direction. Fixed zero pins resolve first; other domains are enriched from
current known neighbors and checked against current relations, nogoods and
separation before selection. A zero-size current domain waits when an unassigned
neighbor can still supply candidates. It does not fail merely because its source
has not been positioned yet.

Each frame enumerates only base candidates plus constructions from that frame's
related source positions and authored anchors. Unrelated provisional structures
do not add directions. Geometry qualification is cached by demand/position under
the fixed environment/profile/prior-stage context; cached candidates from other
branches are never enumerated. Current relationships, nogoods and spacing are
rechecked against the current frame. Preference ranking is finite search, not a
global optimization guarantee. Diagnostic/conflict counts include actual domain
qualification and lookahead work.

The existing candidate limit counts unique admitted points across the entire
search, including the base pool. It is not reset per branch. The existing 4096
qualified-option cap, search-state limit and work budget remain unchanged.
Construction, cache lookup/miss evaluation and compatibility checks are charged.
No random angle sweep, grid, count reduction, source shrinking or legacy fallback
is introduced. Prescribed and connected-growth construction keep their own inputs.

A bounding-box diagonal proof rejects separation greater than any possible
inter-envelope distance in the declared domain. It includes the polygon owner's
existing boundary tolerance plus operand roundoff; this is an early impossibility
check, not a relaxed clearance rule. It prevents an impossible 500m-separation
request in an 80m-scale domain from consuming branch candidates before refusal.

[Four examples](../examples/metadata/provisional-relations/README.md) qualify both
provisional sources, reverse links, a truly empty initial domain, source movement,
repeated parent frames, exact budgets, replay and successful/failed edits.
[Verification](evidence/2026-10-30-provisional-relations-verification.json) includes
independent catalog expansion, source polygons, parent containment, terrain and
all declared distance/separation checks. Finite candidate search does not prove
continuous feasibility, complete coverage of every coupled grammar, global
optimality or a finished professional generator. Godot remains deferred.

## Analytic fixed-anchor relation candidates

Ordinary `WorldPlacementExecution.Search` now supplements terrain/source-contact
candidates with declared fixed-anchor distance intent. For each demand's anchored
relations, the C# placement owner derives distinct nonzero minimum/ideal/maximum
circles, their pairwise intersections, intersections with semantic boundary
segments, and radial directions toward semantic vertices and authored anchors.
These are finite constraint-derived candidates. No lattice, angle sweep, jitter,
source resizing or count reduction is introduced. Generated positions retain
`relation:<id>:<radius-role>:<construction>` provenance.

Semantic edges come from the placement/surface boundary, terrain patches,
exclusions and selected region/allocation boundaries. Internal triangulation
edges do not introduce this candidate family. Candidate deduplication and the
existing `MaximumCandidates`, work, option and search-state ceilings remain
in force. Out-of-range/out-of-domain-bounds analytic positions do not enter the
candidate pool. Every retained candidate still passes the common point,
whole-footprint, reservation, region, access, relation and dependent-stage owners.
Searchable layouts can therefore change when a new candidate scores better;
this is a current-algorithm replacement, with no compatibility placement path.
Prescribed placement and connected-growth construction retain their own inputs.

Distance comparisons allow arithmetic roundoff only: four times the sum of the
ULPs of both point coordinates, computed distance and declared bounds. ULP means
the gap to the next representable positive double at that magnitude. This scales
with coordinate precision instead of a gameplay distance. Exact zero-distance
pins remain exact. When the allowance is as large as a positive bound, that
bound uses the measured distance strictly; it cannot erase an unresolved tiny
radius or equate distinct representable poses. Analytic tangencies use similarly operand-scaled arithmetic
checks; accepted positions still pass the distance owner. A measurable 0.0001m
violation refuses, as do contradictory zero pins one representable step apart.

The analytic reference places both required source-sized catalog members at
`(3,-4)` and `(3,4)`, each 5m from authored anchors `(0,0)` and `(6,0)`. Moving the
second anchor to `(8,0)` produces `(4,-3)` and `(4,3)`, without editing source
sizes or counts. Rotation, translations up to nine million coordinate units,
external/internal tangencies, narrow bands, semantic boundary contacts,
minimum/maximum selection, terrain/reservation refusal, budget boundaries,
file replay and successful/failed edits are qualified in
[the verification packet](evidence/2026-10-30-relation-candidates-verification.json).

This fixed-anchor construction is also used for provisional structure relations
as described above. It does not solve continuous packing or qualify every
gameplay/terrain matrix. The complete density references retain all members and
38 directed paths, but each now needs six provisional source layouts and about
45–47M placement-work units instead of three layouts and about 25–26M. All stay
within their original 200M ceiling; no performance improvement is claimed.

## Coordinated source commitment

`PopulationSource` now keeps source placement provisional until the consumer's
complete placement passes. A failed sibling-spacing check retains a bounded
`WorldPlacementSeparationWitness` with both exact envelopes and node identities.
For a source root-stage member conflicting with a root-level area-population
member, the engine can add an attributed `MetadataPlacementNogood` for that exact
source pose and reconsider source placement. Terrain, environment, region,
allocation, root and source-envelope dependencies must remain identical.
Prescribed centroids, source footprints, counts and required clearance stay fixed.

A stricter consumer spacing rule between two source members instead
produces a joint two-pose nogood. Either pose remains eligible when paired with a
different pose. Generated guard identities avoid existing authored guard IDs;
authored constraints are retained. The same dependency, work, attempt and replay
owners apply. The source-pair reference keeps its preferred first pose at
`(10,5)`, replaces the incompatible second pose `(7,5)` with `(1,1)`, and retains
both 2×2 sources with the requested 2m consumer clearance. This is selection from
finite purpose-derived candidates, not nearest continuous packing.

Generated clauses now use `MetadataPlacementDefinition.Nogoods`, whose
`MetadataNodePose` assignments identify allocated nodes across instantiated
stages. Incomplete matches stay eligible; complete matches reject at the
candidate/stage owner before descendants are committed. This supports nested
siblings and authored clauses spanning a parent and a child without rewriting
the reusable program. Unknown/unbound nodes, duplicate identities and invalid
sizes/coordinates refuse before search. All checks use the existing metadata
placement budget. Ordinary request-level `WorldPlacementNogood` remains the
local demand-based constraint API; generated coordination no longer injects
clauses there.

`PopulationCoordinationPlan` is version 3. Node-pose clauses cover attributed
spacing conflicts; whole-assignment clauses cover complete consumer infeasibility.
Older generated receipts must be regenerated; there is no compatibility execution path. Two repeated-parent
references retain every parent and preferred sibling while satisfying stricter
child spacing. Program declarations and all instantiated members stay exact.

The shared placement owner supports finite partial-assignment nogoods, including
multi-pose combinations. A partial match does not ban an individual pose. Guards
are snapshotted and canonicalized, limited to 128 declarations with at most 16
unique demand poses each, and charged to the existing search/work budgets.
Consumer-derived guards carry the original consumer recipe digest.

`PopulationCoordinationPlan` records the original full recipe/catalog,
dependencies, each rejected source input/world identity, its added constraint,
the accepted attempt and accounted placement work. The existing consumer
`MaximumFeasibilityAttempts` and `MaximumWork` govern reconsideration; receipt
size is bounded at 8 MiB. Accounting covers source and consumer placement-owner
work across attempts; other owners retain their own declared budgets. No global
CPU-time bound or exhaustive world solver is implied.

Generation sessions isolate provisional stage caches and adopt only the accepted
one. Failed attempts do not leak accepted stage entries. The terminal chunk stage
is recomputed with the accepted coordination receipt, preserving source digests
and subsequent reuse. Typed reads replay the original full input; missing or
tampered proof refuses. JavaScript checks saved structure and geometry while C#
authenticates and replays derivations.

When a consumer's complete placement is infeasible, the coordinator can reject
exactly the complete allocated source assignment. `WholeAssignmentSha256` is the
SHA-256 of UTF-8 JSON of `{NodeId, Position}` members sorted by ordinal node ID,
using the engine JSON options. Such a clause has an empty `Assignments` list;
a node-pose clause has 1–16 assignments and no whole digest. Mixed/invalid forms
refuse before snapshotting. The 128-clause ceiling remains unchanged. A complete
digest covers every allocated member, including more than sixteen members and
repeated nested stages. It is checked only after all stages have assignments.
Partial matches remain eligible; digest construction and comparisons are charged
to the existing placement budget.

This fallback covers route-dependent population, exact sockets and other typed
consumer placement infeasibility. Invalid inputs, work/attempt exhaustion and
cancellation never become layout constraints. Every source attempt must keep
terrain, regions/layouts, source footprints, non-position node metadata, relative
offsets, access definitions, route/network rules and authored connections fixed.
Source poses and derived routes may change provisionally. Once accepted, the
consumer must preserve the complete selected source and its paths exactly.
The last consumer failure remains available if no source arrangement survives.

This is finite bounded search over the existing purpose-derived candidates.
A feasible continuous arrangement outside those candidates is not guaranteed;
this does not yet qualify every terrain/domain/gameplay combination.

The baseline/richer-density references now retain 248/270 nodes, all 219/241
required population members, exact source sizes and all nineteen source
connections. Each uses three provisional source layouts within the original
200M planning ceiling. [Current verification and limits](evidence/2026-10-30-complete-source-coordination-verification.json).

## Source-contact placement candidates

Ordinary `WorldPlacementExecution.Search` without a connected-growth grammar now
includes source-contact candidates alongside terrain representatives, intent
anchors and regional features. For each distinct resolved source envelope
(reservation when declared, otherwise footprint), the shared C# owner translates
each source vertex onto declared surface, patch, exclusion, region and allocation
boundary vertices. Internal tessellation edges are not contact features. Each
candidate retains feature, demand and envelope-vertex provenance.

All candidates still pass point and whole-envelope suitability, union containment,
separation, access and dependent-stage checks. Existing global unique-candidate,
retained-option, search-state and shared work limits apply; exhaustion refuses.
Prescribed positions and connected-growth semantics are unchanged. This finite
vertex-contact family is not complete continuous packing or an optimal solver;
edge contacts, rotations and coupled future-stage choices are not implied.
Changed search/work derivations require regenerating saved search packets.

The original six-compound source qualifies with 29 nodes, 28 physical members
and nineteen bidirectional routes. Its former full-population spacing failure
is resolved by coordinated source commitment above; the fixed area centroid,
required count and one-meter spacing remain intact.
[Source example](../examples/metadata/source-contacts/README.md) and
[versioned verification](evidence/2026-10-29-source-contacts-verification.json).

## Optional automatic wind derivation

Recipe `WindMoisture` requests the existing directional moisture grammar after
the effective surface is available. `wind-moisture` depends on `surface` or
`water-surface`; `environment-input` combines authored expressions/scalar inputs
with generated fields. Existing region, placement, suitability, route and chunk
consumers receive that effective environment. Original recipe data remains
unchanged for hashing and checkpoint replay.

`WindMoisturePlan` retains authored environment plus the core source/control/field
receipt; `EnvironmentPlan.WindMoistureId` binds it to the effective definition.
World and standalone reads replay the receipt. Publication additionally binds the
receipt to the supplied original recipe and writes `wind-moisture-plan.json`.
Population compilation preserves existing wind authority. The capability is
optional and does not impose terrain or atmosphere on other worlds.
See [API/example](../examples/metadata/wind/README.md) and
[verification](evidence/2026-10-29-wind-metadata-verification.json).

## Optional sampled scalar inputs

Environmental expressions may select `WorldFieldInput.SampledField` and a named
`WorldSampledScalarField` in `Environment.SampledFields`. Typed values, units and
source provenance remain explicit; frames must match the terrain heightfield.
The shared triangle interpolation supplies values and gradients to the existing
field DAG, clamps, footprint/region constraints, density and site placement.
Different scalar frames and uncovered queries refuse instead of using sparse
probes or fabricated zeroes. Empty sampled input lists impose no new capability.

The optional `WorldWindMoisturePlanner` derives humidity/rainfall through its C#
API and replayable standalone plan. Metadata may consume scalar snapshots as
explicit authored inputs or request the automatic recipe stages above. See
[API, controls and limits](../examples/terrain/environment/README.md)
and [current evidence](evidence/2026-10-29-environment-wind-verification.json).

## Optional occupied water

`Water.RetainedBasins` optionally derives fixed-level occupied bodies from
declared retained drainage terminals. Shared strictly positive-depth terrain
regions preserve disconnected depressions, islands and zero-depth seams.
Original bed planes/depth/capacity receipts remain separate from flat water
surface patches. Default terrain-shoreline policy refuses open/unresolved
boundaries; explicit clipping remains marked. Levels do not imply dynamic flow
or storage simulation. Lake and stream patches share validation, metadata/session
dependency closure and ordinary output limits. See [retained-water API/example](../examples/metadata/retained-water/README.md)
and [evidence](evidence/2026-10-29-retained-water-verification.json).

Recipe `Water` requires `Drainage` and stores `WaterPlan`. The base `surface`
stage remains original terrain for drainage. `water` derives occupied patches;
`water-surface` supplies the effective surface to every terrain consumer. Width,
threshold and source edits invalidate that dependency closure while unrelated
content can remain reused. Without water, existing consumers use `surface`.

`ValidateDocument` replays original drainage and water, checks the metadata frame
and compares complete effective-surface composition. Existing population source
water must remain unchanged. Atomic export adds `water-plan.json` and summary
counts; standalone plans require their drainage context for explicit validation.
Schematic blue patches/tooltips and bounded witness checks display saved engine
geometry; the renderer does not replace numerical/digest replay in C#.

See [the recipe/session example](../examples/metadata/water/README.md) for actual
checks and the qualified larger physical-world reference. The feature is
optional and bounded; it does not certify broad game-world design, climate,
lakes, production art or native runtime.

## Optional sampled drainage

Recipe `Drainage` uses the shared terrain planner and stores `DrainagePlan` in the
world. Its stage depends on the canonical surface; no stage is added when absent.
A source/outlet edit recomputes drainage, then any chunk index. A terrain edit
invalidates surface dependents. Drainage alone does not alter placement, routes or scalar fields.
Optional `Water` derives occupied geometry consumed by downstream terrain stages. Existing source drainage survives population
compilation unchanged. Terrain and drainage remain shared chunk dependencies.

`ValidateDocument` checks the current surface/frame and replays stored drainage.
The atomic packet includes `drainage-plan.json` plus explicit rate units/totals in
`summary.json`. `ReadAsync<WorldDrainagePlan>` replays standalone files; plain JSON
deserialization alone does not validate them. The shared bounded JSON transport
keeps each file at or below 16MiB and preserves every sample in larger generated
plans through verified parts. Authored logical inputs remain capped at16MiB;
reconstructed generated plans use the separate256MiB transport bound.
See [the recipe/session example](../examples/metadata/drainage/README.md) for
actual edit/rollback/restore qualification and current limits. Version-1 recipes
and version-2 documents use optional fields; cache/checkpoint implementation
identity continues to pin the current algorithm without a historical fallback.

## Validation of adapted worlds

Composite document validation shares a freshly computed placement replay from
local-layout adaptation with the placement validator in the same call. The
adaptation validator still compares the complete search history, accepted layout,
placement and positioned nodes. The placement validator still checks its feedback
against the document's current routes, networks, access, services, context,
corridors and interiors, then compares the expected placement and positions.
Standalone public validators continue to perform their own replay.

The internal result is single-use and bound to the exact document and registry
instances, complete document hash, and freshly authenticated registered structure
policies and their reproducibility identities. Changes reject the result.
Unversioned providers keep full placement replay. No stored JSON field, receipt,
checkpoint or persisted cache grants this authority; public contracts and world
output remain unchanged. This reduces duplicate work at a validation boundary;
it does not remove source-world or downstream validation or establish large-world
throughput. Godot remains deferred.

## Connected recursive area partitions

`WorldAreaPopulationPattern.PartitionSearch` optionally declares a
`WorldAreaPartitionSearch(AxisOffsetsDegrees, MaximumAttempts, Seed, Bend, AlternateBends, BendOffset, AlternateBendOffsets)`. With no
seed, the shared area owner tries offsets in declaration order relative to each
recursive principal direction and the pattern rotation. An optional nonblank
seed of at most 256 characters deterministically orders the declared offsets
for each split; it does not add angles or guarantee a distinct or feasible
layout. Explicitly pinned partition choices remain fixed. Seed ordering consumes
the declared work budget. It solves the existing weighted
mass target, requires both children connected, then recurses. A later semantic
failure restores provisional leaves/cell accounting before the next candidate.
Input, work, cancellation and numerical-resolution failures do not become retries.
No source pieces or required members are omitted to obtain connectivity.

`Bend` is an optional dimensionless slope from -1 to 1, defaulting to zero.
For each split the boundary has two straight arms meeting at a position along
the tangent to the allocation's mass centroid; the signed slope determines their bend relative
to the selected axis. The mass solver splits source pieces at that tangent and
integrates each arm against the actual density, exclusions and domain. For
example, `new WorldAreaPartitionSearch([0,45,90], Seed:"world-1", Bend:.15)`
requests bounded bent cuts with seed-selected axes. This produces piecewise
linear boundaries, not smooth splines or automatic geographic design. Invalid
or infeasible geometry and exhausted budgets refuse; no content is resized.

`BendOffset` optionally moves the arms' meeting point along that split's signed
tangent, in the surface's coordinate units. Zero leaves it aligned with the mass
centroid; positive and negative values produce different asymmetric boundaries.
The same offset applies to each recursive split in that policy, relative to its
own centroid and selected axis. It must be finite and within ±10,000,000; a
nonzero offset requires at least one nonzero declared bend. The actual cut is
still solved from the requested area/density, terrain, connectedness and work
constraints. It is a finite authoring control, not an inferred coastline or road.

`AlternateBendOffsets` may add one to eight distinct finite positions within the
same bounds. The search considers the declared axis, bend and position
combinations, charges the existing attempt and work budgets, and records the
actual `BendOffset` on each attempt and selected physical-feedback partition
choice. Straight cuts consider only the primary position because changing that
position does not change a straight line. A seed deterministically orders the
declared combinations. Rejected attempts remain attributed; exhaustion does not
mean no continuous solution exists. The primary and alternate positions require
at least one nonzero declared bend.

`AlternateBends` optionally declares one to eight additional distinct slopes
in the same range. For example, `new WorldAreaPartitionSearch([0], Bend:1,
AlternateBends:[.5])` lets the area owner try the alternate if the primary cut
cannot form connected required regions. Each recursive split chooses among the
declared axis/bend pairs; a seed deterministically orders those pairs. The same
`MaximumAttempts` and work bounds apply across them. Every attempted and
selected bend is recorded, and regional physical feedback can reconsider a
declared bend when a complete candidate fails its physical constraints.

The policy accepts 1..16 distinct axes modulo 180 degrees, each offset within
-180..180, and 1..4096 total internal-cut attempts. This is a finite recursive
partition grammar, not proof of a globally feasible/optimal connected division.
Without the declaration, the existing single-direction behavior remains current.

`WorldAreaPopulationPlan.PartitionAttempts` records each ordinal, member range,
depth, offset, canonical world angle, origin/relative cut, target/child masses,
status/rejection and subtree work. `Selected` records form exactly the final
`count-1` internal nodes; `Superseded` records are locally successful subtrees
withdrawn by ancestor reconsideration. Work is inclusive for each attempted
subtree and must not be summed across attempts; the plan owns aggregate work.
Unresolved exceptions retain `AreaPartitionAttempts`, `AreaRetainedStrata` and
`AreaRetainedCells`. Failed outputs are not published.

Population phases must preserve their accepted source layout. The adaptation
owner validates source scope/replay first and rejects differing layout candidates
before attempting to repin fixed source nodes. A caller cannot move or redesign
a source compound merely because new landscape geometry is being generated.

[Framework examples](../examples/metadata/connected-area/README.md) and
[evidence](evidence/2026-10-29-connected-area-verification.json) cover C-shaped
support, rotations/translation, unequal masses, rollback, full steep worlds,
density edits, CLI, current JavaScript and saved-source replay. Whole-generator
expressive range, maximum scale and owner design acceptance remain open; Godot
is deferred.

## Explicit interior anchors and area representatives

`MetadataAllocationAnchor.Interior` optionally declares a
`WorldRegionInteriorSearch(Precision, MaximumCells, MinimumClearance)`. The shared
region search returns a point maximizing distance from the actual allocation's
exterior/hole edges within the declared precision and cell/work bounds. Source
footprints, reservations, hierarchy, access and paths still must pass their exact
checks. Default allocation bindings keep the program anchor; this policy is not
silently enabled when placement fails. It is a purposeful layout rule, not vacant
space scattering. A circle-clearance preference does not prove an arbitrary
source polygon fits; the physical planner owns that acceptance.

`WorldAreaPopulationPattern.Interior` uses the same policy for a connected mass
stratum's representative. `MassCentroid` preserves the real integrated centroid,
which may lie in a hole; `Position` and `Interior` identify the valid representative
and its clearance/upper-bound/evaluation witness. Counts, all cells, density/mass
and source geometry stay intact. Disconnected support, inadequate clearance,
invalid policy, finite budget or cancellation still refuse. Without this explicit
policy, centroid semantics and existing refusals remain unchanged.

Policies require precision 1e-6..100000, cells 1..1000000 and minimum clearance
0..10000000 in world units. Precision bounds the shared search, not universal
polygon-placement feasibility. On unresolved layout adaptation, exception data
`LayoutAttempts` and `LayoutWork` preserve complete bounded rejection history.
The CLI still reports failure without publishing a successful world.

[Examples](../examples/metadata/interior-anchors/README.md) and
[verification](evidence/2026-10-29-interior-anchors-verification.json) distinguish
uniform/weighted source programs, a holed-area analytic oracle, and the complete
seven-compound steep source. The connected-partition qualification now extends
this bounded case through landscape population; wider world generation and
Godot remain open/deferred.

## Bounded required-space orientation

`MetadataLayoutDialKind.AreaRotation` authorizes changes to an `AreaBalanced`
layout's `AreaPattern.AxisRotationDegrees`. It uses the current `LayoutAdaptation`
owner and joint scopes, signed step ordering, aggregate work and replay history.
The complete range must stay within -180..180 degrees. `ProgramId` and `CutId`
must be absent. All program IDs, purposes, source rules, minima and density/pattern
settings remain fixed. This rotates partition guidance; it does not rotate source
structures or silently change recipes that omit adaptation. The source attempt
is tried first, and infeasibility/work/cancellation still fail explicitly.

[Orientation examples](../examples/metadata/area-adaptation/README.md) retain seven
metadata nodes, two exact physical sites and their required physical interface
route. [Generated terrain examples](../examples/metadata/layered-landforms/README.md)
exercise seven complete compounds on the full shared heightfield, without a
planar-patch replacement. Conservative geometry broad phases reduce comparison
work while retaining exact overlap/topology predicates. [Evidence](evidence/2026-10-29-area-landforms-verification.json)
records bounds and counterexamples; arbitrary terrain/whole-world design remains
unqualified and Godot remains deferred.

## Complete layered reference input

[The layered-world inputs](../examples/metadata/layered-world/README.md) combine
terrain-selected regions, generated required spaces, full nested physical content,
local/regional paths, terrain-demand population and storage ownership. They use
current reusable APIs without a city-specific runtime or an image prerequisite.
The inland/coastal pair preserves every mandatory part; source constituent
coordinates remain authored catalog design. [Measured evidence](evidence/2026-10-29-layered-world-verification.json)
qualifies this integration only. Declared PartialScope, open design/scale gates and
Godot deferral are retained.


## Physical feedback across program and region choices

With `ProgramComposition`, the existing cursor selects all encountered catalog
mixes, then a complete region-intent assignment. Typed downstream physical failure
advances regional assignments before an earlier catalog decision changes. Region-
only composition works with fixed catalog counts and zero `MixWork`. Selection
is the first complete configuration in this authored preference order, not an
aggregate world-price or geometric optimum. No source sizes, required counts,
profile/minimum constraints, fixed seeds or protected anchors are weakened.

`MaximumRegionAlternatives` defaults to 32 (hard 64). `MaximumRegionWork` defaults
to 100 million (hard 200 million) for cumulative regional-choice proof work,
including unsuccessful exhaustion and charged cached selection. Each region's own
work/evaluation/state/candidate limits still apply. Downstream owners keep their
existing per-attempt budgets. Both decision families share composition attempt,
selection-request, cache-byte and receipt-byte limits. Budget, numerical, ambiguous
precision, cancellation and malformed-source failures abort instead of advancing.

`MetadataProgramCompositionAttempt.RegionSelection` retains the chosen regional
plan for that attempt; an exhausted regional decision has no selected plan and an
explicit rejection. `RegionWork` records cumulative selection proof work, while
`MixWork` remains catalog-mix work. Region `ExcludedAssignments` are earlier
component vectors in ordinal intent-ID order. Primitive region queries can use
them to enumerate the next complete assignment; metadata recipes must not author
them (`METADATA_REGION_EXCLUSIONS_INTERNAL`). Generated plans with exclusions
require the complete composition receipt. Original recipes remain unrefined.
Read/export regenerate the full search, including geometry and rejected attempts.

Protected global local-layout anchors are never moved to repair a region choice.
Static definitions are validated first. A globally eligible anchor outside the
currently selected region becomes a typed regional-context conflict through the
metadata layout adapter. Context-dependent partition outcomes and fully exhausted
geometric adaptation can also reject the current configuration; invalid definitions,
unresolved numerical geometry and resource limits remain terminal. Adaptation
dials keep their original owner and source/neighbor protections. Source-history
edits, rollback and chunk ownership use the existing boundaries.

See `examples/metadata/region-feedback` and
[local evidence](evidence/2026-10-29-region-feedback-verification.json).

## Terrain-derived required regions and placement anchors

Optional recipe `RegionDemands` derives region intent minima from the actual
expanded root placement program. `MetadataRegionDemandDefinition` contains version1,
ID/purpose/source,1..32 rules and a preparation `MaximumWork` (default1,000,000;
allowed1..20,000,000). Each `MetadataRegionDemandRule` names an existing intent's
`RegionId` and a finite `MaximumCoverage` in(0,1]. Rule IDs and region IDs must be
unique. At least one corresponding expanded root source is required per rule.

The existing placement owner expands root part bindings and resolves source
footprints, rotations, reservations and declared site features. Each actual root
member is counted once. Nested members are contained by root programs and are
qualified later by the existing complete placement/access/route owners; their areas
are not added again. Envelope area uses the source reservation when present,
otherwise the source polygon. Required intent area is the larger of its authored
minimum and summed envelope area/coverage. Required anchor clearance is the larger
of its authored minimum and the maximum distance from any selected envelope vertex
to that envelope's actual local origin. Offset geometry is not silently recentered.

`RegionDemandPlan` retains original/derived requests, normalized rules, complete
resolved sources, measured areas/radii, derived bounds and actual preparation work.
`RegionDemandId` binds its presence. Preparation has its own finite ledger; existing
region geometry work limits are unchanged. With `ProgramComposition`, actual
preparation work also charges the cumulative regional-work ledger. Content-derived
region refusal can advance the complete program choice; invalid input and exhausted
budgets remain terminal. New content-dependent composition attempts record
`RegionRequested`, including requests that failed before a region could be selected.
Original requests without `RegionDemands` retain their existing receipt shape.

The `region-demands` execution stage depends on expanded content, and `regions`
depends on its measured output. Source-size/count changes therefore invalidate
region selection through the existing session/cache owners. Read/export remeasures
sources and rederives the complete region plan. JS checks source bindings, geometry
measurements and derived bounds, while native replay owns the execution ledger.
The schematic exposes the source requirement for each selected root.

These are conservative source-capacity requirements. Circular clearance can reject
a shape that another layout grammar could place. They do not certify packing,
complete occupied/open ground, generation ownership or arbitrary game-world design.
See `examples/metadata/catalog-region-demand` and its qualification record.

`WorldRegionRequest.Intents` supplies named game-authored roles with purpose/source,
minimum area, minimum interior clearance and optional suitability profile. Explicit
`Seeds` remain protected claims. At most 32 seeds plus intents share the existing
terrain/field atomic partition and positive-edge component topology. By default,
profile preferences do not alter region geometry or the selection objective.
An intent may set `PreferSuitableAnchor` to rank its eligible component anchors
by the profile's point preference score before clearance. Each component tests its
maximum-clearance point, each constituent cell's vertex-average point, and
midpoints from that point toward its vertices, subject to the existing
anchor-cell and work budgets, hard eligibility and clearance.
Ties favor clearance then coordinates. The witness records `AnchorPreferenceScore`.
This finite candidate set is not a continuous optimum or an area-average score;
it does not reshape a region, and downstream physical placement/routes must still
qualify. A preference must be declared; this option cannot be combined with a
required-position pin. `MetadataRegionalProgramRegion` and explicit regional
design policies expose the same option. With `IntentPartition`, preference
selection occurs inside each finished partition, before final distance and
shared-boundary relationships are checked. Intents may use different soft
preferences when their profile identity and hard suitability rules are otherwise
identical. If a declared relationship rejects the current stratum's preferred
anchor, its other qualified candidates are tried in score order before changing
the partition; `AnchorPreferenceRank` records the zero-based chosen candidate.
When later relationship checks exhaust their candidates, the search may revisit
earlier preferred anchors as well. The finite candidates and partition choices
use the existing work, candidate and attempt budgets; budget exhaustion is not
reported as infeasibility. The first valid result is not a global optimum. By default,
intents choose complete suitable components, preserving holes/concavities and
exclusive positive-area ownership. Optional `IntentPartition` instead permits
several required region roles within one connected suitable component, as below.

An optional `WorldRegionIntent.RequiredPosition` fixes the exact 2D anchor while
retaining normal suitable-component assignment. Its candidate witnesses evaluate
the original boundary/hole edges at that point. `UpperBound` then equals attained
clearance for the constrained point, not a claim about the component's maximum.
Other components retain `required-position-outside` witnesses, including signed
nonpositive clearance. Eligible selections require positive clearance and the
declared minimum. Automatic and pinned intents may coexist; their anchor work is
accounted separately. Area-partitioned intents also support these exact anchors.
Each assigned component and final connected stratum measures clearance at the
chosen point, rather than substituting a maximum-clearance center. The point must
be strictly interior, suitable and positive in any declared density field. Its
leaf must meet both the intent's minimum clearance and the pattern's `Interior`
minimum. Declared partition cut alternatives can revise surrounding regions when
an initial split excludes a pin. Failed leaves retain
`WORLD_AREA_REQUIRED_POSITION_OUTSIDE`, `WORLD_AREA_REQUIRED_POSITION_ZERO_DENSITY`
or ordinary clearance witnesses. Finite cut/component exhaustion remains explicit;
the algorithm does not move pins or promise continuous partition completeness.
Fixed `Seeds` retain their existing whole-component ownership meaning.
Within declared `PartitionSearch`, pinned groups may use either directed normal
of an allowed axis. Every direction counts against the existing attempt/work
limits and retains its own cut receipt; unpinned groups keep canonical direction
ordering. A policy without cut search retains its single canonical split.
See the [complete fixed-destination example](../examples/metadata/protected-region-partitions/README.md).

The owner finds an interior anchor using signed distance to actual exterior/hole
edges and bounded priority subdivision. These computational rectangles are not
gameplay blocks or tiles. `WorldRegionInteriorAnchor` retains position, attained
clearance, a remaining global upper bound and evaluation count. The upper/lower
gap is at most `AnchorPrecision`, subject to existing finite geometry arithmetic.
Only attained clearance may satisfy a minimum. If the minimum lies between the
attained value and unresolved upper bound, generation refuses with
`WORLD_REGION_INTENT_CLEARANCE_PRECISION` instead of accepting or declaring a false
infeasibility. Refine precision within declared work limits when that distinction
is required.

Candidate witnesses rank by attained clearance, area, anchor X/Y and canonical
component index. Required intents are assigned in ordinal ID order, with bounded
backtracking to avoid consuming a later role's only compatible component. This is
the first complete assignment in that preference order, not a joint geometric
optimum. Without `ProgramComposition`, later physical rejection refuses the chosen
region. With composition, the bounded joint cursor above can reconsider component
assignments. `IntentPlan` records candidates, selections, total anchor evaluations and
assignment states; selected `WorldRegion.Seed` positions come from the engine while
`Request.Seeds` retains only authored points. Read/export rederive the complete plan.

Defaults: `AnchorPrecision=0.1` (allowed 1e-6..1e5),
`MaximumAnchorCells=65536` (hard 1 million across components),
`MaximumIntentCandidates=1024` (hard 4096 references),
`MaximumIntentStates=100000` (hard 1 million). Existing cell/profile and aggregate
work limits remain; exhausted limits are terminal and cannot drop roles.

Optional `WorldRegionIntentPartition(Pattern,MaximumAssignments=64)` delegates
shared-component subdivision to `WorldRegionIntentPartitionPlanner` and the common
connected area kernel. It requires at least one intent and one normalized effective
suitability profile, including protected seeds. Mixed-profile requests refuse with
`WORLD_REGION_PARTITION_SINGLE_PROFILE_REQUIRED`; absent policy retains complete-
component allocation. Fixed seed components remain protected. Component assignment
uses canonical intent order, candidate ranking above and summed minimum area.
Each occupied component is fully partitioned using each selected intent's optional
positive `AllocationWeight` (maximum `4e14`); an omitted weight uses that
intent's minimum area. The weights express relative mass, which is relative
area only when no density field is declared. They do not relax minimum physical
area, clearance or downstream content requirements. A weight without an
`IntentPartition` is invalid. Weights compare only intents assigned to the same
component; unused suitable components remain unclaimed. An infeasible split
refuses rather than changing declared weights or omitting required content.

`Pattern` is the existing `WorldAreaPopulationPattern`, including optional density,
axis, partition-search and interior rules. Every leaf must meet its own minimum
physical area and clearance during split search. Without explicit `Interior`,
the region request's anchor precision and cell limit apply per stratum. Original
component-anchor evaluations remain separately counted in `IntentPlan.AnchorCells`.
The existing aggregate region work limit covers both searches. The total retained
output region cells must also meet `MaximumCells`.

`IntentPlan.Partitions` binds each source component to its exact selected intents
and saved `WorldAreaPopulationPlan`. `PartitionAttempts` retains assignment vectors,
accepted/failed partitions, semantic rejection and failed area-search receipts.
`MaximumAssignments` is1..4096; existing state, candidate, cell/depth, interior and
work limits remain hard. Component exclusions still exclude whole assignment
vectors. `RequiredAssignment` constrains this same canonical intent/component
vector during search; an unavailable forced assignment fails rather than being
silently replaced. Component indices belong to the current queried terrain.

Declared region relationships use the derived interior points, not the original
component anchor. As each candidate stratum becomes known, its relationships to
earlier strata and protected seeds are checked through the existing area qualifier.
A failing distance can reconsider declared cuts. A later occupied component can
also reject an earlier component's choice before it commits, so cross-component
relationships participate in bounded design. Rejected prefixes are discarded;
their points do not constrain a later alternative.

`WORLD_REGION_PARTITION_RELATION:<id>` identifies the unsatisfied relationship;
failed cut receipts retain `region-relation:<id>` or downstream component
qualification attribution. Each area solve retains its own declared cut-attempt
limit; assignment/state limits and the original shared work limit remain hard
across the composed search. Limits, malformed inputs and cancellation are terminal,
not geometric alternatives. Unconstrained requests retain their previous geometry
and work. This is bounded declared grammar feasibility, not a global layout optimum
or continuous infeasibility proof. Read/export rederives the full plan; JS consumes
saved exact geometry and binding receipts. The
[relationship-search example](../examples/metadata/region-relationship-search/README.md)
includes complete nested physical programs, source edits and explicit refusals.

See `examples/metadata/terrain-program-regions` for three seedless regions feeding
complete nested physical programs and a generated connecting network. Semantic
region subdivision does not itself derive generation ownership, streaming,
unrequested content or a complete playable world.

Optional `MetadataPlacementDefinition.RegionAnchors` maps an anchor ID to a region
ID with purpose/source. It compiles into the root request's actual anchors before
part/program placement, without an authored root point. Duplicate IDs or an
explicit anchor with the same ID reject; existing coordinates are never overwritten.
`TemplateDefinition` retains the authored binding, while compiled `Definition`
contains the point and clears `RegionAnchors`. Nested program anchors retain their
existing local-frame semantics. C# owns geometry/selection; JS checks saved
witnesses and exact binding positions. See `examples/metadata/region-intents` and
[local evidence](evidence/2026-10-29-region-intent-verification.json).

## Physical qualification of program composition

Optional recipe `ProgramComposition` supplies purpose/source and finite attempt,
per-mix alternative, cumulative selection and solver-work limits. The existing mix
owner ranks complete count vectors by exact decimal cost, instance count and
canonical count vector. The generator tries them in parent-first hierarchy order,
exhausting descendant/sibling combinations before advancing an earlier part.
The result is the first physical configuration in that declared order, not a
minimum aggregate cost across different world hierarchies.

Every attempt runs the same catalog/content/placement/access/route/constraint
pipeline with unchanged required quantities and source dimensions. Only typed
physical rejection advances the search. Malformed inputs, cancellation, solver
budgets and placement budgets abort immediately. All-infeasible enumeration refuses
publication. Default limits are 16 generation attempts, 32 alternatives per mix,
4,096 selection requests and 20 million cumulative solver work; hard limits are
64 attempts/alternatives and 16,384 selection requests. Count-vector comparisons,
including unsuccessful exhaustion proofs, charge shared work. `MaximumCacheBytes`
and `MaximumReceiptBytes` bound the ranked-choice cache and saved composition
receipt, each defaulting to the hard 8 MiB ceiling. Existing per-attempt limits and
publication/read limits continue to apply.

`ProgramCompositionId` requires its `ProgramCompositionPlan`. The plan snapshots
source recipe/catalog, actual registered dependency identities, ordered prefixes,
every selected mix and physical rejection, cumulative work and selection count.
`ExcludedCounts` on a mix request records earlier ranked vectors in ordinal offer
order; it is replayed by the solver and requires the composition receipt in world
documents. Read/export regenerate the search and compare the selected world and
attempts; chunk ownership is validated separately. Supply actual matching catalog
and layout registrations to `ValidateDocument`/`ReadAsync`/schematic export when
used. Serialized paths do not install or execute missing dependencies.

Composition uses one transactional session stage. Catalog/recipe edits invalidate
the stage; failed edits retain the accepted snapshot, and checkpoints retain source
history. JavaScript consumes saved receipts without running composition search.
See `examples/metadata/program-composition` and
[local evidence](evidence/2026-10-29-program-composition-verification.json).

## Intent-driven catalog program mix

Optional `MetadataPart.ProgramMix` selects whole reusable archetypes from authored
capacity requirements. `Offers` declare archetype IDs, positive decimal cost,
per-offer minimum/maximum count and optional registered `StructureCapacityId`.
`Demands` declare capacity ID/unit and quantity, optionally bound to an integer
recipe parameter. Supply uses catalog `Capacities`; optional `ProgramInputs`
consume per-copy capacity. Each capacity's provided total minus consumed total
must meet external demand, including prerequisite capacities with zero external
demand. This models static capacity balance, not time, startup or delivery.

One C# planner minimizes total decimal cost, then total instances, then the
lexicographically ascending count vector in ordinal archetype-ID order. It retains
the part minimum and all descendants. Program mix cannot share a part with
variation, capacity-derived count, parameter count or terrain-derived count.
Offers can have different kinds; child-kind requirements still apply. Registered
structures must map actual selected-variant capacity instead of shadowing it with
authored quantities. Footprints, access and layout remain mandatory downstream
when physically requested. Without `ProgramComposition`, failed physical selection
refuses generation. With it, the engine considers declared alternatives under the
same constraints; neither route drops content or changes source dimensions.

Bounds: 16 offers, 16 demands, 32 distinct capacities, 128 selected instances,
one million search states, 20 million work per solve and per assembled plan,
4,096 allocations. Defaults are stricter. Proof-budget exhaustion rejects even
when a feasible incumbent exists. Decimal conversion follows the existing capacity
owner. `ProgramMixPlan` retains requests, catalog facts, selected member IDs,
balances, costs and work receipts; direct selected nodes retain `ProgramMixKey`.
Read/export recomputes the optimum and membership. These saved facts are replay
provenance, not cryptographic catalog authentication. JS checks saved arithmetic
and membership only. See `examples/metadata/program-mix` and
[local evidence](evidence/2026-10-29-program-mix-verification.json).

## Generated room contacts and physical interiors

Necessary contact checks also run when a candidate is considered against already
selected current-stage sites. The shared solver accepts an internal constraint
predicate supplied by the metadata owner; it does not infer room semantics.
Search and prescribed execution retain the same final assignment validation.
Rejected contacts count as explicit conflicts and consume the existing shared
work budget. This avoids spending the candidate allowance on descendants of a
room pair that cannot provide its declared doorway.

Unrestricted search expansion remains bounded to16 demands per stage. Validated
connected growth supports128 demands; its graph-derived candidate domains are
distinct from the unrestricted terrain-candidate product. An interior rule's
2–32-room limit is separate. Source part expansion keeps its classification code
and adds `expanded`, `maximum`, `execution` and selected-member counts. Declaring
a grammar only provisionally admits expansion: invalid roots, disconnected links
and unlinked members without exact anchors still refuse. The unchanged24-room
rectangle/hexagon programs now generate with all fixtures and original budgets.
[Current consolidated evidence](evidence/2026-10-31-connected-capacity-verification.json)
supersedes the [earlier16-demand refusal evidence](evidence/2026-10-30-interior-size-shape-verification.json).

Placement uses the interior owner's same shared-boundary contact calculation for
already placed room pairs before entering descendant programs. Children cannot
repair a missing room contact. This is a necessary early check; final interior
assembly still checks ownership, all contacts, wall cuts, openings and swept
routes against complete contents. A finite candidate-set refusal does not prove
continuous geometric impossibility. The current two branching-plus-loop study
refusals retain their input and now report a root shared-boundary failure rather
than a descendant attempt budget. [Evidence](evidence/2026-10-30-interior-contact-verification.json).

Optional recipe `Interiors` selects actual direct-child room kinds and typed
connections inside named floor/container scopes. Existing topology and connected
placement owners generate room graphs and source-sized modules from one anchor;
boundary contact must be explicitly enabled. Catalog parts and nested programs
retain required equipment. No per-room or doorway coordinates are inferred by a
renderer or supplied by an agent after generation.

Each `MetadataInteriorRule` supplies purpose/source, scope, room/connection kinds,
opening width, inward wall thickness, approach depth, traversal profile and search
policy. The assembly owner requires convex reservation floors, disjoint room
interiors and a connected selected graph. Oppositely oriented shared edges define
contacts; the longest eligible contact receives a centered opening with retained
corner piers. Duplicate/overlapping openings reject. Remaining boundary spans
produce inward wall strips clipped by the shared convex half-plane utility.
Unselected adjacent rooms remain closed.

Optional `Entrances` (1–16 when present) binds each exact connection ID to a room
catalog port and optional outside endpoint port. Exactly one endpoint must be a
selected room; the other is an outside destination. The catalog owns the opening
center, normal outward approach and width capacity. Opening width cannot exceed
that capacity; inward approach depth is declared by the interior rule. Corner
piers remain. The exterior leg stays outside all declared room floors, while the
interior leg remains inside its exact room. The full crossing is qualified against
other rooms, walls, source solids and terrain. Coincident endpoint/approach points
do not create zero-length segments. Unbound ports do not cut openings.

`MetadataInteriorPlanner.WithWalls` adds generated wall polygons to a copy of the
base surface for shared travel consumers. Generation, placement feedback and
route/network/corridor replay use the same geometry. Interior assembly executes
before these consumers; changes invalidate each dependent stage. An interior
connection and a regional route cannot both own the same selected connection kind.
Named outside ports are honored; movement between different ports on one object
is not inferred. Logical progression alone does not establish physical continuity.
The [exterior/interior examples](../examples/metadata/exterior-interior-travel/README.md)
independently qualify every joined endpoint and full-width leg.

The common route owner finds room-center → approach paths constrained to the
exact floor and avoiding equipment/walls. The complete center-to-center journey,
including crossings and bends, is requalified in each permitted direction with
source terrain/elevation costs. Geometric failures feed existing bounded placement
backtracking; invalid declarations and work exhaustion are fatal. `InteriorPlan`
retains walls, contacts/openings, routes and consumed work. Read/export rederives
the plan. Session changes invalidate placement feedback and the `interiors` stage;
chunk indexing includes every interior direction. JavaScript displays saved engine
geometry without resolving it.

Limits: 1–16 rules, 2–32 rooms, 1–64 selected links and at most256 wall pieces per
rule, sharing the surface's256-exclusion capacity with existing exclusions;
combined exported assemblies must also fit that surface limit. Accounting and
JavaScript inspect the same bounded complete wall set. At most16 vertices per
convex room. The shared interior work bank defaults
to 20 million and is capped at 200 million; per-search and shared surface limits
also apply. Approach depth must exceed wall thickness plus traveler radius and
opening width must exceed traveler diameter; actual full-envelope tests still
must pass. Thickness is inward per room, so shared boundaries have two strips.
Dimensions are game-authored, not building-code certification. Search completeness
is bounded by its finite candidates.

Junction approaches and ordinary segments charge the same terrain-trace upper
bound: every boundary is scanned, but only intersecting edges can contribute
sampling cuts. Heightfield cuts are charged separately. Remote walls remain in
full-envelope collision/terrain checks; they do not cause a quadratic charge as
though every wall intersected each local bend. Exact-limit replay and one-unit-
short refusal are qualified in the current connected-capacity evidence.

Current scope: convex single-floor modules with explicit exterior bindings and
shared wall-aware routes/networks/corridors. Concave rooms, multiple storeys,
door interactions/artwork and broader world composition remain open.
Population sources from interior plans still reject rather than lose protected
geometry. Godot is deferred. [Examples](../examples/metadata/generated-interiors/README.md)
and [executed evidence](evidence/2026-10-28-generated-interiors-verification.json).
The [entrance qualification](evidence/2026-10-28-entrance-travel-verification.json)
supersedes that earlier increment's global-travel refusal.

For blocked first-feasible searches, the shared router can retain a qualified
one-bend incumbent while trying shorter two-bend joins. It refines that incumbent
only when it exceeds 1.25 times direct distance; already-short qualified one-bend
routes return without optional refinement. This is an internal work heuristic,
not a hard game path-length limit. Refinement retains near/far endpoint-visible
representatives in 32 angular sectors of the existing candidate
graph; these are search geometry, never world grid cells or placements. Every
edge is fully qualified. If no shorter join succeeds, the incumbent remains
available; otherwise bounded A* handles the remaining search. Work/edge exhaustion
is still fatal. This improves a controlled boundary-detour failure without
claiming shortest routes or continuous completeness. Default lowest-cost policy
retains its semantics. An exact exclusion-distance precheck rejects blocked edges
before terrain tracing; complete corridor and swept-area checks still run on
accepted edges. Saved receipts require regeneration when implementation changes.

Production consumers can use `GameArtsLifecycleRegistry` as both registry and
approval authority. It reads canonical lifecycle records without configuring a
game or changing approvals. Schema 2 retains stable lifecycle and native bundle
IDs separately; exact artifact approval is required for captured source images
and production bundles. See the [lifecycle contract](GAMEARTS_INTEGRATION_CONTRACT.md#canonical-read-only-lifecycle-authority).
Metadata-only generation remains independent of GameArts and Godot.

For captured-scene production consumers, the CLI now provides `structure-pipeline
production-bind` and `structure-pipeline native-static` using that canonical
authority and explicit versioned host configuration. See the [command contract](GAMEARTS_INTEGRATION_CONTRACT.md#canonical-command-handoff).
The native export is a pending Godot project until its installed native compiler
runs; technical compilation does not establish visual or gameplay acceptance.

## Optional native tile-topology consumer

Physical-member production bindings can now carry optional sampled component
animation through the same verified-frame owner. All component/frame sources,
parent/mount relationships, timing, pivots and facing order are retained. Native
AnimationPlayer consumes the approved sample timeline; absent samples, incorrect
frame timing and a moving fixed physical root refuse binding. See the
[animation contract](GAMEARTS_INTEGRATION_CONTRACT.md#sampled-component-animation).
This does not infer gameplay behavior or evaluate unsampled continuous curves.

`GodotStaticWorldSpecification.Movement` now declares optional exterior-domain
collision/navigation requirements. Exact domain/exclusion/solid polygons feed
Godot's native physics and baked per-radius navigation. Required journeys retain
exact endpoints and undergo continuous circle sweeps; absent movement grants no
acceptance. Doors/interiors and terrain-kind policy are not inferred. See the
[movement contract](GAMEARTS_INTEGRATION_CONTRACT.md#optional-native-exterior-movement).

The Godot adapter now also exposes `ExportMetadataAsync` for explicit verified
static art bindings. The shared native compiler builds actual TileMapLayers and
static Sprite2D/AtlasTexture members, checks PNG decoding, terrain joins, measured
ground anchors and native save/reload. Input scene/assembly/plan digests and
external approval authorities remain mandatory; the direct procedural-world art
route is not implemented by this captured-scene entry point. See the
[static compilation contract](GAMEARTS_INTEGRATION_CONTRACT.md#verified-frame-native-static-scene-compilation).
Technical fixtures do not establish actual artwork, collision/navigation,
animation or whole-world visual acceptance.

`WorldTileTopologyPlanner` derives bounded presentation masks from existing
surface, passage, entrance and site geometry. It does not generate streets or
change source coordinates. Explicit cell resolution/origin/error band and source
digest are required; material groups are a downstream choice. The Godot adapter
can export a native terrain-demand measurement project, using built-in terrain
matching and PackedScene save/reload. See the [integration contract](GAMEARTS_INTEGRATION_CONTRACT.md#presentation-masks-and-native-terrain-combination-inventory)
for statuses, limits and actual evidence. Transparent probe tiles are technical
data; production art, native gameplay and visual acceptance remain unfinished.

## Optional production-asset bindings

The final scene may be passed to `StructureFirstPipeline.PlanMetadataProductionAsync`
for a replayed production inventory, then `BindMetadataProductionAsync` for
externally approved, hash-pinned measured static frames and tile libraries. Every
member stays accounted for; contours, route points/widths, ground anchors, access
and corridor facts survive the handoff. The [integration contract](GAMEARTS_INTEGRATION_CONTRACT.md#final-map-production-inventory-and-measured-bindings)
owns exact API/status/CLI semantics. Static binding verification does not certify
tile transition completeness, moving parts, actual artwork quality or native
Godot behavior. Core metadata generation still requires none of those consumers.

## Optional art and capture handoff

The existing `StructureFirstPipeline` can derive an art inventory from exact
metadata recipe/catalog/world inputs. The shared `MockupCapturePipeline` then
reconciles explicit captured identities against complete detail/overview jobs,
preserving every required member and its nearest visual parent. This is optional
downstream framework functionality; metadata generation requires no game setup
or artwork. See the [integration contract](GAMEARTS_INTEGRATION_CONTRACT.md#metadata-instance-reconciliation)
for digest/source verification, missing/extra-content diagnostics and the boundary
between matched annotations and faithful map or production acceptance.

The optional calibrated ground compiler adds exact captured pins to an existing
physical program and invokes `WorldMetadataGenerator` again. It preserves source
dimensions, constraints and hierarchy; incompatible observations reject. Exported
world metadata stays on the ordinary validation/schematic path, with an additional
replayable capture receipt and per-view source overlays. The version-2 request
assembles complete detail scopes and optional overview observations with an explicit
owning view for every physical instance. Other views must agree; the owning pose is
preserved without averaging. See [ground compilation](GAMEARTS_INTEGRATION_CONTRACT.md#ground-design-and-production)
for required inputs, geometry tolerance and the limits of `GroundLayoutDraft`.

`ExportMetadataSceneAsync` adds fully accounted domain/patch/exclusion traces and
prescribed route links to a qualified ground result. The ordinary surface, physical
access, traversal and corridor owners validate the result. Exact path points,
per-segment widths, graph endpoints, source attributes and all existing members
remain authoritative; a blocked trace rejects. `Logical` art roles retain
nonphysical junctions without inventing artwork. See [scene tracing](GAMEARTS_INTEGRATION_CONTRACT.md#calibrated-terrain-and-explicit-route-traces)
for supported modes and `SceneLayoutDraft` limitations.

## Spatial ownership index

Optional recipe `Chunks` produces `ChunkPlan` and a separate `chunk-index.json`
through the existing atomic packet publisher. `MetadataChunkPlanner` owns this
index; it does not place objects or change terrain. `Version`, `Id`, `Purpose`,
`SourceRuleId`, `Origin`, `Width` and `Height` declare a two-coordinate storage
frame in the world's units. Do not interpret its cells as streets or regions.

Every positioned node and realized directed route/network path has one canonical
`Kind`/`Id`, one `OwnerChunkId`, all touched `ChunkIds` and a reversible
`OwnerLocalAnchor`. Point owners use floor division and half-open cells; physical
overlap is closed, so a footprint touching an exact seam references both sides.
Negative indices are valid. Node coverage includes current PlacementPlan
footprints, reservations, site features and entrance approaches. Path coverage
uses the common conservative full-width passage envelope. Original polygons and
paths stay in the source world without splitting or resizing. Unpositioned nodes
are listed in `SharedNodeIds`; terrain and other plans remain shared dependencies.
Other MapGeometry consumers are outside this index; it is not a native streaming API.

`SourceWorldSha256` binds the complete serialized document with ChunkPlan omitted.
Read/export rederive the plan and reject stale ownership or altered source data.
After normal world validation, `Select(world, chunkIds)` returns each touching
member once, in canonical order, regardless of request order or duplicate IDs.
Consumers resolve geometry by kind/ID from that validated source document and
own runtime instance lifetime; repeated independent calls do not manage it.

For repeated selection, a developer can validate and build one detached lookup:

```csharp
var lookup = new MetadataChunkPlanner().CreateSelectionIndex(world);
var visibleMembers = lookup.Select(chunkIds, cancellationToken);
```

The lookup captures member identities and chunk references, not world geometry.
It retains `SourceWorldSha256` and `ChunkPlanSha256`; recreate it after changing
or reloading a different world or chunk plan. Selection deduplicates chunk requests
and members and returns canonical plan order. It rejects unknown chunk IDs and
honors cancellation. Source lists cannot mutate the captured index. Lookup
creation revalidates the chunk plan, but the consumer still validates and resolves
the source package under its normal trust boundary. This is not independent chunk
generation or runtime streaming.

For repeated access to the full source coverage geometry, a consumer can build
an in-memory geometry index after validating the world:

```csharp
var geometry = new MetadataChunkPlanner().CreateGeometryIndex(world, maximumWork: 20_000_000, token: cancellationToken);
var visible = geometry.Select(chunkIds, cancellationToken);
```

Each selected member appears once, in canonical order, with its complete world
anchor, typed, unclipped coverage polygons and, for paths, original centerline and
passage radius. Coverage distinguishes footprint, reservation, site feature,
access approach, path segment and generated interior wall, retaining source IDs, site-feature kinds and
path segment indices.
For an interior wall it is the same `interior-` source digest used by the
interior surface owner, over rule, owning room and wall identity.
For footprints and reservations, `SourceId` is the placement demand ID scoped
to that member; for site features it is the feature ID, for access approaches
the port ID, and for path segments the route ID.
Positioned node members also carry a detached `WorldMetadataNode`, including
their parent ID, archetype, purpose, position and game-authored properties;
path members have no node. Each positioned node also includes its original
incident connections, including direction and properties. A connection can
reference an endpoint outside the selected chunks; if both endpoints are loaded,
deduplicate the connection by ID. Parent IDs likewise do not guarantee that the
parent is selected. Unpositioned nodes and connections between only unpositioned
nodes remain shared world data. Node and connection metadata contributes to the
extraction-work budget. Room members also carry `Interiors`: exact generated
wall polygons/edge endpoints, doorway widths/approaches and their rule purpose,
source rule and wall thickness. A doorway's canonical identity is
`(RuleId, ConnectionId)`, and both incident rooms can reference that one doorway.
The reader rejects inconsistent references when both rooms are selected.
Nodes also include their declared occupancy where available; only a footprint
with `Solid` occupancy declares solid content. `InteriorWall` coverage separately
identifies generated solid wall geometry without making the whole room solid.
Reservations, features and route
envelopes do not automatically imply collision. The snapshot owns read-only copies and retains source and plan
digests. Creation has an explicit extraction-work budget and refuses a changed
or inconsistent plan; cancellation is supported. Shared terrain, unpositioned
nodes and other shared world data remain in the
source package. This snapshot materializes existing geometry in memory; it is not
independent chunk generation, incremental disk loading or runtime streaming.

To load previously generated coverage selectively from disk, publish a separate
package using the shared operation reporter and retain the returned manifest
digest in a trusted location:

```csharp
var publisher = new MetadataChunkGeometryPackage(new MetadataChunkPlanner(), reporter);
var receipt = await publisher.PublishAsync(world, newDirectory, token: cancellationToken);
var reader = await MetadataChunkGeometryPackage.OpenAsync(newDirectory, receipt.ManifestSha256, cancellationToken);
var visible = await reader.ReadAsync(chunkIds, cancellationToken);
```

Publication validates and snapshots the existing world, writes one bounded
geometry record per occupied chunk, and commits the directory atomically. It
uses package schema 4 for node, connection and generated interior metadata; earlier geometry packets must be
regenerated.
It refuses existing output and reports start, publication and failure; a reporting
failure after commit exposes the already-published directory in
`MetadataChunkGeometryReportingException` together with its manifest digest. The reader checks its pinned manifest
and only reads requested records, verifies each record's digest, and deduplicates
whole members across seams. An unrequested damaged record does not affect a
selection, but is detected if requested. Each record is limited to 8 MiB and the
package data to 256 MiB; selected members and metadata are materialized in memory.
The manifest pin and hashes provide integrity relative to the caller's trusted
pin, not source authorship or approval. The directory root and requested files
are checked for reparse points; concurrent replacement and ancestor-link races
are not a hardened filesystem sandbox. Full terrain, relationships and other
shared world data remain separate. This loads pre-generated geometry; it does not
independently generate or validate a playable world.

For an already generated domain collection or regional/domain composition,
`MetadataDomainChunkIndex.Create(document, generator, planner)` validates and
snapshots the source, then creates the existing geometry index for each domain
that declares a chunk plan. `Domains` exposes domain-qualified chunk IDs and
indicates physical content without a chunk plan. `Select` accepts distinct
`MetadataDomainChunkRequest` entries and returns each selected local member
under its domain ID, plus that selected domain's unpositioned shared nodes and
their incident connections. A physical domain without a chunk plan cannot be
selected; an unpositioned logical domain can be selected with an empty chunk
list. Duplicate chunk IDs within a domain are deduplicated, and local IDs on
different floors never merge.

```csharp
var index = MetadataDomainChunkIndex.Create(composedWorld, generator, chunkPlanner,
    token: cancellationToken);
var selected = index.Select([
    new MetadataDomainChunkRequest("lower-floor", lowerFloorChunkIds),
    new MetadataDomainChunkRequest("upper-floor", upperFloorChunkIds)
], cancellationToken);
```

Each result also contains the **complete** explicit transition graph, journey
and progression witnesses, and, for a regional composition, all regional route
geometry. These are shared dependencies rather than chunk-filtered records;
remote endpoints need not be selected. The result is detached and source-bound.
Each selected physical domain includes shared surface boundaries, exclusions,
analytic patches and original height-field geometry. `AvailableSamples` states
the source's actual original-lattice sample extent. Each selected chunk has an
exact bounded terrain window where samples exist, including overlapping seam
samples. A null window means no field samples were captured for that chunk; it
does not mean the terrain is empty, since analytic patches may still apply.
Captured windows are clipped to their saved extent and are never extrapolated.
Index construction and selection are in memory. Extraction work has an aggregate
limit; source replay/validation and serialization are separate costs.

The validated index can also be published as a single multi-domain package:

```csharp
var package = new MetadataDomainChunkPackage(reporter);
var receipt = await package.PublishAsync(index, newDirectory, cancellationToken);
var reader = await MetadataDomainChunkPackage.OpenAsync(newDirectory,
    receipt.ManifestSha256, cancellationToken);
var lowerChunks = await reader.GetChunkIdsAsync("lower-floor", cancellationToken);
var selected = await reader.ReadAsync([
    new MetadataDomainChunkRequest("lower-floor", [lowerChunks[0]])
], cancellationToken);
var floor = selected.Domains.Single(d => d.DomainId == "lower-floor");
if (floor.Surface is { } surface && floor.Terrain[0].Window is { } window)
{
    var terrain = new MetadataDomainTerrainQuery(surface, floor.Terrain[0], cancellationToken);
    double height = terrain.HeightAt(window.FirstColumn, window.FirstRow);
}
```

Publication refuses physical domains without chunk plans and commits one
directory atomically. Root package schema 2 pins shared data, each selected
terrain record and the existing per-domain chunk manifests; those manifests pin
the individual geometry records. Schema 1 packages must be regenerated.
Keep the returned root hash in a trusted location. Open verifies and holds the
shared graph in memory; reads open only the requested domain manifests and chunk
records, verify their hashes and deduplicate members within each domain. An
unrequested damaged chunk does not affect the selection. Shared graph and
surface data is limited to 8 MiB, the whole package to 256 MiB, and each geometry
or terrain packet to 8 MiB. Terrain extraction per operation has a 20-million
sample/work limit. Publication reports operations; if success reporting fails after
commit, `MetadataDomainChunkReportingException` exposes the committed path and
manifest hash. The pins prove consistency with a trusted hash, not authorship.
For a non-null terrain window, `new MetadataDomainTerrainQuery(surface, chunk)`
provides original-index height and triangular-interpolation queries without
reconstructing a full field. It refuses missing or outside-window samples;
surface masks and patches remain separate. These source-bound samples are not a
complete terrain simulation. Independent chunk generation, environment-layer
paging and native runtime streaming remain outside this package.

Defaults are 4,096 occupied cells, 200,000 member/cell references and 20 million
work units. Configurable ceilings are 16,384 cells, one million references and
200 million work units. Cell dimensions must be finite and between 0.01 and
10 million world units. Coordinate/index, geometry, scan, cell, reference and
work limits reject instead of publishing a partial index. No more than 16,384
selection IDs may be supplied in one request.

The optional `chunks` execution stage follows the shared geometry owners. A
storage-size-only edit reuses the preceding fourteen stages in the standard
fixture, while invalidation, checkpoint replay and failed-edit rollback use the
existing generation-session owner. [Qualification](evidence/2026-10-27-world-chunks-verification.json)
covers saved ownership and exact geometry; it does not qualify independent chunk
generation, terrain paging, volumetric support or Godot streaming.

`MetadataSpatialMemberKind` is the common `Node`/`Path` kind. The former
chunk-specific enum name is removed; there is no compatibility alias. The shared
`MetadataSpatialMembers` owner extracts actual footprints, reservations, site
features, entrance approaches and directed paths for both this storage index and
generation-region ownership. Storage selection still never generates content.

## Independent shared-coordinate generation regions

### Terrain-selected regional networks

Optional `MetadataRegionalOwnershipRequest.NetworkRules` and
`MetadataRegionalWorldDefinition.NetworkRules` supply1..4
`MetadataRegionalNetworkRule` declarations. Each has an ID, connection kind,
2..32 named destinations, ordinary `WorldNetworkOptions` and an optional routing
domain (otherwise the world boundary). A destination retains purpose/source and
an exact region/node/optional-port binding, or a selector for a unique game-authored
`NodeKind` and optional `ArchetypeId` within the named region. For a selector,
set `NodeId` to the empty string and preserve the desired `PortId`. For example:

```csharp
new MetadataRegionalEndpoint("north", "", "entrance",
    new MetadataRegionalNodeSelector("settlement", "village"))
```

The engine resolves selectors after regional content is generated. Zero or multiple
matches fail explicitly; they are not guessed. The network witness records concrete
resolved destinations, and replay checks them against regenerated content. The
optional design-composer `Bindings` use the same post-generation resolution; a
missing or ambiguous role can reject a design alternative. Regional program
composition can also reject a complete physical candidate on that failure and
try its next authored program, charging failed qualification work. Joint
terrain-coupled composition can likewise consider another declared program;
component/partition search does not currently retry selector failures. Multiple destinations
may belong to one region; duplicate bindings or coincident resolved endpoints refuse.

`MetadataNetworkPlanner` binds the complete actual regional programs and delegates
candidate search, terrain costs, sparse backbone selection, required links and
optional single-connection-failure redundancy to `WorldDestinationNetworkPlanner`.
All bodies/walls, source-sized footprints, declared port approaches and traversal
constraints remain hard. Scale and purpose identify the authored channel; they
do not infer a road hierarchy or change physics without declared profile/rules.
`Topology` can also supply staged transitions under separate connection kinds.
Authored, topology-owned and network-owned kinds cannot conflict.

The world document's `NetworkPlan` retains every candidate attempt, exact selected
forward/reverse route, connectivity analysis, source-to-regional link mapping and
actual work. Generated routes join the ordinary `Connections` and member index;
they are derived output, not copied into authored `Definition.Connections`.
The combined regional-link limit remains128; total node/connection/reference,
per-route, per-network, pair and aggregate work limits still apply. Source
declarations are detached and canonicalized before use. Invalid declarations
refuse before local content generation.

Every direction is qualified through the current route owner and actual named
access owner. Native read/export and independent assembly replay the complete
network. Source sessions regenerate affected connections, reuse exact unchanged
local packets, preserve failed edits and restore/roll back full history. An
infeasible network throws `MetadataRegionalNetworkInfeasibleException` with its
`RuleId`, complete failed `Plan` and work; required links are never silently
replaced. The search selects within its bounded candidate graph, not a global
continuous optimum. Network failure does not currently attribute one ownership
anchor to reject or search alternate global source designs.

The shared traversal owner now supplies conservative cost lower bounds for
declared rectangular terrain bands spanning the domain's bounding box in an
axis. Lower/equal-cost overrides weaken the bound; parallel overlaps use disjoint
intervals and perpendicular projected costs use their maximum. Bound preparation
and lookup consume work. Other geometry retains the ordinary admissible bound;
this optimization does not impose rectangles, cells or paths on a world.

The [reusable framework input](../examples/metadata/regional-networks/README.md)
changes a terrain-kind cost from1to9 while preserving the actual elevation plane
and all local sources. It changes selected macro links within the unchanged
50-million-work network budget. See
[commands, geometry/cost checks, browser and limits](evidence/2026-10-31-regional-networks-verification.json).

### Procedural connections between regional destinations

`MetadataRegionalOwnershipRequest.Topology` optionally supplies a version-1
`MetadataRegionalTopologyDefinition` with ID/purpose/source,2..16 named
`Destinations` and1..16 `Rules`. Each destination has a game-authored kind and
binds an exact regional node and optional named access port. This bounded route
requires distinct destination regions; other regions may remain outside the
declared graph. Use `MetadataTopologyPlanner.RegionalDestinationId(worldId,id)`
for logical IDs in the ordinary `MetadataTopologyRule`.

The shared topology owner generates staged trees, degree-limited branches and
the exact requested extra loops; shared progression verifies required stage
transitions. Each `MetadataRegionalTopologyRule` couples that grammar to an
explicit traversal `Profile`, route `Search` and optional `RoutingDomain`
(otherwise the actual world boundary). Scope must be the world ID; a regional
topology rule cannot override layers or own an already-authored connection kind.
Every declared destination must participate. Stages select all matching member
kinds, without deleting excess members to meet a maximum.

`TopologyPlan` retains the canonical source definition, geometry-free projected
nodes, full topology/progression witnesses, original authored connections,
logical-to-physical link bindings and work. Projected nodes are not extra physical
world members. Generated links use stable grammar-edge IDs and become ordinary
regional route obligations, owned by their From region. The existing physical
owner resolves actual endpoints/ports, source solids and terrain and qualifies
each requested direction. The root work budget includes projection, graph and
progression work once; existing per-rule limits still apply. Reading, assembly,
publication and source sessions replay the complete source and receipt.

The exact regional source edit API can change a grammar seed, regenerate macro
links and reuse all unchanged local packets. Failed edits preserve the accepted
world; saved history and rollback retain original source inputs. Impossible graph
demand, unused/missing destinations, conflicting kinds/layers, invalid endpoints,
unresolved physical paths and exhausted budgets refuse. No nearest-port, missing-
link or arbitrary-position fallback exists.

The required-transition proof applies to each declared connection channel.
Separately authored channels may offer other journeys; this is not proof of
universal gameplay gating. This bounded2D regional adapter does not supply road
surface meshes, arbitrary multi-level connection policy, optimal physical graph
selection or a complete playable game. See the
[reusable input](../examples/metadata/regional-topology/README.md) and
[current evidence](evidence/2026-10-31-regional-topology-verification.json).

### Deriving ownership and program anchors

`MetadataRegionalOwnershipPlanner.Plan(MetadataRegionalOwnershipRequest)` derives
an ordinary `MetadataRegionalWorldDefinition` from2..16 complete regional program
templates. Each `MetadataRegionalProgramRegion` supplies ID/purpose/source,
positive `Weight`, `MinimumArea`, `MinimumClearance`, an actual terrain suitability
`Profile`, an optional generated `PlacementAnchorId`, recipe and catalog. The
request owns the original shared surface, authored and/or generated regional connections, an area
pattern, anchor-search limits and the existing total-node/connection/reference/
work limits. Source size remains16MiB and snapshot depth32.

Templates leave `Surface` and `Origin` unset. This first derivation policy also
requires semantic `Regions`, `LocalLayouts` and `RegionDemands` unset. When root
placement is declared, its boundary must be unset and the selected anchor ID must
be valid and absent from existing anchors. Other authored anchors and all nested
program geometry remain unchanged. Without placement, a generated placement
anchor ID is unused and refuses. The recipe's2D space/unit and environment are
required for actual terrain qualification.

Joint terrain/program composition performs a conservative preflight for
patch-defined, generated-terrain, supplied-heightfield and captured-terrain surfaces: when a
required region's allowed kinds have no overlap with the declared terrain kinds, the planner refuses with
`WORLD_REGION_INTENT_UNSATISFIED:<region-id>` and attaches a
`RegionalTerrainKindFailure` witness containing the allowed and available
kinds. The surface, all regional profiles and field references, area pattern,
and joint composition/coupling/physical-feedback rule envelopes are validated
before this proof. The check charges generated terrain work and supplied sample
validation to the regional budget, including the original-lattice capture work
and validation of a saved capture plan when present. It does not establish that
a listed kind has usable coverage or sufficient area.

Regional ownership and world definitions may carry a complete supplied
`HeightField`, with or without a `Terrain` recipe. Every supplied sample is
validated before region selection; if both are present the field must exactly
match the regenerated recipe. Field-only inputs retain the complete source
field in regional packets. A matching recipe/field pair retains both as source
and uses the existing generated-window capture for child recipes. This does not
provide selective supplied-field streaming; complete supplied fields and source
snapshots remain subject to existing size and work limits. Supplied-field changes
are global regional dependencies. A bounded original-lattice terrain capture is
also accepted as a top-level regional source; its domain must contain the world
and any saved plan must replay against its source recipe and current engine.

The common connected area kernel partitions the complete outer world polygon.
Its temporary root minimum comes from the smallest declared program minimum;
there is no implicit one-square-unit/meter threshold. Existing finite geometry
resolution still applies.
Each occupied stratum meets its geometric minimum area and its relative mass
weight. Without `Pattern.DensityFieldId`, mass is uniform area. To select a
density field, supply `OwnershipEnvironment` (`WorldEnvironmentDefinition`);
the field must exist there. A missing environment or an unused environment
refuses. This optional property is omitted from JSON when absent. Program
environments remain separate and continue to qualify actual playable anchors.

Density measurement uses the original terrain and its actual affine field pieces,
including dependent/clamped fields, through the shared area kernel. A temporary
measurement view omits exclusions so unplayable ground remains owned; actual
generation never removes exclusions. Unknown/uncovered terrain refuses. Root
cells preserve terrain and field planes rather than merging them as eligibility
alone. Negative density, zero total mass, insufficient geometric capacity and
anchor clearance remain hard refusals. Weights control mass shares, not necessarily
equal geometric areas. The pattern's axis, interior, partition search and depth rules apply.
This regional program `Weight` applies to ownership across whole program regions;
`WorldRegionIntent.AllocationWeight` instead controls intents sharing one suitable
component inside a region plan. When a regional program declares `Capacity`, its
effective ownership weight is `Weight * RequiredArea` as described above; without
that capacity policy the regional `Weight` is the relative mass weight directly.
`MaximumCells` is1..4096, matching the region owner. Resulting ownership must have
one simple polygon loop of at most64 vertices. Excluded terrain remains owned;
the computational ownership support is never added to or substituted for the
actual game surface. Ownership masks are independent of streets and storage cells.

Within each mask, the ordinary terrain/environment region owner derives a suitable
component and interior point under that program's declared minimum area and
clearance. The definition binds this actual point as recipe origin and the named
root anchor, and binds the ownership polygon as generation/root placement domain.
Complete catalog expansion, nested placement, access, local paths and regional
journeys still run through their existing owners and retain hard source facts.

`MetadataRegionalWorldDefinition.OwnershipPlan` retains version1, the canonical
source request, exact area partition, actual terrain anchor plans and charged
derivation work. `OwnershipDerivationId` binds its presence. Ordinary regional
preparation replays the source derivation exactly and charges its work to the
existing preparation/assembly ledger. Each derivation call and regional generation
operation retains its declared budgets; original terrain generation and per-
program work owners keep their own limits. Cached packets never bypass source
derivation or global constraints. Existing authored ownership remains a separate
meaningful input and is unchanged when these optional fields are absent.

The public CLI entry is `generate-derived-regional-world <request.json>
<new-output-dir>`. It uses the same registry discovery, reporting, generation,
replay and atomic publication owners as explicit regional definitions. Source/
weight/terrain edits work through `MetadataRegionalGenerationSession` with a newly
derived definition. Saved source history and packets authenticate on restore.

The combined schematic validates source/partition/anchor bindings and renders
whole source geometry. Its regional source check follows the native surface
owner's conservative closed AABB filter, then priority/ordinal ordering. Relevant
patches/exclusions retain complete geometry; irrelevant sources must be absent.
Native replay owns complete authentication and work accounting.

This policy supplies generation ownership and terrain-qualified anchors. Optional
capacity derives source-demand weights as described below. Packing, complete
game-world design, independent streaming and whole-game acceptance remain separate. See
`examples/metadata/derived-regions` for complete3/4/8/16-region controls.

`MetadataRegionalWorldGenerator` composes complete game-authored programs in one
shared two-coordinate world frame. It uses `WorldMetadataGenerator` for catalog
expansion, placement, nested content, terrain, access and local paths. It has no
city/genre switch and does not generate a complete world then filter a requested
piece. Independent coordinate frames and floor/domain transitions remain the
separate `GenerateDomains` capability.

`MetadataRegionalWorldDefinition` owns one shared `Surface`,2–16 generation
regions and0–128 explicit cross-region connections. The shared surface requires
a bounded3–64-vertex world polygon, authored patches and/or a source terrain
recipe, a complete supplied `HeightField`, or a bounded terrain capture. A saved
capture is checked against its original recipe and lattice before use; the
capture domain must contain the world. Captures do not fabricate a complete
heightfield. Every region has a unique ID, purpose/rule, an `Ownership`
polygon, a containing `GenerationDomain`, a metadata recipe and a catalog. Its
recipe has no separate `Surface`, uses two axes and the shared unit. Root metadata
positions must belong to their declared ownership region.

Ownership polygons cover the world without positive-area overlap or gaps.
Generation domains may overlap so a whole source-sized compound can cross an
ownership seam. They stay inside the world. These are semantic generation
contracts, not storage cells, parcels or streets. Descendants retain their source
region's ownership even when their geometry crosses the seam. The canonical
member address is `(OwnerRegionId, Kind, Id)`: different regions may use the same
recipe template and local IDs. Closed references include all touched ownership
regions without splitting, resizing or duplicating physical sources. The explicit
owner reference is retained for semantic members and owned connections.

Cross-region collision qualification uses the existing immutable placement bounds
index over each physical node's complete source envelope: footprint, reservation,
site features and access approaches. Only strictly separated closed bounds omit
the existing exact polygon checks. Candidate conflicts retain original source
order, tolerance and diagnostics. Bounds construction, index construction and
queries charge the existing regional work budget and observe cancellation.
This does not change public node/site/surface/port limits or truncate remote
sources. Dense envelopes retain bounded pair work. The
[complete physical scale example](../examples/metadata/regional-physical-scale/README.md)
qualifies eight authored regions/1,032 nodes/1,024 solid sources and reservations,
with required travel, under the existing 20-million default regional work limit.

`GenerateRegion(definition, regionId)` expands only the selected complete program.
The common schema validator can probe registered layouts with zero members;
unrequested programs are not expanded. Inherited terrain uses saved original-
lattice capture and the exact shared global source. A generated packet contains
version, region ID, selected context digest, current Core module identity, complete
world metadata and registered implementation dependencies. Its context includes
the shared world boundary/height recipe, region inputs, complete ownership/domain
masks and the existing surface owner's closed regional patch/exclusion projection.
Whole relevant source polygons/provenance remain intact, including boundary
contact. Provably disjoint overlays and another region's unrelated content edit
do not invalidate it. Equivalent source orderings use normal canonical surface
ordering. Source/provider validation and complete global constraints still apply.

`CreateGenerationPlan(definition, maximumRetainedPacketBytes, token)` prepares an
opaque live SDK handle with frozen source/dependency pins. The overloads
`GenerateRegion(plan, regionId, token)`, `ReleaseRegion(plan, regionId, token)` and
`Assemble(plan, token)` support explicitly requested complete packets, resident
reuse, explicit release and complete assembly without local re-expansion. Returned
definitions, packets and worlds are isolated snapshots. The snapshot reports
resident IDs/serialized bytes and successful generation/reuse/release/assembly
counts. Limits, failed requests/assembly, cancellation and dependency drift leave
accepted resident state intact; foreign/reentrant mutations refuse.

The retained packet cap is1 byte..128 MiB, default128 MiB. It is a serialized byte
bound, not a managed-heap or temporary-allocation guarantee. Over-cap retention
refuses without automatic eviction or source truncation. All regions must be
resident for complete assembly; a regional consumer may retain only a subset,
without certifying missing world journeys. Dependency rechecks select source/
catalog providers directly and do not materialize unrelated terrain windows.

For authored definitions, preparation performs schema/ownership validation without
expanding local content. Derived ownership preparation can replay wider semantic/
physical design; `DerivedDesignPreparation` explicitly reports that distinction.
The handle is ephemeral, not a file format or CLI session. Normal untrusted reads
and publication retain authoritative replay, including broader derived preparation.
`PublishRegionAsync(plan, regionId, newDirectory, token)` uses that same owner.
See the [SDK example](../examples/metadata/regional-demand-generation/README.md)
and [bounded verification](evidence/2026-10-31-regional-demand-generation-verification.json).

`Assemble(definition, packets)` requires every declared region exactly once. It replays each selected
program through the same generator, enforces aggregate content/reference/work
limits and canonicalizes order. Missing, duplicate, stale and altered packets
refuse. Regions' physical occupancy must be declared. Different owners cannot
overlap their source/reservation/site/approach geometry by positive area. Every
saved local path is requalified against all relevant solid bodies and constructed
walls, including foreign regions. Inputs whose generation changes the shared
surface refuse; per-region terrain/water overrides are not silently reconciled.

Each `MetadataRegionalConnection` declares its ID/kind/purpose/rule, two region-
qualified node/port endpoints, owner region, explicit `RoutingDomain`, traversal
profile, finite search and directionality. The existing access owner resolves
actual openings. Approaches can leave their own body through the declared port
but must clear other solids/walls in both directions. The shared surface route
owner calculates the actual full-width path; bidirectional declarations require
a qualified reverse path. Disconnected or blocked journeys refuse assembly.
No unrequested road, automatic collision repair or inferred progression path is
created. All registered dependencies are checked again after assembly and before
publication commit. An identical current harness fails on the prior Core that
omitted the final cross-region dependency check.

`Generate` is whole generation using those same independent requests and assembly.
`PublishRegionAsync` writes the normal complete metadata packet plus
`regional-definition.json` and `generation-region.json`, through the existing
JSON transport and atomic publisher. `ReadRegionAsync` authenticates the receipt,
actual recipe/catalog files, world digest, original context and current replay.
An optional current definition accepts an unchanged packet after an unrelated
content edit while still validating its saved provenance. `PublishAsync` writes
the assembled `world-regions.json`, reusable definition and complete child
packets; `ReadWorldAsync` and generic `ReadAsync<MetadataRegionalWorldDocument>`
replay every source and proof. There is no serialized executable cache or legacy
loader. Failed/cancelled operations cannot replace an accepted output.

The CLI commands `generate-region`, `generate-regional-world` and
`assemble-regional-world` use these same APIs through the existing
Log/Audit/Notification owner. A directory-list JSON array supplies independently
saved packets for assembly. The output index links a combined `schematic.html`
and complete individual region inspections. All use saved global geometry and
samples. Transactional regional edit history is described below.

The combined viewer reuses the ordinary source renderer through scoped scene
contexts. It never merges or rewrites child metadata into a synthetic document.
Presentation IDs include owner region, so repeated local IDs and parent/child
navigation remain independent. Ownership clips terrain presentation only; source
footprints, entrances and local/regional paths stay whole. Terrain windows share
one elevation/slope color scale. Node selection exposes the original local source
and semantic owner; regional paths expose direction, exact width and saved costs.
Pan/zoom, region focus, layer solo/opacity/restore, semantic filters/palettes and
SVG export use the existing inspection owner. Export restores all source labels
and carries regional context/build/dependency provenance. File imports undergo
bounded structural checks before scene replacement, and cannot retain misleading
links to a different export folder. Browser checks are not native source replay,
cryptographic authentication, collision proof or owner design acceptance.

Defaults bound the complete collection to50,000 nodes,100,000 logical connections,
200,000 spatial references and20M assembly work; hard ceilings remain100,000,
200,000,1M and200M respectively. Aggregate content is checked as each region
finishes, not after retaining an unbounded set. Preparation geometry charges the
assembly bank; each child algorithm and terrain query also retains its own
declared budgets. Existing access/solid, terrain, geometry, source-size and JSON
transport limits still apply; these ceilings are not supported-size performance
claims. Geometry uses the existing clipping/intersection tolerances, including
1e-8 positive-area and scaled1e-10 partition-area guards. It is not arbitrary-
precision geometry. Generated captures remain tied to the exact current build.

Qualification retains the complete10-node/6-source control, a whole seam-spanning
compound, exact504 shared samples, local and cross-region paths, richer required
content and unchanged neighboring packets.4/8/16-region controls retain20/40/80
nodes,12/24/48 physical sources and6/14/30 directed regional paths, with identical
whole, reversed-request, declaration-order and saved replay. The16-region measured
generation allocated632,956,616 bytes in total and the process's cumulative peak
working set reached192,090,112 bytes; allocation is not simultaneous live memory
and neither measure is a universal benchmark or ceiling. These are authored
technical programs, not populated-world design acceptance. Independent storage-
cell generation, runtime streaming, per-object/in-session control editing, broader
joint ownership redesign and the full world/design gate remain open.
Godot/artwork deferred. [Example](../examples/metadata/generation-regions/README.md)
and [generation evidence](evidence/2026-10-31-regional-content-verification.json).
[Combined inspection evidence](evidence/2026-10-31-regional-inspection-verification.json)
qualifies current 2/4/8/16-region readers and live 2/16-region rendering/export.

### Transactional shared-coordinate regional sessions

`MetadataRegionalGenerationSession` owns accepted state for the current planar
regional pipeline. It reuses `MetadataExecutionCache`, `MetadataJsonStorage`,
`MetadataDirectoryPublication` and `MetadataSessionOperationRunner`; independent
coordinate domains retain their separate current session variant.

Construct it with `WorldMetadataGenerator` and a complete regional definition.
`Snapshot` returns an isolated `MetadataRegionalSessionSnapshot(World, Report)`.
`MetadataRegionalEditRequest(Id, Purpose, SourceRuleId, ExpectedDefinitionSha256,
ExpectedWorldSha256, Definition)` pins both source definition and accepted world.
The world hash prevents a stale edit after a dependency-only change under the same
definition. An edit proposes a whole reusable definition, not a geometry patch.

`Apply` / `ApplyAsync` and `ApplyAndPublishAsync` generate through the same regional
and ordinary metadata owners. Private accepted packets are reused only when the
entire context (shared source, regional program and all ownership/generation
polygons), current build and registered dependencies match. Changed programs use
the existing stage cache. All local paths, foreign solids/approaches, complete
seam-spanning sources, ownership references and declared regional journeys still
undergo global assembly checks. A connection-only edit can retain every content
packet while recomputing the actual wider/different journeys. Failed proposals
cannot install partial packets, a new generator or changed caches.

`MetadataRegionalGenerationReport` exposes `AlgorithmId`, `EditId`, `WorldSha256`,
per-region `MetadataRegionalExecution` records and aggregate `CacheBytes`.
`Regions[].Reused` denotes whole-packet retention; those records have no executed
stages. Changed regions expose actual stage execution/reuse and dependency pins.
An ownership-only change invalidates context even when source stages remain reusable.
Publication additionally replays full source proof; a reuse report is not a claim
that validation/publication performs no generation work.

`PublishAsync` and `ApplyAndPublishAsync` atomically publish complete combined and
individual-region inspection packets with `generation-session.json`. A checkpoint
contains initial source/report, accepted attributed requests/reports and final
world hash. No executable cache is saved or installed. `RestoreAsync` regenerates
the initial world and every accepted edit, comparing all reports and the final
hash. Reverting uses an explicit new edit with a previous definition and current
base hashes; it remains part of the accepted history.

Restore requires the same Core build and exact registered dependencies for each
historical step. Optional `IMetadataGenerationDependencyResolver` supplies already
installed historical implementations for a snapshot's combined dependency set.
Every region's actual dependency selection is checked; null source/recipe/catalog
rows refuse before invoking the host resolver. No saved path/type is
executed and nothing is downloaded or substituted. The SDK also accepts an
optional generator on an edit; its selection commits with the world only after
success. The CLI uses its configured current registry and refuses unavailable
history rather than inventing a resolver.

History is bounded to32 accepted edits,16MiB source/edit envelopes and the existing
256MiB checkpoint/snapshot transport limit. The existing128MiB execution-cache bank
is applied to the sum of current regional caches, checked as regions finish.
`CacheBytes` counts the owner's cached payload/memo accounting; it is not a live
process-memory ceiling. Existing source dimensions, counts, physical widths and
region/content/geometry/work limits are retained. Snapshot/checkpoint size and
cancellation are checked before acceptance.

`StartAsync(..., MetadataSessionReporting)` and reported restore/edit/publish use
the existing shared services. Sources freeze before awaited host callbacks. Busy
sessions refuse concurrent mutations/publication. Precommit cancellation/failure
preserves the exact accepted state; cancellation after commit cannot relabel
success. `MetadataRegionalSessionReportingException` exposes the committed session
and exact output directory when only a success-reporting sink fails. Recover them
instead of repeating the accepted operation. Async lifecycle methods do not
automatically schedule CPU generation on a background thread.

CLI entry points are `region-session start <definition.json> <new-output-dir> [--controls <controls.json>]` and
`region-session edit <generation-session.json> <edit.json> <new-output-dir>`.

For a derived world, submit reusable source directly through
`MetadataRegionalGenerationSession.StartDerivedAsync(generator, source, reporting?, token, controls?)`.
`MetadataRegionalSourceEditRequest(Id, Purpose, SourceRuleId,
ExpectedDefinitionSha256, ExpectedWorldSha256, Source)` carries a complete
`MetadataRegionalOwnershipRequest`. `ApplyDerivedAsync` and
`ApplyDerivedAndPublishAsync` derive it inside the same transaction before normal
regional generation. Both base hashes and edit identity are checked before
derivation. The source freezes before awaited host reporting callbacks. Source
derivation failures use `MetadataRegenerationException` with stage
`regional-ownership`; original causes, cancellation and work limits survive.

The CLI equivalents are `region-session start-derived <request.json> <new-output-dir> [--controls <controls.json>]`
and `region-session edit-derived <generation-session.json> <source-edit.json> <new-output-dir>`.
They use the same installed registry/dependency selection and operation reporter.
History stores the resulting definition with its complete reusable ownership
source, explicit pins and actual selection receipts, through the existing checkpoint schema.
It does not store a second editable source, executable caches or a trusted
precomputed derivation. Restore rederives every historical source and selection.
Source-based and explicit-definition workflows produce identical accepted worlds,
reports and checkpoints for the same attributed edits.

For an authored regional design, `StartDesignedAsync(generator, design,
reporting?, token)` uses `MetadataRegionalDesignGenerator` and the same regional
session owner. `MetadataRegionalDesignEditRequest` carries the edit identity,
purpose, source rule, accepted definition/world/design-source hashes and the complete revised
design. `ApplyDesignAsync` and `ApplyDesignAndPublishAsync` validate those hashes
before generation. `ExpectedDesignSha256` comes from snapshot
`DesignSourceSha256` (also in its report); it is null only when the accepted
state has no design source. The separate source pin detects stale revisions
even when two authored requests generate identical world geometry. Snapshot `DesignSource`, checkpoint `InitialDesignSource`,
and each design edit's `DesignSource` retain the authored seed plan, choices and
requirements. Restore regenerates each design and compares its effective
definition before replaying the ordinary regional history. Changed design-source
provenance cannot silently substitute a different definition. An ordinary
definition/source edit intentionally ends design-source authority for that
snapshot; the earlier design history stays in its checkpoint.

The CLI equivalents are `region-session start-designed <design.json>
<new-output-dir>` and `region-session edit-designed <generation-session.json>
<design-edit.json> <new-output-dir>`. Registry discovery includes every design
template and option, including historical design sources. These commands use
the shared session reporting, protection checks, cancellation, finite history
limits and atomic publication. Design generation is recomputed to authenticate
source; it is not a promise of selective design-search reuse.

`StartDesignedAsync` also accepts a `MetadataRegionalBlueprintDesign`, with
named reusable programs and optional shared option sets. Supply revisions as
`MetadataRegionalBlueprintDesignEditRequest` to the same `ApplyDesignAsync` or
`ApplyDesignAndPublishAsync` methods. They use the accepted definition/world and
`DesignSourceSha256` pins just like expanded design edits. The session stores
the original blueprint at `Snapshot.DesignSource.BlueprintSource`; the expanded
template/options remain the actual generation input. Generation and checkpoint
restore re-expand that blueprint and require exact agreement with the stored
expanded design, otherwise raising `METADATA_REGIONAL_BLUEPRINT_SOURCE_MISMATCH`.

The design-source hash includes the original blueprint. Refactoring inline
options into a shared option set can change the source pin while preserving
every world byte and reusing every regional packet. An edit pinned before that
refactor is stale even though its definition/world pins still match. History
keeps each original blueprint; explicit source rollback restores the original
source identity and world. Existing per-file, history, cancellation, protection
and work limits apply. The CLI accepts `region-session start-blueprint-design
<blueprint-design.json> <new-output-dir>` and `region-session edit-blueprint-design
<generation-session.json> <blueprint-design-edit.json> <new-output-dir>`.

For design edits, active input and anchor protections also qualify each complete
candidate before selection. A valid candidate with a protected-value conflict
is recorded as rejected and the finite declared design search can try the next
option. Invalid declarations, work limits and cancellation retain their normal
failures. `ProtectionChanges` are validated against the accepted edit before
search, so an explicit release takes effect for that revision. This selects
among the declared alternatives. Protected region origins are bound into each
selected candidate as effective `PinnedAnchor` constraints before regional
planning, after seed derivation and program selection. The saved authored design
stays unchanged; the session protection plan supplies the effective coordinates
again during replay. An explicit authored pin at a different coordinate rejects
that candidate before world generation, so its attempt has no candidate world
hash. Invalid pin declarations retain their normal errors.

`Report.DesignAttempts` retains ordered choices, counts, rejection reasons and
candidate world hashes for design-backed sessions. Creation, design edits and
checkpoint replay compute the same trace from source and active protections.
The report's ordinary `WorldSha256` identifies the final accepted session world.

The complete program-choice edit control makes the preferred alpha host feasible
by selecting a separately authored24m source envelope. The engine reselects that
whole catalog program, reuses only the unchanged beta/gamma packets, retains all
children/functions and journeys, and restores the exact original world with an
explicit accepted rollback. Three current-Core native scopes, five JS checks,
SDK/definition/CLI byte equality, typed CLI failures and independent geometry
checks qualify this combined workflow. [Evidence and remaining scope](evidence/2026-10-31-regional-source-history-verification.json).

[The example](../examples/metadata/generation-regions/README.md) includes a richer
reusable definition. Complete2/16-region controls, source-sized nested additions,
dependency history,32-edit replay and atomic fault cases are qualified in
[current evidence](evidence/2026-10-31-regional-session-verification.json).
Regional input locks and generated-origin protections are supported below.
Finer per-object pose editing, changing controls inside an existing session,
broader joint terrain/content redesign, streaming and the complete core/design
gate remain open. Source-driven ownership
derivation is now part of the session transaction; this does not supply arbitrary
world intent or general macro-design synthesis.
Godot/artwork remain deferred.

### Regional developer protections and origin pins

Pass optional `MetadataRegionalSessionControls(Inputs, Anchors)` at session
creation. At most64combined definitions are allowed. IDs must be unique across
both collections; each anchor region and each input scope/region/pointer must
be unique. Empty supplied controls, unknown scopes, missing regions/values and
malformed pointers refuse. Controls detach before host reporting callbacks.

`MetadataRegionalInputProtection(Id, Input, RegionId, Pointer, Purpose,
SourceRuleId)` selects `World`, `Recipe` or `Catalog`. `World` requires null
`RegionId` and resolves the reusable ownership source for a derived world, or
the explicit definition otherwise. `Recipe` and `Catalog` resolve by semantic
region ID, independent of array order; derived worlds use their source templates.
Pointers use the existing bounded JSON-pointer owner (empty selects the whole
value, `~0`/`~1` escape tilde/slash). Captured value hashes must remain unchanged
before regeneration and after normal source validation.

`MetadataRegionalAnchorProtection(Id, RegionId, Purpose, SourceRuleId)` captures
an origin from a normally generated and validated derived world. The session
binds that exact point into its source as `PinnedAnchor`, rederives and fully
qualifies a fresh initial world. It then binds the same constraint before every
source edit. Explicit-definition edits also check the retained point; they cannot
bypass it. Authoring an anchor protection requires a derived ownership source.

Games may directly author `MetadataRegionalProgramRegion.PinnedAnchor` as
`MetadataRegionalAnchorPin(Purpose, SourceRuleId, Position)`. It binds the shared
terrain intent's `RequiredPosition`: actual component containment, signed
distance to exterior/hole edges, minimum clearance and point suitability still
apply. Complete source sizes, occupancy, entrances, all required paths and work
limits remain hard. No post-hoc comparison or relocation substitutes for these
planner constraints. Impossible pins produce attributed
`METADATA_REGIONAL_ANCHOR_PIN_UNSATISFIED:<region>` errors; budget/geometry/input
failures are not relabeled or retried as pin conflicts.
Terrain-qualified ownership search now considers declared partition alternatives
to accommodate a pin before committing the ownership. Explicit physical feedback
can also reconsider declared cut assignments without moving the pin. Neither
search certifies global continuous infeasibility.

`Report.Protections` retains the effective current input definitions/hashes and
anchor definitions/positions. Checkpoint `Protections` and
`InitialReport.Protections` retain the initial policy. Restore regenerates the
initial source, recaptures/compares its protections, and replays every accepted
edit's transition and resulting policy. It never trusts saved
cached geometry. `MetadataRegenerationException.ProtectedInputIds` lists retained
input and anchor controls; it does not assert that every listed rule caused the
failure. A rejected or cancelled edit preserves accepted world/cache/history and
publishes nothing. Success-sink outages retain the authoritative committed
session/publication, including its controls, through the existing reporting owner.

Both regional definition/source edit requests accept optional `ProtectionChanges`
as `MetadataRegionalProtectionChange(ExpectedEditId, ReleaseIds, Inputs, Anchors)`.
Its edit ID must equal the accepted `Report.EditId`; this also catches stale
policy-only proposals whose world/definition hashes have not changed. At most64
releases and64 additions may be proposed, with at most64 active protections after
the change. Lists must be present. Empty transitions, unknown/duplicate releases,
duplicate active IDs/targets and missing target values refuse. A simultaneous
replacement of an active ID uses a new ID; all-release is explicit and valid.

New definitions capture current accepted values/anchor positions before the
proposed source change. Surviving definitions keep their original receipts;
activation never recaptures a conflicting edited value. New anchors bind normal
planner pins. Direct definition activation authenticates the supplied derived
receipt before changing its source; forged geometry cannot be repaired through
protection changes. Policy release alone preserves explicit source `PinnedAnchor`
data; supply an explicit unpinned source edit to permit automatic relocation.
The next policy is committed with the existing world/report/history state only
after all generation, constraints and requested publication succeed. Failures,
cancellation and callback mutation cannot partially replace accepted policy.

This scope does not provide a graphical lock editor. Input locks preserve authored
values; they do not freeze
terrain-derived counts, selected program variants or every child pose. Only the
declared regional origin is an exact output-position protection here.
Terrain overlays invalidate intersected declared dependency domains; height-recipe,
world-boundary and ownership/domain changes retain conservative broader invalidation.
The same validated closed surface projection is used for generation and context
identity, without dropping source polygons or hiding malformed remote inputs.
External packets retain authoritative replay and all complete-world paths requalify.
[Overlay workflow and measured16-region control](../examples/metadata/regional-overlay-invalidation/README.md),
[current commands and limits](evidence/2026-11-01-regional-overlay-invalidation-verification.json).
[Reusable protection source and API example](../examples/metadata/regional-developer-controls/README.md),
[bounded evidence](evidence/2026-10-31-regional-developer-controls-verification.json).
The [transactional policy example](../examples/metadata/regional-protection-changes/README.md)
and [current verification](evidence/2026-11-01-regional-protection-changes-verification.json)
cover add/release, actual terrain relocation, initial/effective policy history,
public commands and rollback. Per-object position/orientation intent is separately
supported by the shared placement controls described below.

## Hierarchical scale limits

The shared allocator accepts at most 128 nested child stages, with at most 16
demands in each stage. `MaximumStageAttempts` counts every execution, including
the root and retries: a 128-child-stage program needs at least 129 attempts (the
default is 128; explicitly configure a larger finite budget). The 200-million
aggregate work ceiling remains. Local depth-first search uses explicit frames;
fixed-anchor constraints reject before polygon evaluation without changing the
feasible candidate set or its ordering.

Physical access classification accepts at most 1,024 placed sites. This is not a
general 1,024-building world-performance claim. Authored surface masks remain
capped at 256; the newer [placed-solid query layer](#placed-solids-and-bounded-new-route-search)
separately qualifies bounded 1,024-solid detours. Total entrances remain capped
at 128 and access work at 20 million. Classification polygon
validation now charges that budget. The schematic accepts the same stage/site
limits and retains completeness checks. Regenerate packets after algorithm/build
changes; stale saved work receipts are not a compatibility path.

[Scale evidence](evidence/2026-10-27-world-scale-verification.json) covers complete
authored walkable programs of 85, 169, 493 and 577 nodes, exact source geometry,
connected destinations, saved replay, CLI refusal and actual browser geometry.
The largest program has 16 areas, 112 sites and 448 interaction pads. It isolates
hierarchy scaling with authored anchors; it does not qualify automatic terrain
adaptation, solid-building routing, diverse world composition or chunk streaming.

## Complete mixed-world resource qualification

The [complete mixed-world resource profiles](../examples/metadata/composed-resources/README.md)
qualify 191/309/546/1019 nodes with 186/304/541/1014 physical members, including
all original compounds, nested content, outside structures, rooms and progression.
The larger profiles add terrain-funded exact1×1 landscape sources; they are not
thousand-building benchmarks. Full source-size edits, clean comparison and
checkpoint replay preserve the complete required scope and journeys. Demand above
the unchanged1024 count ceiling refuses without clamping or publishing a world.

Execution-cache hashing/cloning, source population equality and chunk digest/replay
now reuse a standard read-only configuration from `WorldJson`. Hashing feeds the
same native serializer bytes to SHA-256 without retaining another whole payload.
The public `CreateOptions` factory and specialized strict readers keep independent
mutable policies. Exact compact bytes, enum/set converters, duplicate/unknown
property refusals, caller isolation and concurrent hashes are qualified. This
follows [.NET serializer reuse guidance](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-configure-options)
with actual local tests, rather than assuming serialization behavior changed.

Twelve fresh-process case runs retain baseline, streaming-only and final measured
phases. Final cumulative allocation was12–15% lower in these observations; total
phase allocation remains large. Peak memory and timing vary, and publication retry
receipts can change packet size without changing world data. Neither these
measurements nor the1024 access ceiling establish general world-scale throughput,
independent terrain paging, streaming or runtime acceptance. See
[commands, exact comparisons and limits](evidence/2026-10-30-composed-resources-verification.json).

## Stable catalog variation

Optional `MetadataPart.Variation` selects among explicitly permitted archetypes of
the same semantic `Kind`. Its `ArchetypeIds` includes the part's `ArchetypeId` and
contains 1–32 distinct catalog IDs. Each rule declares `Id`, `Seed`, `Purpose` and
`SourceRuleId`. Unknown candidates, differing kinds, cycles and invalid declarations
reject before expansion. The selected archetype expands its complete nested program
through the existing generator; counts and instance IDs remain owned by the part.

The version-one selector ranks candidates by the lowercase SHA-256 of the compact
UTF-8 JSON string array `["world-variation-v1", seed, ruleId, parentNodeId, partId,
childNodeId, candidateId]`. The lowest ordinal hash wins; candidate ID ordering
breaks ties. Candidate enumeration, unrelated catalog data and the complete catalog
digest do not participate. Changing one rule's seed affects that scope, while adding
instances preserves previous identities. Renaming an identity or changing its
candidate set intentionally changes the selection input.

An optional `Weights` object gives every eligible archetype an integer from 1
through 16. A candidate receives that many independent hash tickets; ticket zero
uses the original array above, and later tickets append `"ticket"` and the
decimal ticket index. The lowest hash wins. Unit weights therefore produce the
same choices and hashes as an unweighted rule. Larger weights make an alternative
more likely across identities; they do not guarantee a particular finite count.
Weights must cover exactly the declared candidates. The selection work limit
counts tickets before expansion and during replay.

This chooses a catalog alternative, not a new location. Selected footprints,
reservations, access, registered structure variants, service profiles and nested
content use their existing owners. Geometry must fit the ordinary constraints;
failure rejects rather than shrinking, dropping members or silently trying another
form. Candidate eligibility is explicitly game-authored; this is not an automatic
terrain-conditioned or adjacency solver. Variation may be combined with
`Capacity`: the stable selected sequence accumulates each variant's own source
capacity until the required quantity and minimum count are satisfied. All eligible
sources are validated first; a unit mismatch, missing source or exceeded count
limit rejects instead of substituting the prototype's capacity.

`VariationPlan` saves scoped requests and per-instance choices/hashes. Nodes carry
`VariationKey`, so missing choice records are detected. `MetadataVariationPlanner`
recomputes decisions and membership during read/export. Requests are limited to
4096, use the existing 100000-node ceiling, and replay has a four-million work-unit
cap with cancellation. The existing content stage caches the plan; no extra stage
or parallel generator is added. Population composition preserves source allocations
including their rules, not just whichever form happened to win.

The schematic exposes selected form, permitted alternatives, rule, seed and source
when an instance is inspected. It displays saved geometry; C# owns authoritative
hash and physical validation. [Example](../examples/metadata/variation/README.md)
and [verification](evidence/2026-10-27-world-variation-verification.json) cover root
and nested choices, stable edits/order/count growth, generic and registered source
dimensions, protected population, failed-edit rollback, actual CLI/browser output
and independent hash/geometry oracles. Broad expressive range, mixed capacity and
whole-program scale remain open.

## Protected gameplay areas and sightline envelopes

Optional `Context.Protections` declares tagged 2D keep-clear spaces through the
existing `MetadataContextPlanner` and `CatalogContextPolicy`. Every rule retains
`Id`, `Kind`, `Purpose`, `SourceRuleId` and nonempty blocking `Tags`. Declare exactly
one of `Area` or `Sightline`. `Kind` is game-authored metadata, not a genre switch.

- `Area` is a polygon in world coordinates, or in axes parallel to world axes
  translated by `RelativeToNodeId`. No implicit rotation or inferred footprint.
- `Sightline` names `From` and `To` anchors, each with `NodeId` and a two-dimensional
  `Offset`, plus a positive full `Width`. Both nodes must have resolved positions.
  The existing traversal envelope bounds the segment with circumscribed 16-gons;
  its radius is conservatively enlarged by about 1.96 percent. Coincident endpoints
  reject. This is a declared 2D obstruction envelope, not height-aware visibility,
  terrain occlusion, optical simulation or a substitute for native Godot raycasts.

Matching tags select blocking physical allocations. Their entire footprints,
placement reservations and catalog site features are checked by the same context
exclusion policy; touching is a conflict. Existing placement feedback can reconsider
eligible candidates but cannot drop required members or weaken the rule. At most
64 dynamic protections and 256 combined static/dynamic exclusions are supported,
with the existing context/placement work budgets and cancellation.

`ContextPlan.Protections` saves the exact source definition, resolved polygon and
optional centerline. C# rebuilds them during document replay. The schematic displays
these saved shapes in orange, with purpose/source labels; it does not calculate or
repair physical geometry. It refuses missing records and stale anchored display data.

An explicit `PopulationSource` may contain protections without routes or generated
regions. Source context regions, site tag bindings, separations, static exclusions
and protection definitions must survive final composition unchanged. New context
members/rules may be added. Exact source protection geometry and base members are
preserved within the proposal; changing source inputs requires regeneration.

The [example](../examples/metadata/protection/README.md) and
[verification](evidence/2026-10-27-world-protection-verification.json) cover complete
11-member population, a blocked view, a reservation-only conflict, replay, edit
rollback and actual CLI/browser output. Broader variation, scale, 3D visibility,
artwork and native runtime remain unqualified.

## Population inside generated regions and allocations

`MetadataGeneratedRegionPattern` declares exactly one of `Pattern` (directional
bands), `Contour` (field-derived connected paths) or `Area` (density-weighted
connected strata). These are current purposeful
grammars; neither is a vacant-space fallback. A `WorldContourPopulationPattern`
names an environmental field and ordered `WorldPopulationContour` requests.
Each request declares its value, intent anchor, maximum anchor distance, required
count, spacing, insets and optional reverse direction. The engine intersects the
same piecewise-affine field geometry used by suitability, resolves shared seams,
and selects the unique connected contour nearest that anchor within the limit.
It never switches to another component to obtain more space. Scalar fields may
represent game-authored moisture, temperature or other environmental rules as well
as altitude; no physical climate simulation is inferred.

Open paths start at the lexicographically smallest endpoint; closed paths start
at their lexicographically smallest vertex and first traverse the smaller adjacent
vertex. Reverse is explicit. The shared arc-length sampler determines all member
positions. Closed paths cannot repeat the starting member. Flat plateaus at the
requested value, branching contours, equally near components, unavailable fields,
insufficient capacity and incompatible footprints reject. Endpoint welding uses
an absolute 1e-8-meter numerical resolution; tangent contacts within that resolution
do not supply usable length. This is bounded planar contour geometry, not an
arbitrary implicit/volumetric field backend.

`PlacementPlan.PopulationContours` retains world-coordinate paths, selected
component distance, closed/open status, member positions, stage/binding IDs,
region/allocation, field, purpose and source rule. C# rederives these witnesses
during full placement replay. Nested placement anchors use the parent frame;
saved contour witnesses stay in world coordinates. The schematic consumes these
exact paths, checks member bindings and displays them as dashed green lines. It
does not generate contours. Browser validation cannot replace engine replay.

Contour limits: 1–32 requests, 100,000 total sampled positions in the standalone
geometry API, 16,384 evaluated pieces, 4,096 extracted segments/graph vertices,
64 vertices per input region cell, and up to 200 million charged work units
(standalone default 5 million). Metadata search stages retain sixteen physical
demands; explicit prescribed stages allow 1,024 exact anchored demands within
the shared placement budget. These are explicit
bounds, not qualified large-world population throughput. See the
[mixed-profile example](../examples/metadata/field-contour-population/README.md).

`Population.Region` selects a generated `RegionId` and optional `AllocationId`
from the same explicit source phase used by route/network population. Region-only
worlds need no route or network. The source's actual region/layout plans must
remain unchanged within the completed proposal. Changing terrain in both source
and complete recipes regenerates them and the dependent content.

`WorldRegionPopulationPattern` declares a direction in degrees and ordered bands
relative to the selected shape's seed. Each band names its exact count, normal
offset, spacing, start/end inset and `SpanAnchor` distance along that direction.
The sampler intersects the convex-piece union with each line and merges touching
cell intervals. Only the connected interval whose interior contains `SpanAnchor`
is selected. A hole never becomes extra usable length. Missing spans or insufficient
capacity reject; no item is omitted, scattered or shifted to another interval.
Internal terrain cells do not become object slots. This optional local grammar
neither generates roads nor imposes a world grid.

Expansion derives exact anchors and binds every concrete demand to the existing
region/allocation containment owner. Complete source-sized footprints and required
reservations must fit, including across holes and irregular boundaries. Registered
site features already fit within their source reservation, so that envelope remains
authoritative. Conflicting demand scopes reject. Physical rotation stays explicit
on the binding. Nested stages convert source-world positions to the existing
parent frame once; the parent must exist in the source world.

Band counts must exactly cover the selected catalog members, in declared band
order and numeric child order. Geometry supports up to 32 bands and 100,000 samples
under bounded work; metadata uses the selected execution mode's stage limits
(unrestricted search:16 demands/32 anchors; validated connected growth:128/256;
prescribed:1,024/2,048).
Sampling charges the placement budget. Public geometry calls use a
5-million-work budget and expect generated region geometry; metadata additionally
replays the source provenance. These bounds do not establish world-scale capacity.

See the [region population example](../examples/metadata/region-population/README.md)
and [verification](evidence/2026-10-27-world-region-population-verification.json).
Its terrain, local-allocation and nested cases qualify this grammar, not arbitrary
ecological distributions, sightlines, controlled variants or native game behavior.

## Population derived from generated routes, networks and interiors

An optional recipe `PopulationSource` contains explicit base recipe/catalog inputs.
The same generator executes that base phase before the complete recipe. A binding's
`Population.Route` names a generated connection, direction, spacing, start/end
insets and normal offset. It contains no authored path coordinates. The generated
source world is retained in `PlacementPlan.Definition.SourceWorld`; the original
authoring recipe remains the input hash source.

The connection can be an explicit routed link, an engine-selected network edge,
or an accepted generated interior passage.

For reusable nested programs, `Population.Route.Parent` may instead declare
`MetadataParentRouteSelector(ConnectionKind, ConnectionIndex=0)` with
`ConnectionId=null`. Exactly one mode is required. The selector uses its actual
nested-stage parent in the accepted source world, matching incident connections
of the declared game-authored kind and selecting the zero-based ordinal by
ascending ordinal connection ID. Input array order, distance and spatial vacancy
do not choose a connection. The default path departs from the parent; if it is the
canonical destination, the saved reverse trace is mandatory. `Reverse` flips that
relative choice. A missing selected route/direction refuses without substituting
another incident connection.

The parent must exist with physical coordinates. Ordinals range from 0 to 127.
Selection uses the current placement work/cancellation ledger and a bounded
ordinal prefix, not a copy/sort of the complete incident graph. A non-null parent
selector serializes explicitly; ID mode omits the absent optional field. There
are no aliases or compatibility fallback paths. Source preservation, full-width
access, source sizes and parent containment remain independently mandatory.
See the [reusable parent program examples](../examples/metadata/parent-route-programs/README.md).

Explicit references use the generated connection ID, including for reverse-direction sampling. The
same arc-length sampler handles both; a network does not require `RouteRules`.
Network edge IDs are semantic identities derived by the network owner, not path
coordinates. A missing edge rejects the proposal rather than choosing another edge.

Existing base members receive exact anchor constraints. Their node metadata,
positions, footprints, reservations, entrances, structure definitions, connections
and routes must survive. Existing generated regions/local layouts and environmental
field definitions also remain unchanged. Full population footprints, reservations
and surroundings cannot intersect protected base passage envelopes or entrances,
even when the new item is not a solid obstacle. Conflicts reject the proposal.

The route owner validates and retains the source paths instead of replanning them
around detail. `PreservedConnectionIds` identifies those routes and
`PreservationWork` records their new traversal-validation work. Their existing
route result search counts/work remain the source search receipts. Normal routes
still use the same search owner. Saved-world validation replays the base world and
checks the complete result, including the original population template.

The network owner similarly retains complete source networks: destination identities,
purposes and entrance positions, options, selected edges, connectivity analysis and
forward/reverse paths. `MetadataNetworkPlan.PreservedRuleIds` lists them and its
`PreservationWork` records current traversal checks under each rule's finite budget.
Their candidate/search receipts remain source-phase receipts. Population cannot
silently add a destination to a preserved network; change the base recipe first.
Additional rules use ordinary network generation through the same owner.

The execution cache adds an optional `population-source` stage, with an explicit
edge into allocation. A detail-only edit can reuse that source; a base terrain edit
recomputes it and moves the derived detail. Source and final catalog/layout
dependencies are both measured for sessions. There are fifteen stage receipts for
this route and fourteen without a source phase. The CLI uses the same
`generate-metadata` and `metadata-session` commands and atomic publication owner.

See the [generated-route population example](../examples/metadata/route-population/README.md).
The [network population example](../examples/metadata/network-population/README.md)
uses a required connection and an engine-selected backbone over three destinations.
One source phase is currently supported; recursive sources are explicitly refused.
Generated region population uses the same boundary as described above. This is preservation of
qualified geometric obligations, not a claim
of unchanged simulation outcomes, large-world streaming or full game acceptance.

### Interior source preservation

Interior passages use the existing `Population.Route` connection/direction,
arc-length offset and optional facing contracts. A room-local stage can place its
own catalog parts along an incident source passage. The source phase owns the
room graph and original content; the consumer must retain the exact interior
definition, room membership, walls, openings and directed route records. It cannot
change door widths or remove interior rules. Missing or ambiguous connection IDs
and unavailable reverse directions refuse through the common route-source owner.

The interior owner reconstructs wall/opening geometry, checks new solids against
walls and requalifies every saved full-width path against current terrain/walls/
solids. It does not search replacement paths around new content. Preserved route
search receipts remain the original records; `InteriorPlan.Work` charges the new
geometry and traversal-validation work under the existing ledger. Native source
and final replay validate the original floor/path proof before trusting preserved
routes, including standalone `MetadataInteriorPlanner.ValidateDocument`.

All source bodies and new population footprints/reservations remain subject to
parent containment, source separation and protected passage checks. Geometry
conflicts remain refusals. A fixed path offset may collide with a fixture or another
passage; it does not authorize moving/removing that fixture or shrinking members.
The JavaScript inspector consumes saved geometry and checks source-derived facing
witnesses; it does not replace native source replay.

The [interior population examples](../examples/metadata/interior-population/README.md)
retain complete branching, rotated and compact declarations with all required
equipment. Full core design and arbitrary interior/volume support remain open.

## Purposeful physical population

### Density-weighted area population

`Population.Region.Area` selects `WorldAreaPopulationPattern`: optional
`DensityFieldId` (null means uniform density), `AxisRotationDegrees`,
`MaximumCells` and `MaximumDepth`. Required catalog counts still determine the
number of members. The standalone `WorldPopulationGeometry.Area` takes a
`WorldAreaPopulationDomain` containing the generated `Regions`, `RegionId`, and
optional `Layouts`/`AllocationId`. It replays both upstream plans against the
current environment before consuming their field-compatible cells. Stale or
altered source plans reject. Re-sweeping those cells is unnecessary and can
introduce roundoff fragments at coincident terrain seams.

The engine integrates nonnegative affine/clamped density analytically over
convex pieces. Each recursive step computes the geometric area's principal
axis, applies the declared angular offset and finds a cut balancing density mass
in proportion to the two required integer member counts. One member represents
each final connected stratum at its density-weighted centroid. Open numerical
cut searches use the shared raw clip integrator; final topology uses the common
geometry cleanup and connectivity owner. Signed fan moments preserve collinear
roundoff contributions. An existing geometric event is used only when it retains
the solved mass tolerance; coordinates are not rounded to a placement grid.

Negative density, zero total mass, a zero-density centroid, a centroid outside its stratum or valid terrain,
disconnected leaves, unresolved mass/area accounting, stale source plans and
exhausted budgets reject. The engine does not perturb centroids or look for empty
positions. Actual source-sized footprints/reservations must still fit the selected
habitat and pass shared separation, access and route checks. Strata are internal
population measure regions; they are not physical lots, roads or occupied coverage.
A footprint may cross an internal stratum boundary while remaining fully inside
its habitat and separated from other objects.

`PlacementPlan.PopulationAreas` saves each group's stage/binding/region/allocation,
purpose/source rule, exact member IDs and `WorldAreaPopulationPlan`. The version-1
plan retains the pattern, selected shape ID, total area/mass, charged work and all
leaf IDs, world centroids, areas, masses, cells and exterior edges. Nested placement
translates only derived anchors into the parent frame; saved witnesses remain in
world coordinates. Full document replay rederives all witnesses. The schematic
uses them directly in a separate **Population strata** layer, off by default;
habitat regions and physical objects retain their own layers. JavaScript checks
structural accounting and bindings; it does not solve the partition or replace
engine validation.

Bounds: 1–1,024 required members, 1–4,096 source region cells, 64 input vertices
per cell, 1–16,384 retained cells (default 4,096), depth 1–32 (default 32), at most
64 mass-cut iterations per branch, and the shared work budget (standalone default
5 million, maximum 200 million). Mass solves use a 1e-10 relative target; final
whole/leaf accounting checks use the documented numerical guards in the owner,
including 1e-7 relative equal-leaf mass. Physical and presentation limits remain
separate. The schematic admits at most 32,768 area cells/131,072 vertices.

[Inputs](../examples/metadata/area-population/README.md) retain the same six
compounds and all 194 required environmental members. A reversed density field
moves all environmental members while retaining source compounds, region/layout
geometry and nineteen source connections. Three habitat areas total approximately
3,725.202 square meters; that is represented habitat, not physical coverage.
[Evidence](evidence/2026-10-29-area-population-verification.json) includes independent
mass/centroid equations, midpoint quadrature, exact rational habitat totals,
holes, equivalent triangulations, nested frames, replay and edits. Regional intent,
counts, density fields and habitat rules remain authored; broad world generation
and Godot acceptance remain open.

### Terrain-derived content demand

Optional recipe `TerrainDemands` connects a generated `PopulationSource` to
catalog expansion. `MetadataTerrainDemandPlanner` executes before content,
measures the source's actual replayed region/allocation and determines the exact
member count. This uses the existing source phase and area-integration owner;
it does not infer a game genre, scatter objects or create a second generator.

Each rule supplies `Id`, `Purpose`, `SourceRuleId`, one exact `ParentNodeId` /
`PartId` binding, `RegionId`, positive finite `AmountPerMember`, optional
`DensityFieldId` and `AllocationId`, `MaximumCount` (1–1,024, default 1,024) and
`MaximumCells` (1–16,384, default 4,096). Up to 32 rules share `MaximumWork`
(default 5 million, maximum 200 million). Amount per member is in density-field
units times squared source surface units; absent density means uniform one, so
it measures area directly. These rates are authored game intent, not ecological
or real-world population estimates.

Count is `floor(measured mass / amount per member)`. An exact floating-point
integer is accepted; a noninteger within `1e-9 * max(1, abs(quotient))` of the
nearest integer refuses as numerically ambiguous. Empty demand and exceeded
limits refuse. The targeted catalog part's `Count` becomes its minimum, which
cannot be lowered; `CountParameter` conflicts with terrain-owned count. An optional
`Capacity` rule is checked against exactly that count, including each selected
variant's own capacity. A shortfall rejects with
`METADATA_CAPACITY_EXACT_COUNT_UNSATISFIED` and identifies exact count, provided
quantity and required quantity. The capacity receipt records `ExactCount`, which
native validation and the schematic cross-check against terrain demand.
Each rule must bind exactly once. Counts are never clamped to make geometry fit.
All nested constituents, source footprints, reservations, ports and passages
still pass through the existing physical owners.

`WorldPopulationGeometry.MeasureArea` shares region/field validation and analytic
integration with area placement, without choosing a representative point. A
holed region can be measured even when its centroid lies in the hole. Zero mass
is a valid measurement but cannot satisfy a positive demand rule.

`TerrainDemandPlan` retains definitions, source recipe/catalog identities, exact
area/mass observations, required counts and charged work. Each direct generated
member stores `TerrainDemandRuleId`. Read/export replays the source measurement,
checks exact child IDs/counts and refuses missing/altered plan/member provenance.
Membership uses the shared numeric child-identity check, so similarly prefixed
part names remain separate; duplicate members cannot satisfy count accounting.
The content stage depends explicitly on `terrain-demand`; changed terrain or
density can alter the hierarchy before placement. Session replay and failed-edit
rollback use the same generator. JavaScript checks saved demand/count/member
facts without implementing another integral or placement algorithm.

The [reference inputs](../examples/metadata/terrain-demand/README.md) retain all
six source compounds and nineteen paths. Baseline habitat demand creates 219
environmental members; density multiplied by 1.1 creates 241. Catalog minima stay
96/96/2, giving complete 248-node and 270-node packets.
[Local evidence](evidence/2026-10-29-terrain-demand-verification.json) includes
independent exact integral totals, terrain exclusion/quantity counterfactuals,
physical geometry, replay/rollback and CLI/JavaScript acceptance/refusal.

Known passages and port approaches now qualify base terrain plus every exact
solid intersection directly. A 269-physical-member packet retains 257 solids and
all source routes without materializing them as authored terrain exclusions.
The physical owner retains its 1,024-site and 16,384-combined-vertex ceilings;
this is not a throughput claim. The subsequent placed-solid query backend below
also supports new route/network search and authored trace compilation without
converting these solids to authored masks. The 256 limit applies to authored
terrain masks. Automatic world intent, broad composition/scale and browser
acceptance remain open. Godot is deferred.

### Shared physical population contract

`MetadataPlacementBinding.Population` adds a purpose/source rule and exactly one
geometric source: authored `Path`, `Boundary`, named `Sockets`, generated `Route`, or generated `Region`.
It works on an individual
binding or a `PartId` template. Required catalog counts remain authoritative.
Expansion derives one exact anchor and zero-distance relation per member, then
uses the existing terrain, footprint/reservation, overlap, access and route
planners. An item that cannot fit rejects the world; it is never omitted or moved
to an arbitrary vacancy. The original template remains replayable provenance.

Paths sample arc length with start/end insets and a signed planar normal offset.
An exact bend uses the incoming segment's normal. Boundary patterns select an
explicit starting edge and span of a simple polygon, with winding-aware inward
offset; no rectangle approximation or perimeter wrap is permitted. Full source
footprints and reservations must fit inside that boundary. Offset samples are
not a globally equidistant offset curve: sharp corners may require a revised
span/spacing, and invalid placements reject. Footprint rotation stays explicit
on each binding; tangents do not silently rotate source objects.

Sockets name exact expanded node IDs and must cover the selected set once each.
Part members use numeric semantic indices, so child 10 cannot precede child 2.
Socket declaration ordering does not change positions. Nested population uses
the existing stage coordinate frame, translated by its placed parent. The shared
`WorldArcLengthPath` also owns metadata path and visual path sampling;
`VisualRegionPatternGenerator` now requires an explicit boundary start edge and
uses the same boundary geometry, replacing its rectangle-only implementation.
Metadata population requires no mockup or artwork.

See the [physical population example](../examples/metadata/population/README.md).
Search stages retain 16 physical demands, 32 anchors and 128 relations. Explicit
prescribed stages accept 1,024 exact anchored demands, 2,048 anchors and 8,192
relations; they validate one complete assignment without alternative candidates.
Generation/work budgets still apply. Measured qualification covers the
[prescribed population example](../examples/metadata/prescribed-population-scale/README.md),
not general large-world throughput. The source phase supports generated-route detail;
additional region interior grammars, protected sightlines, controlled variant selection and NPC/runtime
behavior remain separate work. Authored path grammar does not imply a road there.

## Reproducibility of generated data

The reproducibility contract is for identical typed inputs, authored ordering,
seed streams, Core build, installed extension code/configuration, operating
system, architecture and .NET runtime. It covers generated metadata/geometry and
source replay, not timestamps, operation correlations or byte-for-byte copies of
the original input file. Preserve the environment and source digests with a
qualification. Cross-platform or cross-build bitwise equality is not promised.

Node identities derive from recipe and part paths; chunk members use kind plus
node/path identity, with one canonical owner and closed seam references. Region
and domain addresses also include their owner ID, so two instances can reuse a
local template's IDs. Storage size and load order do not prescribe world layout.
Authored list order can express preferences and tie breaks; arbitrary reordering
of a recipe/catalog is not a semantic no-op. Request/release/load-order tests hold
the authored program fixed or exercise an explicitly order-independent owner.

Session execution identity includes the current Core module ID. Deterministic
extensions implement `IWorldGenerationDependency`; their identity includes type,
assembly/module and a lowercase SHA-256 covering result-affecting configuration.
Changing a dependency, source pin, saved receipt or implementation cannot silently
reuse accepted output. Checkpoint restore replays source history and rejects
incompatible identities; it is not an old-build migration mechanism.

Reload equality uses zero numeric tolerance: compare canonical serialized typed
documents/member sequences and original sample bits. A semantic counterfactual
comparison declares its own affected outputs, units and tolerance. Geometry
predicates retain their owning algorithm's tolerance (for example, the connected
region owner uses `1e-8` in its coordinate units); that tolerance is not permission
to move geometry during reload or silently resize source objects.

Resource limits are explicit per request/owner. Regional retained packets and
execution caches are capped at 128 MiB of serialized retention, not at 128 MiB of
managed heap or temporary allocation. Geometry records are bounded at 8 MiB and
their package at 256 MiB; normal authored file limits and generated partitioned
transport have their separate bounds. Exhaustion refuses instead of evicting
required content, truncating sources or substituting another layout. Actual
time, process peak working set and cumulative allocation must be reported with
their measurement scope; they are not interchangeable memory measures. Runtime
streaming and native consumers remain downstream capabilities.

## Executable generation sessions

Optional `MetadataNetworkRule.RoutingBoundary` is an authored simple polygon in
the surface unit (3–64 vertices). The existing route solver must keep the complete
circular passage envelope within it; hierarchy `RegionNodeId` still selects
destinations independently. The region must lie wholly inside the world domain,
including its edges across concave boundaries. An unreachable regional network
fails rather than escaping its domain. Existing guidance domains must also fit
inside the routing region under their normal validation rules.

`WorldSurfaceQuery.RestrictTo(boundary, token)` returns a detached regional query
with the same surface/source identities. It conservatively drops only sources
whose closed AABBs are disjoint from the region, retaining touching/crossing/
containing sources in full. Source world content is untouched. Validated immutable
height queries are shared without field regeneration or buffer cloning. Complete
field inputs retain their complete original lattice/buffer. A shared regional cell window plus one-cell halo controls both
terrain-feature reads and the private dependency key, including every original
vertex used by those cells and the complete field header. Remote sampled edits
outside that window can preserve a policy search. Complete terrain recipes remain
conservative provenance dependencies. Complete projected query hashes drive ordinary network memo reuse,
so unrelated affine terrain/physical edits can preserve regional searches while
local/shared-boundary changes recalculate them. Global containment, full source validation and entrance checks still execute
before lookup. Private windows never replace exported source samples. Whole-field
validation, parent-stage reuse and persistence retain their existing owners.
[Regional qualification](evidence/2026-10-30-network-regions-verification.json) and
[sampled dependency/geometry qualification](evidence/2026-10-30-sampled-regions-verification.json).


Generation and domain-session reports optionally include `NetworkExecutions`:
world/rule/phase, exact input/output SHA256, `Source` (`Computed`, `Current` or
`Accepted`), status and canonical `LogicalWork`. This is bounded execution evidence,
separate from world work receipts. The session-owned private network memo serves
allocation feedback and final generation; it is never loaded from checkpoint JSON.
Complete frozen query geometry (terrain/heightfield/walls), physical obstacles,
destinations, canonical rule/settings, space and engine identity determine reuse.
Scope/count/entrances validate before lookup. Any projected query/solid dependency
change invalidates, even if the old selected paths remain feasible. Nonregional
queries keep complete-field dependencies.

At most four current policy entries and 1,028 report records share the existing
128 MiB accepted-cache serialized-byte bound. Hits return detached results and
charge original logical shared-budget work; a warm cache cannot bypass a limit.
Whole-stage reuse retains active entries, and removed policies are evicted.
Publication/read validation still replays its network checks. Finer terrain-region
invalidation and parent-stage locality remain open. [Qualification and limits](evidence/2026-10-30-network-memo-verification.json).


`MetadataGenerationSession` runs the existing generator through fourteen standard
computational stages. `Snapshot` returns a detached copy. `Apply` checks expected
recipe/catalog digests, protected input pointers and the proposed inputs before
committing a new world. `ApplyAndPublishAsync` commits only after the existing
publisher's atomic move succeeds. Failures/cancellation retain the previous
accepted world and cache. An optional `IProgress<MetadataStageExecution>` observes
completed stages; cancellation is checked at the boundary as well as inside the
underlying planners.

Input and dependency digests determine reuse. Dependent stages invalidate even
if an upstream output happens to remain equal. Coupled local layout, placement
and feasibility feedback share one `allocation` stage. Content expansion is
independent of terrain; positioned source nodes are checked in a separate stage.
Semantic `MetadataLayerPlanner` declarations remain membership/dependency data,
not this execution scheduler. Ordinary full generation uses the same stage code.

Protected recipe/catalog JSON pointers are captured with their exact value
digests; missing or changed protected values fail before regeneration. Edit
reports record semantic changed paths and build-bound input/output digests.
Numeric arrays (including coordinate vectors and sampled fields) are one authored
buffer change with complete before/after SHA256 values. Full initial/edited
buffers remain in accepted source history; the report does not omit samples.
Individual array-element protections still resolve and authenticate before
regeneration. Nonnumeric record arrays retain their member-level paths. The
4096 report-entry limit is unchanged, and malformed buffer dimensions still fail
at their schema owner. Input/checkpoint/publication size limits are unchanged.
Dictionary/property ordering can conservatively invalidate a cache even when
the semantic changed-path list is empty. No cross-platform floating-point
guarantee or minimal spatial invalidation is implied.

The [session example](../examples/metadata/regeneration/README.md) documents the
CLI, limits and replay behavior. Checkpoint reload reconstructs accepted state
from source history, validates recorded receipts and refuses a different engine
build identity. Persisted geometry is never loaded into the execution cache.
Selected layouts and registered catalog policies implement
`IWorldGenerationDependency`: their configuration SHA256 covers behavior-affecting
settings outside explicit method inputs. Type, assembly and module identities are
measured separately. Registered definitions retain existing source provenance and
a digest of actual resolved definitions; composed policies include imported
dependencies. Unselected extensions do not block a session. Unversioned selected
extensions or malformed digests are refused; ordinary generation is unaffected.

Schema-2 checkpoints record dependencies for initial generation and every edit.
`Apply`/`ApplyAndPublishAsync` accept an explicit replacement generator. Changed
dependencies invalidate content and its dependents even when input JSON is equal.
Checks before/after generation and before publication reject observed version
changes without committing proposed state. Extensions must remain stable during
the operation: this trusted contract does not isolate dishonest or concurrently
mutating code.

Replay requires exact recorded versions. An optional
`IMetadataGenerationDependencyResolver` supplies already-installed historical
generators, whose identities are rechecked. It never downloads or executes saved
paths. The CLI uses existing catalog/plugin discovery and rejects unavailable
versions. Earlier checkpoint schemas/builds have no compatibility loader. See the
[registered example](../examples/metadata/versioned-dependencies/README.md).
Whole-stage reuse does not qualify chunks, seams or the complete S08 gate.

## Boolean whole-footprint suitability

`WorldFootprintSuitability.Matches(footprint, maximumWork, maximumPiecesPerRegion,
token)` returns exact hard eligibility without allocating a complete report.
Use `Evaluate` when callers need ranges, rule decisions or placement candidate
geometry. Both retain complete coverage, terrain-kind constraints and every hard
field range, including interior extrema from affine, clamped and sampled fields.
`Matches` is not a center/vertex-only check: it visits the covered terrain/field
pieces and rejects as soon as any hard constraint fails. A query fully inside
an exclusion or priority overlay still samples that source correctly.

Only the boolean path localizes irrelevant terrain boundary events. The existing
report/placement geometry decomposition remains unchanged. Existing work/piece
limits, immutable source validation and cancellation apply. Region-bound route
legs use this boolean owner after containment and full-width terrain/solid checks.
Probe and final route graphs share a request-local cache keyed by exact directed
point pairs; no cache crosses terrain/profile/constraint requests. Failed approach
work exposes `RouteRequestId` and `InterfaceId` in the exception data. Both graph
and work limits remain enforced. Older work/check receipts may differ and should
be regenerated through the current engine; no compatibility path is provided.

The [mixed-region reference](../examples/metadata/mixed-route-regions/README.md)
retains all declared parent/child source geometry across guidance and water
counterfactuals. [Verification](evidence/2026-10-30-mixed-route-regions-verification.json)
records 53 predicate/report/analytic comparisons and independent full-envelope
checks, with the original budget and stale-test failures preserved.

## Region-local route alignment

`WorldTraversalProfile.Guidance` optionally adds soft alignment preference to the
existing terrain-aware route engine. Each source-attributed scope contains a
simple polygon and one to four **undirected unit vectors**. The same profile can
describe planned paths in one area and free-angle travel beyond it; game authors
choose the axes and extent. The engine does not infer a city or mandatory grid.

Example optional profile member (coordinates use the surface's declared unit):

```json
"Guidance": [{
  "Id": "local-axes",
  "Purpose": "Align paths within this region",
  "SourceRuleId": "paths:local-alignment",
  "Boundary": {"Vertices": [
    {"X":0,"Y":0}, {"X":12,"Y":0},
    {"X":12,"Y":20}, {"X":0,"Y":20}
  ]},
  "Axes": [{"X":1,"Y":0}, {"X":0,"Y":1}],
  "CostPerMeter": 2
}]
```

Omit `Guidance` when unused. A supplied list must contain 1–16 unique-ID scopes;
each polygon has 3–64 finite vertices and lies wholly within the domain boundary.
Axes must have unit length within 1e-9 and cannot duplicate an undirected direction.
`CostPerMeter` must be finite, positive and at most 1e6; an empty/zero-cost policy
is rejected. Inputs are immutable snapshots preserving authored order and values.

For each segment displacement `d`, the engine splits exact polygon-covered
intervals and adds `coveredFraction × CostPerMeter × min(abs(cross(d, axis)))`
(divided by the axis norm). Boundary intervals count; isolated touches have zero
length. Overlapping scopes add. `WorldRouteCost.Guidance` is a separate nonnegative
term in `Total`, alongside terrain, ascent, descent and new-passage cost. The zero
term and null policy are omitted in JSON; unchanged unguided worlds preserve their
prior serialized output. Replay validates the source policy and complete cost.

The planner proposes finite bends and scope entries from request endpoints,
authored waypoints, polygon boundaries and axes. Existing candidate/edge/work
budgets apply, with attributed refusal when exceeded. Full-width terrain, slope
and solid constraints remain mandatory. Reuse, network selection, interfaces and
session edits use this same cost. `LowestCost` searches the checked finite graph;
it does not establish general continuous global optimality. `FirstFeasible`
retains its explicit non-optimal behavior. Contour/tensor fields, turning radius
and automatic urban street growth are separate capabilities.

The JavaScript viewer displays the exact saved polygon as an unfilled dashed
outline, reports source axes/rate and includes it in SVG export. It does not
calculate route geometry. [Qualified cases and limitations](evidence/2026-10-30-route-guidance-verification.json).

## Terrain-derived route search

The common route planner now extracts boundary bends of heightfield triangles
whose slope or kind violates the selected traversal profile. Shared edges of
adjacent impassable triangles cancel; exactly collinear subdivisions are removed
before the existing clearance-offset candidate search. The shared heightfield
query owns gradients. Generated and imported fields use the same path. Every
accepted edge still passes full surface, overlay, solid and swept-width checks.
Field samples never become mandatory road segments or a settlement grid.

Heightfield route results declare `terrain-feature-visibility-v1`. Existing
candidate, edge, work and cancellation bounds apply; complex boundaries can
exceed those bounds. This finite graph does not guarantee the global continuous
optimum or prove continuous infeasibility when disconnected. Raw field features
may remain candidates underneath overriding patches; final checks use the
effective surface. It does not discover pure elevation-cost extrema on otherwise
traversable slopes, or implement incremental edit invalidation.

The [ridge-pass example](../examples/metadata/landform-routes/README.md) changes only
landform control paths while retaining the complete fixed physical program.

## Adjoining physical programs

`WorldPlacementRequest.AllowBoundaryContact` explicitly permits sibling footprint
or reservation boundaries to meet when `MinimumSeparation` is zero. It defaults
to false. Positive spacing still requires a gap. The common placement owner
triangulates simple polygons through the existing geometry owner and rejects
intersection area above the existing 1e-8 area tolerance. Identical outlines,
containment and overlapping clearance reservations are not valid boundary contact.
Both concave and rotated source polygons are covered by focused checks. This
permission does not create a traversable connection.

The [single-floor example](../examples/metadata/interior-journey/README.md) joins
room and corridor floors without changing their source dimensions. Its internal
and external journey legs use the same named doorway approaches so adjacent paths
meet exactly. Every selected path still qualifies its full width against floor
bounds and solid contents. Centerlines that already fail terrain/traversal checks
are rejected before expensive swept-envelope checks; accepted routes receive both.
See [verification](evidence/2026-10-27-world-interior-verification.json) for exact
floor area, physical content, negative cases and exported coordinates. Authored
floor geometry is not a procedural dungeon, wall mesh or native game scene.

## Directed gameplay progression

### Generated progression topology

Optional recipe `TopologyRules` generates the graph before physical allocation.
Each rule declares `Id`, `Purpose`, `SourceRuleId`, `ScopeNodeId`, `StartNodeId`,
an exclusively owned `ConnectionKind`, ordered `Stages`, a `Seed`,
`MaximumDegree` (2–32), exact `ExtraConnections` and a finite `MaximumWork`.
A stage declares its `Id`, `Purpose`, mandatory `ExitNodeId`, disjoint
`MemberKinds` selectors, inclusive `MinimumMembers`/`MaximumMembers`, and optional
`MinimumExtraConnections` (default zero) and optional `MaximumExtraConnections`.
A maximum of zero forbids extra connections in that stage; an omitted maximum
imposes no additional stage cap. Each minimum must not exceed its maximum, and
the exact `ExtraConnections` total must lie between the sums of stage minima and
maximum capacities. The bounded search enforces these limits on additional
connections inside each stage. All matching descendants participate; start and exits are excluded from member
counts. Different stages share only their declared transition. No position is
inferred from a kind or a seed.

Optional `Directed: true` generates one-way stage-tree edges from reachable
parents to newly added destinations. By default additional connections follow
the same seeded member order as forward shortcuts, yielding an acyclic graph.
Optional `ReturnConnections` requests an exact number of those additional
connections as directed return links. Each return link closes an existing
stage-tree path; its saved connection properties include `return: true`. The
bounded search can reconsider loop selection and parent assignments to satisfy
that demand. It rejects infeasible requests and never reduces content. A return
count requires `Directed: true` and cannot exceed `ExtraConnections`.
Every selected destination remains reachable from the start, and intermediate
stage exits remain mandatory. A return link inside a stage does not imply a
return journey across the entire world; explicit progression rules can require
that journey and reject an unsuitable topology. Omitting these options retains
bidirectional edges and existing output. Physical routing respects each edge
direction. The shared-coordinate regional planner propagates it to cross-region
links; the schematic checks reachability and validates return witnesses.

The engine grows degree-limited stage trees and selects the exact requested
number of local loop connections with bounded, iterative constraint search.
The stable seeded edge order guides selection; earlier loop choices are undone
when they consume capacity needed by the remaining demand. A residual-degree
and eligible-edge upper bound prunes impossible combinations. Every inspected
candidate consumes the existing work budget, and cancellation remains active.
No partial loop selection is published. Seeded choices operate inside this
declared grammar.
Mandatory exits automatically produce progression rules/witnesses. The final exit
is the goal; removing an earlier exit must make it unreachable. Budgets, missing
members, conflicting connection-kind ownership and unsatisfied loop demand reject
the request. If the first tree cannot satisfy the loop demand, the engine searches alternative
parent assignments in stable order using the same aggregate work budget. Exhausted
search means no solution was found within that ordered tree-construction model;
work exhaustion reports `METADATA_TOPOLOGY_WORK_BUDGET` instead. It does not
enumerate every possible graph or relax the requested constraints. Saved plans are rederived with the
current algorithm, including measured work; regenerate older topology packets
when their derivation differs.

Version-1 `TopologyPlan` saves normalized rules, memberships, connections and work.
Read/export rederive it and require exact connection/progression provenance.
Generated connections are ordinary inputs to placement feedback and `RouteRules`.
The topology stage depends on content; changing its rules invalidates allocation,
networking and dependent routes without requiring unchanged catalog expansion.
Limits: 16 rules, 16 stages per rule, 256 selected destinations per rule, 32,768
extra connections per rule subject to degree/connection budgets, and 20 million
declared total work units. This is bidirectional staged topology; it does not
infer physical rooms, key/inventory logic, combat or arbitrary gameplay.

See [procedural topology examples](../examples/metadata/procedural-topology/README.md)
and [verification](evidence/2026-10-28-world-topology-verification.json).

### Placed solids and bounded new-route search

`WorldSurfaceQuery` now snapshots optional `physicalObstacles` separately from
its authored `Definition`. `Definition.Exclusions` remains the base terrain masks
(maximum 256). `PhysicalObstacles` admits up to 1,024 placed solids under the same
16,384 combined polygon-vertex limit; `Exclusions` exposes the immutable union.
Point sampling, public traces, corridor containment and public area queries include
both layers. The metadata access owner supplies exact classified footprints;
reservations are not silently converted to solids. Metadata persistence keeps
physical geometry in its access/placement plans, and replay reconstructs the query.
Routing consumers retain the query or explicitly pass both layers. Interior and
corridor callers now do so; copying only `query.Definition` loses placed solids.

Traversal qualifies every exact circular-envelope solid intersection before
terrain work. Physical solids do not become terrain-arrangement edges: terrain
trace/area checks operate on the base fields after the complete physical witness.
Junctions qualify their circular physical envelope separately. This removes the
irrelevant arrangement-edge overflow on long passages while retaining public area
query semantics and all physical collision geometry.

`WorldSurfaceRoutePlanner` remains the one finite visibility-graph owner. A bounded
proposal collection (maximum 262,144 distinct points) precedes the existing
2–1,024 retained-candidate cap. Proposals are search features, not placed content.
When proposals exceed that cap, a seed graph of at most 66 nodes uses the same
checked search to seek a feasible incumbent. Lowest-cost mode then retains every
eligible proposal whose start→feature→goal cost lower bound can improve that
incumbent, with a conservative numerical guard. All other proposals are excluded
by the cost proof; every solid still participates in collision checks. If the
potentially winning set exceeds the original candidate cap, generation refuses.
Without an incumbent, the complete eligible set must fit or the budget refuses;
an incomplete graph is not reported as a geometry proof of no path.

If its first probe finds no incumbent for a lowest-cost request, the planner
also tries an endpoint-focused graph of at most 66 nodes. This only supplies a
candidate upper bound; it never replaces the complete winning-set check. Both
probes consume the same edge and work limits. A candidate-limit exception carries
`RouteRequestId` and a `RouteCandidateAudit` in its data, including unexamined
proposals and any incumbent. A failure audit does not certify infeasibility.

First-feasible mode may return a checked seed path with unexamined proposals; it
makes no minimum-cost claim. Edge/work ceilings count both probe and final search.
The usual lowest-cost claim is relative to the complete declared feature graph,
not a globally shortest continuous curve. `WorldRouteResult.CandidateAudit` saves
proposal/retained/probe/bound-pruned/geometry-rejected/unexamined counts, optional
upper cost bound and `CompleteWinningSet`. Arithmetic balances, and full-set
receipts have no unexamined proposals. C# replays the search; JavaScript checks
saved arithmetic and cost/bound consistency without implementing another solver.

[Framework inputs](../examples/metadata/physical-routing/README.md) retain all 270
nodes, 269 physical members (257 solids), six compounds and nineteen exact source
routes while searching a twentieth journey. Its forward search retains 10 of
8,338 proposals under the existing 1,024-candidate policy. Separate 257/512/1,024
solid stress cases retain 22 candidates under the default 256-candidate and
20-million-work limits; independent full-feature Dijkstra and capsule/rectangle
oracles qualify bounded geometry and cost. [Evidence](evidence/2026-10-29-physical-routing-verification.json)
also covers limits, snapshots, source-history replay, tamper and CLI refusal.
These are controlled examples, not universal latency, continuous optimality or
whole-world acceptance. Automatic world intent/composition, broader strategies
and browser/visual acceptance remain open; Godot is deferred.

### Authored progression constraints

Optional recipe `ProgressionRules` declares `Id`, `Purpose`, `SourceRuleId`,
`ScopeNodeId`, `StartNodeId`, `GoalNodeIds`, `ConnectionKinds`, ordered
`RequiredMilestones` and `MaximumWork` (default 1 million, maximum 10 million).
Up to 32 rules, 64 goals and 32 milestones per rule are supported within a shared
50 million work cap. Start, goals and milestones are distinct nodes inside the
selected hierarchy scope. Only selected connection kinds are traversable, and
direction is respected. Links leaving the scope cannot supply a shortcut.

`MetadataProgressionPlanner` requires every goal to be reachable. Removing each
milestone must make every goal and the next milestone unreachable; one successful
path is insufficient when another path bypasses a required encounter. The saved
version-1 `ProgressionPlan` contains normalized rules, deterministic witness paths
and work accounting. Read/export recomputes it against the current graph. Invalid
progression rejects generation before publication through the existing diagnostic
and audit boundary.

The schematic displays the saved journey and structurally checks its nodes,
links, order and direction. C# owns the all-path checks. No terrain, coordinates,
road or artwork is required. See the [branching encounter example](../examples/metadata/encounter-journey/README.md)
and [local verification](evidence/2026-10-27-world-progression-verification.json).
Optional `Capabilities` declares persistent, nonconsumable capability gates:

- `CapabilityIds`: 1–16 distinct game-authored IDs; `InitialCapabilityIds` is a
  subset. Every declared ID must occur in an initial set, grant or gate.
- `Grants`: up to 256 distinct scoped nodes, each granting declared capabilities
  on arrival. Grants do not require a separate runtime interaction.
- `Gates`: up to 512 distinct selected, scoped connection IDs. Every required
  capability must be present before traversal. Bidirectional links apply the same
  gate in both directions; asymmetric travel uses separate directed links.
- `MaximumStates`: 1–100,000, default 65,536, bounds each exact exploration.
  The existing per-rule/aggregate work ceilings and cancellation still apply.
- `RequireReturnToStart`: default false. When true, every reachable state must
  have a path back to the start with its acquired capabilities retained. Checking
  only the chosen goal witness would miss optional reachable traps.

The same planner explores `(node, acquired capability set)` states. It can return
through a junction with a key, rejects required keys behind their own locks, and
recomputes milestone exclusion with the same gate semantics. Witnesses may repeat
a node or connection after acquiring a capability; identical states cannot repeat.
`CapabilityStates` saves the exact acquired IDs after arrival at every witness
node. `ReachableStates` records the explored count. Normalized rules and complete
stateful results replay on read/export. The schematic validates saved acquisition
and gate transitions; C# owns state exploration and all-state return safety.

See the [capability journey inputs](../examples/metadata/capability-journey/README.md)
and [local qualification](evidence/2026-10-29-capability-progression-verification.json).
This is logical progression validation, not a physical map, runtime inventory,
consumable-key solver, combat, timers or reward interaction. Automatic world
composition and physical traversal qualification remain separate work.

This is WorldCreator's shared game-independent composition core. Game-authored urban regions do not define the shared world model. The rejected city generator is removed; the replacement terrain/routes/layout pipeline is unfinished. Optional `WorldMetadataDocument.MapGeometry` carries actual compiled geometry; the generic metadata spatial strategies below produce location coordinates and topology.

WorldCreator supplies a reusable world metadata compiler. A **catalog** defines game-authored archetypes and nested programs; a **recipe** selects a root, parameters, scope and coordinate space. The C# engine expands these inputs into a hierarchy and typed graph. It does not need a mockup, image service, art profile or game engine.

The same algorithm supports arbitrary game-defined kinds. `town`, `dungeon`, `platformer` and `space` are example input data, not cases in the compiler. They are small technical fixtures, not comprehensive game content packs.

## Entry points

Namespace: `WorldCreator.Core.Metadata` in `WorldCreator.Core`.

```csharp
var generator = new WorldMetadataGenerator();
WorldMetadataDocument world = generator.Generate(recipe, catalog, cancellationToken);

// Strict input loading and atomic publication into a new directory:
await generator.GenerateFilesAsync(recipeFile, catalogFile, outputDirectory, cancellationToken);
```

CLI (for integrations and agent execution):

```powershell
dotnet run --project src/WorldCreator/WorldCreator.Cli -c Release -- generate-metadata examples/metadata/dungeon.recipe.json examples/metadata/dungeon.catalog.json generated/metadata-dungeon
```

The agent operates these tools; conversational users do not need to edit JSON or execute commands. The generic CLI route skips settlement manifest discovery when no structure references or explicit settlement catalog flags are present.

## Inputs and output

| Contract | Responsibility |
| --- | --- |
| `WorldMetadataCatalog` | Versioned reusable archetypes, each with a stable ID, arbitrary kind, purpose, JSON properties, parts, connection rules and child requirements. |
| `MetadataPart` | Repeats a referenced archetype using a fixed count or named integer recipe parameter. Parts can own further parts. |
| `WorldMetadataRecipe` | Root archetype, game profile, scale, coverage, remaining scopes, parameters, coordinate space and expansion budgets. |
| `WorldMetadataNode` | Expanded ID, archetype/kind, parent, purpose, cloned properties, optional position and optional structure key. |
| `WorldMetadataConnection` | Kind, endpoints, direction, source rule and game-defined properties. |
| `WorldMetadataDocument` | Hierarchy, graph, scope, recipe/catalog SHA-256 digests and optional resolved structure definitions. |

Recipe/catalog schema version is `1`; generated `WorldMetadataDocument` uses version `2`. JSON uses the exact PascalCase property names and enum names shown in the saved examples. The file reader rejects unknown fields and missing required constructor fields. Each file and authored logical input remains bounded at16MiB; generated documents may use the partitioned transport described below. IDs are bounded strings using ASCII letters/digits and `.`, `-`, `_`, `:`. Node IDs derive from the recipe ID and part path/index, for example `dungeon/floors-0/rooms-5`. Part IDs are unique within their archetype; `self` is reserved for the current node.

Parts and rules expand in ordinal ID order. Counts, parentage, links and positions are deterministic for the same inputs. Digests identify serialized inputs; semantically equivalent reordered input dictionaries need not have the same digest. Adding/removing parts or changing counts can change path-based IDs; there is no automatic saved-game migration.

## Complete published-source reload

`await generator.ReadPublishedAsync(publishedDirectory, token)` reads the existing
`recipe.json`, `catalog.json` and `world-metadata.json` through strict bounded
storage. It validates stored plans, checks recipe/catalog digests, then performs
complete generation using that generator's current registry/layout providers.
The returned world must equal the complete saved result. A missing required
member cannot authenticate merely by repairing layer membership or spatial plans.
Source digest disagreement raises `METADATA_PUBLICATION_SOURCE_MISMATCH`; complete
replay disagreement raises `METADATA_PUBLICATION_REPLAY_MISMATCH`. Missing source,
unknown fields, unsupported providers and cancellation retain their normal errors.
No package files are written by verification.

The CLI entry is `verify-metadata <published-directory>` with existing optional
catalog configuration. It uses the same source replay owner and shared
Log/Audit/Notification services. Generic packages skip unrelated settlement
discovery. Source-only `ReadAsync<T>` retains strict decoding and stored-plan
validation; `ReadPublishedAsync` supplies complete recipe/catalog agreement.
This verifies consistency with the provided source files and current algorithms,
not external provenance approval, historical migration or whole-game readiness.
Existing same-build/numerical/dependency limits and generation budgets apply.
See [public developer workflow](../examples/worldcreator-workflow/README.md) and
[evidence](evidence/2026-11-01-published-source-replay-verification.json).

## Connections and constraints

Rules address immediate part groups by part ID, or `self`:

| Pattern | Expansion |
| --- | --- |
| `Chain` | Consecutive members of one group; `ToPart` must be absent. |
| `Hub` | One source member to every target member. |
| `Pairs` | Matching indices in two equally sized groups. |
| `AllPairs` | Every member of one group to every member of the other. |

Rules carry an arbitrary connection kind, `Bidirectional` and optional JSON properties. Distinct typed links between the same endpoints are allowed. Self-links, unknown endpoints, unequal paired counts and expansion-budget overflow fail. `AdditionalConnections` on the recipe can link exact deep node IDs, such as a stair from one floor's last room to the next floor's first room. Those IDs must exist after expansion.

Child requirements constrain the number of **direct** children with a given kind. `RequireWeaklyConnectedChildren` verifies the local rule graph connects every direct child, allowing a path through `self`. It treats direction as undirected for this check. It does not prove directed reachability, traversal feasibility or physical collision clearance. Explicit deep links are separate from the local rule constraint.

Archetype references must form an acyclic graph. Expansion limits include depth 32, at most 100,000 nodes and 500,000 connections (default 10,000 and 50,000). Property, input and catalog budgets also bound expansion. Invalid data and cancellation stop generation without publishing a partial directory.

## Optional coordinates

Topological worlds declare `"Space": { "Axes": [], "Unit": "none" }`; positions remain null. No arbitrary coordinates are synthesized.

Spatial recipes declare one to eight named axes, a game-defined unit and root origin. Each part selects a `MetadataLayout(StrategyId, Parameters)`. `WorldMetadataGenerator` resolves that ID through its `IMetadataLayout` registry, generates relative coordinates and adds the parent position. Nested parts can select different strategies. The compiler does not inspect `GameProfile` or node kinds to choose an algorithm.

| Strategy | Spatial rule | Authored controls |
| --- | --- | --- |
| `line` | Repeated offsets from a local origin | Origin and step vectors |
| `path` | Ordered locations at measured distances along a polyline | Two or more points, spacing and start distance |
| `arc` | Ordered locations on an arc or closed ring in a chosen plane | Center, radius, two axis indices, start angle and sweep |
| `spiral` | Ordered locations along a spiral, circular ring or ellipse in a chosen plane | Center, starting radius, radial step, signed angular step, two axis indices, start angle, optional axis ratio and orientation |

Use `MetadataLineLayout.Parameters`, `MetadataPathLayout.Parameters`, `MetadataArcLayout.Parameters` and `MetadataSpiralLayout.Parameters` to create typed inputs. Path placement follows bends and rejects counts that exceed the declared path length. Arc sweep is signed and nonzero, with magnitude at most 360 degrees: positive travels from the first selected axis toward the second, negative travels in the reverse direction. Closed rings in either direction omit the duplicate seam; open arcs include both ends when the count is at least two. For a spiral, radius is `StartRadius + index * RadiusStep` and angle is `StartDegrees + index * AngleStepDegrees`; the angular step is nonzero, signed and at most 360 degrees in magnitude. A negative radial step contracts, but every requested radius must remain nonnegative. A zero radial step creates a ring when the start radius is positive; the caller chooses angular spacing, and physical collision constraints still reject overlapping members. All positions must be finite and dimensionally correct. `RequireAllPositions` can require coordinates throughout the hierarchy. A custom `IMetadataLayout` is registered through the generator constructor; registration replaces the default registry, so include any built-ins the custom catalog uses. File-based CLI generation currently exposes the built-ins.

The optional `AxisRatio` scales the second chosen axis (default 1; allowed
0.000001–1000000), and `OrientationDegrees` rotates that plane's axes before
positions are added to the center (default 0). A ring with an axis ratio other
than one is an ellipse; a nonzero radial step makes an elliptical spiral. These
are intentional placement grammars, not orbital physics. Physical collision,
terrain and route requirements still apply. Population rotations turn the whole
ellipse, not merely its member phases.

For physical population, `MetadataPopulationGrammar.Spiral` accepts the same
settings in two dimensions, in the placement stage's coordinate frame. It is
exclusive with the other population sources. Every generated member is still
subject to the declared source footprint, terrain, separation, access and route
checks. Optional `Facing` supports `Along`, `Against`, `Left` and `Right`, using
the continuous spiral tangent in the direction of increasing member index.
Boundary-only `Inside` and `Outside` are invalid. The orientation witness
records each tangent and final source rotation. The schematic validates declared
spiral template/binding positions for faced and unfaced root, nested and reusable
program stages, and checks faced headings. Complete source-package replay remains
authoritative for the original source, including simultaneous removal of all
spiral provenance.

The old direct `{Origin, Step}` part layout has been removed. Current inputs use `{StrategyId: "line", Parameters: {Origin, Step}}`; there is no legacy reader or automatic conversion. Current checked-in examples are migrated; historical evidence is unchanged.

[Composed spatial examples](../examples/metadata/spatial-programs/) use floor-relative room routes, bending side-scrolling sections and planets in the x/z plane. These verify reusable metadata calculations, not complete games. The strategies do not generate terrain surfaces, room walls, collision-free corridors, physics, orbital dynamics or feasible platform jumps. For documents without `Surface` or `MapGeometry`, the schematic uses a hierarchy diagram; its automatic diagram positions are not these world coordinates. Actual positions remain in JSON and the node inspector.

## Scope and reusable structures

`Scale` is a game-authored label. `CompleteDeclaredScope` certifies only the declared recipe scope. `PartialScope` requires explicit `RemainingScopes`; it cannot silently become full coverage. A few town/district nodes do not prove a whole city, world or playable game has been generated.

For games using existing building facts, an archetype may supply `MetadataStructureReference` with a registered settlement catalog selection, definition and variant. Construct the generator with `ISettlementCatalogRegistry`. It resolves existing dimensions/siting/composition facts and emits each definition once, referenced by node `structureKey`. It does not run physical siting or simulation. The CLI uses registered Modern/plugin/catalog-content/composition capabilities for this optional path. Pure metadata needs none of them.

## Export and verification

`GenerateFilesAsync` publishes these files into a **new** output directory:

- `recipe.json` and `catalog.json`: reusable inputs.
- `world-metadata.json`: complete metadata.
- `summary.json`: scope, counts by arbitrary kind and source digests.
- `review.html`: expandable hierarchy and connections; preview limited to 2,000 nodes, 500 connections and 1024 characters per property preview, explicitly marked. Complete values remain stored.
- `schematic.html` and `schematic.js`: interactive JavaScript/SVG schematic with embedded small data or the exact storage manifest, selection, keyboard access, pan/zoom, fit, layers, local plain-JSON loading and SVG export. Partitioned packets fetch only their own hash-addressed local parts; no CDN or image service is used.
- `_worlddata/<sha256>.bin`: shared content-addressed parts when a generated document or checkpoint exceeds the inline threshold.

## Bounded partitioned JSON storage

`ReadAsync<T>` and the existing metadata/domain publishers share one streaming
codec. Canonical compact UTF8 documents up to8MiB stay ordinary JSON. Larger
values use a version-1 `worldcreator-json-chunks` manifest: `StorageFormat`,
`Version`, `DocumentSha256`, `DocumentBytes`, and ordered `Parts` containing only
`Sha256` and `Bytes`. Parts are complete ordered byte ranges of the serialized
source, not simplified geometry, selected records or executable cache entries.
A range may split an escape sequence; only the complete verified stream is JSON.

Every part except the last has exactly1048576 bytes. Filenames are derived from
lowercase64-character SHA256 only, under the same packet directory's `_worlddata`.
No manifest-supplied path or URL is followed. Existing identical parts are verified
before reuse. Reads reject unknown/duplicate manifest fields, wrong versions,
count/size/hash/order mismatches, missing files and reparse-point directories or
parts. The complete stream hash is checked before a typed result is returned.
Ordinary typed schema, source replay, dependency identity and protected-input
checks still run; storage integrity is not approval or semantic acceptance.

The16MiB per-file limit is unchanged. Authoring inputs (recipes, catalogs,
definitions and edit requests) retain the16MiB logical limit. Generated world/
domain documents, their checkpoints, chunk plans and drainage/water/wind receipts
have a separate256MiB reconstructed limit and at most256 parts per document.
Unsupported excess fails visibly; the codec does not truncate. Input/frame/schema,
32 accepted edits and cache bounds remain at their existing owners.

Publication still commits one complete owned staging directory. A cancelled or
failed write cannot publish a partial package or commit a proposed session.
Checkpoints retain every original/accepted input and report, with current-build
source-history replay. The two-domain export uses the same codec for its root
and independently addressed child directories. This is persistence transport;
it does not implement spatial runtime streaming, LOD, native scenes or Godot.

The browser verifies each part and the complete document with native SHA256,
decodes and parses only the complete bytes, then uses the existing schematic
checks and rendering. A partitioned schematic must be opened through an HTTP
preview of its export directory; a selected manifest file alone lacks its parts.
The memory-only `CreateAsync` schematic API stays bounded to8MiB; directory-based
`WriteAsync` and the normal publisher support partitioned worlds. The HTML does
not embed an alternative oversized world. Small inline schematics retain local
file loading. `review.html` is a bounded preview, not a replacement for stored data.

The [partitioned-world input](../examples/metadata/partitioned-world/README.md)
and [execution evidence](evidence/2026-10-29-partitioned-storage-verification.json)
qualify the unchanged full513×513 drainage world, typed standalone/world replay,
large complete history, independent bytes/hashes and actual browser/SVG loading.
Broader world composition and scale gates remain in the master plan.

## JavaScript city schematic

The [city recipe and catalog](../examples/metadata/README.md) demonstrate six districts, 35 hierarchy nodes and 33 relationships, including compound facilities with their own structures. The example is explicitly `PartialScope`; outer neighborhoods and regional connections remain open. Generate it through the same `generate-metadata` command with `examples/metadata/city.recipe.json` and `examples/metadata/city.catalog.json`.

`WorldSchematic` in `Schematic/world-schematic.js` is the single JavaScript layout/interaction owner. It recursively measures and arranges metadata using optional `Properties.schematic` presentation hints:

```json
{ "schematic": { "label": "North neighborhoods", "color": "#dbe9df", "columns": 2,
  "padding": 18, "gap": 26, "header": 36, "corridors": true } }
```

Leaf hints may set `width`, `height`, and `footprint` (false for unbuilt surfaces). Container hints define purposeful diagram organization; `corridors` draws road channels in layout gaps. These channels are presentation geometry, separate from graph connectivity. Enable Connections to inspect the actual typed graph, including arrowheads for forward links. Arbitrary kinds work without a city-specific switch.

Without physical surface/map geometry, schematic positions are **diagram units**, distinct from `WorldMetadataNode.Position`. The viewer does not mutate world metadata, measure physical footprints or solve connected road access, collision, terrain or playable placement. It refuses invalid graphs, cycles, missing endpoints and over 5,000 nodes/20,000 connections rather than silently omitting objects. Existing JSON exports remain available for larger inputs.

`WorldMetadataSchematicExporter` packages the embedded viewer resources inside the existing atomic export. JSON embedded in HTML uses the default escaping encoder; labels/properties render as text. In-browser UI feedback is local; the latest 200 local session events and source digests accompany SVG export. Host applications can subscribe to `worldcreator.operation`. This is not a server audit store or proof that a browser download was saved; CLI generation continues to use the shared durable log/audit services. No accounts, cookies, tracking or remote font libraries are added.

The generated example is at `generated/city-schematic/schematic.html`; a loopback HTTP preview was verified. Direct `file:` navigation was blocked by the test browser's URL policy and is NOT RUN. See the [schematic verification record](SDLC_VERIFICATION.md#javascript-city-schematic-2026-10-25).

Existing destinations are refused. Files are written to a sibling staging directory and published by directory move after cancellation checks. The CLI records started/succeeded/failed/cancelled events through shared log/audit/notification services. Core callers own their host's operational reporting.

Focused and full C# checks are recorded in [SDLC verification](SDLC_VERIFICATION.md). The metadata tests exercise all four example profiles, exact hierarchy/counts/direction/positions, every connection pattern, reusable nested programs, constraints, source-catalog references, invalid inputs, budgets, cancellation, persistence and fresh CLI execution. No Godot/runtime or art readiness is implied. Live Claude/Codex routing scenarios remain NOT RUN until evaluated independently.

## Semantic layers and current output schema

Recipe/catalog inputs remain schema 1. A recipe may declare `Layers`: each `MetadataLayerDefinition` has an `Id`, arbitrary `Kind`, `Purpose`, `SourceRuleId`, `DependsOn`, `Coverage` and `RemainingScopes`. Archetypes and connection rules/explicit links may specify a `LayerId`. Membership is explicit; nodes do not inherit layers silently from parents or from kind names. All authored references are validated, including unused archetypes and zero-count parts.

Output is schema 2 and requires `LayerPlan`. `MetadataLayerPlanner` validates and snapshots declarations, rejects unknown dependencies/cycles, sorts layers dependency-first with stable ID tie-breaking, resolves exact node/connection membership and lists unassigned content. Partial scope requires an explicit remaining-scope list. No layer declaration means unassigned content, not an inferred terrain layer. The world coordinate space supplies axes/units; semantic layers are neither physical floors nor render ordering.

Strict file loading and schematic export revalidate the layer plan against the actual members. Missing/tampered plans and retired output schema versions reject; regenerate current output from its recipe/catalog rather than use a compatibility parser. A layer manifest describes dependencies and membership, not execution of a terrain generator or proof of complete physical coverage. The HTML metadata review shows layer scope and counts. The surface view now has terrain/route toggles and a terrain-kind legend; general semantic stage-layer controls remain S09 work.

The [layered exploration example](../examples/metadata/layered-exploration/recipe.json) uses the same compiler with non-city nested content. [Layer tests](../tests/WorldCreator.Tests/MetadataLayerTests.cs) include an independent enumeration of all 512 directed three-layer graphs, topology-only composition, persistence, malformed references, snapshot/permutation safety and actual CLI outcomes.

## Irregular surface domains and terrain constraints

A recipe can now supply `Surface`, a `WorldSurfaceDefinition` with its own schema version, ID, units, polygon boundary, excluded polygon masks and terrain patches. The generic metadata binding requires exactly two distinct coordinate axes, matching units and an explicit boundary. It accepts concave polygons and does not derive a square layout from their bounds. Layered floors, volumetric space and arbitrary 3D projections are unsupported by this binding; recipes without a surface retain their existing coordinate capabilities.

Each `WorldTerrainPatch` declares an arbitrary kind, purpose, source rule, polygon, priority, reference origin, elevation and two gradients. Height is the declared plane evaluated at the query point. Higher priorities overlay lower ones; any same-priority overlap at a query point is ambiguous, including shared boundary contact. Exclusions take precedence and are boundary-inclusive. Outer domain boundaries include their edges. Masks may extend beyond the domain; only their intersection affects usable ground. A null query boundary means an unbounded domain, not complete coverage; this is used by the mockup source adapter, not accepted as a bounded metadata world.

`WorldSurfaceQuery` returns distinct `Sampled`, `OutsideDomain`, `Excluded`, `Uncovered` and `Ambiguous` statuses. Only `Sampled` has usable terrain fields. `MetadataSurfaceConstraint` checks every positioned node during generation, persisted document reading and schematic packaging. Unpositioned nodes remain explicitly metadata-only. It rejects invalid positions; it does not silently relocate them or generate replacements. The snapshotted field is saved in `WorldMetadataDocument.Surface`.

Current [terrain-policy qualification](evidence/2026-11-01-terrain-policy-acceptance-verification.json) independently checks all five statuses, available zero height, absent environmental values/scores, negative-origin planes, non-square triangle interpolation and mixed-unit refusal. Four complete source/approach worlds demonstrate separate water/slope restrictions and their combination, with exact SDK/CLI persistence. Perfect soft preference never overrides hard eligibility. [Reusable inputs](../examples/metadata/terrain-policy-counterfactuals/README.md). This qualifies the supported surface/suitability contract; broader terrain and world-design acceptance remain separate.

`Matches` splits paths at polygon crossings, checks vertices/crossings and each intervening interval. This prevents thin terrain-kind barriers from disappearing between fixed sample points. Its guarantee applies to patch-constant predicates such as terrain kind/status; arbitrary continuously varying predicates need analytic extrema checks. `ContainsCorridor` checks a circular envelope around the entire polyline against domain/exclusion edges, conservatively rejecting tangency for positive radius. It does not invent water/slope traversal rules; those are game policies for the connection planner.

Supported input limits are explicit: 1,024 terrain patches, 256 exclusions, 1,024 vertices per polygon and 16,384 total vertices; 4,096 points per queried path; two million path/edge checks and twenty million estimated probe/vertex work per operation. Coordinates/elevations at input are bounded to magnitude 10,000,000 units and gradient components to 1,000,000. These numerical/work limits are not claims of accuracy at every scale or supported whole-world performance. Geometry uses floating-point predicates with conservative tolerances; the narrow-barrier fixture is 0.01 units at a 120-unit extent. Query snapshots preserve caller data and cancellation propagates.

The existing `MockupTerrainQuery` now translates source regions into this common owner. `MockupTerrainComposer` consumes the same sampled owner and elevation; source-specific cell blocking/material/spacing rules remain in the raster adapter. The rejected city generator is not involved.

[Runnable irregular-surface input](../examples/metadata/irregular-surface/README.md) and [surface verification](evidence/2026-10-26-world-surface-verification.json) establish the query foundation. Current exports with `Surface` now show its exact polygons and positioned destination markers; metadata-only packets retain the hierarchy diagram. The route extension below builds on this common owner.

## Terrain-aware passage generation

`MetadataRouteRule.RoutingBoundary` optionally supplies an explicit polygonal
physical search domain, contained in the surface boundary. It restricts full
passage width in ordinary search, prescribed alignments and interface legs;
it does not reposition structures or infer a region from the chosen path.
The existing regional query validates complete sources before conservatively
retaining intersecting/touching sources and original-lattice field windows.
Soft guidance remains validated against its original world domain, so a rule
in a neighboring district does not invalidate an otherwise valid bounded query.
Unbounded rules keep their complete world dependencies.

`MetadataGenerationReport.RouteExecutions`, `MetadataDomainStageReport.RouteExecutions`
and `MetadataRegionalExecution.RouteExecutions` optionally expose actual
connection execution: `Computed`, reused from `Current` planning, or reused from
`Accepted` state. Reports include input/output identities and original logical
work. Cache hits charge that work against current limits; clean and cached world
geometry/work remain equal. Entries are private, bounded under the existing
128MiB session cap, preserved across no-op edits and discarded with failed
proposals. Checkpoint restore replays inputs and authenticates reports, never
installs serialized cached routes. Whole allocation/global validation can still
rerun. [Qualified edit flow](evidence/2026-11-02-route-granularity-verification.json).

Directed edge qualification retains actual source endpoint heights. Affine
patches use their source plane and continuous fields their canonical point
sample, avoiding false distinct arrivals caused by midpoint extrapolation.
Source levels are not rounded or merged. The shared 1e-8 height-step tolerance
applies inside segments and at vertices; accepted tiny steps retain their costs.
`MetadataInterfaceJourney.JoinCost` records nonzero ascent/descent at the two
composed joins, separately from its three leg costs. Engine replay and schematic
arithmetic validate this witness; absence means zero extra join cost.

Route search qualifies intermediate turns through the same circular passage
and height-step rules as prescribed paths. Distinct arrival heights remain
distinct search states so an unusable cheap approach cannot discard a later
usable one. Permitted height changes at vertices contribute ascent/descent
costs in search, prescribed paths and rounding. No new input option is required.
Old saved route receipts whose costs/work change must be regenerated from
current sources; replay is not relaxed. This models circular passages, not
vehicle turning radii or general 3D navigation.
[Qualified correction](evidence/2026-11-02-route-junction-verification.json).

The [coastal mixed-world reference](../examples/metadata/coastal-mixed-world/README.md)
combines irregular terrain, a local grid and hillside allocations with 28 exact
building bodies and 56 equipment members. It exercises 74 directed paths,
source-preserving water-crossing changes and a roadless input through the current
CLI and SDK. All geometry comes from the shared engine; the JavaScript packet
only inspects it. Its complete declared recipe does not certify arbitrary world
scale, production art or runtime behavior.

Add optional `RouteRules` to a recipe to realize selected connection kinds. Each `MetadataRouteRule` declares its ID, exact `ConnectionKind`, `WorldTraversalProfile` and `WorldRouteSearch`. No rule means logical metadata only. Unknown/duplicate selectors and selectors matching no connections reject. Routing needs a bounded two-coordinate surface and positioned endpoints. A bidirectional connection produces independently qualified forward/reverse paths, preserving asymmetric terrain costs. The output `RoutePlan` includes the snapshotted rules and connection/rule identities. A route failure rejects the entire generation before publishing files; it never silently drops a required destination or creates a bridge.

The profile supplies purpose/source rule, positive passage radius in the surface unit, maximum planar **surface slope magnitude**, per-kind positive cost per unit, ascent/descent cost per height unit and maximum centerline height step. Unlisted kinds are forbidden. There is no built-in water, urban or vehicle interpretation. `WorldSurfaceTraversal` computes centerline length, terrain-weighted length, ascent and descent separately. It rejects height discontinuities exceeding the declared step limit along the centerline. This does not certify lateral height continuity, directional grade/turning radius, engineering safety or vehicle dynamics.

`WorldSurfaceRoutePlanner` constructs a visibility graph from endpoint positions, optional authored waypoints and eight radial offsets around unique domain/exclusion/terrain vertices. The offsets use `(radius + clearance) × sqrt(2)`. These are internal search candidates, never world placements, blocks, parcels or visible grid cells. A* uses a straight-distance lower bound multiplied by the minimum permitted terrain cost; deterministic coordinate/index ordering resolves ties. It returns `Found`, `EndpointRejected` or `CandidateGraphDisconnected`. The last status means this finite candidate graph found no path. It does **not** prove continuous-space impossibility or justify arbitrary placement. Clearance/waypoints may be revised deliberately; no unbounded retry/fallback runs.

Every candidate edge checks the circular passage against the domain/exclusions and a conservative circumscribed polygon against terrain. The convex hull of two circumscribed 16-gons contains the complete swept disk, reserving at most about 1.96% extra radius. `WorldSurfaceAreaQuery` probes polygon-arrangement cells and their boundaries using critical x coordinates and vertical intervals. This catches thin barriers, off-center pockets, ambiguous coverage and uncovered gaps; it also respects higher-priority valid terrain masking lower-priority forbidden terrain. Predicates must be constant inside a patch, as kind and the declared planar slope are. This is floating-point geometry with the existing tolerances, not exact arithmetic or a guarantee at every numeric scale.

`WorldSurfaceQuery.Trace` partitions a segment at field boundaries for cost integration. The search produces straight segments with constant circular width. Optional rounding below can replace eligible corners with a separately qualified sampled polyline. It does not infer crossing connectivity/levels, generate bridges/tunnels, allocate regions, or turn logical portals into roads. Footprints, generated terrain features and later native TileMapLayer/navigation remain separate tasks.

Work limits are explicit: up to 1,024 candidates, 131,072 attempted edges and 200 million estimated geometry-work units per API route; defaults remain 256 candidates, 32,768 edges and 20 million units. Each area query permits up to 64 query-polygon vertices, 512 relevant boundary edges and 4,096 x events. Metadata permits 16 rule selectors and 128 directed routes. Optional recipe `RouteBudget` declares purpose/source, `MaximumWork` (1–200 million, default 200 million) and `MaximumDirections` (1–128, default 128). All realized access/interface/preservation/route work charges one live aggregate bank, saved as `RoutePlan.Work`; independent per-leg ceilings are not reservations and are not summed. Placement feedback uses the same budget. A budget without route rules rejects. Saved usage and policy replay; exhaustion remains fatal. The query model estimates work; it is not a wall-time/whole-world performance guarantee. Limits fail visibly rather than truncate candidates. Cancellation is checked during search and geometry probes.

`WorldRouteSearch.Objective` defaults to `LowestCost`. Explicit `FirstFeasible` requests may stop at the first fully qualified direct, one-bend or visibility-graph passage. They preserve width, terrain, height, obstacle and endpoint checks but do not claim minimum cost; their saved search model includes `/first-feasible`. On a proven shared affine height plane, the cost lower bound includes unavoidable net ascent/descent. Mixed allowed planes and heightfields retain the distance-only bound. [Mixed-purpose qualification](evidence/2026-10-28-mixed-scale-verification.json) records three 57-object cases and their measured limits. Old saved route work receipts require regeneration with current code.

`WorldRouteSearch.Rounding` optionally declares `MaximumDeviation` (0.000001–100000 world units) and `SegmentsPerCorner` (2–16, default 4), for `FirstFeasible` searches without passage reuse. The planner trims eligible corners by at most that deviation and samples a quadratic curve into straight segments; endpoints and selected authored waypoints are retained. Every emitted segment and junction is requalified against the full-width terrain, obstacles and applicable owner constraints, charging the same work and edge budgets. If the new polyline fails those checks the route is rejected; it is never silently substituted with an unsafe path. The result retains the original path and cost as `RoundingWitness` for replay. Prescribed paths and reuse searches do not support rounding. The resulting polyline is authoritative: this does not certify clearance of an unsampled continuous curve or a vehicle turning radius.

Optionally, `AlternateDeviations` supplies one to eight strictly decreasing,
positive deviations smaller than `MaximumDeviation`. The planner tries them in
order only after a rounded candidate fails, and accepts the first fully qualified
polyline. Every attempt consumes the same cumulative route and parent budgets;
exhausting those limits remains an error. `RoundingAttempts` records deviation,
status, edge checks and work for each tried candidate. If every alternative
fails, the route is rejected; no unrounded fallback is invented. A direct route
or a route with no eligible corners requires no rounding alternatives.

The shared planner first qualifies a direct full-width passage. In default lowest-cost mode it can return before feature enumeration only when its cost reaches the global lower bound and reuse discounts are absent. Otherwise, up to 32 candidate one-bend passages can establish an initial upper bound; A* still searches every candidate that can improve it. Explicit first-feasible mode follows the stopping policy above. These candidates are search geometry, not added world objects. Traversal work estimates account for all boundary scans and potentially intersecting edges instead of charging every remote solid as a crossing. Width, obstacle, terrain, height and cost checks remain unchanged. Saved work receipts must be regenerated with current code. [Complete mixed-program evidence](evidence/2026-10-27-world-mixed-program-verification.json) covers 28 solid members, 70 local route directions and 12 regional directions without removing required content.

The normal `generate-metadata` CLI owns operational logging and atomic publication. Reading a saved document or packaging its schematic rederives its declared route plan and rejects altered geometry/cost/search results. This verifies consistency against the saved declarations, not authenticity of edited input data or missing declarations. Source/input digests remain separate provenance evidence.

The JavaScript viewer now accepts current output schema 2, clips terrain to its irregular boundary, distinguishes terrain kinds with a labeled color key, draws exact route polylines at their declared width, and separates logical links from passages. Circles are positioned-node markers, **not generated footprints**; unpositioned metadata remains in JSON. The viewer is an inspection tool, not a replacement for C# geometry validation. The previous schema-1 runtime reader is removed; historical generated packets must be regenerated to use current code.

See [runnable input](../examples/metadata/terrain-routes/README.md) and [verification](evidence/2026-10-26-world-routes-verification.json). This is locally qualified route generation on authored terrain/destinations, not completion of S04, S09 or the full world/art/runtime pipeline.

## Scoped destination networks

### Composing game-authored regional policies

`MetadataNetworkRule.RegionNodeId` scopes actual descendant destinations of the
declared kind; minimum/maximum counts remain hard requirements. Separate rules
can select different named entrances, movement radii, terrain costs, alignment
preferences and connectivity obligations. These settings drive the existing
shared planner. Connection-kind and `WorldNetworkOptions.Scale` strings are
descriptive metadata and do not select hidden city/genre algorithms.

The [regional policy example](../examples/metadata/regional-network-policies/README.md)
combines a local supply network with footpaths, an explicit cross-region walking
passage, all seven parent sites and fourteen physical children. The network
destinations use the exact named entrance approaches; the explicit passage joins
those saved positions. Each route retains its own width/profile. Geometric
intersection does not create a physical junction or authorize movement-mode
conversion. `SingleConnectionFailure` certifies the declared logical graph, not
physical-segment or junction survival.

Omitting a rule omits its generated connections while preserving the source
program. Wrong hierarchy counts and entrances too narrow for the selected profile
reject before publication. The current session cache safely invalidates stages,
but per-policy/per-region execution reuse remains incomplete; unchanged policy
work can rerun. [Qualification and remaining gap](evidence/2026-10-30-network-policies-verification.json).


Generated terrain fields described below also feed the common traversal owner. The current route search does not automatically add terrain-cell vertices as search candidates; authored waypoints and surface features still bound its expressive range. Large field footprints can exceed the area-query budget. This is explicit refusal, not evidence of continuous-space infeasibility.

Optional recipe `NetworkRules` use the same bounded surface and traversal owners to generate connections between required destinations. A `MetadataNetworkRule` supplies `Id`, `RegionNodeId`, `DestinationKind`, inclusive `MinimumDestinations`/`MaximumDestinations`, `ConnectionKind`, `WorldNetworkOptions` and optional `LayerId`. Only descendants of the specified region with the exact arbitrary kind are selected. All selected nodes require two coordinates. Count mismatches fail; the planner never discards required content or supplies missing positions. Without network rules no network edges are generated.

`WorldDestinationNetworkPlanner.Plan` also accepts a surface and `WorldNetworkRequest` directly. Options identify purpose, source rule, scale, traversal profile, route search, initial nearest-neighbor count, pair/work limits, optional exact required endpoint pairs and connectivity policy. `Connected` requires a connected graph; `SingleConnectionFailure` additionally requires the selected undirected graph to remain connected after removal of any one connection. Both directions of every pair must pass the route planner. The weight is their combined terrain/ascent/descent cost. Directed-only networks are not supported by this contract.

Required pairs are evaluated and preserved first. The planner starts with nearest-neighbor candidate pairs, expands the neighbor count when obligations remain unsatisfied, and joins components in deterministic cost order. Optional redundancy adds feasible edges according to bridge-removal benefit per cost. This is a bounded heuristic over the tested candidate graph, not a globally optimal continuous-space network or an implementation of Azgaar's Delaunay/Urquhart strategy. Failure statuses distinguish `RequiredConnectionUnresolved`, `CandidateGraphDisconnected` and `RedundancyUnresolved`; pair/work exhaustion throws a distinct diagnostic. A failed network rejects metadata export before publication.

The direct API permits 2–32 destinations, at most 496 evaluated pairs and 200 million aggregate route-work units (default 128 pairs / 20 million). Metadata permits up to four rules and 200 million summed declared work units. Graph bookkeeping is separately bounded by node/edge limits; `Work` counts route geometry work, not wall time. Cancellation propagates through candidate evaluation and connectivity analysis. Required pair order and destination input order do not change the result.

Output `NetworkPlan` saves rule snapshots, selected forward/reverse route geometry, required/backbone/redundancy reasons, attempted pair outcomes and component/bridge analysis. Generated connections carry the rule's semantic kind/layer. Network kinds must be distinct from authored connection kinds and cannot also be selected by `RouteRules`. Reading/exporting a document rederives the plan and rejects altered plans/connections; reserved network connection IDs without a plan also reject. This establishes consistency with saved declarations, not input authenticity.

The schematic renders the engine's selected polylines and widths through the same route presentation owner. Logical connectivity alone does not imply a physical junction where two polylines cross, and loss of a shared physical corridor may remove several logical connections. Explicit `Corridors` groups realize junctions; route reuse, suitability, footprints and optional single-segment failure checks have their own contracts below. The destination-network fixture alone does not certify those capabilities. See [example](../examples/metadata/destination-network/README.md) and [verification](evidence/2026-10-26-world-network-verification.json).

## Generated elevation fields and replayable landforms

`WorldSurfaceDefinition.Terrain` accepts a `WorldTerrainRecipe`. By default the shared surface owner generates `HeightField` before metadata positions are checked. The saved surface includes both the recipe and exact samples. On read/export, a supplied generated field is rederived and compared with its recipe; changed samples, geometry or recipe reject with `WORLD_TERRAIN_REPLAY_MISMATCH`. To regenerate after an intentional edit, update the input recipe and omit its generated `HeightField`. Optional `TerrainCapture` selects the bounded storage contract below. A standalone `HeightField` without `Terrain` is an explicitly supplied field; it has schema/geometry validation, not generated-source authenticity.

An operation may declare `DetailProtectionRadius` to keep generated detail out
of a corridor around its control path. It must be positive, smaller than the
operation's `Radius`, and used with a detail declaration. Detail fades in with
smoothstep from the protected radius to the operation radius; overlapping
protection uses the strongest mask. Landform operations are then applied in
their declared order. This fixes generated lattice samples inside the corridor
while detail parameters vary, provided the landform declarations stay the same.
Interpolation near its edge can depend on samples outside the corridor. The
extra path-distance work is included in full and window work limits.

For region suitability, environment pieces are already split at their declared
field clamps. Thresholds at or beyond those bounds do not need a second region
cut; thresholds inside the bounds still partition the actual geometry. Saved
region work/plan receipts are replayed, so regenerate older source packages if
their derived plan differs after this correction. This does not merge gaps or
remove the existing numerical-resolution refusal for genuinely small geometry.

The recipe declares field ID/kind, purpose/source rule, units, origin, positive sample steps, dimensions, a base elevation/gradient plane, optional coordinate-seeded detail and ordered landform operations. `Add` changes height by a distance-dependent influence; positive/negative values form raised/lowered features. A single control point supplies a radial hill or basin; multiple points supply a ridge or trough along a polyline. `Level` blends existing height toward an authored target. An operation's optional `Falloff` selects `Smooth` (also the omitted default), `Linear` or `Quadratic`. Its optional `InnerRadius` is nonnegative and strictly smaller than `Radius`; within it the influence is full, then fades to zero at `Radius`. With `t = clamp((Radius - distance) / (Radius - InnerRadius), 0, 1)`, their influences are `t*t*(3-2*t)`, `t` and `t*t`, respectively. Omitted or zero inner radius preserves the previous calculation and samples; a positive value adds one work unit per sample. An operation can independently set `OrientationDegrees` (-360–360, default 0) and `AxisStretch` (0.125–8, default 1) to rotate and stretch its distance field in world coordinates; both influence radii and the optional detail-protection radius use that metric. An omitted or identity transform retains the prior samples. Operations run after detail in declaration order. `Level` sets sampled nodes inside the core to the target before later operations; interpolation near its edge can include samples outside. Sub-sample features may be missed; choose resolution deliberately. Operations and their purpose/source identities remain persisted.

Detail uses a fixed integer coordinate hash, quintic interpolation and up to eight normalized octaves. Optional `Persistence` (0–1, default 0.5) scales each successive octave's amplitude; optional `Lacunarity` (1–8, default 2) divides each successive wavelength. The shortest wavelength must remain at least 0.001 units. Zero persistence leaves the first octave only. Optional `OrientationDegrees` (-360–360, default 0) rotates the sampling axes around the world origin; `AxisStretch` (0.125–8, default 1) scales the wavelength along the rotated second axis. The shortest wavelength on either axis must remain at least 0.001 units. Non-identity direction adds five work units per sampled point. Omitting these controls preserves the original values and serialized recipe. Equal seed, inputs and coordinates reproduce sample values, including nodes shared by different resolutions. This is terrain variation, never required-object placement. Between nodes, the discrete field changes with resolution; no approximation-error bound, erosion, hydrology, climate or biome model is claimed. Generated fields currently have one arbitrary semantic kind; explicit polygon patches may override it and its height. Water classification and suitability use separate explicit layers.

`WorldHeightFieldQuery` snapshots row-major samples and locates a cell directly. Each cell has two planar triangles split from its minimum-x/minimum-y vertex to its maximum-x/maximum-y vertex. Shared heights give continuous elevation; gradients are piecewise constant and may jump. On the diagonal use the lower/right triangle, and on interior grid edges use the cell at increasing indices; the maximum extent uses the final cell. This avoids ambiguous shared ownership. The common `WorldSurfaceQuery` applies outer domain and exclusions first, existing patch priorities next, then the sampled field. Coverage outside the field remains `Uncovered`. The lattice does not allocate world regions or streets.

Field limits are 2–513 rows/columns, steps of at least 0.001 units, finite coordinates/heights within magnitude 10,000,000, up to 64 operations with up to 64 control points each, and 20 million estimated generation-work units by default (maximum 200 million). Invalid values, budget exhaustion and cancellation fail visibly. Point lookup is constant-time within the field; the outer polygon/patch query still has its own costs. Generation and replay are bounded by sample/operation counts. Local observed timing is evidence for that fixture/environment only.

`Trace` and `Matches` split segments at field grid and diagonal crossings as well as authored boundaries. Full-width area queries use a structured sweep: authored-boundary intersections, field column boundaries and authored-edge/triangle crossings define x events; each vertical cross-section enumerates row/diagonal cuts only within the footprint, including disconnected intervals of concave polygons. This removes mesh-edge pair expansion and the former 512-mesh-edge refusal. Traversal detects interior and off-center slope barriers and integrates ascent/descent on each plane. The 512 relevant-edge limit now applies to authored/query geometry, with at most 4,096 x events and the shared aggregate work budget. Complex shapes still fail explicitly when these limits are exceeded. Whole-footprint and long diagonal queries on 513×513 samples are qualified fixtures, not universal world-scale performance. Corridors still require explicit terrain kind/slope/cost rules.

The schematic displays the exact saved samples using their declared triangulation, with elevation and slope views. Colors are triangle mean elevation or constant triangle slope, rasterized for display to a 1024×1024 image and clipped to the actual domain; pixel colors are not an analytic query. The SVG export embeds this image. Overlays remain above the field. Changing the layer changes presentation only; JavaScript does not run landform generation. Numerical query and generation remain C# responsibilities.

[Runnable terrain recipe](../examples/metadata/landform-terrain/README.md) and [verification](evidence/2026-10-26-world-terrain-verification.json) cover analytic interpolation, continuity, deterministic operations, sampled-anchor leveling, dense-field queries, common traversal, persistence, actual CLI audit and browser layers/export. S02/S09 remain incomplete; this does not place destinations, allocate content or certify a complete world.

The [field-footprint verification](evidence/2026-10-26-world-field-area-verification.json) compares the accelerated query with 93 test-only planar-triangle representations, including negative origins/non-square samples, authored priority masks, thin barriers and uncovered terrain. It also qualifies dense-field whole-area queries, off-center steep pockets, concave gaps, actual route planning, cancellation and work limits. The old all-mesh-edge expansion is removed from runtime. Typed environmental scalars and whole-footprint constraints are qualified below; suitability placement remains unfinished.

### Independent terrain windows

`WorldTerrainGenerator.GenerateWindow(recipe, request, token)` evaluates only a
version-1 `WorldTerrainWindowRequest`'s original-lattice vertex indices. Required
rows/columns are at least two and must fit the existing2–513 source dimensions.
`HaloCells` is0 or1; the halo is clipped only at the complete source boundary.
The result saves its actual global index range, complete detached source recipe,
source digest, implementation module identity, algorithm model, exact row-major
samples and charged work. It does not rebase the source origin or gradient plane.
Whole-field generation and windows share one native height evaluator.

`ReplayWindow` regenerates and checks every sample bit, request/range, work and
identity, then returns the canonical detached result. The shared typed reader
also replays single `WorldTerrainWindow` documents. Changed or stale data refuses
instead of becoming trusted terrain. A source digest establishes consistency,
not authorship or approval. Header and operations are validated globally;
height-range validation covers computed samples, not unrequested coordinates.

`AssembleWindows` consumes up to4096 windows in any order, replays each and
requires complete source-sample coverage. Matching halo/boundary overlaps occur
once in the output. Gaps, incompatible sources/builds, altered data, cancellation
and exhausted aggregate work never produce a successful partial field. Assembly
explicitly allocates the final complete field; generating one window does not.
Its aggregate bound is the smaller of its explicit `maximumWork` and source
`MaximumWork`. Replay sample work and reference-copy work are both charged.
There is no larger source-size or work ceiling and no default-value gap filler.

Current qualification includes independent equations for264258 source samples,
9522 repeated seam references, request-order/duplicate/halo controls and a complete
unchanged prior CLI world. Direct regional queries now consume bounded windows as
described below. Metadata world publication still retains complete terrain; this
does not qualify independently generated content chunks, water/drainage seams or
runtime streaming. Same-build Windows/.NET equality is qualified; cross-platform bitwise
identity is not claimed. See the [example](../examples/metadata/terrain-windows/README.md)
and [source/command evidence](evidence/2026-10-31-terrain-window-verification.json).

### Regional terrain queries from windows

`WorldSurfaceQuery.CreateRegional(definition, boundary, token, physicalObstacles)`
constructs a regional query directly from recipe-only terrain using only the
existing conservative original-cell window and halo. It validates and snapshots
every authored mask/patch/physical source before filtering. Source origin, global
indices, units, provenance, scalar alignment, interpolation and cell-edge ownership
stay unchanged. A supplied complete field still receives complete validation;
`RestrictTo` reuses an already validated immutable query without regeneration or
copying its complete sample buffer.

`WorldSurfaceQuery.HeightField` exposes `IWorldHeightFieldQuery`. Its `Geometry`
is the complete original lattice description; `CompleteField` supplies a captured
full buffer only when one exists. `WorldHeightFieldQuery` owns full-buffer storage.
`WorldTerrainWindowQuery` owns replayed bounded storage and exposes its source plus
optional `Snapshot`. Missing samples raise `WORLD_TERRAIN_WINDOW_SAMPLE_OUTSIDE`.
For a region wholly outside the sampled field, `Snapshot` is null while geometry
remains present: authored patches still work, uncovered points stay explicit, and
route work/model/provenance match full queries. No empty full buffer is invented.

Both variants use one native cell interpolation and segment/column-cut owner.
`WorldSampledScalarQuery` accepts `WorldHeightFieldGeometry` for frame alignment;
use `WorldHeightFieldQuery.Describe(fullField)` when starting from a supplied field.
Drainage, wind and water retain explicit complete-field inputs at their existing
global owners. Without an explicit `TerrainCapture`, definition-only serialization
is source data, not a saved regional execution cache; reconstruct via
`CreateRegional` for bounded query construction.

Qualification compares3375 exact point samples,12 traces,16 footprints and four
complete terrain/solid-aware paths including identical costs/work/candidate audits.
An additional outside-field authored-platform route reproduced and fixed missing
geometry/provenance with the identical final test harness on prior/current Core.
Independent equations verify5920 saved heights,239 path-cost intervals and72
radius-to-boundary checks. Each main query stores1480 rather than263169 samples;
allocation/timing observations apply to those exact fixtures only. Existing full
metadata sessions at that qualification published complete terrain. The following
contract adds persisted capture; independent content persistence and runtime
streaming remain open.
[Example](../examples/metadata/terrain-windows/README.md) and
[current sources, commands and limits](evidence/2026-10-31-regional-window-verification.json).

## Persisted bounded terrain capture

Optional `WorldSurfaceDefinition.TerrainCapture` is a version-1
`WorldTerrainCaptureDefinition(Version, Id, Purpose, SourceRuleId, Domain)`.
It requires a source `Terrain`, a bounded surface and no full `HeightField`.
The domain defaults to the unchanged world boundary; a supplied domain must
contain it. The engine snapshots the normalized domain and derives a conservative
original-lattice vertex window with its cell halo. Source origin, dimensions,
sample steps and values are never rebased or resized. This is terrain storage,
not a layout grid, content chunk or implicit world enlargement.

`TerrainCapturePlan` saves its version, normalized definition, current module
implementation identity, source-recipe digest and optional `WorldTerrainWindow`.
The window is null only when the capture domain is wholly outside the source
field. Global geometry remains available, authored patches still work and
uncovered terrain stays explicit. The saved definition's `HeightField` remains null for
captured storage; `HeightField.CompleteField` on the query is also null.

Normal generation, saved-document validation, session edits, atomic publication
and checkpoint restoration use the same `WorldSurfaceQuery` owner. On read it
regenerates the exact bounded result and compares the complete plan. Missing
definitions, malformed versions, incompatible full fields, uncovered domains,
changed source/build/sample/work data, exhausted limits and cancellation refuse.
An intentional source edit changes the recipe and omits the old capture plan;
the engine then creates a fresh result. A failed edit preserves the accepted
world and publishes no replacement. Restricting a saved surface preserves its
original normalized capture domain. Globally coupled drainage/wind/water still
require their complete-field inputs; partial storage never synthesizes them.

Route features and sampled hazards examine only the world domain's source cells
and conservative halo, in either storage mode. Segment cuts and area events scan
bounded original row/column ranges and charge that work. Calculated slab endpoints
share the existing operation-precision event identity while distinct authored
coordinates remain protected. Hard anchor constraints discard placement candidates
that no demand can use before retention; any unanchored demand disables that
pruning. Child placement stages consume global region/layout plans only when
their demands reference them. Counts, source sizes, traversal widths and all
declared work/candidate/cell ceilings remain intact. Work counters and derived
plans must be regenerated after implementation changes; old packet byte identity
is not claimed for this change.

The JavaScript viewer displays saved samples using their original global indices,
with the captured and source dimensions identified separately. It validates
capture structure; C# replay owns source authenticity. JavaScript never generates
missing heights. Qualification retains24 nodes,21 physical sources,7 programs,
all authored journeys and both network policies with exact current full/capture
route/cost/work parity, accepted terrain edits and complete checkpoint history.
A retained exact-zero datum produces a nonzero sub-resolution clamp face and
still rejects explicitly; arbitrary floating-point geometry is not claimed.
[Reusable input](../examples/metadata/terrain-capture/README.md) and
[qualified evidence](evidence/2026-10-31-metadata-terrain-capture-verification.json).

## Environmental fields and point suitability

Optional recipe `Environment` declares a `WorldEnvironmentDefinition`. Its fields have arbitrary IDs, units, purpose/source identity, an offset, typed linear terms and optional minimum/maximum clamps. Terms read elevation, slope magnitude, x/y coordinates or another declared field; they never execute expressions or scripts. `WorldEnvironmentQuery` validates and snapshots the dependency graph, evaluates in deterministic dependency order and reports unknown/cyclic references. Scalars are calculated from the common surface sample, including authored overrides. Uncovered, excluded, ambiguous and outside-domain terrain return no environmental values. Zero is a measured result only when the declared calculation produces it.

The engine imposes no terrestrial interpretation. The example authors `temperature = 25 − 0.1 × elevation` and `water-depth = max(0, 5 − elevation)`. These are game rules, not a physical climate or hydrology simulation. They do not change terrain kinds, create water geometry or authorize bridges. Units document the author's coefficients; dimensional consistency is not automatically inferred or converted. Supported formulas are linear combinations followed by clamping, not arbitrary formulas or noise/population generators.

`WorldSuitabilityProfile` declares permitted terrain kinds, hard field ranges and separate preferences. `WorldPointSuitability.Evaluate` returns the exact observation, all hard-rule decisions, preference contributions and rejection IDs. Hard ranges are inclusive. Each preference scores `max(0, 1 − abs(value − ideal) / tolerance)`; the final score is the weighted mean in [0,1], or zero when no preferences exist. Any hard failure makes `Eligible=false` and the final score null, regardless of preference weights. A profile is an explicit game purpose, not an engine-wide definition of buildable land.

Optional `SuitabilityRules` bind profiles to nodes of one exact kind within an explicit region subtree, **including the region itself when its kind matches**. Each `MetadataSuitabilityRule` declares exact min/max counts. All selected nodes require positions. `RequireEligible=true` rejects a failed required point before file publication; `false` retains both eligible and rejected observations for inspection. Neither mode moves nodes, reduces required counts or supplies missing positions. Rules require an environment and bounded two-coordinate surface. Environment definitions without rules remain available for API queries and are persisted without invented observations.

The saved `EnvironmentPlan` contains canonical definitions/rules and per-node observations. Reading or packaging a document rederives the plan and rejects altered observations or decisions. This checks consistency with the saved declarations, not authenticity of edited inputs or evidence that an omitted declaration once existed. Standard CLI Log/Audit/Notification owners preserve actual success/failure outcomes and publication behavior.

Limits: 32 fields, 32 terms per field and 256 total terms; 32 requirements plus 32 preferences per profile; up to 16 metadata rules, 1–1,024 selected nodes per rule, at most 4,096 observations and 20 million estimated metadata evaluation-work units. Coefficients, offsets, bounds and final field values must be finite with magnitude at most 1 billion. Invalid results are rejected rather than silently saturated unless a clamp was explicitly declared. Cancellation propagates through graph evaluation and node checking. These limits do not certify whole-world performance.

**Point results certify only a point.** `WorldSurfaceAreaQuery.Matches` supports patch/triangle-constant predicates; passing a varying environmental scalar into it does not certify every point of a footprint. Use the whole-footprint evaluator below for scalar ranges. Point eligibility also says nothing about capacity, access, collision, adjacency, required child structures or route feasibility.

The schematic colors checked markers green only when all their recorded point checks pass, and red when any reject. Selection displays exact values, rule decisions and “Whole footprint not evaluated.” JavaScript presents the engine's results; it does not recompute fields or suitability. [Runnable example](../examples/metadata/environment-study/README.md) and [historical evidence](evidence/2026-10-26-world-environment-verification.json) qualify that initial slice; current stage acceptance is tracked in the original master plan.

The side panel's **Environmental field data** lists every saved derived field
and sampled environmental input, including unused declarations. Definitions retain
purpose/source rules, units, terms and declared clamps. Sampled inputs show exact
original lattice bounds and full buffer extrema; a row-major source index exposes
the saved value, row/column and local coordinates. Coordinates use the surface's
units; scalar values retain their own units. Invalid indices show an explicit
message and cannot leave a stale sample displayed.

Derived-field ranges and up to 64 detailed point entries come specifically from
`EnvironmentPlan.Observations`; the count covers all entries in that rule plan.
No observations means absent bounds, never zero. These ranges are not whole-field
extrema or whole-footprint guarantees. Existing selected-site inspection retains
its separate placement/reservation checks. The panel reads definitions and saved
values without deriving climate or changing geometry; it remains separate from
terrain color scales and the engine's numerical query APIs.

## Whole-footprint environmental constraints

`WorldFootprintSuitability` evaluates a simple closed polygon against the same environment and profile. The shared structured area sweep exposes planar faces, edges and vertices with their actual terrain owner. Each field is affine within a piece; clamps split that piece before dependent fields are evaluated. Extrema are taken at resulting vertices. This preserves correlations between fields and detects extrema introduced inside the polygon by clamping, rather than relying on its corners or center. Concave input polygons are supported by the sweep; holes use surface exclusions or separate declared footprints.

Results contain whole-footprint hard eligibility, coverage, field ranges, per-requirement decisions, rejection IDs, polygon and work counts. Every encountered terrain kind must be allowed. Range limits are inclusive and include one-sided limits at terrain discontinuities, conservatively covering nearby terrain as well as boundary ownership. `CompleteCoverage=false` makes every hard field requirement unsatisfied; reported ranges then describe the sampled portion only. No zero values are fabricated for missing terrain. Preference scores remain point observations; there is no invented area-average score.

A `MetadataSuitabilityRule.Footprint` is an optional local polygon translated by each selected node's two-coordinate position, in the surface unit. Orientations are authored in its vertices; no implicit rotation, resizing or grid is applied. A required rule must pass **both** point and footprint checks. Report-only rules retain failures. Rules without a footprint remain explicit point checks. Footprints and results are snapshotted, persisted and rederived at read/export alongside the environment plan. This validates declared environmental constraints, not placement, shape collision, capacity, access, foundations, nested content or gameplay.

Limits: 3–64 footprint vertices, the shared 512 relevant authored-edge and 4,096 x-event budgets, 20 million work units by default (API maximum 200 million), and 4,096 pieces per planar region by default (API maximum 16,384). Metadata `FootprintWorkBudget` is shared across all selected nodes in that rule; all declared rule budgets sum to at most 200 million. Budget exhaustion or invalid arithmetic rejects explicitly, never falls back to sparse sampling. Floating-point geometry and the declared piecewise-planar field resolution bound the result; it does not certify an unknown continuous terrain between samples. Cancellation is checked throughout.

The schematic shows exact saved footprint outlines, point-versus-footprint decisions and scalar ranges. Green markers require all declared point and footprint checks to pass. Fit bounds include rejected footprints outside terrain. JavaScript validates presentation shape; C# replay owns correctness. [Runnable footprint study](../examples/metadata/footprint-study/README.md) and [verification](evidence/2026-10-26-world-footprint-verification.json) record local evidence. Terrain-driven placement is qualified in the following section.

## Authored per-object placement controls

Optional `MetadataPlacementDefinition.PoseControls` lets a developer select an
expanded physical `NodeId` independently of a repeated catalog part or nested
program. Each `MetadataPlacementPoseControl` carries a unique ID, purpose, source
rule, required `Position`, `Frame` (`World` or `Stage`) and optional
`RotationDegrees`. At most 1,024 controls may be declared, with one control per
node. Coordinates must be finite and within ±10,000,000 domain units; orientations
must be finite and within ±360°. Empty lists, duplicate IDs/nodes, missing targets
and unknown fields refuse. JSON must explicitly supply `Position`.

`World` positions use this metadata domain's coordinates. `Stage` positions use
the existing placement stage's frame: a nested stage adds its placed parent's
translation; the root stage already uses domain coordinates. A parent rotation
does not rotate the child's coordinate axes. A selected world-position child can
make the shared hierarchical solver reconsider a parent's earlier assignment.

An optional orientation replaces only that member's binding default before
resolving source geometry, reservations, ports and site features. It does not
scale or clip a source. Generated-population facing and imported source-world
orientation remain hard; conflicting requested rotations refuse. Regional capacity
measurement uses the same effective binding orientation as actual placement.

Positions remain constraints alongside existing exact anchors, the declared growth
root, terrain, containment, separation, required access and journeys. A free graph
member's exact position actually guides connected growth. Existing exact providers
retain their constraints; a disagreement rejects the candidate so parent search
can continue. Controls do not silently move a growth root or override an authored
population/source position. Finite search/work/candidate and coordinate limits
still apply; no count reduction or arbitrary placement is introduced.

`PlacementPlan.PoseResults` covers every control exactly once, retaining its
definition, actual stage/demand, domain position and effective orientation.
`TemplateDefinition` preserves the authored controls/defaults for native replay;
the compiled definition records effective member bindings. The optional fields
are absent when unused. Tampered/missing receipts fail validation. The schematic
checks these receipts and displays authored intent when an object is selected.

Ordinary `MetadataGenerationSession` edits can add, change or remove controls.
They persist through source edits and source-history checkpoint replay; invalid
edits, missing targets, conflicts, cancellation and failed publication preserve
accepted state. Explicitly set `PoseControls=null` to resume procedural choice.
Existing input protections still apply. This is an engine API, not an interactive
drag editor or a new session/protection manager.

[Reusable four-case input](../examples/metadata/pose-controls/README.md),
[S08 qualification](world-generation/stages/s08-deterministic-chunks-and-persistence.md),
[commands and evidence](evidence/2026-10-31-pose-controls-verification.json).
These bounded 2D placement controls do not establish broader world design,
arbitrary-domain editing, independent streaming, live-agent behavior, artwork or
Godot acceptance.

## Terrain-driven site placement

### Connected physical growth

#### Reusable nested physical programs

Optional `Placement.Programs` declares reusable `MetadataNestedProgram` records:
`Id`, `Purpose`, `SourceRuleId`, `ScopeNodeId`, `ParentArchetypeId`,
`MinimumParents`, `MaximumParents`, a local `WorldPlacementRequest`, typed
`MetadataProgramBinding` entries, and optional `MetadataProgramGrowth`.
The engine selects matching archetypes among the scope's descendants. The scope
itself is not selected. Parent counts are measured from actual expanded nodes,
including parameter/capacity-driven catalog expansion.

Each binding supplies `DemandId` and a direct `PartId`; optional source-footprint,
rotation, site-layout and population settings use the existing owners. Every
actual member of a bound part is included. A bound part must contain at least
one member; no string placeholders, identifier substitution, child omission or
source resizing is performed. Requests use the existing parent-relative placement
frame and explicit binding rotations.

Optional program growth selects `RootPartId` and numeric `RootIndex`, plus its
named anchor, connection kinds, directions, gap and distance limit. Instantiation
resolves the actual child node before creating ordinary connected-layout guidance.
The program request cannot also supply low-level `Growth`. Growth works without
artwork, a registry, access declarations or route realization when the selected
content graph and physical declarations are otherwise sufficient.

There are at most 64 program declarations. Each permits at most 128 matching
parents; the existing combined cap of 128 nested stages still includes explicitly
authored stages. Each stage retains its 16-member limit. Explicit and instanced
stages cannot own the same parent. Missing scopes/parts, count bounds, ambiguous
bindings, duplicate owners, invalid growth and work/cancellation failures reject.
Zero-parent optional programs require an explicit zero minimum; their source
declarations remain saved even when no instance is emitted.

`TemplateDefinition.Programs` preserves reusable source rules. The derived
`Definition` has concrete nested stages and connected layouts with `Programs`
cleared. `ProgramInstances` records `ProgramId`, `ParentNodeId`, `StageId` and
all bound `ChildNodeIds`; stage IDs are `program-` plus 24 deterministic hash
characters derived from program/parent identity. Existing IDs survive declaration
reordering and added siblings. Read/export rederive the complete instance receipt
and geometry from original rules. `summary.json` reports `InstancedNestedPrograms`.

See [nested-program examples](../examples/metadata/nested-programs/README.md) and
[verification](evidence/2026-10-28-nested-programs-verification.json). Program
instancing composes existing physical algorithms; it does not infer undeclared
parts, larger parent envelopes, physical interiors, streaming or unlimited scale.

#### Connected candidate grammar

Connected-growth candidates include staggered contacts derived from two already
placed declared neighbors. For each authored direction, parallel support faces
must share a normal plane. Their allowable tangential translation intervals for
the exact child support face are intersected; the midpoint provides a candidate.
Both source links appear in `CandidateSource` as `~contact-pair`, together with
the primary parent and direction. Existing face-center candidates remain ordered
before paired-contact candidates. All added work and candidates consume existing
budgets. This is a finite contact grammar, not arbitrary free-space placement.
Positive span alone does not certify a doorway: the interior owner still requires
full width and corner piers, followed by complete walls and swept-path validation.
The accepted source polygons, pins, directions, gap and counts are preserved.
[Qualification](evidence/2026-10-30-contact-span-verification.json).

Optional `Placement.ConnectedLayouts` binds physical placement stages to actual
world connections, including `TopologyRules` output. Each declaration supplies
`Id`, `Purpose`, `SourceRuleId`, `StageId`, `RootNodeId`, `AnchorId`, selected
`ConnectionKinds`, ordered `DirectionAnglesDegrees`, `Gap` and
`MaximumConnectionDistance`. It resolves after direct-part binding expansion.
Every selected edge touching a stage must remain wholly in it; the selected
graph must connect every demand in that stage. Duplicate ownership, missing
root bindings and simultaneous explicit low-level growth declarations reject.

The same capability is available directly as `WorldPlacementRequest.Growth`,
with concrete root/anchor demand IDs and `WorldPlacementGrowthLink` records.
There is no second placement solver. The growth root uses its named anchor;
frontier candidates are derived from a placed neighbor. For direction unit vector
u, the center offset is max(parent-envelope·u) − min(child-envelope·u) + gap.
Clearance reservations take precedence over footprints as envelopes. This gives
an exact supporting-plane gap without source resizing. Every grown non-root candidate
records its source connection, parent demand and ordered direction.

The existing solver still checks complete source polygons, terrain, regional
containment, reservations, separation, authored relations and downstream
content/access/routing. Every realized guidance edge must stay within the declared
center-distance limit. Conflicts reconsider earlier choices; there is no
vacant-space or random fallback. Local directions guide one scope and do not
create global streets or cells. Growth itself supplies the purposeful candidate
order, so a separate scalar preference is optional for growth-guided demands.

When a face-center contact does not fit the declared boundary, the same growth
owner can derive tangential contact positions from source supporting faces and
actual boundary/child edge-contact events. Their positive shared span and declared
normal gap remain mandatory. Boundary events and interval midpoints define finite
candidates; complete envelope, terrain, spacing and downstream checks still run.
Events, ordering and generated candidates charge the existing work/candidate bank.
This is boundary-qualified contact design, not vacant-space scattering or a new
solver. Candidates retain `:boundary-span-` provenance. It does not guarantee a
feasible solution for every continuous arrangement.

The growth subset consists of the root and declared link endpoints. Every link
must be in the root's connected component; a disconnected declared component
rejects. Other demands in that stage require independently declared positive-
weight, zero-minimum/maximum anchor relations. Conflicting fixed positions reject.
They are placed in stable ID order before the connected subset, so growth must
respect their complete source envelopes. They retain exact authored positions
and `anchor:` provenance; they do not gain growth links, arbitrary candidates or
implicit movement. Missing pins reject rather than using terrain scattering.

This allows a room assembly, compound or another connected local program to
coexist with purposeful fixed structures in one stage. All normal separation,
terrain, source-size, physical access, feedback and budget checks remain mandatory.
Fixed structures blocking the required root cannot move it or be dropped.
See the [complete physical indoor/outdoor examples](../examples/metadata/mixed-growth-world/README.md).

Growth search limits: at most128 demands/256 anchors/1,024 relations per stage,
128 nested stages,32 local directions,1,024 unique guidance links and the declared
work/search budgets. The128-member direct chain is a bounded capacity control,
not a guarantee that every128-member graph fits a chosen domain or budget.
`MaximumCandidates` limits cumulative generated-candidate attempts for growth;
diagnostics aggregate attempts/rejections per demand. Static terrain-candidate
placement retains its existing behavior. A bounded search can explicitly fail
despite another unenumerated continuous arrangement being feasible.

Heap-based depth-first search retains a dependency set for each generated domain.
Already placed graph neighbors define its contact candidates. Exact collision,
relationship and shared-wall rejection witnesses identify assignments that prevent
a candidate. Exhaustion can jump over an unrelated assignment only when those
dependencies prove it cannot repair the domain; unknown callback dependencies and
later complete-world rejection conservatively retain the whole prefix. Actual
jumps count under `Conflicts["dependency-backjump"]`. All declared alternatives,
original source polygons and finite budgets remain mandatory. Candidate-budget
exceptions retain bounded `WorldPlacementSearch` diagnostics, selected poses and
conflicts; they remain budget failures, not proofs of global infeasibility.

Saved placement includes the derived growth request and original metadata rules;
read and schematic export replay the common document validator. Terrain,
topology, source-size and rule edits invalidate placement and dependent routes
through the current session owner. See [connected-growth inputs](../examples/metadata/connected-growth/README.md)
and [verification](evidence/2026-10-28-connected-growth-verification.json).

### Prescribed execution for purposeful population

`WorldPlacementRequest.Execution` selects `Search` (default) or `Prescribed` in
the same placement owner. A prescribed demand must have at least one
positive-weight, zero-distance anchor relation. Multiple exact pins must agree.
Missing/conflicting pins reject before physical qualification. Prescribed stages
cannot also declare connected growth. No terrain alternatives or vacant positions
are generated: each demand receives exactly its declared/generated anchor.
The engine validates all members together with the same full footprint,
reservation, field, region/allocation, relationship, sibling-separation,
dependent-stage and feedback checks. Failure leaves no partial assignment.
`SearchStates=1` and one candidate per diagnostic identify this execution; a
purposeful pin is mandatory even when a profile has preferences.

Part expansion and reusable nested programs honor the chosen execution limit.
Prescribed execution permits 1–1,024 demands, 2,048 anchors and 8,192 relations per
stage. The existing candidate cap bounds the total prescribed evaluations; work
and cancellation remain shared. A broad-phase bounding-box lower bound bypasses
only provably separated pairs. Near/touching/overlapping boxes still use the
original exact polygon checks. The sixteen-demand search bound and scoring/
backtracking semantics remain unchanged. These are execution modes, not batching
or different collision solvers.

Population passage preservation checks every exact new footprint/reservation
against the shared circular-envelope exclusion owner. It no longer projects all
new reservations into one synthetic surface with a 256-exclusion limit. Entrance
qualification separately checks the base terrain/domain and each actual other
solid with that same owner. Budgets charge those operations instead of assuming
all remote obstacles interact quadratically with every entrance. The independent
route-surface cap of 256 exclusions, access-site cap of 1,024 and port limits
remain; these changes do not provide an unbounded obstacle backend.

[Qualification](evidence/2026-10-28-prescribed-population-verification.json)
covers a 256-slot standalone assignment, a reusable 32-member nested program,
a 307-member reservation program with a reservation-only passage refusal, and
two complete 223-node/222-physical-member terrain worlds retaining nineteen
connections. Both worlds include all 194 required environmental members; no source
footprint or separation is reduced. Generation timing covers `Generate`, excluding
the subsequent independent checks, replay and export. Browser interaction and
world-scale throughput remain unqualified; Godot is deferred.

### Terrain search execution

Optional recipe `Placement` binds each `WorldSiteDemand` to exactly one expanded, unpositioned catalog node. With `Execution=Search`, the shared `WorldSitePlacementPlanner` generates candidates from the common surface arrangement inside a declared search polygon: interior representatives and vertices of planar terrain faces/edges. This is a finite terrain-derived candidate set, not random scattering, a vacant-cell fallback or mandatory visible parcels. Internal field resolution influences candidates; changing it can change a placement. A flat authored patch with few geometric features can have too few candidates despite available continuous area. Failure means no assignment in this candidate set, not proof of continuous impossibility.

Each demand declares purpose/source identity, a local polygon containing its origin, allowed terrain kinds, hard environmental ranges and preferences. A demand must have at least one environmental preference or a positive-weight distance relationship. Relationships name a required destination or explicit anchor, with inclusive minimum/maximum center distance, desired distance and weight. Anchors can be outside the region and do not imply a physical passage. Point scores rank candidates; whole-footprint constraints, region containment and relationships are hard requirements. Source dimensions and orientation are retained. Touching or overlapping demand footprints are rejected even when `MinimumSeparation=0`; positive separation uses polygon boundary distance.

The deterministic search chooses the remaining demand with the fewest qualified candidates, then scores its alternatives using environmental preference plus `weight × (1 − abs(distance − ideal) / max(1, maximum − minimum))` for relationships whose other endpoint is known. It backtracks when a chosen position prevents the remaining required demands from fitting. Ties use demand ID and coordinates. It returns the first feasible assignment in this ordering, not a globally optimal packing or route-cost optimum. No content count, footprint size, spacing or hard field rule is silently relaxed. Diagnostics retain point/footprint rejection counts, eligible counts, relationship/separation conflicts and search/work counts. No solution returns `NoFeasibleAssignment`; budget exhaustion throws an explicit failure.

Metadata expands required catalog counts first, places the bound roots and dependent physical stages, then resolves any deferred parent-relative constituent points. It refuses already positioned targets and ancestor/descendant targets in the same stage. `NestedStages` places direct children inside a previously allocated parent. Positioned descendants must stay inside their placed ancestor's reserved polygon and on valid terrain. Children without physical bindings or a declared point layout remain unpositioned unless `RequireAllPositions` rejects them. A point layout alone is not a physical footprint or collision proof. Existing fixed content must be represented by declared exclusions when it should prevent placement; arbitrary metadata markers do not imply occupied geometry.

The saved `PlacementPlan` contains canonical inputs/bindings, selected sites with point/footprint evidence and deferred offsets. C# rederives the assignment and affected coordinates on read/export. This verifies saved declarations, not their authenticity. Environmental checks and final network/route generation run after placement; the placement feedback pass can reject search candidates when required access/routes fail. Prescribed assignments have no alternative positions to try, so a failed required constraint rejects that assignment. Failure prevents publication. The JavaScript viewer displays exact generated coordinates, reserved polygons, provenance and counts, and preserves parent/child inspection.

Unrestricted search bounds:1–16 demands,32 anchors,128 relationships; connected
growth uses its separately stated bounded capacity. Both use3–64 vertices per
search/footprint polygon;2,048 candidate points by default (maximum8,192); at
most4,096 retained eligible demand/candidate options;100,000 search states by
default (maximum1 million);20 million work units by default (maximum200 million).
Prescribed limits are stated above; the work ceiling is unchanged. Shared surface/
footprint limits also apply. Inputs are snapshotted and cancellation propagates
through qualification/search. These are finite local allocation limits, not
whole-world performance guarantees.

[Terrain-placement example](../examples/metadata/terrain-placement/README.md) and [verification](evidence/2026-10-26-world-placement-verification.json) exercise generated landforms, two purposeful destinations, four preserved constituent points and their generated route network. The following slice adds physical constituent allocation; generated region boundaries, access/capacity, gameplay, artwork and native runtime remain open.

## Hierarchical physical allocation

An archetype may declare `PhysicalFootprint` with unit, purpose, source rule and local polygon. A placement binding selects it with `UseCatalogFootprint=true`; the demand must then omit its own footprint. Registered structure bindings instead resolve the selected building variant's exact width and depth. Competing sources reject. An optional `RotationDegrees` rigidly rotates a source polygon without scaling. Used generic physical definitions are persisted in `FootprintDefinitions`, and every expanded node carrying such a definition requires a physical binding.

`Placement.NestedStages` contains an ID, `ParentNodeId`, request and bindings for direct children. A null child boundary uses the parent's exact placed polygon. An explicit child boundary is parent-local, translated into world axes and must fit inside that polygon. Child anchors are translated the same way. A rotated parent footprint does not implicitly rotate the child coordinate frame. Root boundaries may default to the shared surface boundary. Separate stages cannot bind the same node or allocate the same parent twice. Dependencies are resolved in stable topological order; missing/cyclic dependencies reject.

The existing site solver handles every stage. It accepts a parent assignment only after all dependent allocations succeed. Child infeasibility therefore causes backtracking into parent alternatives, without shrinking or dropping required content. Each stage preserves whole-footprint qualification, sibling separation and source dimensions. Parent polygons can reserve space for constituents: ancestor/child containment is intentional, not a claim that both polygons are solid occupancy. Node positions are committed only after the complete allocation succeeds.

The hierarchy shares a live aggregate work budget (maximum 200 million) and stage-attempt budget (default 128, maximum 4,096), with at most 128 child stages. Per-stage limits above still apply. Plans retain exact resolved source polygons, nested results, attempts and aggregate work; read/export replays the declarations and rejects mismatches. Finite candidate resolution and first-feasible ordering remain explicit limitations.

Registered source dimensions do **not** certify the source catalog's access, road width, siting rules or context/capacity requirements. Resolved source records list the applicable outstanding checks in `RemainingRequirements`. Source clearance is now enforced through placement reservations as described below. Generic authored polygons certify only their declared geometry/environment requirements. The physical-access section adds explicit child occupancy and named boundary ports; shared corridors and junctions remain open. Placement reconsideration is described below.

The [hierarchical example](../examples/metadata/hierarchical-placement/README.md) preserves 11 nodes and 10 polygons across three physical levels. The JavaScript viewer displays saved polygons and source facts, sizes labels/markers to their physical bounds, and focuses/navigates the hierarchy. [Verification](evidence/2026-10-26-world-hierarchy-verification.json) distinguishes engine, CLI, browser and outstanding game/runtime gates.

## Physical occupancy and entrance access

Catalog archetypes can declare `Access` with `Occupancy` (`Reservation` or `Solid`) and named `Ports`. This is an explicit physical capability: placement-only polygons without access declarations do not establish walkability or solid occupancy. Once any used archetype declares access, every physically allocated node must have a classification; partial classification rejects. Declarations on unallocated nodes also reject. `MetadataPhysicalAccessPlanner` owns their validation and the derived `AccessPlan`.

Each port declares ID, purpose, source rule, `BoundaryPosition`, `ApproachPosition` and `Width`, in the source footprint's local coordinates and unit. Both positions undergo the same rigid rotation and translation as that footprint. A port must fit wholly along one boundary edge, point perpendicularly outward and leave the polygon without crossing its interior. Its approach must be at least 0.51 times the port width long. Concave interior crossings, inward/diagonal openings, corners with insufficient edge width and approaches blocked anywhere across their full declared width reject. Registered structures additionally enforce the catalog's `AccessWidthMeters`; other catalog access/siting/service requirements remain unresolved. Placement reservations enforce source clearance separately from solid occupancy.

The common route surface combines existing exclusions with every allocated `Solid` polygon. Reservations remain traversable. This is a single two-coordinate occupancy surface; a solid containing another solid's entrance does not implicitly create a navigable interior, floor or doorway through the parent. Author separate supported space/occupancy semantics instead of treating an ancestor as exempt.

`MetadataRouteRule.FromPortId` and `ToPortId` choose entrances for authored connections. `MetadataNetworkRule.PortId` chooses the same named entrance on each scoped destination. Solid endpoints must select an entrance; centers remain logical object positions. Reservation or metadata-only endpoints may retain their centers when no entrance is selected. Missing entrances and traversal diameters exceeding their declared width reject. Both directions of each selected approach pass the common terrain/traversal checks. External routes then use the approach points with **all** solids present, including their destination buildings. Only the declared entrance approach temporarily excludes its own solid; arbitrary route segments never receive that exemption.

External route geometry/cost and entrance approach legs remain distinct. Existing path/network cost totals cover external routes, not approach costs. `AccessPlan` stores exact world polygons/ports and source declarations; route/network plans retain their approach validation work. All are rederived at read/export. Source declarations are replayable, not authenticated against an external catalog. Limits are 256 placed sites, 16 ports per archetype, 128 expanded ports, the existing 256 total surface exclusions, and 20 million work units separately for approach geometry and each route/network batch's approach traversal. Existing route search budgets still apply; cancellation propagates.

The [physical-access example](../examples/metadata/physical-access/README.md) requires a route between two entrances with a third solid structure obstructing the direct line. C# routes around the obstacle at full width and builds a network over entrance approaches. The viewer displays exact solid polygons, distinct declared approach legs and port coordinates. [Evidence](evidence/2026-10-26-world-access-verification.json) records focused checks and browser export. The following slice adds placement reconsideration. Service capacity, complete catalog siting policy, route merging/junctions, interiors, artwork and native runtime remain unfinished.

## Placement feasibility feedback

When a recipe combines placement with access, explicit routes or destination networks, `MetadataPlacementPlanner` evaluates each complete hierarchical candidate before acceptance. It projects physical and deferred child coordinates into a temporary node set, then calls the existing access, network and route owners. Blocked approaches, untraversable approaches, unresolved required connections and invalid deferred spatial positions reject that candidate and resume the existing child/root backtracking. Required counts and source dimensions stay fixed. Malformed declarations, exhausted budgets and cancellation propagate immediately when encountered; they are not converted into spatial retries.

`Placement.MaximumFeasibilityAttempts` defaults to 32 and accepts 1–256. It bounds complete hierarchical candidates, separately from stage attempts and search states. Access geometry, approach traversal and all route searches charge the live placement aggregate work budget as well as their local budgets. `PlacementPlan.Work` includes this dependency work; individual stage `Placement.Work` counts only that stage's solver work. Exhaustion is distinct from no feasible assignment in the finite candidate set. This is first-feasible search in declared score order, not a global optimum or a proof that no continuous solution exists.

`PlacementPlan.Feedback` snapshots the used access declarations, route/network rules and authored connections. `FeasibilityAttempts` stores ordinal, candidate-position SHA256, acceptance/rejection and work for each attempted complete candidate, ending in exactly one accepted attempt. Rejected geometry is not stored. Read/export verifies the feedback against the document's dependent plans and replays the search, including attempts, work and final coordinates. The final access/network/route plans are recomputed from the accepted nodes. Replay is bounded but can be expensive; these inputs are replayable provenance, not authenticated external catalog evidence.

The [feedback example](../examples/metadata/placement-feedback/README.md) demonstrates an independently known preferred position, blocked access and an impassable terrain strip. Tests separately cover network-driven relocation, nested child access, fatal malformed inputs, cancellation, work/attempt limits, tampering and actual CLI audit/nonpublication. The schematic displays accepted geometry and recorded rejection reasons; it does not rerun or repair the solver. [Verification](evidence/2026-10-26-world-feedback-verification.json) records the focused evidence. Capacity, full siting policy, adaptive region generation and physical junctions remain open.

## Capacity-driven content and direct-part placement

An archetype may declare named `Capacities` with exact unit, quantity, purpose and source rule. A catalog `MetadataPart.Capacity` optionally requests a quantity from a named `CapacityId`, or selects the registered structure variant's `ServiceCapacity` with `UseStructureVariant=true` and no CapacityId. Registered variant capacity and its definition's CapacityUnit remain the source of truth; a generic quantity cannot override that registered source. This does not infer demand from a population or automatically choose a different size variant.

`RequiredQuantity` supplies demand unless `QuantityParameter` names a recipe integer parameter. The engine creates `max(part minimum count, ceiling(required quantity / per-instance capacity))` instances. The minimum count is the part Count or its existing CountParameter value. Units must match exactly; no implicit conversion or aggregation across unrelated parts. Generic quantities range from 1e-9 through 1e12; demand additionally permits zero. Decimal division avoids an extra instance for inputs such as 0.3 / 0.1. `MaximumCount` defaults to 10000 and accepts 1–10000; node, depth, child-count and connection limits still apply. Demand exceeding any limit fails before publication. A zero demand with zero minimum can produce no children in metadata-only expansion.

`CapacityPlan` retains definitions, each parent/part demand, resolved requested quantity, minimum count, provided quantity and exact generated node IDs. Each allocation's `Contributions` groups members by their actual archetype and registered structure key, with that source's unit, quantity, provenance, provided amount and node IDs. This single contract also covers homogeneous programs. Repeated nested parents have separate demand scopes. Replay validates counts, choices and membership against each saved source. Package-level `ReadPublishedAsync` additionally replays the supplied recipe and catalog. Capacity allocations determine content quantity, not operational supply/consumption, shared-resource allocation or network throughput.

Each generated member owned by a capacity allocation carries a `CapacityKey`
formed from its parent identity and part ID. Stored-world validation and the
schematic require the corresponding capacity plan and reject missing, incorrect
or unaccounted member bindings. These links detect inconsistencies in retained
markers; they are not signatures. If a plan and all of its markers are removed,
or an allocation has zero members, complete `ReadPublishedAsync` replay against
the supplied recipe and catalog is needed to detect the missing requirement.
Previously saved capacity-bearing worlds without member keys must be regenerated
from their recipe and catalog; the validator does not infer legacy bindings.

The earlier single-`Source` allocation shape is retired; consumers should read
`Contributions`. Existing saved documents using the retired shape need to be
regenerated from their recipe and catalog. No compatibility parser is provided.

A `MetadataPlacementBinding` with `PartId` targets all generated direct children of that part under its `NodeId` parent. The associated demand is a template; its profile, source shape selection and rotation remain unchanged for every instance. Anchor/group relations expand over the generated members through the existing relation contract. Concrete demand IDs follow numeric semantic child indices, preserving the declared part order beyond index 9. The stored placement `Definition` contains the resolved bindings; optional `TemplateDefinition` preserves the original reusable input and is expanded again during replay. Parent IDs of nested stages remain explicit; reusable `Programs` instantiate scoped stages through their own documented contract. Unknown/empty groups, collisions, excessive relation expansion or exceeding the selected execution mode's demand limit reject. The engine never truncates a generated content group to fit a placement limit.

The [capacity example](../examples/metadata/capacity-world/README.md) generates three camps for 25 rest spaces, and four when the parameter becomes 31, retaining two child points per camp and the terrain-dependent network. [Verification](evidence/2026-10-26-world-capacity-verification.json) covers independent count/source oracles, nested scopes, physical template expansion, replay/tamper, bounds, actual CLI audit/nonpublication and browser inspection. Full siting/context, shared service capacity, adaptive regions, junctions, art and runtime remain unfinished.

## Source clearance and placement reservations

`MetadataCatalogFootprint.Reservation` optionally declares a local polygon containing the exact physical `Polygon`. A direct `WorldSiteDemand.Reservation` is allowed only when no catalog source owns that demand's shape. Registered structure variants resolve their own `SiteClearanceMeters`: expand their source rectangle by that amount on every side, then rotate physical and reservation polygons together. Generic reservations may be concave; they must contain the entire physical shape. There is no automatic arbitrary-polygon offset algorithm. Source dimensions are never enlarged to masquerade as clearance, and a recipe cannot override a catalog footprint or reservation.

The common placement solver tests the entire reservation against the declared environmental profile and search/parent boundary. Sibling separation uses reservation polygons where present, otherwise physical polygons. Nested children still fit inside their parent's physical polygon; a child's required reservation can cause parent backtracking. Required failures reject publication. Candidate, search, work and cancellation limits continue to apply; finite candidate failure is not proof of continuous-space impossibility.

Placement output saves physical and reservation suitability results independently. Replay rejects missing or changed reservation data. JavaScript displays the saved polygons without expansion: physical footprints are filled, reservations are unfilled dashed outlines. Access and routing still treat only declared physical solids as obstacles; circulation may use reserved space. A reservation does not itself create a yard, path, equipment or other constituent content.

The [clearance example](../examples/metadata/clearance-world/README.md) retains two rotated 4 × 4 meter solids with 10 × 8 meter reservations and explicit entrance approaches. [Verification](evidence/2026-10-27-world-clearance-verification.json) covers whole-area field/hazard checks, sibling conflicts, exact-boundary acceptance, child-driven parent reconsideration, capacity-generated members, selected registered variant clearance, replay and CLI failure/nonpublication. Independent SVG comparison checks all four polygons and both directed paths. The next subsection adds explicit source-rectangle site bands. Independent reservation profiles, road/service policy, shared service delivery and full siting remain open.

## Catalog surroundings and directional site bands

Optional `MetadataPlacementBinding.SiteLayout` declares front/rear/left/right depths in meters, an `EntrancePortId`, purpose and source rule. It requires `UseCatalogFootprint`, explicit solid occupancy and a named source entrance. Registered sources take their rules from the selected building definition; front is negative source Y before `RotationDegrees`, width meets the registered access width, and effective depths cannot fall below variant clearance.

Game-authored `MetadataCatalogFootprint.SiteRules` supplies the same `BuildingSiteRule` grammar without a registered building or settlement profile. A non-null declaration requires `SiteLayout` when that source is allocated. Generic rectangular sources support an entrance on any edge where its entire declared width fits. Front follows the selected edge and rear its opposite. For either Y edge, left/right mean lower/higher source X; for the positive X edge they mean lower/higher source Y, and for the negative X edge higher/lower source Y. The approach must align with that entrance and the outer front reservation edge. The walkway and split front bands follow the selected entrance before the complete source is rotated and placed. Source rectangles may be off-center. Existing rectangular reservations are preserved by taking the outermost existing/requested edge on each side; neither source shape nor authored reservation is shrunk. Convex nonrectangular reservations remain subject to the existing containment, frontage and band checks. Rules use meters, are limited to64 and share native rule validation. Missing layout/rules, unsupported shapes/units, invalid entrances or unsatisfied required bands reject explicitly. Null `SiteRules` leaves existing footprints unchanged; no rules are inferred. The [generic example](../examples/metadata/generic-surroundings/README.md) has no registered structure reference.

Generic site layout also accepts an authored convex irregular `Reservation` of
3–64 vertices around an unchanged axis-aligned rectangular source body. Binding
rotation then rigidly transforms the complete source/plot/feature frame. The
authored irregular reservation is fixed: requested depths extending its extrema
reject with `METADATA_SITE_RESERVATION_TOO_SMALL`; it is never replaced by a box.
Centered Y-edge entrance semantics remain. The full access rectangle must fit the
actual plot; its width is never clipped. Required bands need minimum depth across
the source-facing span after inset. Their output clips to each inward-offset
parcel edge, so a distant corner cannot supply false frontage capacity. Front
non-forecourt bands retain their declared split around the entrance.

`BuildingSitePlanner` owns the geometry through its current partial class. Its
public `Plan` overloads accept an optional cancellation token. Irregular clipping
has a two-million-work local cap and charges the existing parent placement/demand/
anchor budget; budget/cancellation errors propagate. Rules remain capped at 64.
Concave reservations, nonrectangular building bodies, inadequate depth/access and
sub-resolution edges return explicit diagnostics. Existing rectangular geometry
and complete generic packets remain exact. See the
[complete nested irregular-site example](../examples/metadata/irregular-site-program/README.md)
and [qualification](evidence/2026-11-01-irregular-site-grammar-verification.json).


`MetadataFootprintResolver` calls the existing `BuildingSitePlanner` to construct the source catalog's bands and entrance walkway. Its common geometry overload accepts source rules, entrance width and provenance; generic metadata does not fabricate a building definition. Required depths, insets, maximum depths and registered required surrounding kinds must be satisfied. Missing required regions, mismatched entrance geometry and duplicate feature identities reject. Explicit optional rules may omit a band that cannot fit. The common reservation solver then enforces terrain, parent bounds and sibling space; required geometry is never reduced to fit. Direct-part and nested placement templates retain these declarations for every generated child.

Resolved footprints retain rotated local `SiteFeatures`; the placement plan retains their exact translated world polygons with node ownership, kind, rule, purpose and entrance provenance. Replay regenerates and compares them. The JavaScript viewer validates source-to-world correspondence and draws those saved polygons with named colors; it neither packs nor sizes them. This produces semantic grounds/walkway geometry, not sprites, planted assets or independently navigable services. Existing physical access remains a separate capability, and only declared solids obstruct routes.

The [source-surroundings example](../examples/metadata/source-surroundings/README.md) keeps an 8 × 10 meter registered building, a 12 × 18 meter site reservation, a 1.5 × 4 meter front walkway and an 11.5 × 3.5 meter rear garden at 30°. [Evidence](evidence/2026-10-27-world-surroundings-verification.json) covers independent dimensions, required-source failures, forbidden garden ground, repeated members, source identity conflicts, replay/tamper, actual CLI and browser SVG. `catalog-site-rules` is resolved only when this explicit capability runs; full siting, district/technology context, service delivery, boundary assemblies and road policy remain unresolved. Schematics are not artwork or runtime acceptance.

## Shared-service delivery

Optional archetype `Services` declares supplies and requirements. Recipe `Services` binds each required demand to one provider supply and an ordered directed chain of existing connections. Each participating link declares a capacity and unit. `MetadataServicePlanner` validates kind, unit, origin, qualities, direction, chain continuity and complete demand coverage. Missing bindings, provider-dependency cycles and insufficient source/link capacity reject generation. Quantities are declared game units; generation does not invent consumption or physical flow simulation.

`ServiceCapacityLedger` is the single atomic source/link accounting owner shared with `StructureSitingPlanner`. A failed batch reserves nothing. Current metadata delivery uses one provider per requirement; splitting one requirement across providers and automatic provider selection are unsupported. Limits include 4,096 service-bearing nodes/demands/bindings, 128 connections per chain, bounded work and cancellation.

For a registered structure, set `UseStructureProfile` and select `MetadataStructureReference.Technology`. The registered policy resolves that technology before variant selection. Demand uses the selected variant capacity and source requirement coefficients; output uses that capacity and the source output multiplier. Generic overrides are rejected. Output quality tags are explicit authored assertions, not independently certified material properties. The synthetic source test distinguishes definition capacity five from selected variant capacity nine: demand is 1.9 units and supply is eighteen with a multiplier of two.

Requirements may be logical-only or require physical delivery. Physical delivery uses existing qualified route/network results, declared width, continuous interfaces and entrance-inclusive distance in meters. Registered physical endpoints require declared access ports. Consecutive routes at different approach points do not acquire an invented internal connection. An exceeded distance bound can reject a placement candidate and trigger the existing bounded reconsideration; a globally invalid declaration or exhausted capacity fails directly. Topological allocations have no distance and do not certify physical access.

`ServicePlan` saves definitions, resolved source facts, allocations, usage and work. Read/export replays this plan; placement feedback also retains its service inputs. This is consistency with the saved declarations, not authentication against an independently trusted original catalog. The schematic checks saved record consistency and shows provider usage, consumer chains and physical/logical scope without doing generation.

Examples: [logical shared services](../examples/metadata/shared-services/README.md) and [physical catchment](../examples/metadata/physical-services/README.md). [Verification](evidence/2026-10-27-world-services-verification.json) covers API, CLI audit/nonpublication, source-backed quantities, replay, placement feedback, six JavaScript cases and actual browser inspection/export.

The following common-policy and context integrations execute registered supply callbacks, declared region/neighbor constraints and source-area containment. Full siting policy, shared physical corridor capacity/junctions, whole-world acceptance, artwork and native runtime remain open.

## Common catalog supply policy

The subsequent 2026-10-27 integration replaces the mockup-shaped callback. `ISettlementCatalogPolicy.ValidateSupply` now receives a `CatalogSupplySource` and `CatalogSupplyContext`. `CatalogSupplyPolicy` owns immutable source-graph validation and dispatch; both `StructureSitingPlanner` and `MetadataServicePlanner` use it. Catalog-specific code remains in its existing policy. The old callback overload is removed; custom catalog plugins must implement the current contract.

Metadata supply facts may declare `UpstreamSources` as node/supply references and `PortId` as a named access port. Registered outputs use optional profile `Outputs` bindings for those facts; their quantities still come only from the registered variant and profile. Missing, duplicated or cyclic upstream references reject; traversal is bounded and cancellable. Generic point sources supply positions only in an explicit two-coordinate meter space. Registered source points come from named boundary ports; absent geometry stays unavailable. Physical delivery must use the selected output port's approach. A provider identity comes from an actual registered structure, not from a generic source label.

Successful `ServicePlan.PolicyEvaluations` identify consumer, requirement, policy and source. Required source policy failures reject publication. Typed spatial policy issues participate in the existing placement feedback; other issues fail directly. The Modern policy now checks contradictory water quality, protected upstream contamination, actual parent-provider identity and distinct cooling intake/outfall points through this same contract. These rules remain optional catalog-specific game constraints, not defaults for every world.

Generation, placement feedback, file read and schematic export require the selected registry whenever a registered service source is present. `ReadAsync<T>` accepts an optional `catalogs` parameter, and `WorldMetadataSchematicExporter` accepts it in its constructor. The service owner matches saved catalog digest/policy identity and re-resolves the exact selected definition/variant before calling the real policy. Missing implementation, changed provenance, altered source facts or forged saved evaluations reject. Source snapshots alone cannot stand in for policy execution. This does not authenticate an entire metadata document against an external original recipe; removing unrelated declarations is outside this source check.

Current registered service sources retain `catalog-context` as unresolved. Supply callbacks now execute. The following context and resource-area sections qualify declared region compatibility, neighbor policy and source containment; complete siting remains open. [The current example](../examples/metadata/catalog-supply-policy/README.md) and [verification](evidence/2026-10-27-world-supply-policy-verification.json) record the actual tested scope.

### Common region and neighbor context

Optional recipe `Context` enables a shared context pass, independent of `Services`. Its `Regions` contain explicit IDs, game-authored `Use`, full simple polygon `Boundary` and `SourceRuleId`; no global grid, district or urban role is inferred. Each `Sites` binding selects one region by node ID. Every physical allocation and every registered structure must be covered. Generic sites declare their own `Tags`; registered sites must omit Tags and use the selected catalog/technology profile. Registered use and tags are authenticated against the current implementation through `MetadataRegisteredStructure`, also used by service policy replay.

`CatalogContextPolicy` is shared by metadata and authored mockup siting. It checks allowed use, whole-footprint plus reservation/site-feature containment, and explicit `SitingSeparationRule` edge distances. Concave boundaries use polygon edge intervals, not just inside vertices. Contact with the boundary is allowed. Each site's registered policy can require an explicit separation rule. Distances are game-authored parameters with provenance, not real-world regulatory standards. Generic declarations apply only their explicit rules; no universal infrastructure requirement is added.

Missing bindings, forbidden source overrides, incompatible use or absent required neighbor policy fail as input errors. Valid geometry constraints that reject a candidate feed the existing bounded placement search. Saved `ContextPlan` holds source tags/policy identity, evaluations, input definition and work. Read/export replays both context and placement against the actual registry; mismatched results or missing context relative to the saved feedback reject. This is consistency replay, not authentication of an externally supplied original recipe.

The common evaluator bounds regions (512), sites (4096), polygons (256 vertices), separation rules (256) and work; the placement solver retains its stricter own limits. JavaScript checks record shape/coverage and shows purple context boundaries and per-site inspector decisions at saved C# coordinates. It never runs catalog code or repairs coordinates. See [registered example](../examples/metadata/region-context/README.md), [nonurban example](../examples/metadata/wilderness-context/README.md) and [verification](evidence/2026-10-27-world-context-verification.json).

This qualifies declared region use, complete supplied geometry containment and explicit neighbor separation. The following resource-area/exclusion extension qualifies those two capabilities. Complete mandatory surroundings, boundary assemblies, corridor/junction realization and broader context semantics remain open. Existing broad remaining-requirement markers are not cleared by this subset.

### Contained resources and tagged exclusions

A `MetadataServiceSupply.Area` is an explicit world-meter polygon, with the supply's existing source rule and purpose as provenance. Registered output bindings can also declare `Area`; output kind and quantity remain catalog-owned. Generic requirements may declare `SourceContainsSite`; registered requirements derive it from the selected source profile. The former unsupported-containment rejection is removed.

A contained requirement must have an empty connection chain, no physical-route flag, no route-width/distance request, a source area and an actual allocated consumer. The planner tests the entire source-sized footprint, its reservation and composed site features against the area with complete polygon containment, including concave edge intervals. It consumes ordinary shared source capacity and still runs actual catalog supply policy and provider-cycle checks. It consumes no transport-link capacity. `AreaContained` distinguishes this geometric relationship from logical delivery and routed physical delivery; its `Distance` is null. `Plan` accepts optional actual `placement` for this capability, and read/export supplies the document's placement automatically.

`Context.Exclusions` declares up to 256 `CatalogContextExclusion` records: ID, full world polygon, nonempty affected tag set and source rule. Any matching site's primary footprint, reservation or composed surroundings touching or intersecting an exclusion fails spatial feasibility. A fully enclosed exclusion also conflicts. Unmatched tags are unaffected. Authored mockup structure siting calls the same exclusion owner; landscape and public-feature checks retain their existing owners.

Both spatial constraints feed bounded placement reconsideration. Missing/malformed declarations, conflicting connection modes, capacity failures and exhausted work remain hard failures. Definitions and results replay on read/export; source policy provenance remains mandatory for registered structures. Resource fields and exclusions are authored facts here: no climate/resource simulation, changing depletion, water classification, legal safety buffer or unrequested hard terrain obstacle is inferred.

The viewer displays source areas in green and tagged exclusions in red, with the exact engine polygon coordinates. Its inspector distinguishes contained resources from transported service, and records whether exclusion checks passed. See [example](../examples/metadata/resource-area/README.md) and [verification](evidence/2026-10-27-world-resource-area-verification.json). Full world generation and GameArts/Godot stage gates remain open.

### Shared physical corridors and surface junctions

Optional recipe `Corridors` selects up to sixteen groups by `Id`, `ConnectionKind`, `SourceRuleId`, `Level` and `JunctionPolicy`. This bounded capability accepts only level zero and the explicit `connect-at-grade` policy. Every connection of a selected kind must have actual route/network results for each declared direction. Unselected kinds remain unchanged, and roadless recipes acquire no physical passages. Groups remain topologically separate.

`MetadataCorridorPlanner` collects those actual results and applies access exclusions, then delegates geometry to `WorldCorridorPlanner`. Each group requires compatible traversal width, terrain costs, slope and height-step constraints. The owner splits intersecting/overlapping polylines, shares coincident segments and retains ordered directional journey references, source rules, protected status and original endpoints. Stable segment/junction IDs derive from group and coordinates. Original near-coincident points and numerically ambiguous intersections reject instead of silently snapping source geometry. The current tolerance is 1e-8 world meters; it is not a gameplay grid.

Every directed segment is requalified by the existing terrain traversal owner. Junctions store a full circular-traveler envelope and explicit incoming/outgoing transitions, with agreement between approach elevations. The envelope uses the same conservative circumscribed sixteen-sided shape as route clearance. This does not qualify vehicle turning, lane rules, bridge/tunnel levels or arbitrary elevation transfers. Input limits are 128 journeys, 256 points per journey, 1024 total source edges, 2048 canonical vertices and 4096 transitions per junction, with aggregate work and cancellation bounds.

`CorridorPlan.Groups` stores `Segments`, `Junctions`, `Journeys` and `SharedDependencies`. Dependencies identify distinct logical connections using the same physical segment; opposite directions of one connection do not count as independent alternatives. Optional shared-service capacity and physical failure checks are described below. Placement feasibility includes the corridor obligation, and read/export replays it. JavaScript validates record consistency and renders saved segments once and junction envelopes in amber; it does not regenerate terrain or intersections.

See the [diagnostic example](../examples/metadata/shared-corridors/README.md) and [focused verification](evidence/2026-10-27-world-corridors-verification.json). Reuse-aware routing is described below; elevated interfaces and the full S04 exit gate remain open.

### Optional physical-segment failure tolerance

`MetadataCorridorGroup.RequireSingleSegmentFailureTolerance` (default `false`)
requires every selected directed journey to retain endpoint reachability after
the loss of any one realized physical segment. Both directions of that segment
are unavailable. `WorldCorridorDefinition` exposes the same option for direct
engine consumers. Logical cycles alone do not satisfy this requirement.

The engine searches only the selected group's actual directed segments and
explicit junction transitions, under its existing compatible traversal policy.
It does not synthesize paths, reverse one-way permissions, cross another group,
move endpoints or relax terrain clearance. A shared-trunk cycle fails even when
its logical connections are redundant. In metadata placement feedback this is
an attributed spatial failure, allowing bounded reconsideration by the existing
owner; it never silently drops the requested policy.

`WorldCorridorPlan.Resilience` records version 1, measured work and an ordered
`Alternatives` list. Each entry identifies `JourneyId`, `FailedSegmentId` and a
complete directed `Steps` witness. Only failures on that journey's original
segments need new witnesses: failures elsewhere leave its original path intact.
Read/export rederive the receipt. JavaScript checks complete unique coverage,
directions, transitions, exact endpoints and exclusion of the failed segment.

Limits are 4,096 failure checks and 1,000,000 total witness steps, in addition to
existing geometry/work limits and cancellation. Unreachable alternatives fail
with `WORLD_CORRIDOR_RESILIENCE_UNSATISFIED`; resource exhaustion is a separate
failure, not proof of infeasibility. No receipt is produced when the option is off.
This proves single-segment endpoint reachability within the realized group,
not survival of junction loss, an area hazard, conditional gameplay gates,
required intermediate visits, vehicle turning or time-based traffic. It verifies
the candidate design; it is not a complete network redesign solver.

See the [reusable examples](../examples/metadata/physical-resilience/README.md)
and [verification](evidence/2026-10-29-physical-resilience-verification.json).

### Reuse-aware route selection

`MetadataRouteRule.Reuse` and `WorldNetworkOptions.Reuse` optionally declare `WorldRouteReuseSettings`: a finite nonnegative `NewPassageCostPerMeter` and `MaximumSourceEdges` (1–1024, default 512). The common route API accepts `WorldRouteRequest.Reuse`, containing those settings and up to 128 identified directed `WorldRouteReusePath` records with their actual polyline, traversal profile and explicit bidirectional permission.

The current route owner requalifies every source segment against the current surface/access exclusions, compares normalized traversal constraints, then adds source vertices and projections of the requested endpoints to the bounded feature-visibility candidate graph. Endpoints remain fixed; connectors have actual geometry and terrain cost. It reuses only collinear, direction-compatible interval coverage, unioning overlaps rather than counting duplicate sources twice. Numeric collinearity tolerance is 1e-8 meters; proximity alone does not connect parallel paths. Source geometry is bounded to 256 points per path and the declared total edge limit. Existing candidate, work and cancellation limits still apply.

`WorldRouteCost.ReusedDistance` records reused physical distance. `NewPassage` is the declared rate times the remaining distance. `Distance`, `Terrain`, `Ascent` and `Descent` retain their physical meaning; `Total` includes the additional new-passage cost. The physical-distance heuristic remains an admissible lower bound because the added cost is nonnegative. A rate of zero applies no construction preference; omitting reuse retains independent routing. These are game-authored cost units, not real construction prices.

Within a metadata route rule, connections run in stable ordinal ID order, forward then reverse. Each completed route becomes a source for later routes of that rule; a one-way connection does not grant reverse reuse. A bidirectional route can be used by its return journey. Other rules do not silently share paths. This deterministic sequence is part of the current capability, not an optimality guarantee or a protected author-edit system.

Destination networks first select their logical graph using baseline terrain costs, then realize selected edges with reuse, required edges first and stable edge-ID order within each priority. `Attempts` remain baseline topology evidence; `Edges` contain the resulting reuse-aware routes and costs. Both phases consume the network work budget. This preserves selected connectivity but does not jointly optimize topology and construction or prove physically independent alternatives.

Settings, costs and geometry replay through read/export and placement feedback. The existing physical-corridor pass splits the resulting paths into shared segments and explicit junctions. See the [analytic example](../examples/metadata/corridor-reuse/README.md) and [verification](evidence/2026-10-27-world-route-reuse-verification.json). Shared service capacity, multi-level interfaces, adaptive regions and full S04 acceptance remain open.

### Shared physical service capacity

Optional `Services.CorridorCapacities` declares up to sixteen group limits, each with `GroupId`, positive `CapacityPerSegment`, `Unit`, `Purpose` and `SourceRuleId`. Each selected physical corridor group has one declared unit and one limit applied to every segment, shared across both directions. No capacity is inferred from geometry. Omit the declaration for unconstrained groups or logical-only services. Missing groups, repeated group declarations, incompatible units and logical-only delivery over a constrained connection reject explicitly.

The existing service owner resolves each delivery's actual forward/reverse corridor journey. Every step charges the traversed physical segment, so different logical connections share a pool and repeated traversal within a chain counts repeatedly. `ServicePlan.SegmentCapacities` stores the resolved limits; each allocation's `SegmentUses` records group, segment, traversal count and charged quantity. `Usage` adds `segment:<segmentId>` entries alongside source/link entries. The shared decimal `ServiceCapacityLedger` reserves all applicable dimensions atomically.

Insufficient source or logical-link capacity remains a hard input/allocation failure. When those dimensions fit but a physical segment exceeds its limit, `METADATA_SERVICE_CORRIDOR_CAPACITY_EXCEEDED` is a spatial constraint that can trigger bounded placement reconsideration. Required demand is never reduced or omitted. The selected fixture independently exercises candidate rejection and an accepted geometry with all segment limits respected. This is placement feedback, not a capacity-aware route-search optimizer.

Read/export validates corridor geometry before service replay, and placement replay includes the same corridor/service obligation. The viewer reconstructs traversal accounting from selected directed journeys, checks limits/usage and exposes exact saved geometry: teal below capacity, purple at capacity, plus textual amounts and inspector details. See [example](../examples/metadata/corridor-capacity/README.md) and [verification](evidence/2026-10-27-world-corridor-capacity-verification.json).

This is static declared game-service accounting. Timed traffic, queues, separate directional limits, multiple independent units per group, junction throughput, width-derived capacity, protected-author-edit regeneration and physically disjoint alternatives remain separate capabilities. The shared-capacity slice does not complete S04, S06 or the world pipeline.


## Terrain-connected generated regions

Optional recipe `Regions` supplies one `WorldRegionRequest`: boundary, default suitability profile, required explicit seeds and/or terrain intents, minimum areas and work/cell budgets. Per-seed/intent profiles may override the default. The surface and environment are mandatory. `WorldEnvironmentRegions` is the common piecewise-affine field/clamp evaluator used by both footprint qualification and region generation. The region planner partitions terrain faces at all effective profiles' hard field bounds, records eligible profile memberships and connects pieces across eligible positive-length shared edges. Explicit seeds select their containing components; terrain intents use the bounded clearance/assignment policy above. Suitable area is the union of eligible atoms, counted once even when profiles overlap. Selected regions may share a boundary but cannot own the same positive-area atom. Point contact does not connect components. Preference fields do not reshape regions; they continue to score placement. `WorldRegionPlanner.ProfileFor` resolves the effective profile for the actual selected root; placement, nested local layout and interface qualification use that root profile.

`WorldRegionPlan` stores the original request, each seed's exact convex-piece union and exterior boundary segments, plus boundary, suitable, unclaimed-suitable and ineligible areas. Concavities, excluded channels and holes are preserved without a bounding-box substitution. One-sided field limits define the area interior; full closed-footprint qualification still runs for actual content. Region connectivity is topology, not proof of a width-qualified passage. Internal pieces are neither streets nor gameplay/render tiles. Region identity comes from its named seed, not terrain mesh indices.

Extraction streams eligible faces through shared convex union. The default
`WorldRegionRequest.Representation = TerrainFaces` merge
requires a full identical shared edge, identical eligible-profile membership,
terrain source ID/rule/kind, terrain plane and every effective environmental
affine plane. Exact binary-rational plane normalization prevents cancellation
from identifying different values as equal; exact orientation prevents a rounded
zero from erasing a real corner. These predicates operate on the already derived
finite binary64 arrangement, not an exact-arithmetic replacement for surface
extraction. No plane tolerance, terrain resampling, convex-hull filling or source
sample deletion is used. Nonconvex unions or merge candidates above 64 vertices
remain separate. Original height/scalar buffers remain unchanged.

Explicit `Representation = Membership` instead preserves exact eligible-profile
membership and terrain source ID/rule/kind while permitting convex union across
different field planes. It represents suitability membership, not a constant
terrain/field plane. All original samples and fields remain at their query owners;
placement and physical routes still qualify them. The same full-edge adjacency,
exact orientation, concavity/gap protection, vertex and retained-reference budgets
apply. Retained-water and other terrain-face consumers keep the default. Unknown
representation values reject. This does not authorize approximate geometry or
merging distinct provenance/profile sets.

Area-based population and allocation re-sweep membership cells against the
original terrain and environmental fields before integrating declared density:
a merged cell need not have one terrain plane. With no density field, a replayed
membership region already proves the complete eligible shape, so uniform area
and mass are integrated directly from that shape without expanding its terrain
triangles. Density fields whose complete dependency graph uses only coordinates,
constant offsets and other coordinate fields are also integrated directly,
including declared clamps. Only that density's dependency closure is evaluated;
unrelated terrain fields do not impose terrain subdivision. Regional ownership
selects compact membership for these densities and preserves terrain faces for
elevation, slope and sampled-field densities. Explicit site-feature and ground-material population masks lack that
proof and are re-swept even for uniform density. Their ordinary work and cell
budgets still apply; compact membership does not discard terrain samples.

`MaximumCells` bounds active and final eligible profile/face memberships. A
streaming frontier can exhaust it even when a later union could have fewer
faces; failure remains explicit instead of keeping an unbounded hidden buffer.
In the default terrain-face representation, different slopes, scalar planes,
clamp pieces, provenance and profile membership remain separate. The full
513×513 flat/four-plane/masked/scalar references use the
same 32-face and 200M-work declarations. A dense metadata recipe preserves its
32768m² minimum across a one-meter basin relocation, with normal read, publication
and full source-history restoration. [Input](../examples/metadata/dense-terrain/README.md)
and [bounded evidence](evidence/2026-10-29-dense-regions-verification.json).
Full dense drainage now publishes and replays through the bounded partitioned
transport, preserving all native samples and original per-file limits. This closes
the recorded monolithic-file failure; broader arbitrary-field scale remains open.

A `WorldSiteDemand.RegionId` selects a generated region. Existing source-sized placement gains region feature/seed candidates, checks the complete footprint or containing reservation against the union and holes, and rechecks the region's hard profile. Site-specific profiles remain additional constraints. Region declarations are replayed against the actual environment/surface at generation, read/export and public placement use; missing, forged and stale plans reject. Region replay participates in placement's aggregate work budget. Existing hierarchy and physical feasibility feedback continue to own child/content acceptance. The region itself does not resize to rescue an infeasible content program.

Limits: at most sixteen distinct effective profiles/request, 1–32 required seeds, 64 boundary vertices, 1–4,096 retained eligible profile/cell memberships (default 1,024), 16,384 intermediate atomic pieces per evaluated environmental piece, 32,768 exterior segments per selected region, and at most 200 million charged work units (default 50 million). Adjacency comparison is bounded pairwise edge work, not a qualified continent-scale mesh backend. Geometry uses the common surface/polygon numerical tolerances and a 1e-8 shared-edge length threshold; positive clipped areas at or below 1e-8 fail with `WORLD_REGION_GEOMETRY_RESOLUTION` rather than disappearing. Duplicate seeds in a component, overlapping selected profile ownership, ambiguous/unavailable seeds, insufficient area, and exhausted budgets reject. This contract selects terrain-connected components; it does not yet divide a suitable component among competing programs or infer regional intent. Existing local layouts supply declared grid/power subdivisions downstream. Saved plans must be regenerated and replayed against the current source implementation.

When generated interior walls coexist with physical region interfaces, region/local-layout replay uses the original base `regionSurface`; route collision checks use the wall-projected traversal surface. `MetadataRoutePlanner.Plan` accepts that optional base surface. Both surfaces must match in every property except exclusions, and every base exclusion must remain identical in the traversal surface; only additional collision exclusions are allowed. Omitting the base surface when replaying a plan originally generated without walls rejects rather than silently changing region geometry. A different base terrain rejects with `METADATA_INTERFACE_REGION_SURFACE_MISMATCH`. Complete-document generation, placement feedback and validation supply the base surface automatically. This separation does not permit traversal through walls.

[Terrain-region composition inputs](../examples/metadata/terrain-region-composition/README.md) qualify a sparse six-compound program on irregular terrain, exact area accounting, terrain response and cross-profile interfaces. Their 11,081 square meters of suitable land are regional eligibility, not a claim of populated-world coverage. Regional intent and catalogs remain authored. [Evidence](evidence/2026-10-28-region-composition-verification.json).

The schematic fills each named region with a distinct color, outlines only exterior edges (including holes), reports area accounting and renders engine coordinates unchanged. Its structural checks are not independent terrain recomputation; C# replay owns geometric/field validity. See [runnable input](../examples/metadata/terrain-regions/README.md) and [verification](evidence/2026-10-27-world-regions-verification.json). S05/S09 and the complete world/GameArts/Godot pipeline remain unfinished.


## Local programs and shared interfaces

Optional recipe `LocalLayouts` is a `WorldLocalLayoutDefinition` containing scoped `Layouts` and explicit `Interfaces`. It requires generated `Regions` and their surface/environment. A layout owns a root region or, with `ParentAllocationId`, subdivides an existing allocation within that same root `RegionId`. Its `OrientedGrid` or `PowerDiagram` policy applies only inside that scope. Every `WorldLocalProgram` supplies an ID, purpose, source rule, protected anchor and minimum area. Parent dependencies resolve deterministically regardless of declaration order. Missing parents, cross-region parents, cycles, duplicate scope ownership and anchors outside the parent refuse generation. Child shapes clip to the complete parent union, retaining terrain holes and boundaries; coordinates remain global.

Area receipts retain both the root region ID and optional parent allocation ID. `RegionArea` is the actual scope area for that receipt. The schematic totals terminal allocations, avoiding ancestor/descendant double-counting. A parent allocation boundary does not imply a road or traversable interface. [Mixed-program verification](evidence/2026-10-27-world-mixed-program-verification.json) qualifies adjacent ordered and irregular child layouts on one continuous land component, all nested physical content, a shoreline counterfactual and exact saved/CLI/browser output.

An oriented grid supplies origin, rotation and cell width/height. Each program selects a unique integer column/row and must retain its anchor inside that slot. The rectangle is clipped to the region's actual convex-piece union, preserving sloping boundaries, exclusions and holes. Missing grid slots leave residual land, which requires `ResidualPurpose`. Grid coordinates describe allocation only; no road or render tile is inferred.

Power diagrams clip the same union by pairwise weighted squared-distance half-planes. A program's `Influence` is in squared coordinate units and moves the bisectors; zero influence gives the ordinary nearest-anchor partition. The policy covers its whole parent region. It does not approximate geodesic distance or automatically optimize weights to fit buildings. An influence that loses a required anchor or empties/undersizes its allocation rejects. Clipping a concave domain into disconnected islands also rejects; the planner never keeps just the convenient island. One program must own one connected allocation.

`WorldRegionGeometry` is the shared owner of clipping, connected components, exterior/hole edges and complete-footprint union containment. Rotated clipping may produce roundoff duplicates; consecutive computed vertices within the common 1e-8 geometry tolerance are coalesced without rounding to a lattice or moving authored anchors. A positive region that collapses below supported geometry resolution fails explicitly. Both terrain regions and local allocations use this owner; no legacy region path remains.

`WorldLocalLayoutPlan` stores normalized declarations, the source region-plan digest, allocations, parent/assigned/residual area accounting, required interface geometry and work. `WorldLocalInterfaceRequirement` names two program IDs and a minimum **continuous straight span**. Collinear contact pieces merge across terrain decomposition; a hole/gap splits them. The longest qualifying span and its midpoint are saved, with deterministic tie-breaking. Summing disconnected spans cannot satisfy the requirement. These are shared geometric boundaries, not collision/navigation or passage-width acceptance.

A placement demand selects both `RegionId` and `AllocationId`. The existing catalog-aware solver adds allocation anchors/interior features to its bounded candidates, retains exact catalog polygons and reservations, and applies allocation union/hole and parent-profile constraints. Missing IDs, stale/forged plans or impossible source dimensions reject the complete generation before publication. Nested placement, access/network feasibility and shared services retain their existing owners. Layout shapes currently remain fixed during placement; adaptive local repartition and physical interface realization remain open.

Bounds: 1–32 layouts, one layout per root region or parent allocation, 1–32 programs per layout and at most 128 total across all hierarchy levels, 128 interface requirements, 1–16,384 retained cells (default 4,096), 32,768 raw contact segments per interface, grid indices -1024..1024 and a shared maximum 200 million work units (default 100 million). Region replay, dependency ordering and geometry work participate in the shared budget, including public placement replay. Cancellation and numerical/complexity exhaustion propagate rather than dropping content.

The schematic displays each allocation and its true exterior, highlights required shared spans, reports allocated/residual area and exposes the selected site's program/source identity. C# recomputes geometry on read/export; JavaScript checks bounded structure and area relationships and renders saved coordinates. Allocated program area is distinct from physical content coverage. See [mixed example](../examples/metadata/local-layouts/README.md) and [verification](evidence/2026-10-27-world-local-layouts-verification.json). S05/S09 and the complete world/GameArts/Godot objective remain unfinished.

## Physical connections through local interfaces

Optional `MetadataRouteRule.Interfaces` binds selected explicit `ConnectionId` values to `LocalLayoutPlan` interface IDs. Both endpoints must have named catalog ports and placement allocations matching the declared interface pair. Surface, environment, region and local-layout plans are required and replayed; the shared boundary alone never implies a road. A connection may run in either orientation relative to the declared pair.

An interface binding can instead set `Source` to `RegionRelation` and name a
partitioned region relation with `MinimumSharedSpan`. Both route endpoints must
belong to the two named regions through an explicit placement `RegionId` or a
nested parent's region binding. This mode does not require local allocations.
The saved region spans supply crossing candidates; the same full-width terrain,
region, access and route checks apply. Under the explicit candidate limit, the
engine tries the midpoint of each qualifying span before additional positions on
those spans. Before using it, the routing owner replays
the region plan against the supplied surface and environment, charging that work
to the aggregate route budget. A stale plan rejects with
`METADATA_INTERFACE_REGION_PLAN_MISMATCH`. Missing or mismatched region bindings
reject, and the relationship alone does not create a route.

Each `MetadataInterfaceRoute` saves the chosen span/center, source and destination domain IDs as `FromDomainId` and `ToDomainId`, crossing attempts, and forward/reverse `MetadataInterfaceJourney` records. Each journey contains `FromLeg`, `Crossing` and `ToLeg`. The engine checks the mandatory perpendicular crossing first, then routes from the source approach to its crossing entry and from the exit to the destination approach. Full swept envelopes remain within their assigned domains and satisfy the original region field profile; crossing halves qualify against their respective domains. The existing terrain/solid traversal and join-height checks remain authoritative. Segment envelopes use the existing conservative circumscribed 16-gon traveler model.
Interface routes use search model `domain-interface-v1`. Earlier packets using
allocation-specific interface field names or the former search model must be
regenerated from source; there is no compatibility reader.


The planner considers qualifying spans by length and stable position, then centers at fractions 1/2, 1/4, 3/4, 1/8 and 7/8, with width clearance from span endpoints. `MaximumCrossingCandidates` is a limit of 1–16, not a promised candidate count. Searches and replay share finite work/cancellation budgets. Invalid declarations and exhausted budgets propagate; only spatially infeasible candidate crossings are retried. Accepted routes retain rejected candidate reasons/work. Failure of all candidates refuses export; it does not silently select an unrelated boundary.

Individual site candidates now qualify their selected interface entrance envelopes through the shared physical-access and route owners before assignment combinations are explored. Diagnostics retain those rejection reasons. Complete assignments still qualify sibling collisions and complete routes, so early pruning does not substitute for the final physical check. Required source sizes and constituents remain unchanged. Whole-assignment attempt-budget errors retain the last spatial rejection.

The schematic structurally checks bindings, composed paths, costs and attempts, draws exact full routes, and highlights realized crossings in magenta. C# performs geometric replay; JavaScript does not recalculate or repair physical geometry. See the [runnable fixture](../examples/metadata/local-interfaces/README.md) and [verification](evidence/2026-10-27-world-interfaces-verification.json). This capability does not establish adaptive repartition, bridges, vehicle turns, complete world content, art approval or native Godot runtime acceptance.

## Composed world domains and transitions

### Regional worlds with independent domains

`MetadataRegionalDomainWorldGenerator` composes one generated regional world
with one or more independent domains. Its definition supplies the regional
source, additional local recipes and catalogs, explicit domain transitions and
optional domain reachability and journeys. Region IDs become domain IDs and
must be unique alongside the additional domain IDs. At most 16 total domains
are supported. Every regional child is regenerated by the domain owner and
must exactly match the regional result and its dependencies.

The source selects exactly one of `RegionalWorld` (a concrete regional
definition) and `RegionalDesign` (a bounded regional design request). For a
design, each candidate that passes its regional constraints is also checked
against the composed domain reachability and journeys. Unreachable or
checkpoint-bypass/order failures reject that candidate so the designer can try
another authored option. Malformed references, budget failures and cancellation
remain terminal. The composition records attempted selections and their reasons;
the complete composition, including those decisions, is regenerated on read.
Selection evidence is also shown in the published index. This is a composed
selection, not a standalone regional design receipt.

Each qualified regional connection is represented in the domain journey graph
by a deterministic `regional-route-NNNN` transition, with its forward and
optional reverse path saved in `RegionalRoutes` and checked against the resolved
endpoints. A journey step using that transition therefore refers to the actual
terrain-qualified regional path in the **composition document**. Additional
domain transitions remain explicit game-defined changes without inferred
connector geometry. The domain document alone does not carry the regional
route evidence; retain and validate the composition document. Regional route
endpoints may use explicit access ports or positioned non-solid nodes. The
composition resolves each node position through the domain owner's movement,
terrain and obstacle checks; solid structures require an entrance.

The SDK `Generate`, `Validate`, `PublishAsync` and `ReadAsync` APIs rederive
the complete composition. Publication atomically writes the composition and
existing regional and domain inspectors, refusing existing output directories.
The CLI exposes `generate-regional-domain-world <definition.json> <new-output-dir>`.
The composition does not provide continuous 3D connectors, runtime movement or
automatic art. The regional and local owners'
individual limits and generation work still apply; composing them is not an
aggregate CPU budget.
[Runnable example](../examples/metadata/regional-domain-world/README.md).
The composition index lists up to 128 steps per journey direction and labels
regional route geometry separately from explicit domain changes. Full step and
route evidence is in `world.json`, including any steps beyond that display limit.

`IWorldMetadataGenerator.GenerateDomains(MetadataWorldDomainDefinition)` composes
2–16 independent `MetadataWorldDomain` entries (ID, purpose/source, recipe and
catalog) through the existing `Generate` API. Each local document retains its
coordinates, units, coverage, layers, sources and IDs. Duplicate node/recipe IDs
across domains are legal; endpoint identity is the `(DomainId, NodeId)` pair.
This is not one flattened surface or an implicit collision merge between floors.

`MetadataDomainTransition` declares ID, game-authored kind, purpose/source, `From`,
`To` and direction. `MetadataDomainEndpoint` names a domain/node and one
existing `AccessPortId`, `InteriorConnectionId`, `NetworkRuleId` or explicit
`UseNodePosition` selector plus a local
traversal `Profile`. Physical domains require a positive-radius profile and exactly
one physical reference. A network reference selects this node from that exact
saved network's destinations, including outdoor encounter markers with no building.
Its profile must match every saved movement/cost field (including profile ID;
editorial purpose/source text is excluded). The existing access owner requalifies
a solid destination's configured port; its approach coordinate must equal the
saved network destination. Marker endpoints retain that exact destination
coordinate and have null `AvailableWidth`, since no entrance width was authored.
A network selector does not make a logical node physical or bypass solid access.
`UseNodePosition` explicitly selects a positioned non-solid node without
requiring a local network. The movement profile's full-radius envelope must fit
permitted terrain and avoid exclusions, interior walls and solid footprints;
the result has null `AvailableWidth`. The selector cannot be combined with
another endpoint selector and is invalid for logical domains. It does not
invent a route to the point.
The current access/traversal/solid/wall owners qualify the port approach
or room-side opening segment in both directions and preserve source width. The
result stores exact endpoint coordinates and available width. Geometry-free worlds
require logical node endpoints without unused physical controls; positions may be
absent. A transition does not create a 3D connector or execute a game scene change.

Optional `MetadataDomainReachability` obligations name distinct source/destination
domains, purpose/source and `RequireReturn`. Bounded directed BFS saves transition
ID paths; required unreachable directions fail. This graph describes explicit
inter-domain transitions only. It does not certify that arbitrary entry/exit
points connect within a domain; declare the required local routes/interiors in
that domain's recipe and inspect their own proofs.

Optional `Progression` rules describe directed gameplay reachability across
domain-qualified nodes, local connections and explicit transitions. A progression
link with `DomainId` set names a local connection; a null `DomainId` names a
transition by ID. Rules may require ordered milestones and persistent
capabilities granted at nodes and required by links, and may require returning
to the start. The shared progression owner enforces its existing work and state
limits and records domain-qualified witnesses. The result is regenerated during
source replay. Progression work is recorded separately in `Progression.Work`
and is bounded by the progression owner's per-rule and total limits; it is not
included in the domain journey `GraphWork` ledger. Progression is a logical gameplay proof: it does not establish
physical movement, so declare a separate qualified domain journey when needed.
The projected progression graph accepts at most 99,999 domain nodes.
In a regional/domain design, a progression reachability or milestone failure
can reject an otherwise valid authored candidate; malformed inputs and exhausted
budgets remain terminal.

The definition is version 1. IDs, references and counts are validated, then sources
are snapshotted and canonicalized. `MaximumTotalNodes` (default 50,000, hard100,000)
and `MaximumTotalConnections` (default100,000, hard200,000, including transitions)
bound aggregate content. `MaximumTransitionWork` (default20M, hard200M) covers
endpoint proof work; `MaximumGraphWork` (default1M, hard10M) covers graph work.
Each child retains its own generation budgets; these proof budgets are not a
claimed aggregate CPU budget for all child generation. At most128 transitions and
32 reachability obligations are accepted. Serialized sources/output are bounded
at16MiB for logical source definitions. Generated documents/checkpoints use the
bounded partitioned transport, capped at256MiB reconstructed. Cancellation and
all failures remain terminal; no child is dropped.

`MetadataWorldDomainDocument` retains source definition/digest, each complete local
world and registered dependencies, transition proofs, reachability paths and work.
`ValidateDomains` and `ReadAsync<MetadataWorldDomainDocument>` regenerate the complete
source and compare results. Dependency identity is checked during generation and
again before publication; unpinned current dependencies cannot impersonate earlier
source output.

CLI: `generate-world-domains <definition.json> <new-output-dir>`. Existing generic
versus registered catalog routing and shared Log/Audit/Notification reporting apply.
The API `GenerateDomainFilesAsync` publishes one atomic directory containing
`world-domains.json`, `definition.json`, `index.html` and one ordinally named child
directory per domain with the existing metadata/schematic exports. Domain IDs are
not filesystem paths. Existing targets are refused; failure/cancellation leaves
no partial collection. The root review links independent colored schematics.

[Example](../examples/metadata/world-domains/README.md) and
[evidence](evidence/2026-10-29-world-domains-verification.json) qualify outdoors and
two overlapping-coordinate floors, plus logical domains. This does not qualify
whole-world scale, continuous volumes/geodesics, all transition kinds, runtime
movement or visual acceptance. Godot and production artwork remain deferred.

### Declared journeys through local routes and domain transitions

`MetadataDomainEndpoint.InteriorOpening` optionally selects a room-relative
opening with `MetadataInteriorOpeningSelector(ConnectionKind, ConnectionIndex=0)`.
It is mutually exclusive with `InteriorConnectionId`, `AccessPortId` and
`NetworkRuleId`. The zero-based ordinal (0–127) selects among all metadata
connections of the declared kind incident on the endpoint node, sorted by ordinal
connection ID. Both endpoint orientations qualify; input-array order is irrelevant.
The selected connection must then have exactly one actual incident generated
opening. Missing openings, insufficient width or blocked approaches refuse;
selection does not skip an infeasible choice or infer another connector.

The existing domain owner resolves selectors during generation. Authors do not
need a world-generation prepass to discover derived opening IDs. Physical endpoint
proofs preserve the selector and record `ResolvedInteriorConnectionId` with the
actual approach and available width. These fields also apply to journey endpoints.
Logical endpoints reject spatial selectors. Explicit opening IDs remain supported;
unused new optional fields are omitted from JSON. Selection retains at most the
requested 128-ID prefix (129 transient), scans current connections/openings under
the existing transition ledger, and honors cancellation. It adds no generation
budget or geometry. Read/checkpoint replay rederive selection and its proof.
See the [current composed example](../examples/metadata/composed-world/README.md)
and [qualification](evidence/2026-10-30-semantic-opening-verification.json).

Optional `MetadataWorldDomainDefinition.Journeys` declares 1–32
`MetadataDomainJourney` records: ID/purpose/source, `From`/`To` endpoint references,
allowed `ConnectionKinds`, per-physical-domain `Profiles`, and `RequireReturn`.
Endpoints use the existing port/opening/network-destination/logical contracts. A required physical
endpoint must match the selected local movement profile. Logical domains need no
profile; unused physical controls on them are refused.

An optional `RequiredMilestones` list declares 1–32 distinct endpoints, in
forward order, which every allowed route from the journey start to its goal
must traverse. Each milestone uses the same endpoint and profile qualification
as the start and goal. The planner rejects an unreachable milestone, a path
that bypasses it, or a path that reaches a later milestone first. Resolved
milestones are saved in the result and regenerated on replay. If requested,
the return path is independently required but does not impose checkpoint order.
These checks apply to the declared movement graph and do not prove the absence
of undeclared runtime paths.

`MetadataDomainJourney.Capabilities` optionally adds persistent, nonconsumable
capabilities to this physical journey search. Each grant names an exact
qualified domain endpoint; each gate names a local connection or an explicit
transition using `MetadataDomainProgressionLink` (null `DomainId` means a
transition). Every listed capability is required to traverse the gate in either
direction. Shared corridor segments retain each eligible source route separately;
the source connection's gate applies to its directed segment. A route switch is
possible only at an explicitly recorded corridor turn. A connection gate is a
gameplay rule on that source connection, not a simulated physical barrier across
every overlapping route. Gates must name an existing connection of an allowed kind; grants
must resolve to existing qualified endpoints. No route or connector is invented.

The search tracks `(graph vertex, capability set)` and charges the existing
journey graph-work budget. `MaximumStates` bounds each search, with at most 16
capability IDs. Return search starts with the capabilities acquired on the
forward journey. If an early arrival cannot return, the search may select a
later reachable capability state that can. `ForwardCapabilityStates` and
`ReturnCapabilityStates` contain the capabilities before each saved step and
one final state; internal zero-length graph transfers can grant capabilities
between saved steps. These witnesses replay from the source and are not a game
runtime inventory system.

The domain owner builds a finite directed witness graph from actual qualified
`RoutePlan`, `InteriorPlan` and `NetworkPlan` paths, filtered by declared connection
kinds and matching movement profile ID, radius, slope, kind costs, ascent/descent
and height-step fields. Editorial purpose/source text does not alter movement
compatibility. Both directions require their own source-qualified trace. Graph
vertices preserve domain and directed source-route identity. Physical routes
transfer only through the same declared node/port anchor, or through the exact
selected interior-opening side on its own source route. The route endpoint or
opening point must match the qualified anchor position. Equal coordinates on
unrelated routes do not create a connector; neither do unsplit crossings, near
points or absent local routes. Geometry-free worlds use their declared directed
metadata connections. Eligible explicit domain transitions connect qualified
endpoint identities in their respective domains. Within a physical domain,
qualified corridor segments and their recorded directed turns can connect their
matching source routes. A shared coordinate alone still does not authorize a
turn, and corridor groups do not implicitly connect separate domains.

Bounded BFS saves `MetadataDomainJourneyStep` references: `route-segment` contains
domain, exact source route ID, zero-based segment index and source direction;
`corridor-segment` records the source route and direction, corridor group and
segment IDs, and corridor direction, with segment index -1; its source must be
a member of the qualified corridor and each turn must be explicitly recorded;
`transition` names the explicit domain change and direction; `logical-connection`
names an existing metadata connection. `MetadataDomainJourneyResult` preserves
resolved endpoints, forward/optional return sequences and graph counts. A missing
continuous local path fails even when domain-level reachability passes. This is
not shortest-distance optimization; no geometry or child-world input is changed.

The existing cumulative endpoint and graph ledgers cover journey work too. Per
journey graph caps are 65,536 vertices and 262,144 directed edges. All source/work/
size/cancellation/refusal and atomic export boundaries remain authoritative.
Read/export replays the entire source and compares witnesses. The root review
lists declared qualified journeys; each local schematic retains exact geometry.

[Example](../examples/metadata/domain-journeys/README.md) and
[evidence](evidence/2026-10-29-domain-journeys-verification.json) qualify a three-domain
11-step forward/return path and logical-only journeys. Unsupported/missing traces
are not synthesized; temporal progression, kinematic turning, continuous 3D
connectors, runtime scene behavior and world-scale stress remain open. Godot deferred.

## Relationships between required regions

`WorldRegionRequest.IntentRelations` optionally declares 1–128
`WorldRegionIntentRelation` records: unique ID, `FromId`, `ToId`, purpose, source
rule and inclusive `MinimumDistance`/`MaximumDistance`. Endpoints name distinct
required intent IDs or protected seed IDs in the same request. At least one
required intent must exist. Multiple relations may intersect requirements on the
same pair; repeated IDs, unknown/self endpoints, empty collections, non-finite
bounds, negative minimums, reversed intervals and maximums above 30 million are
refused. Static checks precede JSON snapshotting, including direct API calls.

For requests using `IntentPartition`, a relation may also specify a positive
`MinimumSharedSpan`. The partition search measures actual shared boundary edges
and requires one continuous straight span at least that long; the longest span
is recorded as `SharedSpan` and all actual segments as `SharedSegments` in the
relation result. Gaps remain separate. Disconnected or undersized
boundaries fail the constraint. The requirement is not currently supported
without `IntentPartition`, and invalid or unsupported declarations reject.
A shared span is a geometric interface: an actual passage or route must be
declared and qualified separately. Region-interface packets created before the
`SharedSegments` witness was introduced must be regenerated from source. See the
[shared-interface example](../examples/metadata/region-shared-interface/README.md).

Distances are 2D Euclidean distances between selected interior anchors or protected
seed points in the surface coordinate unit. They are not boundary separation,
road construction, network distances or physical passage guarantees. Apply such
relations only when the game's authored design calls for them.

The existing region owner indexes incident requirements and checks them as both
endpoints become available in its canonical ranked component search. A failed
relationship backtracks; it cannot move explicit seeds, lower area/clearance,
relax suitability, scale components or drop required roles. Fixed-pair contradictions
fail before unnecessary interior-anchor work. Every relation-index/check/result
operation charges the existing region work ledger; cancellation, state/candidate/
anchor/work limits and cumulative composition limits stay authoritative. Excluded
assignment enumeration returns only relationship-compatible alternatives.

`WorldRegionIntentPlan.Relations` saves ordered `WorldRegionIntentRelationResult`
records with exact requirement, endpoint positions and measured distance. It is
null when no relationships are declared. C# read/export regenerates assignment,
proof and complete world; JavaScript checks endpoints, requirement equality and
saved measured distances, then displays the saved region geometry. It does not
reassign regions or construct roads. Source edits and checkpoints preserve the
requirements and failed-world rollback semantics.

[Example](../examples/metadata/region-relations/README.md) and
[evidence](evidence/2026-10-29-region-relations-verification.json) cover independent
exhaustive distance/assignment cases, simultaneous constraints and a complete
physical program. Roles and rules remain game-authored; world-design synthesis,
scale and visual acceptance remain open. Godot is deferred.

## Region-relative local content

A `WorldLocalLayout.RegionFrame` (`WorldLocalRegionFrame`) declares purpose and
source-rule provenance for a translation frame. With it, `Programs[].Anchor` and
`Grid.Origin` are offsets from that layout's selected region seed. Without it,
these remain protected world coordinates. Translation does not change influence,
area requirements, cell sizes, grid angles, source footprints or building facings.
Nested frames are relative to the same named region, not recursively to a parent
allocation; child geometry still clips to the full parent allocation. Relative
and absolute declarations can coexist under explicit ownership.

`WorldLocalLayoutPlan.Definition` retains canonical original declarations and
offsets. `Allocations[].Program.Anchor` and all allocation geometry are resolved
world coordinates. The current layout owner validates all static declarations
before candidate-dependent slot/region fit, translates with coordinate/work
bounds, qualifies terrain and parent containment, and rederives everything on
read. Slot failure must not mask malformed later programs or interfaces.

Optional `MetadataPlacementDefinition.AllocationAnchors` contains up to 32
`MetadataAllocationAnchor` records (`AnchorId`, `AllocationId`, purpose, source
rule). They create root placement anchors at the selected allocation's resolved
program anchor. Reuse the usual declared placement relations or connected-growth
root to bind structures to these anchors. Missing layouts/allocations, empty or
malformed bindings and collisions with explicit anchors, region anchors or other
allocation bindings fail. The original placement template retains bindings;
its compiled definition contains the actual anchors. These bindings do not move
explicit anchors or invent relationships. Nested structure programs keep their
existing local placement owner.

This composes with terrain-derived region selection, physical region reconsideration
and bounded local adaptation. JavaScript checks saved source-to-anchor consistency
and displays the exact engine allocations; it does not calculate new placement or
repair geometry. [Examples](../examples/metadata/regional-content/README.md) and
[evidence](evidence/2026-10-29-regional-frames-verification.json) cover translated
terrain, grid and weighted partitions, nested scopes, complete physical content,
checkpoint replay and refusal. Original roles, offsets, strategy and constraints
remain game-authored. General synthesis, world-scale throughput, visual acceptance
and Godot remain outside this increment.

### Footprint-aware allocation anchors

An allocation anchor can opt into `Footprint: { DemandId, Search }` instead of
`Interior`. These modes are exclusive. `DemandId` must identify one explicitly
bound root demand in the same allocation/region; part-population bindings are
not accepted by this mode. The existing footprint resolver supplies the exact
source polygon, rotation and reservation. The search uses the reservation when
present, and respects the request's outer boundary and the complete allocation
union, including holes. It never resizes the source object.

`WorldFootprintAnchorSearch` declares `MaximumCandidates` (default 4096),
`MaximumContacts` (4096), `MaximumWork` (5,000,000) and `MinimumClearance` (0).
Candidate/contact maxima are bounded at 16,384 and work at 200,000,000. The
boundary representation is limited to 16,384 edges/16,384 convex cells and 65,536
cell vertices; the source polygon has 3–64 vertices. Degenerate contact segments
at the existing 1e-9 intersection scale refuse explicitly.

The C# owner checks the intended activity anchor first. If it does not fit, it
derives translation contacts from source vertices/edges and allocation/outer
boundary vertices/edges, their projections and intersections. Positive declared
clearance adds conservative normal offsets, rounded away from the boundary;
every result must meet the actual clearance. Candidates are ordered by distance
to the intended anchor with coordinate tie-breaking. This is finite geometric
feature search, not scattering or a continuous completeness guarantee. Failure
means no verified candidate in this representation.

`PlacementPlan.FootprintAnchorResults` saves anchor/allocation/demand identity,
preferred and selected positions, selected contact provenance, source-envelope
hash, candidate/contact/evaluation counts and work. Reads and exports rederive
it. The viewer checks receipt bounds, anchor identity, the complete footprint
against the allocation union/outer boundary and declared clearance. It does not
recreate the search or authenticate the source hash independently.

Geometry-qualified anchoring does not replace environmental, separation, access
or route checks. Closed forbidden terrain boundaries may require an explicitly
declared positive clearance. Candidate/contact/work exhaustion remain distinct
errors; no required content or constraint is dropped. See the
[analytic inputs](../examples/metadata/footprint-anchors/README.md) and
[whole-program reference](../examples/metadata/diverse-programs/README.md).

### Early physical access rejection and failed-placement diagnostics

The access owner transforms and validates the same source openings for candidate
placement and final world validation. Candidates check their approaches against
the terrain domain and prior solids, and protect already placed openings from
new solids. A complete sibling assignment is checked before generating its
descendants. Full route/network feasibility remains a later mandatory check.
This avoids spending whole-world attempts on an already blocked entrance without
raising limits or permitting narrower approaches.

Exhausted assignments retain `Exception.Data["PlacementFailures"]`: stage/parent
identity, per-demand candidate/eligibility/rejection diagnostics, conflict counts,
search states and work. Existing stage-attempt bounds also bound these records.
The concise exception retains an observed attributed rejection. This does not
certify continuous-space infeasibility; failures do not replace accepted state.

The surface-area sweep now discards only source edges outside the actual query
polygon, while sampling the unchanged full source. Constant terrain predicates
avoid decomposition when no boundary crosses the query and there is no
heightfield. Geometry-producing and varying-heightfield queries retain the full
sweep. Exclusions containing the entire query are still sampled and rejected.
This changes work receipts; stale derived packets must be regenerated.

[Verification and scope](evidence/2026-10-29-diverse-programs-verification.json).

## Bounded local repartition

Optional recipe `LayoutAdaptation` requires `LocalLayouts`, generated regions, environment, surface and placement. Its `LayoutId` selects the primary owner. `Dials` authorize `PowerInfluence` changes to a named program or `GridWidth`/`GridHeight` or `GridRotation` changes for an oriented grid. Rotation steps are degrees about the fixed declared grid origin; the complete authorized range must stay within -360..360 degrees. Rotation does not rotate buildings or move protected anchors. Cell size dials retain their existing positive size limits. Each dial declares a positive step and integer `MinimumSteps`/`MaximumSteps` including zero. Each owner accepts at most three unique dials and -8..8 steps each; the complete adaptation accepts at most 64 combinations. The default attempt limit is 16 (maximum 64); one aggregate work budget is capped at 200 million.

`MetadataLocalLayoutPlanner` snapshots inputs, tries the unchanged baseline first, then combinations in increasing total absolute step distance with stable ties. For each candidate, the common local-layout owner recomputes geometry and the existing placement owner runs source-size, nested content, access, routing and declared feedback checks. Only explicit spatial infeasibility permits the next candidate; malformed input, numerical failure, work/attempt exhaustion and cancellation propagate. Protected anchors, program identities, minimum areas, interfaces, source footprints/reservations and unrelated layouts cannot change. Explicit placement anchors also supply bounded candidate positions.

`LayoutAdaptationPlan` saves version 1, original layout and placement declarations, the policy, per-attempt parameter settings/digest, rejection or acceptance, and work. Only the accepted candidate changes node positions. C# read/export reconstructs the entire search, checks the chosen layout/placement/nodes and compares attempt history. JavaScript checks the authorized parameter changes and saved history, then displays exact engine geometry and a collapsible repartition report. This is deterministic finite parameter search; it is not arbitrary topology redesign, continuous optimization or a guarantee that every feasible layout can be found.

The [local orientation example](../examples/metadata/local-orientation/README.md) and [current rotation evidence](evidence/2026-10-29-layout-rotation-verification.json) cover both rotation directions, independent half-plane/area checks, protected anchors, unchanged complete source programs, physical interfaces, checkpoint replay and bounded refusal.

The [source-sized example](../examples/metadata/local-repartition/README.md) and [local evidence](evidence/2026-10-27-world-adaptation-verification.json) cover grid and weighted partitions, physical interface recalculation, independent area oracles, unchanged unrelated allocation, bounded failure and replay. The remaining world-content, diversity, art and Godot gates stay open.

### Declared local strategy alternatives

`MetadataLayoutAdaptation.Alternatives` optionally declares 1–15 ordered
`MetadataLayoutAlternative` records: ID, purpose, source rule and complete selected
`WorldLocalLayout`. This mode requires `Dials: []`. Numeric dial adaptation and
strategy alternatives are distinct modes; combining them is refused explicitly.
The unchanged baseline is tried first, followed by alternatives in declaration
order under the existing attempt/work ceilings. The maximum default 16 attempts
can cover the baseline plus 15 choices; a smaller declared attempt budget remains
a hard refusal boundary.

Every alternative must preserve the selected layout's ID, region, purpose/source,
parent ownership, residual purpose, frame and complete program IDs/purposes/sources,
anchors and minimum areas. It may change strategy, grid declaration, weighted
influences and strategy-specific row/column assignments. Other layouts and interface
requirements cannot be changed. The shared `WorldLocalLayoutPlanner.ValidateSource`
checks all static declarations before search, including unused alternatives.
Slot, region and partition fit remain candidate-dependent; a source-invalid grid
cannot hide behind an earlier success. Program or interface failures do not permit
resizing, moving protected anchors, lowering minimums or dropping descendants.

Each attempted complete partition and placement uses existing physical owners.
`MetadataLayoutAdaptationAttempt.AlternativeId` is null for the baseline and names
the source alternative for later attempts; strategy-only steps are empty objects.
Receipts retain every rejected attempt, original source and selected definition.
When all strategies are physically infeasible, the existing composition search may
advance the region/program choice. Invalid declarations, cancellation and exhausted
budgets stay terminal. C# read/export replays the full realization; JavaScript
checks authorized changes and history, then displays saved geometry.

[Examples](../examples/metadata/layout-strategies/README.md) and
[evidence](evidence/2026-10-29-layout-strategies-verification.json) cover both strategy
directions and local-choice exhaustion before region reselection. This is bounded
selection from authored grammars, not autonomous strategy invention, continuous
optimization, whole-world throughput or visual acceptance. Godot remains deferred.

### Bounded population-grammar selection

`MetadataPopulationSelector.Select` accepts an existing recipe, catalog, a
`MetadataPopulationTarget` and 1–15 explicitly authored `MetadataPopulationChoice`
records. The target identifies a root demand, a demand in a named nested stage,
or a demand in a named reusable program. A program alternative applies to every
parent instantiated from that program. Missing or ambiguous declarations are
rejected. It tries the original grammar first, then the choices in order. Each
choice preserves the original population purpose and source rule. Every
attempt runs the full metadata generator with only that population grammar changed;
the same members, source geometry, terrain, routes and limits apply. The result
contains the selected recipe, generated world and ordered attempt history. Pass
the selected recipe, original catalog and world to normal publication to retain
source replay. Attempt history is returned to the caller; it is not embedded in
the normal world packet. Choice identities, provenance and grammar structure are
validated before generation; geometry and member capacity are validated when a
choice is attempted. Inputs
are snapshotted, and cancellation and non-placement errors are terminal. When
all candidates have placement infeasibility, the selection exception contains
the rejected attempts and last underlying error. Each generation retains its
own limits; the bounded attempt count is not a shared aggregate work budget.
This API is a finite authored choice, not a search of arbitrary layouts or a
guarantee that no other feasible grammar exists. It does not publish a world.

`MetadataPopulationSelectionPackage` adds durable publication. Its version-1
request contains the original recipe, catalog, target and ordered alternatives.
`PublishAsync` writes the request, selection receipt and a complete selected
world package under `world/` in one atomic directory move. The receipt binds the
request, selected recipe, catalog, world, algorithm, dependency identities and
attempts. `ReadPublishedAsync` reruns the selection, compares the receipt, and
replays the selected world from its saved source. It verifies consistency rather
than authenticating the author; a caller must establish external trust in inputs.
The receipt pins the execution algorithm identity. A different engine build may
reject an earlier receipt even if the selected geometry is unchanged; regenerate
the package from its saved request with the current engine to obtain new evidence.
An exhausted selection, cancellation or existing target does not publish a
partial result. Publication is logged and audited through the supplied operation
reporter. If reporting fails after the commit, the API throws
`MetadataPopulationSelectionReportingException` with the already committed
output directory; the caller must not assume that publication was rolled back.
The CLI exposes this package through `select-population <selection-request.json>
<new-output-dir>` and `verify-population <selection-package-dir>`. Verification
replays the original selection as well as the selected world. Both commands use
the existing catalog registry and plugin flags when the source requires them.

As an alternative to a manually listed `Alternatives` array, a request may set
`Translation` to a `MetadataPopulationTranslationPolicy` and leave `Alternatives`
null. Its purpose, source rule, ordered displacement vectors and positive maximum
step count define at most 15 candidates. The original is tried first; subsequent
candidates use step-major then vector order. The vectors operate in the target
placement's coordinate frame and translate authored spiral centers, path points,
boundary vertices or named sockets without changing shape or facing. Zero,
duplicate, nonfinite, oversized or excessive offsets are rejected. Generated
route, region and domain source grammars cannot be translated by this policy.
The normal placement, source-footprint, terrain and route checks still decide
feasibility. Package replay regenerates the alternatives from the recorded rule.
Callers using `MetadataPopulationTranslation.Create` directly should retain the
policy themselves if they need to reproduce how the alternatives were created.
This is a bounded game-authored search, not an unrestricted placement optimizer;
each attempt retains its own existing generation limits.

For ordinary `Generate` / `generate-metadata`, set `Placement.PopulationAdaptation`
to a `MetadataPopulationPlacementAdaptation` containing the target, translation
policy and `MaximumWork` (1–200,000,000). The placement owner tries the original
and then the rule-generated alternatives with one aggregate spatial-work budget.
Every attempt uses the original source footprints and declared placement-time
access, route, network and other feedback. Failed attempts operate on provisional
nodes. Invalid input, cancellation and exhausted budgets are terminal; failures
in later generation stages are not retried by this placement policy.

Optional `AdditionalScopes` adds one to three distinct targets with their own
translation rules. The original choice is index zero in each scope; the complete
product, including originals, is capped at 64 combinations. The engine tries
combinations by increasing sum of their per-scope choice indices, retaining
declared enumeration order for ties. This ordering is deterministic; it is not a
claim of shortest physical movement or global optimality. Each attempt records
the choice for every scope. Duplicate targets, empty scope lists, excessive
products and aggregate-work exhaustion are refused. A program-scope choice
continues to apply to all parents instantiated by that program.

Each scope specifies exactly one of `Translation`, `Rotation` or `Rigid`. A
`MetadataPopulationRotationPolicy` declares purpose, source rule, a pivot in
the placement's coordinate frame and 1–15 ordered, nonzero angles in degrees.
Equivalent angles, nonfinite values, out-of-range pivots and unsupported derived
source grammars are rejected. Rotation preserves the authored path, boundary,
socket or spiral shape; spiral axes and tangent-based facing are respected.
For populations without a facing rule, fixed object rotation remains whatever
the developer declared. Rotation and translation scopes may be coordinated in
the same bounded search. Selection packages may likewise use `Rotation` instead
of `Translation` or explicit alternatives, and replay the recorded rule.

`MetadataPopulationRigidPolicy` combines an authored rotation policy and
translation policy for the same target. It rotates around the declared pivot
first, then translates in the target placement's coordinate frame. Its bounded
candidate product includes rotation-only and translation-only choices as well
as combined choices; the unchanged original is tried separately. There may be
at most 15 non-original combinations. The product is ordered by the sum of
rotation and translation choice indices, with stable rotation-then-translation
enumeration for ties. Joint multi-scope selection and the shared work limit still
apply. Selection packages may provide `Rigid` as their only choice source.

The selected placement keeps the original definition in `TemplateDefinition`
and records the policy, ordered attempts and aggregate work in
`PlacementPlan.PopulationAdaptation`. C# world validation recomputes that plan;
source publication replay also regenerates it from the recipe. The schematic
reconstructs the selected translation and checks the saved attempt sequence and
compiled population against it; it does not independently rerun placement or
prove the rejected candidates infeasible. The integrated
[`integrated-recipe.json`](../examples/metadata/spiral-terrain-world/integrated-recipe.json)
uses the existing example catalog. Omitted adaptation keeps the ordinary path.

### Coordinated local adaptation scopes

`MetadataLayoutAdaptation.AdditionalScopes` optionally supplies 1–3
`MetadataLayoutAdaptationScope` records (layout ID, purpose/source, dials and optional
strategy alternatives). Together with the primary owner this declares 2–4 unique
layout scopes. Each owner uses its existing numeric-dial or strategy-alternative
mode; numeric and alternative modes can differ between owners. Empty, duplicate,
missing or malformed scopes are refused. No independent work/attempt budget is
created per scope.

The same adaptation owner prepares each declared finite choice set and refuses a
Cartesian product above 64 complete combinations. It enumerates the full product,
ranked by summed deviation: absolute numeric steps or strategy alternative index.
Ties retain primary-first then additional-scope declaration order and each owner's
existing choice order. Baseline is first. This preference is finite declared search,
not global quality optimization. One aggregate work ledger and original attempt
limit cover preparation and every complete world qualification.

A joint attempt applies all selected changes to the original definition, then uses
the current layout, full placement, access and route owners. Changing one owner does
not grant a partial success. Source dimensions, protected anchors, identities,
minima, hierarchy and interfaces retain existing rules. Untargeted layouts remain
unchanged; region-context conflicts still delegate to the existing composition
boundary. Invalid declarations, exhausted budgets and cancellation remain terminal.

Multi-scope `MetadataLayoutAdaptationAttempt.ScopeSettings` saves each owner's
layout ID, steps and alternative ID in declaration order. Primary `Steps` and
`AlternativeId` mirror the first scope; single-owner receipts keep `ScopeSettings`
null. C# read/export and source/checkpoint restoration replay the joint search.
JavaScript validates declared finite choice/order and authorized definition changes,
then displays saved engine geometry; it does not place structures or repair maps.

Shared `MetadataRoutePlanner.NormalizeBindings` canonicalizes interface/alignment
bindings for both generated route declarations and placement-feedback snapshots.
Unsorted multi-interface inputs now replay through the same owner; malformed
bindings remain refused.

[Examples](../examples/metadata/joint-layouts/README.md) and
[evidence](evidence/2026-10-29-joint-layout-verification.json) qualify two owners in
numeric/numeric and numeric/strategy modes. Maximum four-owner/nested/chunk stress,
world-scale performance and visual acceptance remain open. Godot remains deferred.

### Nested physical endpoints

The route owner resolves a nested endpoint's local allocation from its placement demand, then its nearest explicitly allocated ancestor. Ownership comes from `NestedStages.ParentNodeId`, independent of declaration order. An explicit child `AllocationId` takes precedence; a contradictory interface pair fails. Cyclic, missing or excessive-depth placement ancestry is rejected. Inheritance does not move geometry or exempt child containment, full-width approaches or solid-obstacle checks. The early placement candidate check and final route/replay use the same ownership resolution.

The [expedition outpost example](../examples/metadata/expedition-outposts/README.md) integrates shelters, outdoor yards and nested solid caches inside two adaptive allocations, with internal paths and a shared passage. [Verification](evidence/2026-10-27-world-compounds-verification.json) covers complete physical content, three-level endpoints, explicit overrides, finite oversized-content failure and exact browser export. This small surface example is not whole-world or art/runtime acceptance.

## Domain generation sessions

`MetadataDomainGenerationSession` edits a collection through the existing generator.
`StartAsync(generator, definition, reporting)` installs `MetadataSessionReporting`
with the host's shared Log/Audit/Notification services. Await `ApplyAsync` for a
memory-only edit or `ApplyAndPublishAsync` for atomic publication. Reported sessions
reject synchronous `Apply`; awaited edits and publications are serialized through
the same session lock. Pure constructor/algorithm APIs remain current for hosts
that own operation reporting separately. Async reporting does not dispatch CPU
generation to a background thread.

Both session types use one internal operation runner. Domain event names are
`world.domains.session.<create|edit|edit-publish|publish|restore>.<outcome>`.
Each actual action records actor/correlation, accepted definition/world identity,
proposed definition or checkpoint identity, reuse counts and failure stage/reason.
Sources and checkpoints detach before awaited callbacks. Actor is host attribution,
not authentication; source content is not copied into event context. Supply runtime
services again on `RestoreAsync(..., reporting: reporting)`. Restore replays source
history without pretending its historical edits were newly requested actions.

Failures/cancellation before commit retain the prior session and output. Atomic
publication or accepted-state mutation commits the operation. Late cancellation
cannot change that fact. A failed success sink throws
`MetadataDomainSessionReportingException`: `Committed=true`, `Session`, optional
absolute `OutputDirectory`, `Operation` and `CorrelationId` expose recovery state.
Do not repeat a committed edit/export just because reporting failed. If both the
operation and its failure reporting fail, an `AggregateException` retains both.
The CLI uses this same lifecycle and distinguishes committed reporting failures.

`Apply` accepts a full `MetadataDomainEditRequest` containing `Id`, `Purpose`,
`SourceRuleId`, `ExpectedDefinitionSha256` and `Definition`. Obtain the expected
hash from the accepted snapshot's `World.DefinitionSha256`. Each domain has a
private cache keyed by stable domain ID. Local dependency identities determine
stage reuse; transitions, reachability and journeys always run again. Failed,
stale or cancelled proposals preserve the accepted world and caches.

CLI commands (use a new output directory for each accepted version):

```text
domain-session start <definition.json> <new-output-dir>
domain-session edit <domain-session.json> <edit.json> <new-output-dir>
```

`PublishAsync` saves the accepted collection and `domain-session.json` together.
`ApplyAndPublishAsync` commits the proposed state only after successful atomic
publication. `RestoreAsync` regenerates initial sources and at most 32 accepted
edits, comparing every report and final world hash. Checkpoints contain source
history, not executable caches; current algorithm identity and registered
dependencies must reproduce that history. Historical dependency switching and
protected-pointer edit policies are not implemented for domain sessions.

Aggregate current cache entries remain bounded at128MiB. Checkpoints use the
bounded partitioned transport:16MiB per file and256MiB reconstructed. These are limits, not demonstrated maximum-size throughput
or peak-memory guarantees. Awaited reporting lock contention, callback mutation,
precommit cancellation and late cancellation are qualified; maximum-size exhaustion,
broader concurrent stress and mid-algorithm cancellation injection remain separate.

The three-domain/21-node fixture retains complete forward/return journeys.
Journey-purpose edits reuse 47/47 stages; an upper-floor geometry change reuses
2/16 upper stages and every lower/outdoor stage. API rollback, source-history
restore, tamper refusal and CLI replay passed. One CLI publication attempt hit a
Windows rename access denial, cleaned up without publishing, then passed in an
isolated run using identical binaries; the cause is unconfirmed.

[Domain session evidence](evidence/2026-10-29-domain-session-verification.json). The full generator remains IN PROGRESS;
Godot, production artwork, runtime and visual acceptance remain deferred.

The [composed-world example](../examples/metadata/composed-world/README.md)
qualifies the same lifecycle with 91 required nodes and 86 physical members:
terrain-derived compounds/landscape, actual outside structures, generated rooms,
parent-route content and geometry-free progression. Explicit transitions bind
actual ports/openings, with complete physical/logical forward and return journeys.
The complete coastal source edit preserves unrelated domains. Actual API, CLI and
public SDK world/checkpoint bytes match; a missing-door journey refuses publication.
See [bounded composition evidence](evidence/2026-10-30-broad-composition-verification.json).
Independent coordinate domains are connected by declared game transitions, not
inferred physical bridges. This is metadata qualification, not runtime acceptance.

The [source-size/terrain sequence](../examples/metadata/source-terrain-morphology/README.md)
retains all 91 nodes and 86 physical members while replacing an exact 10×6 hall
with a 14×10 source, then changing only terrain patches to the coastal case.
Required parent sizes, counts, movement rules and budgets stay fixed. Local
approach/path geometry and terrain-dependent source placement change; unrelated
domains remain exact. An 80×60 source refuses with its parent-fit constraint.
The public Core-only workflow host supports ordered `--domain-sequence` edits
through the existing reported domain session, with clean equality after each
commit and full-history checkpoint replay. CLI and SDK packets agree exactly.
See [bounded qualification](evidence/2026-10-30-morphology-verification.json).

## Recursive cut partitions

`WorldLocalLayout.Strategy = BinaryPartition` uses an explicit `Partition`:
`WorldLocalPartition(RootId, Cuts)`. Each `WorldLocalPartitionCut` declares an ID,
purpose, source rule, origin, normal angle in degrees, `NegativeId` and `PositiveId`.
The origin lies on the cut; `(cos(angle), sin(angle))` points into the positive
child. Children reference another cut or one required `Programs[].Id`. A valid
tree contains every program once and exactly `programCount - 1` cuts. One program
may be the root with an empty cut list. Cycles, shared children, unused cuts,
missing leaves and unknown references refuse before geometry generation.

The engine clips existing terrain/parent polygons by every ancestor half-plane,
then runs the existing connected-region, seed, minimum-area and interface owners.
All leaves must form connected valid allocations; disconnected results refuse.
Holes/exclusions remain holes and all suitable parent area is accounted for.
Programs require zero influence, no grid slots and no grid declaration. Other
strategies reject a partition declaration. Optional `RegionFrame` resolves both
cut origins and program anchors relative to the current generated regional seed;
saved source retains those offsets. Source cut order is canonicalized by ID.

There are at most 32 leaves/31 cuts per local layout, within existing total
program, cell, work and cancellation budgets. Strategy alternatives may replace
the cut geometry while preserving the same hard scope, anchors, purposes, source
rules and minimum areas. Bounded cut-offset adaptation is supported as described below.
The schematic consumes the same definition and saved geometry, checks tree and
half-plane coherence, and draws the existing semantic allocation layers. It does
not generate geometry itself or turn partition boundaries into roads or walls.

The framework fixture in `examples/metadata/binary-partitions` includes a direct
recursive recipe and an alternative recipe whose unsuitable grid advances to the
same recursive layout. Both retain seven nodes, two source-sized structures and
their constituents/reservations. Local analytical tests use three 40 m² spaces
with a diagonal second cut, then retain a 1 m² protected hole or remove a 4 m²
terrain notch. These are bounded algorithm tests, not all-game design acceptance.

[Recursive partition evidence](evidence/2026-10-29-binary-partitions-verification.json). Whole-world topology synthesis, large-scale
stress, nested-strategy cross-product qualification and browser/design acceptance
remain open. Artwork and Godot remain deferred.

## Bounded recursive cut adaptation

`MetadataLayoutDialKind.PartitionOffset` selects one recursive cut through
`MetadataLayoutDial.CutId`. Its `ProgramId` must be absent. Other dial kinds reject
`CutId`. Duplicate targets refuse. `Step` is a positive distance in the local
world unit; `MinimumSteps`/`MaximumSteps` bound signed integer multiples around
zero. Moving by distance d adds `(cos(normal) * d, sin(normal) * d)` to the cut
origin. The cut's angle and child references remain unchanged. The optional
regional frame then resolves that origin relative to the generated regional seed.

The current adaptation owner tests all permitted settings in nearest-change
order, preserves attributed rejection history and accepts the first feasible
complete program. Existing limits apply: at most three dials per scope, step
counts in -8..8, at most 64 combined settings and explicit attempts/shared work.
All endpoint origins must remain finite and within the source-coordinate bound
of ±10,000,000 per axis; transformed regional coordinates retain their own checks.
The engine recomputes partitions, interfaces, exact source placement and applicable
feedback for each attempt. No content, minimum area or protected anchor is dropped
to obtain feasibility. Failure preserves the accepted session world and cache.

Source-history restore replays the same bounded search. The JavaScript schematic
reconstructs declared settings and checks selected source/geometry coherence;
it does not independently generate a new world. Current framework examples are
in `examples/metadata/partition-adaptation`: four full physical programs test
positive, reversed-normal, signed-negative and oblique offsets. Every fixture
rejects two settings and accepts the third with seven retained nodes and two
unchanged structures/reservations. Exact work limits, translation, source edits,
rollback, checkpoint replay, CLI refusal and fourteen Node checks have
[evidence](evidence/2026-10-29-cut-adaptation-verification.json).

These offsets do not select a new tree, rotate a cut or optimize whole-world
quality. Multiple-cut/nested-scope cross-product stress, world-scale throughput,
browser/design acceptance and the complete generator remain open. One CLI export
hit the existing Windows directory-rename access denial before an isolated run
passed; publication reliability is not declared solved. Godot remains deferred.

## Metadata publication receipts

The shared metadata directory publisher commits complete staged output by rename.
Single-world exports, domain children, domain collections and their session
publications use this owner. No existing destination is replaced. The new
`publication.json` file has `Version: 1`, `CommitAttempt` in 1..4 and
`EarlierFailures` entries with sequential `Attempt` and `Win32Code`. The world and
its geometry/proof hashes do not include this operational receipt. A receipt in
private staging is preparation evidence; only the final target denotes committed
publication. Readers observing private receipt changes must permit read/write/delete
sharing and tolerate incomplete reads; ordinary consumers use committed outputs.

On Windows only, rename errors 5/32 may receive three cancellable waits of
50/150/350 ms. Retry requires staging still to exist and the destination to remain
absent. It retries the rename of the same complete data, never generation or a
partial copy. Every attempt rechecks cancellation and source dependency identity.
Session publications use accepted report identities; ordinary publications pin
dependencies at publication entry; domain collection checks retain their generated
domain identities. These checks do not constitute an external registry transaction.

An exhausted rename throws `METADATA_PUBLICATION_RENAME_RETRY_EXHAUSTED` with all
four failure codes and the native inner exception. Nonselected errors, existing
targets, cancellation and dependency mismatches refuse. Cleanup verifies the
absolute parent and owned staging prefix before recursive removal. If cleanup
also fails, `METADATA_PUBLICATION_AND_CLEANUP_FAILED` names the retained staging and
preserves both exceptions; it does not claim the staging was removed. Accepted
session state still changes only after successful publication.

Real controlled directory and child-file handles produced native errors 32/5;
both recovered after handle release, with retained receipts. Persistent child locks
exhausted all four attempts and proved cleanup failure preservation. Cancellation,
dependency-check rejection and a competing destination prevented publication.
Public world/domain exports retained exact generated data and all expected
receipts; the existing-output CLI check preserved every file. These have
[local evidence](evidence/2026-10-29-metadata-publication-verification.json).

The earlier external holder is unknown; code 5 can also be a permanent permission
failure and is never treated as proof of a transient lock. No ACL, antivirus,
process, software installation or native Godot changes are made. Receipt-write,
disk/network failures and arbitrary external mutation are not claimed recoverable;
maximum contention/throughput and real mutable-plugin drift stress are unqualified.
Godot and the broad world-generator design/scale gates remain deferred/open.

Primary API references checked 2026-10-29:
[Windows MoveFile](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefile)
and [.NET FileShare](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileshare?view=net-10.0).
The controlled local observations establish the 32/5 reproduction; the references
describe API/sharing behavior and do not identify the historical holder.

## Generated anchor partitions

`WorldLocalLayout.Strategy = AnchorBisection` needs only the existing required
programs, purposes/source rules, protected anchors, minimum areas and scope. It
rejects `Partition`, grid slots, grid declarations and nonzero influence. The
engine derives a binary tree; explicit `BinaryPartition` remains available when
the game deliberately authors its cuts. No missing anchor or program is invented.

For each group, the engine measures unweighted anchor covariance relative to a
source anchor and takes its principal axis. It sorts projections with ordinal ID
ties, keeps equal projections together and chooses the separable gap closest to
half the member count (earlier gap wins ties). The cut passes through the midpoint
of the two adjacent separating anchors, normal to that principal axis. Recursion
continues until each leaf names exactly one required program. Generated cut IDs
are deterministic for identical canonical input and cannot shadow program IDs;
they are not stable edit handles across arbitrary regrouping. Gaps at or below
1e-6 source units refuse with `WORLD_LAYOUT_GENERATED_PARTITION_RESOLUTION`.

At most 32 programs/31 cuts per layout are allowed under existing total-program,
cell, work and cancellation limits. Generated geometry uses the current binary
clip/connected-region/minimum-area/interface owners. Disconnected, empty or
insufficient allocations refuse; there is no scattering, shrinking or missing-leaf
fallback. Source anchors/purposes, minimum areas and physical dimensions remain
hard requirements. Minimum area is a constraint, not a covariance weight or an
optimizer objective. A different permitted strategy can be selected through the
existing bounded alternatives owner; numeric cut dials still target explicit trees.

`WorldLocalLayoutPlan.GeneratedPartitions` records each generated layout ID and
its complete cut tree, while `Definition` retains `AnchorBisection` with no authored
partition. Cut origins use the same source frame as anchors. Optional `RegionFrame`
resolves both against the generated regional seed; nested strategies clip the
existing parent's complete geometry. Read/export validation regenerates and
compares the plan. The schematic consumes saved trees and verifies source, tree
and half-plane coherence; it does not generate a competing tree implementation.

Analytical tests yield areas 50/35/35 m² and interfaces 5/7 m from three anchors,
retain a 1 m² hole or remove a 4 m² terrain notch, and preserve areas under a
nondegenerate 30-degree rotation/translation. Single/nested scopes, ID collisions,
a 32-leaf collinear case (4 m² each), disconnected allocations, resolution and
exact work limits are checked. Framework CLI examples in
`examples/metadata/anchor-partitions` retain seven nodes, two unchanged physical
structures/reservations and an interface route, with direct and strategy-selected
generation. [Evidence](evidence/2026-10-29-anchor-partitions-verification.json)
records source/tamper replay, edits/rollback/checkpoints, CLI refusal and ten
current Node checks, plus binary/cut/strategy and area-population regressions.

This chooses a deterministic local tree from authored anchor intent. It does not
search every tree or guarantee a feasible result whenever another partition could
fit. Isotropic/near-tied covariance can choose different axes after rotation;
global rotational invariance is not promised. Extreme-coordinate/conditioning,
arbitrary mixed nested strategies and whole-world throughput remain unqualified.
It does not generate streets/walls or autonomous game purposes. Full generator,
browser/design acceptance, artwork and Godot/runtime gates remain open/deferred.

## Terrain-derived required program spaces

`WorldLocalLayout.Strategy = AreaBalanced` uses `Programs: []`, an explicit
`ProgramIntents` list and `AreaPattern`. Each `WorldLocalProgramIntent` carries an
ID, purpose, source rule and positive minimum area, without an anchor. Authored
program coordinates, grid declarations/slots, partitions and `RegionFrame` are
rejected for this strategy; other strategies reject its intent/pattern fields.
Intents are sorted by ordinal ID, counted under existing per-layout/global limits
and participate in the same parent-ownership/interface validation as anchored
programs. No caller-supplied placeholder point is silently ignored.

The local owner replays its region plan and resolves any parent allocation. It
then enters the existing area's verified-region boundary; population and local
programs share one integrator/recursive partitioner, connected-leaf/centroid and
field/geometry checks. This avoids recursively reentering the local planner when
subdividing its own parent. It does not skip region provenance validation.

Each intent may declare a positive `AllocationWeight` up to `4e14`; an omitted
weight uses its minimum area. Each program receives target mass
`totalMass * effectiveWeight / sum(effectiveWeights)`. Uniform density therefore
gives area proportional to the chosen weights;
nonuniform density changes geometry/anchors, and the actual area must still meet
the hard minimum. There is no automatic guarantee that a density grammar can fit
all required shapes. Existing bounds apply: 32 programs per layout, 128 total,
shared cell/work/cancellation limits and bounded area depth/geometry. Unresolved
mass balance, zero/negative density, disconnected leaves, outside/hole centroids,
insufficient area or impossible physical placement refuse. No centroid is nudged
to a vacancy and no source member or dimension is dropped or shrunk.

`WorldLocalLayoutPlan.GeneratedAreas` records each layout ID and its full
`WorldAreaPopulationPlan`. That plan's `RelativeWeights` retain the exact canonical
effective-weight vector. `Definition` retains intents without coordinates. Each saved
stratum supplies the resulting program's world-space anchor, cells, boundary and
area; minimum/purpose/source provenance is copied from the corresponding intent.
Allocation-anchor bindings then position content through the existing placement,
access, interface and route owners. Nested scopes use their complete parent
geometry. Ordinary area population passes no relative weights and retains its
equal-mass behavior. Read/export validation regenerates and compares the plan.

With declared `AreaPattern.PartitionSearch`, actual minimum physical areas qualify
leaves during cut search. Required local interface spans also qualify candidate
allocations through the same shared-boundary calculator used for saved output.
When a later or nested layout cannot satisfy a geometric interface/parent constraint,
it can reject an earlier area choice before that choice commits. Each retry uses
only its own selected prefix; rejected future allocations are discarded. The other
strategies retain their existing clipping/ownership algorithms. Without cut search,
a single authored/default grammar can still fail explicitly.

The original per-area attempt/depth and shared local work/cell/cancellation limits
remain hard. Malformed source, numerical resolution, exhausted limits and
cancellation are not geometric alternatives. `GeneratedAreas` retains rejected
`local-interface:<id>`/`local-layout:<id>` qualification and cut receipts. Region
minimums remain constraints, not substitutes for source-sized placement. A shared
semantic boundary alone does not create a physical door, road or route; those
capabilities remain separately declared. See the
[local-interface search example](../examples/metadata/local-interface-search/README.md).

The schematic consumes the same saved area/weight provenance, verifies strata
against generated programs/geometry and shows existing semantic allocation layers.
It does not run another world generator. The current framework examples in
`examples/metadata/program-spaces` cover uniform density, density response, unequal
minimum areas and an authorized axis alternative after a reservation cannot fit.
All four retain seven nodes, two 84 m² physical structures, two 192 m² reservations
and the required interface route within explicit partial-world scope. Nested
structure count/geometry remains governed by the existing catalog, not area alone.

[Evidence](evidence/2026-10-29-program-spaces-verification.json) includes analytical
uniform/linear-density/hole centroids, unequal/odd weights, exact work limits,
input/centroid/cancellation refusal, nested scopes, complete content, density
counterfactuals, strategy selection, source/tamper replay, clean/incremental
equality, rollback/checkpoints, CLI/refusal and fifteen Node checks. Existing area
geometry, six-compound/194-member/19-route population, terrain-demand, generated
anchor and joint-layout regressions pass on matching current Core binaries.

Allocated area is not occupied-world coverage. Program purposes, counts and minima
remain game-authored intent; this does not infer every game's objectives or search
every possible subdivision. Extreme weight ratios/conditioning, maximum-size
throughput and broad mixed nested strategies remain unqualified. Strict centroid
and connectedness checks can reject a region that another grammar could use.
Browser/design acceptance, artwork and Godot/runtime remain open/deferred.


### Restricting direct network connections

`WorldNetworkOptions.ForbiddenConnections` optionally lists undirected pairs of
destination IDs that the generated network must not connect directly. The shared
solver excludes those pairs before routing or charging pair checks, and considers
the nearest allowed neighbors. Required and forbidden declarations cannot overlap;
unknown, duplicate, or self pairs are rejected. Regional network rules use their
regional destination IDs; scoped metadata rules use their selected node IDs.

This controls direct edges only. A forbidden pair may still reach one another
through other destinations, and route geometry may pass nearby. If the permitted
graph cannot satisfy connectivity or single-connection-failure requirements,
the planner returns its corresponding unresolved status rather than inventing
a forbidden link. Inputs are normalized for deterministic replay.

For regional rules, an allowed-pair graph that is already disconnected is
rejected during source preflight with
`METADATA_REGIONAL_NETWORK_ALLOWED_GRAPH_DISCONNECTED:<rule-id>`. If
`SingleConnectionFailure` is requested and the complete allowed graph has an
unavoidable bridge, preflight reports
`METADATA_REGIONAL_NETWORK_ALLOWED_GRAPH_NOT_REDUNDANT:<rule-id>`. Passing this
check is necessary but does not establish that terrain admits the routes.

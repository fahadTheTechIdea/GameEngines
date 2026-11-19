# S01 — Shared world model, layers and generation contracts

## Verified — supported-core contract gate (2026-11-02)

All six original S01 task criteria have bounded technical acceptance for current
supported core contracts. The qualification inventory covered 1,175 public types and 93
internal record models, including regional designs, blueprint sources and chunk
interior geometry. Thirteen recorded checks cover strict current inputs/callers,
semantic layers, independent physical domains, topology-only worlds, mixed local
layouts, actual parameter/seed effects and reported session/study/CLI operations.
[Current gate evidence](../../evidence/2026-11-02-s01-core-gate-verification.json)
owns this qualification; dated sections below retain their historical scope.

The reproduced CLI post-publication notification fault is fixed. CLI completion
and study reporting retain the exact published path or completed report, record
reporting failure separately and never append a false generation failure after
completion. Actual subprocess filesystem faults and direct API sink faults pass;
precommit failures still prevent publication and preserve earlier output. A
reporting failure remains a nonzero CLI result. No runtime fault switches, new
persistence owner or legacy route were introduced.

This qualifies the shared-contract stage, not the complete professional generator
or every possible game/extension. Remaining stage and whole-world design/variety
acceptance stays open. Optional artwork, native Godot and continuous physical
volumes are not qualified by reflection or this gate.

## Verified — blueprint edit and regeneration workflow (2026-11-02)

Use the public SDK consumer to generate from a blueprint, expand a revised
shared program, apply it against the accepted regional session and publish the
result. Check all region instances receive the change without dropping required
members or connections; compare clean generation, published replay and restored
checkpoint. Keep blueprint input hashes distinct from expanded session history.
Refuse invalid input and existing output; Godot remains deferred.

The public SDK example's `--regional-blueprints` mode hashes each authored
blueprint, expands it with the Core owner and submits the resulting source to
the existing transactional regional session. Published checkpoints retain the
expanded source history; the original blueprint inputs remain separate. A
receipt records their hashes and identifies the input kind.

On Windows/.NET 10.0.14, `dotnet run --project examples/worldcreator-workflow --
--regional-blueprints examples/metadata/regional-blueprint/blueprint.json
examples/metadata/regional-blueprint/larger-modules.json
generated/sdk-blueprint-edit-20261102` generated the baseline, applied the shared
module-size edit, matched clean generation, replayed both published worlds and
restored the final checkpoint. With `WORLD_SDK_BLUEPRINT_ROOT` set to that
output, `node --test tests/javascript/world-sdk-blueprint-edit.test.cjs` passed
two independent checks: exact blueprint hashes and every one of eight physical
module footprints changing from 1.5 to 1.7 square meters, with all 20 nodes,
12 sites and three bidirectional regional connections retained. A missing program
failed before output creation; an existing output was refused. The unchanged
`--regional-sources` first-world mode generated and replayed
`generated/sdk-regional-source-current-20261102`, passing the existing
`WORLD_SDK_FIRST` JavaScript check. Browser, live-agent and native runtime checks
were not run. Consumer sources SHA-256
`493E22D3C00017122F44CEDD053B6304B55DD5F7AFE3162BAF3AF3E5E37E85AB` and
`0A0B5A362C760EEB117ADC6DF0BA146138916F23516AF58FEC3A6C4053C2A9D3`;
edited input `6B8A2D37F390C748324A354192044FEE83C5C1672D6846951A5B667C6A972F6E`,
independent check `15A2200C7221EDE6A0677964E10ED540A8CAA9FA1E3CA5046F93FCCAD2FFF2CE`,
Core DLL `F55CFBB89028E6BA1065B586335AAFA2B370795D8E236E37DBFD59760C762EA5`.
This is one bounded SDK authoring flow, not the professional-generator gate.

## Verified — reusable regional program blueprints (2026-11-02)

Allow a developer to declare a complete region program once and explicitly bind
it to multiple semantic region IDs. Expansion must be bounded, deterministic,
strictly validate references and IDs, preserve every source rule and connection,
and pass the expanded request through the existing regional planner and replay.
Verify equality with the equivalent expanded world, CLI/SDK behavior, malformed
references, cancellation and no output on failure. This is input composition,
not automatic game design or a genre-specific generator. Godot is deferred.

The shared ownership planner expands named program prototypes into explicitly
named region instances and then uses its unchanged normal planner. Instances can
override weight, minimum area and clearance. The SDK consumer and
`generate-regional-blueprint` CLI both publish the same expanded source; save the
original blueprint separately for editing. Prototype pins and all unoverridden
settings are inherited, and endpoints remain explicitly game-authored.

On Windows/.NET 10.0.14, `dotnet run --project tests/WorldCreator.Tests --
--world-regional-blueprint generated/regional-blueprint-mixed-20261102` passed
exact request and generated-world equality with the existing four-region source,
mixed program binding, overrides, deterministic instance order, duplicate/missing
references, invalid override, cancellation and published source replay. That
world retains 20 nodes, 12 physical sites and three bidirectional connections.
The CLI generated `generated/regional-blueprint-cli-20261102`; its world JSON
has the same SHA-256 as the SDK result,
`C19AAB212A875FDB4589E9D909B71F07E08287699F93119EC41B9F825475ECD4`.
An unknown program reference exited 1 and produced no output directory. CLI
build passed with zero warnings; the test build retained the existing CS8602
warning in `WorldGroundUseCapacityTests.cs:133`. The example input is 11,863
bytes versus 36,128 for its repeated-program original. Core source SHA-256
`FC2425BE121936D83C1E3001AD7F60832C38BE94F2CB2FD015190CC4C3461A43`,
CLI source `3E5D692AF608203D70F7FDA3C189869E1A4D5DE691C60ED9278A3543A9BF41E3`,
test source `6A6DCCBCEA7274CCCBC35C6D475938BB3273923D791B2A1AF51B638FE884EC0F`,
Core DLL `F55CFBB89028E6BA1065B586335AAFA2B370795D8E236E37DBFD59760C762EA5`.
This does not qualify arbitrary game design, visual quality or native runtime.

## Verified — authored spiral spatial grammar (2026-11-01)

Provide a reusable deterministic expanding or contracting spiral grammar in
selected coordinate axes. The game declares center, starting radius, radial
step, angular step and direction; this is not arbitrary scattering or a city
rule. Preserve hierarchy and topology and let existing physical placement,
access and routing owners enforce full source geometry. Validate finite inputs,
nonnegative radii, direction, coordinates, cancellation, deterministic source
replay, physical 2D content and a non-city higher-dimensional example. Godot
is deferred; this grammar alone is not a complete world generator.

The shared grammar generates expanding/contracting, clockwise/counterclockwise
positions in selected axes. Physical population uses the same calculation under
the existing placement work budget. A small world retains four solid sites and
bidirectional travel, with independent nonendpoint collision checks; overlapping
sites and unsupported facing refuse. A higher-dimensional non-city hierarchy,
source edit/replay, failed collision rollback, CLI/SDK equality, strict invalid
input and exact schematic positions pass. Existing layout and full prescribed-
population regressions also pass. Modified saved source is refused by package
verification. The schematic now checks declared spiral source and positions
for both faced and unfaced populations; complete source-package verification
remains authoritative. Tangent-facing is implemented separately in S07. Broader genre/design
acceptance and Godot remain open.
[Example](../../../examples/metadata/spiral-world-program/README.md),
[evidence](../../evidence/2026-11-01-spiral-world-program-verification.json).

## Verified — bidirectional arc grammar (2026-11-01)

Allow a nonzero signed sweep up to 360 degrees in the existing arc layout.
Negative sweeps traverse clockwise in the selected axis plane; open arcs retain
both endpoints and full rings omit the seam in either direction. Preserve positive
results and deterministic hierarchy order; reject zero, out-of-range and nonfinite
values. Direct strategy, nested generation, CLI generation/source replay and unchanged positive-sweep packet pass. See [evidence](../../evidence/2026-11-01-clockwise-layout-verification.json). This is a bounded grammar improvement; broad world design remains open.

**Current — public SDK entry workflow (2026-11-01):** The acceptance below is
verified at the existing consumer boundary. `RegionalWorkflow` delegates complete
design/edit/reuse/history to public Core owners; no algorithm wrapper or game
dependency was introduced. Program and domain modes forward the same cancellation
token through file and session operations. The Core-only build passes with zero
warnings/errors. Four public runs,13 consumer checks, three console cancellation
probes and26 byte-identical packet/checkpoint comparisons pass;41 durable
correlations include failures/cancellation. Root/metadata/consumer guidance uses
the current metadata-first route. An existing stale SimEngine consumer link was
replaced with its current runtime integration owner, without touching those engines.
[Consumer](../../../examples/worldcreator-workflow/README.md),
[exact qualification and retained check failures](../../evidence/2026-11-01-developer-entry-verification.json).
This does not close the full S01 inventory/reporting tasks or professional world
design, browser/live-agent, broad variety/scale gates. Godot/artwork deferred.


**Acceptance before developer-entry cleanup (2026-11-01):** Correct the root
README's stale procedural-feature limits and unconditional visual setup route.
Metadata-only framework work must lead to current catalog/recipe, region and
domain APIs without GameArts or a named game. Extend the existing public-Core-only
workflow example with regional generation/edit/publication/restore, rather than
adding a wrapper engine. Keep existing single-world/domain modes current, propagate
cancellation through real API/file boundaries, reject malformed arguments/inputs
before accepted output and compare edited worlds with authoritative clean generation.
Exercise the runnable consumer across distinct programs, regional overlay edits,
checkpoint replay, normal reporting and failures. Scope/art/runtime limits remain
explicit; no solution-wide/full-suite, artwork, Godot or live-agent claims.

**Acceptance before direct input isolation — 2026-10-31:**
The direct Generate and DescribeParameters APIs must detach recipe/catalog values
before invoking registered layout code. A layout callback changing the caller's
parameters, properties, part array or coordinates cannot change the captured
program, provenance hashes or binding descriptions. Keep direct unversioned
layouts available; measured incremental dependencies retain their own contract.
Use native typed serialization for the internal copy, preserving nonfinite typed
numbers for rejection by their existing semantic validator. File readers and
publication remain strict. Verify callback mutation during validation and actual
layout, returned data isolation, cancellation, original validation failures,
session equivalence and unchanged complete-world output. Concurrent writes during
input capture are outside this contract. Godot/art remain deferred.

**Acceptance before explicit generation-parameter bindings — 2026-10-31:**
At the existing metadata generator, reject supplied generation parameter names
that no catalog count, capacity-quantity or program-demand binding declares.
Expose those actual catalog bindings through the C# API without generating a
world or starting art/game setup. Keep inactive/zero-count/alternative declarations
valid; declaration does not certify runtime use. Preserve arbitrary game metadata
in archetype/connection properties. Verify all three consumed parameter roles,
missing/unknown/case-sensitive names, repeated and inactive references, immutable
descriptions, cancellation, session rollback, publication/CLI and unchanged valid
world geometry/counts. Reproduce the old silent acceptance first. Do not add a
compatibility path, new world-layout algorithm or larger generation limits.

**Explicit ground-use contract — 2026-10-30:**
Catalog physical footprints may declare game-authored GroundUse kind, purpose, source
rule and minimum source-open fraction. The existing physical accounting owner binds
and enforces actual physical sources, role, source geometry and measured solid unions.
Undeclared reservations do not imply intentional open ground; no default world density
or traversability claim is introduced. See the canonical [contract](../../WORLD_METADATA.md#explicit-ground-use-intent)
and [qualification](../../evidence/2026-10-30-ground-use-verification.json).

**Developer lifecycle qualification — 2026-10-30:**
The reported public session lifecycle uses the existing operation reporter for
create/edit/publish/restore. It snapshots source inputs/context, attributes stage
and reason, excludes interleaved mutations and distinguishes committed reporting
faults from failed/cancelled worlds. Final durable API/CLI evidence spans four world
shapes; pure/optional-domain coverage remains separately open.
[Exact sources, commands, retained failures and limits](../../evidence/2026-10-30-session-workflow-verification.json).
Stage/core remain IN PROGRESS; Godot/artwork deferred.

**Acceptance before direct session lifecycle reporting — 2026-10-30:**
Extend the existing MetadataGenerationSession owner with an asynchronous reported
start/edit lifecycle using the shared WorldOperationReporter. Report generation,
edit, publication and restore with correlated structured input/world identity,
stage/diagnostic attribution and explicit outcomes; preserve pure algorithm APIs.
Use the same generation, proposal, checkpoint and atomic publication owners.
Detach caller inputs before reporting callbacks. Preserve all original world
members, geometry, constraints and generation budgets across distinct world types.

Before commit, failures/cancellation retain the accepted session and output. After
the actual state/directory commit, late cancellation must retain success; a failed
success-report sink must expose the committed session/output explicitly and must
not record a failed world or retry generation/publication. Preserve both the
operation and reporting exceptions on a failure-event sink error. Hold session
exclusion across awaited reporting so callbacks/concurrent calls cannot interleave
mutations. Replay source history without inventing fresh edit actions. Qualify
successful create/edit/publish/restore, invalid/protected/stale inputs, progress
cancellation, callbacks, busy contention, all sink failures and atomic behavior
through actual durable API/CLI operation stores. Complete developer workflow cases
must include a non-settlement surface, generated interior and topology-only world.
This does not close all low-level/optional-domain reporting or design gates.

**Generation-study boundary — 2026-10-30:**
The current executable inventory includes 18 root contracts, adding the study
input/report. Study definition validation, detached bounded snapshots, per-case
refusal stages and existing strict/multipart storage are qualified. Its direct
workflow emits real Log/Audit/Notification events; this does not establish complete
operational coverage of every existing raw generator API. Generic JSON reading of
a study report does not independently authenticate its measurements: compare the
complete saved study/case sources and world hashes, or rerun the study. No public
arbitrary-study replay verifier or cryptographic signature is claimed.
[Current evidence](../../evidence/2026-10-30-generation-study-verification.json).

**Current contract audit — 2026-10-30 (bounded qualification; stage in progress):**
The inventory below maps 22 fact/ownership boundaries; the executable inventory
records properties, constructors and assembly identities for 16 root contracts.
A reproduced current CLI run accepted conflicting `Scale` fields and published.
`WorldJson` and the storage manifest parser now use .NET 10 native duplicate-name
rejection. The hand-written manifest-only walker is removed. Typed opaque catalog
properties pass the same native shape check before expansion. Required/unknown
fields, source hashes, byte/depth bounds, cancellation and atomic publication stay
with their existing owners.

Current tests reject root, nested, escaped-equivalent and opaque duplicate names,
including correctly hashed complete multipart bodies and a token crossing a 1MiB
part boundary. Unique arbitrary properties and a valid large body remain accepted.
The real CLI publishes a valid graph-only world, refuses duplicate recipe/catalog
and retired input fields without output, emits three failed/one succeeded durable
events and preserves the previously published world. Layered graph publication,
source preservation and cancelled generation are checked separately.
[Source-qualified commands, inventory, failures and limits](../../evidence/2026-10-30-core-audit-verification.json).

Remaining S01 gates include the complete planner/optional-consumer duplication
inventory, direct API reporting coverage, unsupported-domain capability review and
broader contract/version assurance. The broad regression matrix uses its frozen
revision; final affected regressions are reported separately. These checks do not
close the full core, design, scale, art or runtime gates. Godot remains deferred.

**Cancellation contract correction — 2026-10-30:**
The shared surface-area visitor now observes cancellation immediately after the
predicate returns, including the constant-field fast path and a false predicate.
Both outcomes have deterministic cancellation tests. Existing route cancellation
and the sixteen affected regression scopes pass. This is bounded S01.3 evidence,
not completion of the shared-contract audit.
[Exact verification](../../evidence/2026-10-30-route-guidance-verification.json).

**Mixed-domain endpoint qualification — 2026-10-29:**
`MetadataDomainEndpoint.NetworkRuleId` selects a saved regional-network destination
with an exact movement-profile match. Markers require no fabricated building;
solid nodes keep the existing access-owner port and width. A 19-node terrain,
generated-room and geometry-free progression program preserves complete forward/
return source witnesses. Existing domain/session contracts remain current.
See [bounded verification and remaining gates](../../evidence/2026-10-29-mixed-domains-verification.json).

**Cross-domain journey qualification (2026-10-29):** Existing route witnesses
and explicit transitions now form declared forward/return journeys, with exact
source-vertex joins and full movement-profile/kind matching. Three domains/21 nodes
retain unchanged generation; the journey has 11 qualified steps in each direction.
Independent positional/domain/direction continuity, logical-only paths, missing
local kinds/profile/return, exact work, source/tamper/read/export and CLI refusal
have [evidence](../../evidence/2026-10-29-domain-journeys-verification.json). Three
Node artifact/oracle checks and existing-domain/interior regressions pass. Stage
remains IN PROGRESS: absent-route synthesis, maximum graph/scale stress, 3D meshes,
runtime movement and visual acceptance are unqualified. Godot deferred.

**Transition-domain qualification (2026-10-29):** Independent local worlds now
compose through the current generator with explicit physical/logical endpoint
references and directed domain reachability. The outdoor/two-floor fixture retains
21 nodes and two qualified transitions, with identical local geometry/IDs safely
separated. Exact endpoint/path/source oracles, aggregate proof/count limits,
logical-only domains, blocked/narrow/missing/one-way refusal, source/read replay,
atomic export, CLI audit and three Node artifact checks have
[evidence](../../evidence/2026-10-29-world-domains-verification.json). Interior and
joint-layout regressions pass. Stage remains IN PROGRESS; transitions do not create
3D connectors, runtime switching or undeclared local journeys. Whole-world scale,
autonomous synthesis and visual acceptance remain open; Godot deferred.

## Active increment — composed transition domains (2026-10-29)

Bounded implementation locally qualified; the stage remains IN PROGRESS. The
existing metadata generator accepts a versioned
collection of 2–16 independent domain recipe/catalog pairs. Keep local coordinates,
units, source geometry and IDs independent; shared node IDs in separate domains
are legal. Transitions name endpoint domain/node plus an existing physical access
port or interior opening and local traversal profile; geometry-free domains use
logical node endpoints. Qualify physical endpoints through current traversal,
wall and solid owners. Do not invent a global surface or continuous stair/portal
geometry. Optional directed domain-reachability obligations refer to the explicit
transition graph, not undeclared local paths or runtime scene changes.

Acceptance: an outdoor program and two overlapping-coordinate interior floors,
complete source preservation, independently verified transition endpoints and
directed graph requirements; bad/missing/blocked/narrow endpoints, impossible
return, source/witness tamper, cancellation and finite aggregate counts/proof work;
source replay, atomic multi-domain export/refusal and actual CLI evidence. Keep
per-domain schematics and a root review index. Registered dependency identity must
remain checked. Godot/runtime transition behavior and 3D stair geometry deferred.

Plan date: 2026-10-26. This document specifies future work; it is not an implementation-completion claim. Status and evidence are owned by the [master stage tracker](../../ROADMAP.md#master-stage-tracker). Requirements, decisions and shared delivery rules are in the [master plan](../../ADVANCED_WORLD_GENERATION_MASTER_PLAN.md#current-staged-plan).

Dependencies: None; first implementation stage.

Requirements: W01, W03, W07, W08, W10, W12, W13. Responsible executor: Codex, including explicitly labeled self-review. Accountable product/design owner: Fahad. No external or independent reviewer is assigned or claimed.

## Purpose and current gap

The C# metadata engine now composes semantic intent, terrain, regions, local layouts, source-sized programs and connections through shared owners. Authored mockup compilation remains a separate optional visual input route. The current gap is full contract/consumer qualification and explicit authority coverage, not restoration of city geometry or a mandatory grid, artwork or Godot.

## Algorithm and design

Separate game intent, topology, spatial domains, semantic layers, content programs and presentation bindings. A generation layer declares its dependencies, coverage, units, coordinate frame, owner and provenance. Layer types are extensible; terrain, water, routes and structures are examples rather than required enum values. Physical elevation levels are separate from layer ordering. One recipe can select different algorithms for different regions. Use capability contracts for surfaces, volumes, topology and movement instead of a switch on genre names. Catalog source dimensions and required counts must be resolved before spatial allocation.


## Inputs and outputs

**Inputs:** Current source/contracts, game-authored examples, catalogs, source dimensions, existing schema/load/export behavior and the owner corrections.

**Outputs:** A versioned shared schema and capability matrix; a dependency-aware generation plan; layer inventory; contract migration/removal map; runnable non-city and urban-region programs through shared world primitives, with no reuse of the removed city generator.

## Code and workflow owners

### S01.1 current owner inventory — 2026-11-02

At S01 qualification the reflection inventory covered 1,175 public API/data types and 93
internal record models across Core, Worlds, Catalogs.Modern, Godot and the CLI.
It records actual properties, constructors, method input/output signatures and
assembly/module identities, including current regional/blueprint/session/interior
transport shapes. This replaces the previous manually selected 18-root subset.
Internal models and optional Godot API presence are inventoried, not declared
supported file entry points or qualified native consumers. No native run is
inferred from reflection or transitive C# compilation.
The ownership decisions below distinguish original authority from derived replay
receipts; copying a source into a self-contained packet does not create a second
writable authority. Optional consumer qualification remains separate; the current
S01 gate record maps the supported-core inventory to the original task criteria.

| Fact/input → output | Responsible owner | Keep/extend/replace/remove decision and duplicate-fact guard |
| --- | --- | --- |
| Direct recipe/catalog values → private generation/discovery input | WorldMetadataGenerator (Inputs partial) | Extend the current owner with a native typed copy before layout callbacks. Counts/properties/coordinates and input hashes share one captured source. Direct unversioned layouts remain distinct from measured incremental dependencies; concurrent mutation during capture is unsupported. |
| Catalog generation-parameter declarations -> validated control names and descriptions | WorldMetadataGenerator (Parameters partial); capacity-rule validation stays at MetadataCapacityPlanner | Extend the current owner. Count/capacity/program-demand fields are the sole binding authority; unknown supplied controls refuse. Static descriptions include inactive declarations and do not claim executed use. Game metadata remains in Properties. |
| Recipe/catalog → hierarchy, counts, purposes and logical links | `WorldMetadataContracts` / `WorldMetadataGenerator` | Keep the generic semantic owner. Archetypes/parts declare required content; expansion and replay retain source IDs. Game-profile/scale/kind names are descriptive and do not choose a city algorithm. |
| Axes, units, authored relative coordinates → final node poses | `MetadataSpace`, `MetadataLineLayout` / `MetadataPathLayout` / `MetadataArcLayout`, allocation/placement owners | Keep separate metadata coordinates and physical constraints. Registered layout dependencies have measured implementation/configuration identities. A 3-axis metadata point is not a physical volume capability. |
| Domain recipes/catalogs and transitions → independent local worlds and journey witnesses | `WorldMetadataGenerator.Domains` | Keep independent local identities/units and explicit transitions; overlapping local coordinates are legal. No invented global terrain or 3D connector. |
| Layer declarations/memberships → ordered layer plan | `MetadataLayerPlanner` | Keep semantic kind/dependency/coverage validation. Layer order is not physical elevation or execution order. Unassigned content remains explicit. |
| Authored terrain/landforms/height samples → surface samples | `WorldSurfaceQuery`, `WorldTerrainGenerator`, `WorldHeightFieldQuery` | Keep the shared immutable query and original lattice/source buffer. Terrain sampling cells do not define game layout. |
| Environment/drainage/water/wind inputs → derived fields and source receipts | `WorldEnvironmentQuery`, `WorldDrainagePlanner`, `WorldWaterPlanner`, `WorldWindMoisturePlanner` and metadata binders | Keep optional game-authored derivations. Surface/source IDs join outputs; physical realism is not a default game requirement. |
| Suitability/region/program intent → region/local allocations | `MetadataRegionPlanner`, `MetadataLocalLayoutPlanner` and shared region/layout solvers | Keep terrain-constrained allocation with declared programs. Grids are local strategies beside irregular layouts, not a world requirement. |
| Archetype footprints or registered variants → exact source envelopes/reservations | `MetadataFootprintResolver` / catalog registry | Keep one dimension authority. Conflicting direct/registered/demand footprints reject; resolved footprint copies are replay receipts. |
| Parent/child programs and source-sized demands → complete placements | `MetadataPlacementPlanner`, program/composition/population coordinators | Keep full declared hierarchy and counts. Derived node poses, site polygons and relative transforms are validated together; no empty-position scatter or part reduction. |
| Access definitions, solids and walls → physical sites/entrances | `MetadataPhysicalAccessPlanner` / `MetadataInteriorPlanner` | Keep source ports, widths and occupancy separate from reservations. Placed geometry is derived and replayed; an entrance is not an arbitrary marker. |
| Traversal/network/interface declarations → exact full-width passages and graph receipts | `MetadataRoutePlanner` / `MetadataNetworkPlanner` / shared traversal/network solvers | Keep game profiles, terrain and explicit joins. Routing boundaries constrain actual envelopes; complete query/cell-window dependencies drive private memo reuse. |
| Capacity/service/context definitions → allocation and source-use receipts | `MetadataCapacityPlanner`, `MetadataServicePlanner`, `MetadataContextPlanner` | Keep game-authored demand/capabilities; source dimensions are referenced rather than independently reauthored. |
| Topology/progression/variation declarations → graph witnesses and selected variants | `MetadataTopologyPlanner`, `MetadataProgressionPlanner`, `MetadataVariationPlanner` | Keep optional graph-only worlds. Variation/terrain seeds are effective controls at their actual owners; descriptive metadata is not advertised as an algorithm. |
| Protected inputs and edit proposals → accepted snapshots/checkpoints | `MetadataGenerationSession`, `MetadataDomainGenerationSession`, `MetadataExecutionCache` | Keep atomic accepted state and source-history replay; no private geometry cache restored from JSON. Engine identity/configuration guards stale history. |
| Chunk declaration → member ownership/index | `MetadataChunkPlanner` | Keep half-open ownership plus closed references without changing source geometry. Shared plans and complete persisted parts are not independently generated streaming terrain. |
| Shared surface, arbitrary region programs and ownership intent → complete regional worlds | `MetadataRegionalOwnershipPlanner`, `MetadataRegionalWorldGenerator` and shared partition/program owners | Extend the existing common world pipeline. Regions are semantic generation domains, not mandatory streets/blocks. Complete source programs, actual terrain, ownership masks and directed journeys remain hard constraints. |
| Regional source/dependency context → eligible complete private packet | `MetadataRegionalWorldGenerator` context/regeneration partials, `WorldSurfaceQuery` regional projection | Keep one surface selection owner. Whole relevant patches/exclusions and closed contact determine local eligibility; height recipes, world boundaries and ownership/domains remain conservative dependencies. External packets replay; reuse never exempts global constraints. |
| Explicit region requests/releases → bounded resident packets | `MetadataRegionalGenerationPlan` within `MetadataRegionalWorldGenerator` | Extend the same generator with an ephemeral private SDK handle. Serialized packet bounds and actual resident/generation counts are distinct from managed heap or independent initial world design. No alternate save protocol. |
| Regional source/definition edits and protection changes → accepted world/policy/history | `MetadataRegionalGenerationSession` | Keep one atomic accepted report/state. New protections capture accepted values/poses; surviving receipts remain; explicit releases and initial/effective policy history replay through normal publication. Source pins are authored input, not a second mutable location store. |
| Inline/multipart typed files → validated source/document streams | `MetadataJsonStorage` / `WorldJson` | Replace duplicate-name acceptance with .NET 10 native controls for parser and serializer. Remove the custom manifest-only walker. Keep required/unknown-member rules, byte/depth/hash/path limits and full source values. |
| Current metadata/domain source and receipts → JSON, summary, review, schematic and SVG | `WorldMetadataGenerator` publication paths, `MetadataDirectoryPublication`, `WorldMetadataSchematicExporter` | Keep staged atomic publication and exact source geometry. Standalone/embedded receipts are copies checked against the same original source; renderers do not regenerate positions. |
| Raster mockup design/capture → `WorldDocument` / authored semantic scene | `WorldModel`, `MockupWorldDesign`, capture compiler, `SemanticWorldBuilder` | Keep as a separate bounded optional visual route. Pixel anchors/provenance are not procedural terrain authority and do not become mandatory metadata inputs. |
| Registered authored catalog content → catalog pack definitions | `CatalogContentAuthoring` / `SettlementCatalogRegistry` | Keep strict typed content and explicit trusted plugin selection. Test plugin location now comes from its actual copied build output, not inferred repository/configuration paths. |
| Approved art and scene-production status | GameArts lifecycle/production gate and approved-art registry | Keep exact external approval/source hashes as authority. A scene boolean or metadata coverage value is not an independent approval. Visual/capture/native-consumer qualification remains separate and deferred here. |
| Original named-program blueprint → expanded design/accepted history | `MetadataRegionalDesignGenerator` and existing regional session | Keep original source as authority; expansion is a checked derived copy. Source-only refactoring has its own source pin, even with identical world geometry. |
| Generated interior walls/openings → source-bound chunk geometry | `MetadataInteriorPlanner` owns facts; `MetadataSpatialMembers` extracts their coverage | Extend derived export, retaining complete original geometry and canonical identities. Immutable copies and shared-door consistency checks do not create another source authority. |
| Operational success/failure/cancellation | Shared reporter and durable Log/Audit/Notification stores at reported sessions, studies, packages and CLI boundaries | Completed/committed reporting faults preserve recoverable results; precommit failure preserves previous output. Raw algorithms/file helpers use the host's reporting boundary and do not silently create a global audit store. |
| Rejected city generator, old request fields and retired commands | Removal enforced by `CurrentContractTests` and strict current input readers | Remove/keep removed: no production types, CLI routing, aliases, compatibility parser or fallback engine. Shared authored catalog/geometry utilities remain distinct current owners. |

First implementation acceptance: one validated semantic layer manifest for generic spatial and topology-only outputs; authored arbitrary layer kinds, purpose/source-rule provenance, dependencies and declared coverage; stable dependency-first order; explicit unassigned nodes/links; unknown memberships, duplicate declarations, cycles, malformed coverage and resource limits reject. Preserve coordinates/hierarchy/links and allow topology-only worlds with no invented ground. Snapshot caller collections. Round-trip through actual files/CLI; fail before publication and preserve prior output. Output metadata advances to schema 2 with a required layer plan; input recipe/catalog schema 1 gains optional layer declarations/membership. This is an additive input capability, not a compatibility parser. The original slice did not qualify physical domains, dependency-driven execution/invalidation or renderer controls. The current S01 gate qualifies supported domain/session contracts, actual dependency invalidation and mixed local-layout execution through the owners above; continuous physical volumes and optional renderer controls remain outside this acceptance.

Current shared contracts and algorithms are in WorldCreator.Core through WorldMetadataContracts and WorldMetadataGenerator. WorldCreator.Worlds retains authored visual compilation/export owners. WorldModel, MockupWorldDesign and SemanticWorldPackage are inventoried above as optional visual boundaries; the rejected city document has no runtime owner. Reuse OperationalServices and current CLI reporting. Godot remains a deferred downstream adapter. Root inventory execution does not qualify every visual/native consumer.

The inventory names current inspected owners, not a promise to retain deficient behavior. Replace superseded behavior at its current owner, migrate all callers and remove the retired implementation; no compatibility aliases or parallel legacy path. Preserve unrelated changes and immutable historical evidence.

## Ordered implementation tasks

| Task | Work | Acceptance evidence |
| --- | --- | --- |
| S01.1 | Inventory every current input/output and locate duplicated spatial, hierarchy, source and approval facts. | A keep/extend/replace/remove table names one responsible owner per fact; rejected city orchestration is removed. |
| S01.2 | Specify coordinate domains, units, transforms, geometry roles, hierarchy, layer dependencies and coverage. | A layered surface and a topology-only world can both be represented without invented terrain. |
| S01.3 | Define algorithm inputs, outputs, hard constraints, diagnostics, cancellation and bounded redesign requests. | Unknown capabilities fail explicitly before output publication. |
| S01.4 | Define deterministic identities, actual random-stream use and source/build digests. | No no-op seed or metadata field claims an algorithmic effect. |
| S01.5 | Migrate current callers and fixtures to one current contract at each changed boundary. | Retired fields reject; no alias, compatibility parser or parallel old/new pipeline remains. |
| S01.6 | Wire success, failure, cancellation, export and dependency invalidation into existing reporting. | A failed run cannot record a successful world; prior output remains intact. |

## Failure handling and operations

Validate inputs and required capabilities before expensive work. Apply explicit cancellation and finite work/resource budgets at stage boundaries and inside iterative work. Return attributed unsatisfied constraints instead of silent fallback, missing required content or partial success. Preserve prior accepted output on failure. Use the existing Log/Audit/Notification owners for applicable generation, export and review boundaries; UI consumers display actionable failures and cancellation. Record source/input identities, stage outcome and unresolved dependencies. Retry only after the cause or inputs change, with the same declared reproducibility guarantees.

Recovery is a coherent correction of the current owner and affected inputs, followed by focused checks. Do not restore a retired implementation as a runtime fallback. Artifact approval gates apply only to the relevant visual stages and exact revisions.

## Verification and acceptance cases

- Contract round trips preserve nested ownership, arbitrary kinds, units, layer IDs and source references.
- A local grid and an irregular local region share world contracts without invoking the removed city generator; the current mixed-program execution qualifies this bounded geometric case, beyond the original layer slice.
- Malformed dimensions, cycles, unknown strategies, stale dependencies, budgets and cancellation reject with attributable diagnostics.
- Current metadata/catalog/current-contract checks remain valid after intentional fixture migration; do not use those checks as whole-world acceptance.

Run relevant checks with commands, environment, input/source digests and observed outcomes recorded. NOT RUN is not PASS. Existing tests are baseline evidence only. Keep implementation, local technical qualification, browser/native review, owner acceptance and unavailable CI/release gates distinct.

## Risks and boundaries

Avoid an abstract plugin platform that only wraps the existing city generator. Do not retain the rejected city-generation algorithms. Qualify any independently useful low-level geometry utility before reuse. Schema changes must be explicit and evidence/history preserved.

## Exit gate

The supported-core technical exit gate is verified. Current mixed-program runs
retain all 43 nodes, source-sized content and connections in inland/coastal
physical worlds with different local layout strategies. Reported wilderness,
interior, compound and topology-only workflows exercise generation, edits,
publication, restore and invalidation through the shared owners. Current caller
and strict-input checks reject retired APIs/commands/fields. Effective seed and
parameter tests reject no-op or unknown controls. Completion/failure fault checks
preserve earlier output and recover completed results. See the
[current six-task evidence map](../../evidence/2026-11-02-s01-core-gate-verification.json).
The earlier [removal/layer](../../evidence/2026-10-26-world-replacement-verification.json)
and [surface](../../evidence/2026-10-26-world-surface-verification.json) records
remain historical increments, not the current gate boundary.

S01 closes only when both urban-region and non-urban inputs execute through the new shared world boundary without the rejected city code and all replacement callers are migrated. A design diagram or new interface alone is insufficient.

The existing master tracker records this technical qualification. Continue
remaining original stage capability/design criteria listed in the master tracker. This stage's
acceptance does not qualify all genres, optional consumers or the whole generator.

## Owner-supplied implementation references — 2026-10-26

R17/R18 motivate distinct generated field data, generator operations, controlled edits and pure rendering. Define stable terrain-cell ownership at shared boundaries and spatial-index queries before representing a dense mesh as thousands of priority overlays. R19/R21 are optional source/volume adapters, not mandatory world models. See [R16–R22 and the implementation priority](../../WORLD_GENERATION_RESEARCH.md#owner-supplied-implementation-references--2026-10-26). This records a design decision, not integration or runtime evidence.

## Research references

[R01](../../WORLD_GENERATION_RESEARCH.md#r01), [R02](../../WORLD_GENERATION_RESEARCH.md#r02), [R08](../../WORLD_GENERATION_RESEARCH.md#r08). These references motivate the proposed design; they do not certify this implementation or imply adoption of external code. The research record distinguishes accessed full text from abstracts and records relevant limits.

**Source-ground capacity qualification:** the shared accounting owner now provides
a conservative contained-program preflight with typed witnesses and existing
pose-invariant/source-coordination handling. Exact final accounting remains required;
this is not automatic placement repair or complete ground/genre support. See the
[contract](../../WORLD_METADATA.md#explicit-ground-use-intent) and
[current evidence](../../evidence/2026-10-30-ground-capacity-verification.json).

**Current qualification (2026-10-31):** complete67-node/63-source physical
fence assembly, automatic source generation, width-qualified gate journeys and
preserved refusal causes pass. Independent complete union equals the intended
boundary with one3.8m opening. Five native scopes,20 JS checks and three final
Release builds pass; three160-node/156-source world packets remain byte-identical
to prior output. Isolated final harness and prior-Core failure are explicit; one
prior test warning remains. See [current evidence](../../evidence/2026-10-31-fence-assembly-verification.json).
Original full core/design gates remain open; Godot/art deferred.

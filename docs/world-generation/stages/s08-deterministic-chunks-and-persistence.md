# S08 — Determinism, chunks, authored edits and persistence

## Technical S08 gate — verified for supported core protocols (2026-11-02)

All six original task acceptances now have bounded technical evidence, mapped
individually in the [S08 gate record](../../evidence/2026-11-02-s08-core-gate-verification.json).
The [reproducibility contract](../../WORLD_METADATA.md#reproducibility-of-generated-data)
specifies source/member identities, build/configuration pins, numeric comparison,
ordering semantics and resource bounds. This completes the technical S08 gate
for current metadata/surface/interior/regional/domain protocols. Other core
stages and the professional-generator/design goal remain open. Runtime streaming,
cross-platform bitwise equality and native consumers are separate capabilities.

The audit found and fixed an actual source omission: chunk extraction had kept
rooms/routes but dropped generated walls and doorway facts. Shared extraction and
schema-4 geometry packages now retain exact wall polygons, edge endpoints,
doorway widths/approaches and rule provenance. Each room remains one whole member
across seams; both incident rooms reference the same canonical rule/connection
doorway. Partial/reversed/duplicate loads preserve six rooms, 34 walls, five
doorways and ten directed interior paths across 37 chunks. Inconsistent doorway
facts and retired packages refuse. One complete shared water mask remains whole.
No legacy loader or raised source/storage/work cap was introduced.

Fourteen recorded checks pass: current build, geometry/domain packages,
generation/release/load order, exact whole-world assembly, actual terrain edits,
untouched regional reuse, dependency drift, source replay, staging cancellation,
serial scale/refusal cases and seven independent JavaScript checks. Sixteen-region
locality retains all 80 nodes/48 bodies and reuses fifteen untouched packets.
Current persistence qualification also retains the unchanged owning source code
and its source-history/failure evidence. CLI build has zero warnings/errors; the
test build has one pre-existing nullable warning in `WorldGroundUseCapacityTests`.

Serial Debug measurements on Windows 11/.NET 10.0.14, Ryzen 5 5600X and 64 GiB
qualify 85/169/493/577-node hierarchies (20/40/108/128 child stages) and
257/513/1025-node physical populations. Observed process peak working sets reach
273,354,752 and 214,798,336 bytes respectively. These are lifetime high-water
marks within each process; allocation counters are cumulative, not live heap.
The 144-child-stage hierarchy and other declared infeasible/work-bound cases
refuse. These measured profiles are not universal maximum world sizes or timing
guarantees. Exact inputs, timings, resources, rejects, source hashes and earlier
fixture errors are preserved in the gate record.

Earlier qualification entries below retain their original increment status;
this gate record and the master tracker own current S08 technical acceptance.

## Verified — editable reusable blueprint designs and S08.4 persistence (2026-11-02)

Close the authored-source gap in the existing regional session: preserve the
original named-program blueprint along with its validated expanded design,
without creating another generation or persistence owner. A blueprint session
and blueprint revision use the normal seeded design/terrain/content pipeline,
protection policies, atomic publication, source pin and replay. The source pin
must include blueprint-only changes even when generated geometry is unchanged.
Acceptance: initial and edited blueprint sources survive checkpoint restore;
seed edits match clean generation and retain complete parts/routes; stale,
malformed, mismatched-source, cancelled and failed-publication edits preserve
accepted state. Tampered blueprint provenance must fail replay. Exercise public
SDK and CLI paths, source-only edit and rollback. Keep existing byte/work limits.

All checks passed on the final local Core. The CLI and SDK produce byte-identical
initial, changed-seed and rollback world packets, retaining four regions, twenty
nodes, twelve physical sources and three complete bidirectional journeys. Named
option-set refactoring preserves every world byte while updating the authenticated
authored-source pin. Malformed/stale/mismatched/cancelled requests and occupied
output preserve accepted source/world state. Original blueprint history replays;
semantically equivalent but altered blueprint provenance is rejected. Accepted
design-source identities are now included in the common regional audit context.

The public Core consumer passes wilderness, interior, compound and topology
generation/edit/publication/restore (198 nodes and 160 physical sources across
eight packets). Current metadata session operation checks and the three-domain,
21-node operation checks pass failure/cancellation, immutable callback inputs,
publication preservation, postcommit reporting failures and committed-state
recovery. These establish **S08.4 bounded technical acceptance for the supported
session APIs**. The subsequent seam/order/scale qualification above completes
the technical S08 gate; broader core/design acceptance remains in S10.
That increment's final CLI build has no warnings/errors; its test build retains
one pre-existing nullable warning in `WorldGroundUseCapacityTests.cs:133`.
[Commands, source hashes and acceptance evidence](../../evidence/2026-11-02-blueprint-session-verification.json).

## Pin-conditioned seeded design regeneration — locally verified (2026-11-02)

Pass active protected region coordinates into each selected program's source
before regional planning, using the existing anchor-binding owner. Preserve
the unmodified authored design separately from effective pinned requests.
Explicit conflicting authored pins are bounded candidate refusals with no
fabricated world hash; malformed source, cancellation and budgets remain errors.
Replay must apply the same preparation and qualify the resulting world normally.
Acceptance: change the world seed with terrain/partition/program variation,
retain the exact arrival coordinate, all 20 nodes, 12 physical sites and three
bidirectional connections, preserve authored sources and restore history.
The protected-alternative regression still rejects explicit conflicting
pins and permits explicit releases.

`--world-regional-design-pinned-seed` passed at
`generated/regional-design-pinned-seed-20261102`. Independent saved-data checks
confirmed zero movement of arrival while grove, outpost and pass moved about
81.39, 73.49 and 32.64 m; the actual terrain seed changed. All 20 nodes,
12 physical sites and three bidirectional connections remain, authored source
is unchanged, and history replay passes. The protected-choice regression passed,
with no fabricated world hash for the explicit conflicting-pin refusal.
JavaScript consumed the resulting exact world without mutating it. CLI seed
rollback restored the exact earlier accepted pinned-world hash
`598f996a72d7e1c2e194c64790b7bb04ed38516821d9f9da238fb8115298deba`.
CLI build passed with zero warnings/errors; the test build retained the existing
CS8602 at `WorldGroundUseCapacityTests.cs:133`.
[Evidence](../../evidence/2026-11-02-regional-design-pinned-seed-verification.json)
records source identities, commands and observed geometry. These are bounded
cases; infeasible pins can still refuse, and broad world qualification remains
open. Godot deferred.

## Protection-aware authored design search — locally verified (2026-11-02)

Use the existing regional session protection plan when qualifying authored
design alternatives. A valid generated candidate that changes a protected
input or anchor is a rejected design choice; malformed inputs, work limits and
cancellation remain errors. The same qualification must authenticate accepted
definitions during edit and checkpoint replay. Preserve attempt order,
rejection reasons and candidate hashes in `Report.DesignAttempts`, and replay
that report from source. Explicit protection releases must allow the formerly
blocked choice without weakening any other world requirement. Acceptance:
first candidate changes the protected catalog value, second moves a protected
origin, and the third preserves both;
conflicting-only source preserves accepted state; explicit release works;
20 nodes, 12 physical sites and three bidirectional connections remain;
published history restores with the same design attempts.

The expanded `--world-regional-design-protections` test passed at
`generated/regional-design-protections-anchor-20261102`, retaining the catalog
and anchor rejection reasons followed by the compatible third choice. Explicit
release, failed-state preservation and full history restore passed. The seeded
design-session regression also passed. The CLI published a two-attempt design
and exposed its rejection count; altering a saved attempt's acceptance flag
was refused with `METADATA_REGIONAL_CHECKPOINT_REPLAY_MISMATCH`, without output.
Final CLI build passed with zero warnings/errors; the test build retained the
existing CS8602 at `WorldGroundUseCapacityTests.cs:133`.
[Evidence](../../evidence/2026-11-02-regional-design-protections-verification.json)
pins source, commands and limits. This selects among declared alternatives;
automatic synthesis of new authored alternatives, full-world design and the
broader core gate remain open. Godot deferred.

## Authored regional design sessions — locally verified (2026-11-02)

Extend the existing regional generation session with design-source creation and
revision rather than introducing another session or generation engine. Preserve
the complete authored design request, including seed plans and program choices,
in initial and edited checkpoints. Re-generate that design before accepting its
definition during edits or checkpoint replay. Use existing reporting, stale-base
checks, protections, transactional publication, failure preservation and finite
history limits. Acceptance requires a real seed change, clean-generation equality,
complete physical content and bidirectional routes, reported publication/restore,
and stale/invalid/cancelled refusal. Design-source hashes also bind each accepted
report and each next edit, including source-only changes with identical world
geometry. Source-less edits cannot carry an unused design-source precondition.
Source-bearing snapshots and checkpoints remain subject to the existing byte
limits during creation, edits and restore.

`--world-regional-design-session` passed on the final source with a four-region,
20-node, 12-physical-site world and three bidirectional regional connections.
It checks a real seed revision against clean generation, stale world/source
pins, invalid and cancelled revisions, preservation of accepted state, reported
publication and history restore, and changed source-only provenance. The final
ordinary-session validation regression also passed. CLI `start-designed` and a
source-only `edit-designed` passed; the latter reused all four regional packets
and retained identical world bytes while changing the design-source hash.
Final CLI build passed with zero warnings/errors. Prior precursor runs also
checked explicit rollback and source substitution; they are not substituted for
the final-source checks above. [Source and command evidence](../../evidence/2026-11-02-regional-design-session-verification.json)
records exact scope and remaining gates. The complete cross-genre corpus was not
rerun after this session feature. Godot remains deferred.

## Active — original-lattice terrain in domain chunk packages (2026-11-02)

Attach exact original-lattice terrain windows to selected physical chunks and
retain the authored surface boundary, exclusions and patches as shared data.
Reuse source validation and the existing lattice/window owners; never invent
samples outside a captured source window. Persist, pin and selectively load
terrain records with the matching domain chunks. Preserve logical domains,
explicit uncovered areas, budgets, cancellation and no-overwrite publication.
Verify actual source samples and overlapping seams for generated and captured
fields, corruption isolation, source replay and existing package regressions.
Godot and native runtime streaming remain deferred.

The domain index now retains each physical surface's boundary, exclusions,
patches, original field geometry and actual available-sample extent. Selected
chunks include bounded original-lattice windows, clipped to a captured source's
available samples; absent windows are explicit and do not erase analytic
patches. `MetadataDomainTerrainQuery` samples a detached window with the same
triangular interpolation owner and refuses outside-window access. Root package
schema 2 pins a separate terrain record per physical chunk; schema 1 is refused.
Terrain extraction is bounded to 20 million work/sample units per selection or
publication, with existing packet and aggregate byte limits.

On Windows/.NET 10, `--world-domain-chunk-index
generated/domain-terrain-chunks-final3-20261102` passed with complete authored
height fields on two independently named floors, analytic-only surface patches,
logical nodes and source-qualified selection. `--world-regional-domain-chunk-index
generated/regional-domain-terrain-chunks-final3-20261102` passed with generated
captured terrain: every selected sample matched both the original full generated
field and saved capture, adjacent windows overlapped, and window-query
interpolation matched the full-field owner. Detached query inputs, unavailable
samples, selective terrain corruption and a structurally invalid rehashed window
were rejected as appropriate. The existing 52-packet single-world regression
passed at `generated/domain-terrain-single-regression-final2-20261102`. Four
independent JavaScript checks compared saved source samples, geometry and pinned
terrain packets using `DOMAIN_CHUNK_WORLD`, `REGIONAL_DOMAIN_CHUNK_WORLD`,
`DOMAIN_CHUNK_PACKAGE` and `REGIONAL_DOMAIN_CHUNK_PACKAGE` pointed at the two
outputs, with `node --test tests/javascript/domain-chunk-index.test.cjs
tests/javascript/domain-chunk-package.test.cjs`. CLI build passed with no
warnings; the test build retained the existing CS8602 warning at
`WorldGroundUseCapacityTests.cs:133`. Index SHA-256
`C0DDE10B9CC3BB7545BE27560D37621B04B4A65C166E917DBF19CE48A52C5F80`;
package SHA-256 `939450D84A4E9AB411210060ECA8EE4ED1992301EE1EDB1C9A302CF0E6CC2CA3`;
terrain-query SHA-256 `FA7849CE1E8DD6EDF691FD5AF4559C25D3D70F415FA63AAB137EBD621F61A4D8`.
These are selected saved surface samples and shared masks, not independently
generated chunks, paged environmental layers or native runtime streaming.

## Active — persisted multi-domain chunk packages (2026-11-02)

Publish the validated domain chunk index as one atomic pinned package. Reuse
the current local packet format and selective reader; retain domain-qualified
shared transitions, journeys, progression and regional routes. Refuse missing
chunk plans, corruption, invalid pins and existing output; preserve cancellation
and reporting. Verify floors, regional composition, selective corruption and
source parity. Selective terrain was added above; runtime streaming remains
separate and Godot is deferred.

`MetadataDomainChunkPackage` atomically publishes one pinned root manifest,
shared domain graph data and the existing per-domain geometry packets. The
reader verifies the root pin and shared-data hash at open, opens only requested
domain manifests and reads only requested chunk files. Physical domains without
chunk plans refuse publication. Shared data is limited to 8 MiB, total package
and aggregate selected record bytes to 256 MiB, and existing local packet
limits remain in force. The publisher uses the shared operation reporter; a
post-commit reporting failure exposes the committed path and root pin.

On Windows/.NET 10, `--world-domain-chunk-index
generated/domain-chunk-package-final3-20261102` passed with independently
addressed overlapping floors and a logical domain. It compared disk reads
with source selections, rejected an incorrect pin, modified shared data,
inconsistent rehashed shared data and requested corrupt chunks, while a corrupt
unrequested chunk did not affect another selection. Cancellation, existing
output, missing physical chunk plans, no partial output and post-commit
reporting recovery passed. `--world-regional-domain-chunk-index
generated/regional-domain-chunk-package-final2-20261102` passed with full shared
regional route and portal evidence. The 52-packet single-world regression
`--world-chunk-package generated/domain-package-single-regression-final2-20261102`
passed after extracting the common staged writer. Four independent JavaScript
checks passed using `DOMAIN_CHUNK_WORLD`, `REGIONAL_DOMAIN_CHUNK_WORLD`,
`DOMAIN_CHUNK_PACKAGE` and `REGIONAL_DOMAIN_CHUNK_PACKAGE` pointed to those
outputs and `node --test tests/javascript/domain-chunk-index.test.cjs
tests/javascript/domain-chunk-package.test.cjs`. CLI build passed without
warnings; the test build retained the existing CS8602 warning in
`WorldGroundUseCapacityTests.cs:133`. Domain package source SHA-256
`6A491A7ABADBB17634E41868971C27EC61CE4A541176C41531CB2037E94145A0`;
local package source SHA-256
`02BAAD08003FB39392045B3C04E1A89E5B8B96F62AE3038AB13B5B0F96527D1A`.
At this earlier checkpoint the reader materialized selected geometry and shared
data without terrain; selective terrain was added above. Independent chunk
generation, a hardened filesystem sandbox and native runtime streaming remain open.

## Active — selective geometry across independent domains (2026-11-02)

Provide a source-validated in-memory index for generated domain collections and
regional/domain compositions. Reuse each domain's existing chunk geometry
owner, qualify every selected member by domain, preserve unpositioned semantic
nodes, explicit transitions and the complete regional route network as shared
data, and keep local coordinate systems separate. Refuse selection of positioned
domains without chunk plans. Bound extraction, detach returned data and verify
same-ID floors, logical domains, partial selection, tampering and cancellation.
This is not a disk streaming or native runtime implementation; Godot is deferred.

`MetadataDomainChunkIndex` now validates and snapshots either an independent
domain collection or a regional/domain composition. It reuses the existing
per-domain geometry owner, returns domain-qualified selected members, selected
domains' unpositioned nodes and incident links, and retains all explicit
transitions, journeys, progression and regional route geometry as shared
dependencies. Chunk IDs are deduplicated only within their own domain. A
physical domain without a chunk plan refuses selection; a geometry-free domain
can be selected with no chunk IDs. Shared result data is detached. Extraction
uses an aggregate budget; replay and source serialization are separate costs.

On Windows/.NET 10, `--world-domain-chunk-index
generated/domain-chunk-index-final2-20261102` passed with two physical floors
sharing local IDs, an outdoor domain and a logical domain: selected geometry
matched the existing owner, progression and explicit transitions survived,
duplicate requests were deduplicated, mutations did not alter the index, and
tampered, unchunked, invalid, cancelled and exhausted-budget cases refused.
`--world-regional-domain-chunk-index generated/regional-domain-chunk-index-final-20261102`
passed with a selected regional area, full regional route geometry and an
explicit independent-domain portal. `DOMAIN_CHUNK_WORLD` and
`REGIONAL_DOMAIN_CHUNK_WORLD` pointing to those outputs passed both tests in
`node --test tests/javascript/domain-chunk-index.test.cjs`, independently
checking source hashes, source nodes/connections and shared routes. The
existing `--world-chunk-package generated/domain-chunk-index-package-regression-20261102`
passed its 52-file selective-load regression. CLI build passed without warnings;
the test build retained the existing CS8602 warning at
`WorldGroundUseCapacityTests.cs:133`. Index source SHA-256
`DCFD59E55C4A52FD387B70B09B97248021E54D780E2786FEAA5CB30B3E9E1026`.
This API materializes a full source snapshot and shared network in memory. The
multi-domain disk package and selective terrain are now available above; runtime
streaming remains open.

## Active — incident connections for loaded nodes (2026-11-02)

Attach every source connection incident to a positioned node to that node's
chunk record, including direction, properties and endpoint identities. A
connection may reference an endpoint outside the selected chunks; consumers
deduplicate connection IDs when both endpoints are loaded. Preserve immutable
source copies, declared work limits, seam selection, integrity and replay.
Reject inconsistent node/connection records and increment the package schema,
with no legacy reader. Test exact source equality, cross-chunk endpoints,
authored properties, direction, tampering and cancellation. Shared terrain and
unpositioned nodes remain separate; this does not generate a game runtime.

Each selected positioned node now carries its incident source connections,
including IDs, direction and detached authored JSON. An endpoint can remain
unloaded; when both are loaded, the reader verifies their copies agree and each
endpoint includes the connection. Consumers can deduplicate by connection ID.
Connections between only unpositioned nodes remain in the shared world. Schema
3 replaces schema 2 without a compatibility reader; earlier packages require
regeneration. Connection extraction contributes to the work budget.

`--world-chunk-package generated/chunk-connections-final-check-20261102` passed
with 169 nodes and 52 files, including a one-way authored link to an unloaded
area, exact source incident lists, detached properties, a deliberately
inconsistent but rehashed pair of endpoint records, old-schema refusal,
budgets, cancellation and publication failures. `--world-chunks` also passed.
An initial fixture used the unqualified author ID and failed; the corrected
test uses the actual generated ID. Test builds report the existing CS8602 warning
at `WorldGroundUseCapacityTests.cs:133`. This does not include shared terrain,
independent chunk generation or a playable engine runtime.

## Active — semantic nodes in selective chunk records (2026-11-02)

Include each positioned node's complete authored metadata with its typed
geometry in the detached index and selective package, retaining identity,
parent, archetype, properties and position without shared mutable collections.
Keep paths distinct, deduplicate seam members, charge extraction work for
metadata, and reject mismatched node identities. Increment the package schema;
older packets require regeneration. Verify source equality, nested hierarchy,
properties, immutability, tampering, budget and source replay. Shared terrain,
cross-node relationships and full world design remain separate. Godot deferred.

Each positioned chunk node now carries a detached copy of its original node,
including parent, archetype, position and game-authored JSON properties; paths
carry no fabricated node. Metadata extraction is charged to the existing work
budget. Selective package schema 2 requires matching node ID and anchor and
rejects earlier packet schema rather than providing a legacy reader. Parent IDs
do not imply that the parent is in the selected chunks. Shared terrain,
cross-node connections and other source dependencies are not included.

`--world-chunk-package generated/chunk-semantic-package-final-20261102` passed
on the 169-node/52-file world, including nested authored JSON, parent identity,
source and returned-dictionary immutability, seam deduplication, old-schema and
tamper refusal, limits and reporting failures. `--world-chunks
generated/chunk-semantic-replay-20261102` passed index and published-world
replay checks. Test builds reported the existing CS8602 warning at
`WorldGroundUseCapacityTests.cs:133`. This is selective loading of already
generated content, not independent generation or native runtime integration.

## Active — selective published chunk geometry (2026-11-02)

Publish the already generated, source-validated chunk geometry as bounded,
individually readable chunk records with a pinned manifest and hashes. Use the
existing atomic publication, bounded serialization and operation-reporting
owners; never overwrite a target. Readers select only requested chunk files,
verify their identity and digest, deduplicate seam members in canonical order,
and reject missing or changed data and links. The caller retains the manifest
digest and the full source package for shared terrain and semantic dependencies.
Test replay, duplicate and seam selections, tampering, cancellation, limits,
failure preservation and exact comparison with the in-memory owner. This adds
selective loading of generated geometry, not independent procedural chunk
generation, native runtime integration or a complete game world. Godot deferred.

The C# package owner now snapshots and source-validates an already generated
world, atomically publishes one bounded typed-geometry file per occupied chunk,
and returns a manifest digest for the caller to pin. Its reader checks the pin,
loads only requested files, verifies each file's digest and deduplicates whole
members across seams. Shared terrain and other world data remain separate. The
package limits each file to 8 MiB and total geometry to 256 MiB; it refuses
overwrites. Publication uses the existing log/audit/notification reporter and
reports both the committed directory and digest when reporting fails afterward.

`--world-chunk-package generated/chunk-package-verified-20261102` passed on a
169-node world with 52 chunk files. It compared all loaded geometry with the
in-memory source, checked seam deduplication and immutable loaded vertices,
detected a changed requested file and manifest, loaded valid chunks while a
different file was damaged, and checked budget/cancellation, non-overwrite and
post-commit reporting failure. The test build reported the existing CS8602 in
`WorldGroundUseCapacityTests.cs:133`. Link refusal was implemented but not
exercised here; hashes prove consistency relative to the trusted manifest pin,
not source authorship. No native streaming/runtime or independently generated
chunks are claimed. The source document and selected records are materialized
in memory during publication; selected records are materialized during reads.

## Active — typed chunk geometry (2026-11-02)

Expose the source role of each chunk coverage polygon: physical footprint,
reservation, site feature, access approach or route segment. Preserve each
relevant source ID and segment index, and report declared node occupancy when
available. Capture these roles in the existing spatial extraction owner so
chunk references, terrain envelopes and regional assembly keep one geometry
source. Verify exact matching to source footprints/ports/features/paths,
seams, source replay, immutability, bounds and existing chunk behavior. These
roles do not turn reservations or approach envelopes into solid collision;
terrain and shared metadata remain external. Godot remains deferred.

The shared spatial extraction owner now emits coverage roles for the detached
geometry index: footprint and reservation carry their scoped demand ID, site
features carry their feature ID and kind, access approaches carry the port ID,
and route envelopes carry route ID and segment index. A node's declared access
occupancy is retained independently of its coverage roles. The 169-node chunk
fixture checked exact footprints, reservation presence, every declared port,
site-feature IDs/kinds/boundaries, all 14 directed network paths, seam
deduplication, detachment, work limits, cancellation and saved replay. A
two-region shared-extraction regression retained all ten nodes and two-way
paths. The earlier untyped `CoveragePolygons` field was replaced by `Coverage`;
there is no compatibility alias. Terrain and other shared metadata are still
source dependencies, and no native streaming or game runtime is claimed.

Evidence: `--world-chunks generated/chunk-typed-geometry-final-20261102` and
`--world-regional-rounding generated/chunk-typed-regional-regression-20261102`
passed on the current Core. A concurrent earlier build observed an in-progress
test edit and failed to compile; the clean subsequent build passed with only
the existing CS8602 warning in `WorldGroundUseCapacityTests.cs:133`.

## Active — source-bound chunk geometry selection (2026-11-02)

Provide a detached, validated in-memory geometry index for an already generated
world. A consumer selects occupied chunks and receives each referenced member
once, with its full original anchor, coverage polygons and, where applicable,
route centerline and width. Preserve canonical owner/references, source digest,
seams and cancellation; enforce a bounded extraction budget and reject stale or
tampered chunk plans. Verify source sizes, full-width routes, duplicate selection,
detachment and regeneration. The polygons are source coverage geometry, not a
substitute for typed occupancy or shared terrain. This is not independent chunk
generation, disk streaming, runtime integration or Godot.

`MetadataChunkPlanner.CreateGeometryIndex` now creates a detached geometry
snapshot bound to source and chunk-plan digests, with an explicit extraction
work limit. The 169-node fixture retained 183 unique members across 52 occupied
cells and 307 references. Checks verified whole source footprints across seams,
all directed network centerlines, radii and segment envelopes, duplicate-load
deduplication, immutable returned vertices, detached source membership,
invalid IDs, stale plans, bounds and cancellation. A published world rebuilt
the same selection; graph-only nodes remained shared without invented geometry.

Evidence: `--world-chunks generated/chunk-geometry-replay-20261102` passed the
publication/replay checks, and `--world-chunks` passed again after the final
cancellation check. The test build reported the existing CS8602 warning in
`WorldGroundUseCapacityTests.cs:133`. At that earlier check the selected coverage
polygons did not have source roles; the later typed extension is recorded above.
They do not include shared terrain. The snapshot requires
the already generated world in memory and is not independent streaming.

## Verified — remove duplicate regional-design replays (2026-11-01)

Publish and read a regional design by replaying its complete design source once,
comparing the entire saved receipt and world, then passing the verified generated
world directly to the existing atomic publication owner. Preserve bounded JSON
storage, dependency rechecks, all local/region/route generation, cancellation,
non-overwrite and tamper refusal. Public standalone regional reads and publishes
retain their own source replay. Verify actual seeded physical worlds and changed
world/receipt inputs; measure comparable end-to-end timings without making a
general speed guarantee. Godot remains deferred.

Design publication and read each execute one complete design-source replay and
compare the complete receipt/world; standalone regional reads/publishes retain
their own replay. The focused test counted 14 calls to a versioned layout in a
fresh design generation, and exactly 14 during each publish and read. Changed
world, receipt and dependency identity were refused, failed output was absent,
standalone regional read and cancellation passed. The full seeded physical-world
regression, CLI generation and CLI verification passed.

One sequential Windows/.NET 10.0.403 comparison using the identical seeded
request measured CLI verification at 55.56 seconds with the previous build and
28.93 seconds after this change. This is one local observation, not a benchmark
or general speed claim. Terrain seed, region ownership, node positions, physical
footprints and both-direction route paths matched across those builds. The prior
lowest-cost route-search work-budget failure remains unresolved.

Evidence: `--world-regional-design-replay
generated/design-replay-check-20261101`, `--world-regional-design-world-seed
generated/design-replay-world-seed-regression-20261101`, and CLI output
`generated/design-replay-cli-world-20261101`. Test build has the existing CS8602
in `WorldGroundUseCapacityTests.cs:133`; CLI build has zero warnings. Design source
SHA-256: `66F62EF44FC23206BB475D791DA2EB87442AB43DF6AE37C2EAD5C1CB32E7BEB0`.
World-design/scale acceptance and the full S08 gate remain open; Godot deferred.

## Verified — protect generated positions during source edits (2026-11-01)

Allow a developer to select generated nodes at session creation and preserve
their exact generated positions and identities through edits and checkpoint
restore. Generate and validate the entire candidate normally; reject a changed
or missing protected node before committing or publishing. Unrelated edits must
still work. Protecting a position does not pin the placement algorithm, protect
shape or prove that changed surroundings remain playable. Exercise physical and
logical examples, refusal/rollback, cancellation, source replay and tampering.

The focused SDK test passed 3D position/identity preservation, moved or missing
node refusal, unrelated edits, rollback, cancellation, checkpoint replay and
tamper refusal. A terrain edit rerouted three journeys while preserving eight
physical sites; an outpost move was refused. The CLI started a protected session,
replayed its checkpoint and refused a conflicting edit with the protection ID;
no target directory was published. The existing quick regeneration regression
passed. Evidence: `--world-position-protection generated/position-lock-final-20261101`,
`--world-regeneration-quick`, and the CLI start/edit outputs under
`generated/position-lock-final-cli-20261101`. CLI build has zero warnings; the
test build reports the existing CS8602 in `WorldGroundUseCapacityTests.cs:133`.
Position-protection source SHA-256:
`E7B22A8EF804E674EC0314A1081393BDED89C32519AEBFAC8214381F3D8AE712`.
Locks are declared at session creation and do not guide placement search. Dynamic
lock changes, shape locks, broader world design and streaming remain open.

## Current — reusable validated spatial selection (2026-11-01)

`MetadataChunkPlanner.CreateSelectionIndex` validates an existing world index
once and returns a detached lookup keyed by occupied chunks. It records both the
source-world and chunk-plan digests, deduplicates requests/members, preserves
canonical order and honors cancellation. A new index is required for a changed
world or plan; source geometry and other shared data remain with the world.

The affected .NET build passed with the existing CS8602 warning at
`WorldGroundUseCapacityTests.cs:133`. `--world-chunks
 generated/chunk-selection-index-20261101-a` passed with 169 nodes, 183 members,
52 occupied cells and 307 references. Tests compare selection against the
existing validated selector for multiple orders and overlap, exercise detached
source mutation, tamper/cancellation, and recreate an identical lookup after
publishing/reloading. This does not qualify independent generation, streaming,
world-scale performance or the full S08 gate. Godot/artwork remain deferred.
[Source hashes and checks](../../evidence/2026-11-01-chunk-selection-index-verification.json).

## Acceptance — reusable validated spatial selection (2026-11-01)

A consumer must be able to validate a saved chunk plan once, obtain a detached
read-only selection snapshot, and request multiple sets of occupied chunk IDs
without rerunning world geometry or retaining mutable source lists. Repeated and
reordered IDs return each original member once in canonical source order; unknown
IDs and stale/tampered source plans refuse, cancellation is honored, and the
snapshot identifies the exact source-world digest. Prove equivalence to the
existing full-validation selector across seam-spanning nodes and routes, empty
and overlapping requests, and preservation after the caller mutates its source.
This is indexed selection from an already generated world, not independent chunk
generation, runtime streaming, physical geometry extraction or Godot support.


## Bounded qualification — complete regional physical content (2026-11-01)

Measure regional assembly with eight independently generated regions containing
128 declared solid fixtures each, their full catalog reservations, and a required
outward/return journey. Preserve all 1,032 nodes and 1,024 physical sources under
the existing 20-million regional work limit. Reuse the current immutable bounds
index to omit only strictly separated source pairs; retain exact polygon checks,
closed-boundary contact, original first-conflict order, source dimensions and
cancellation/work refusals. Compare the same inputs/harness with the previous
Core, qualify smaller collision/seam controls and independently inspect all
exported bodies/reservations and journey clearance. No limits, required content,
occupancy or gameplay requirements may be weakened to obtain a pass. This is a
bounded scale increment; broader world design/variety and native streaming remain
open, and artwork/Godot remain deferred.

Status: VERIFIED for this bounded fixture. The same source content takes 687,476
regional work versus 59,756,516 on prior Core with a comparison-only higher
budget. Default remains 20 million. Five native scopes/five saved-JS checks pass;
two full/contact SDK and CLI packets match exactly. Independent equations check
4,096 body vertices, 1,024 reservations, 458,752 cross-region reservation pairs
and both required full-width travel directions. Existing complete 4/8/16-region
and transactional edit/history/replay/publication controls also pass. Closed
reservation contact succeeds; reservation overlap refuses even when bodies merely
touch. No public node/site/surface/port limits changed. Dense envelopes retain
bounded pair costs; authored source-scale qualification does not close broader
design or streaming acceptance.
[Reusable example](../../../examples/metadata/regional-physical-scale/README.md),
[exact evidence and limits](../../evidence/2026-11-01-regional-physical-scale-verification.json).

**Current — localized overlay dependencies (2026-11-01):** The existing regional
session retains complete packets outside a validated patch/exclusion dependency
domain. Old/new overlay locations invalidate together; global height recipes and
ownership/domain dependencies remain conservative. Untrusted saved packets replay
normally and complete-world constraints always requalify. The same-harness16-region
control preserves all80 nodes/48 sources while retaining15 packets. Eleven native
scopes,11 JavaScript checks, four exact public SDK/CLI world/checkpoint pairs and
independent complete geometry pass. [S04 owner](s04-terrain-aware-connections.md),
[commands and limits](../../evidence/2026-11-01-regional-overlay-invalidation-verification.json).
Browser review is NOT RUN (local browser disabled). Original63 task/13 requirement
statuses remain; broader design/variety/scale gates and Godot/artwork deferral remain.

## Bounded qualification — transactional regional protection changes (2026-11-01)

Developers can now add/release regional input and generated-anchor protections
through ordinary definition/source edits. New locks capture current accepted
values/positions; surviving locks retain original receipts. Explicit all-release
works without discarding world history. New anchors constrain the normal planner;
direct activation first authenticates the supplied derived definition. Release
removes policy without silently erasing explicit source pins.

The effective policy is part of the single accepted report/state. The checkpoint
retains initial authority and replays every transition. `ExpectedEditId` guards
policy-only changes even when geometry hashes are unchanged. Failed generation,
malformed/stale changes, cancellation and publication failure retain world,
protections and history. Post-commit reporting failure exposes committed authority.

Nine named native scopes and21 JavaScript checks pass. Five public world packets
and checkpoints exactly match the SDK; two public refusals publish nothing. All14
session correlations reach existing Log/Audit/Notification channels, with effective
lock counts after each edit. Independent geometry verifies98 nodes,66 whole bodies/
264 source vertices,70 directed journeys,1776 segment/obstacle-edge checks,8640
terrain samples and all four policy transitions. Five saved world views retain
every node/body and exact embedded data/resources; no browser/editor acceptance
is claimed. [Reusable workflow](../../../examples/metadata/regional-protection-changes/README.md),
[commands, exact identities, retained failures and limits](../../evidence/2026-11-01-regional-protection-changes-verification.json).

Two pre-existing history checks used exact floating-point area equality. The
previous Core reproduces the same287.9999999999998-versus288 failure. They now use
the declared geometry tolerance and check every rigidly transformed host vertex;
complete original/preferred/rollback sources remain unchanged. Wider world-design,
variety/scale, editor and native streaming gates remain open. All63 task/13
requirement scopes are retained; Godot/artwork deferred.

Acceptance before implementation: extend the existing regional edit/source-edit
owners with explicit protection additions and releases. Preserve the initial
checkpoint policy and replay each accepted transition in the normal edit history.
New protections capture the current accepted source values/generated anchor poses;
surviving protections retain their original receipts. Never silently recapture a
changed value, remove a protection because its target vanished, or discard history.
New anchor locks must constrain the ordinary ownership planner. Explicit release
removes policy; authored source pins still require an explicit source edit.

Unknown/duplicate/conflicting IDs, missing targets, stale edits, incompatible
terrain, cancellation, dependency failures and publication failures must preserve
both world and policy. Returned snapshots cannot mutate authority. Empty transition
requests refuse; releasing every current protection is valid. Exercise inputs,
anchors, actual terrain/source edits, region reuse, clean-generation equality,
save/restore/tampered history, ordinary public commands and shared reporting. Keep
all original tasks/requirements and Godot/artwork deferral intact. This is developer
editing within the existing world engine, not a separate editor or generator.

## Bounded qualification — reusable regional demand generation (2026-10-31)

`MetadataRegionalWorldGenerator.CreateGenerationPlan` now creates a private live
plan with frozen source/dependency identities. Developers can request complete
regions, reuse resident packets, explicitly release them and assemble a complete
resident set through the existing seam/source/route owner. A finite serialized
packet cap refuses new retention without eviction or truncation. Snapshots expose
accepted generation/reuse/release/assembly counts and current resident bytes.
Caller mutation, cancellation, dependency drift, foreign/busy plans, failed local
generation and failed complete assembly preserve accepted resident authority.

Actual positive layout observations prove authored preparation expands no content,
one-region requests expand only that region, resident reuse/complete assembly do
not repeat local generation, and an unrequested impossible region is not expanded.
Derived design preparation is explicitly reported and can still require global
semantic/physical analysis. Saved reads/publication retain authoritative replay;
this SDK handle is ephemeral and supplies no alternate persistence protocol.

Six named native scopes and16 JavaScript checks pass. Two public assembled/whole
packets exactly match the SDK demand plan; two public refusals publish nothing.
All six public operation correlations reach the existing Log/Audit/Notification
channels. Independent geometry retains10 nodes,6 whole bodies/24 vertices,
6 directed journeys,96 segment/solid-edge checks and2088 original-lattice terrain
samples. Related same-Core4/8/16-region regression retains every source and return
journey; it does not establish scale/heap guarantees for this live cache API.
[Reusable input and SDK workflow](../../../examples/metadata/regional-demand-generation/README.md),
[commands, identities, retained harness failure and limits](../../evidence/2026-10-31-regional-demand-generation-verification.json).

This advances bounded demand generation, not infinite/native terrain streaming,
independent initial world design or the full professional-generator gate. The cap
measures serialized packets rather than managed heap or temporary allocations.
All63 task/13 requirement scopes remain; Godot/artwork deferred.

Acceptance before implementation: reuse `MetadataRegionalWorldGenerator`'s real
single-region generation and complete seam/route assembly. Add an opaque in-memory
generation plan that validates/snapshots the design once, binds installed dependency
identities, and retains only explicitly requested complete region packets. Expose
actual generation/reuse/release counts and retained serialized packet bytes under
an explicit finite cap. This is not a strict managed-heap bound or persisted cache.

Requests in different orders must produce the same complete sources, terrain and
seam identities. Returning mutable snapshots must not alter private packets.
Dependency drift, cache limits, unknown requests, cancellation and assembly failures
must preserve accepted resident state. Release must be explicit, never automatic
eviction or content truncation. Assembly requires every region already resident
and reuses the ordinary complete-world constraints, without regenerating local
programs. Untrusted file reads/publication keep their normal authoritative replay.

Prove with actual layout-call observations that an authored design's preparation
does not generate content and that requesting one region does not expand another.
Derived design preparation may require metadata analysis and whole-world physical
search; record that scope rather than claiming lazy initial design. Exercise a
complete physical seam-spanning program, blocked/missing regions, original terrain
windows, both route directions, request/release/reload order, serialized limits,
stale/foreign plans, source/output mutation, publication/readback and shared
Log/Audit/Notification boundaries. Retain all63 task/13 requirement scopes and
Godot/artwork deferral; this does not alone close terrain/native streaming or S08.

## Bounded qualification — authored per-object placement controls (2026-10-31)

The shared engine now applies explicit per-member positions and optional rigid
orientations after semantic expansion. Domain/stage coordinates, effective source
bindings and resolved receipts replay through ordinary generation, edits, reads,
checkpoint history and publication. An absolute child can cause actual parent
reconsideration; stage coordinates follow parent translation. Existing root/anchor,
terrain, source/facing, containment, spacing, port and journey rules remain hard.
Explicit removal releases a control; failures and cancellation retain accepted state.

The complete counterfactual keeps all five compounds and their constituents while
fixing one position, turning its source20°, turning one shelter90°, and growing
every shelter from4 to5 meters. A boundary-derived contact preserves the exact
normal gap and positive face span inside the actually rotated parent, using the
same growth solver and original budgets. Regional source measurement resolves
the same controlled orientation as placement.

Four SDK/CLI worlds are byte-identical. Four typed public refusals publish nothing.
Twelve named native scopes and70 JavaScript checks pass. Independent geometry
covers48 nodes,44 bodies/186 source vertices,9 controls,30 sibling comparisons and
60 directed paths with2640 segment/solid-edge checks. The original40-case matrix
still yields39 worlds and the deliberate terrain refusal. Actual browser selection
shows the authored stage/domain pose and orientation. Its SVG retains all20
bodies/85 vertices,10 reservations/40 vertices and30 directed paths exactly.
[Inputs](../../../examples/metadata/pose-controls/README.md),
[commands, exact source/binary identities and retained failures](../../evidence/2026-10-31-pose-controls-verification.json).

This is finite2D engine placement control, not a complete interactive editor,
arbitrary-domain manipulation or independent streaming. Broader world-design,
variety/scale, in-session protection changes and live-agent qualification remain
open. All63 task/13 requirement statuses are retained; Godot/artwork deferred.

Acceptance before implementation: let a developer target an expanded semantic
node with an attributed position and optional rigid source rotation. Use the
existing metadata placement/generation/session owners, with explicit world-domain
coordinates or the existing stage translation frame. A stage frame follows its
placed parent's translation; it does not invent a rotating child coordinate system.
Controls keep their exact positions until explicitly edited or removed. Removing
the selected node must refuse rather than silently discard its control.

Apply controls after catalog/program expansion and before source resolution/search.
An orientation replaces that member's binding default, retaining the complete
source polygon and reservation. Existing authored anchor relations, terrain,
parent containment, spacing, ports, required paths and generated-population facing
remain hard. Imported source-world poses/orientations remain preserved. Conflicts
must return an attributed refusal and leave accepted session/history/output intact.
The declared growth root anchor remains a constraint; controls cannot silently
move it. Other exact graph-member anchors must actually guide candidate generation
through the same bounded contact owner, with no arbitrary scatter or separate solver.

Retain requested and resolved stage/world poses plus effective orientation in
replayable receipts. Qualify complete nested non-city programs, parent movement,
individual source rotation, terrain/size/path counterfactuals, control removal,
reordered declarations, malformed/duplicate/missing targets, hard-anchor conflicts,
cancellation, budgets, source mutation isolation, saved replay/checkpoint, actual
public CLI publication/refusal and exact JavaScript/browser inspection. Use the
existing Log/Audit/Notification boundaries. Do not relabel this as independent
streaming or a full interactive editor. Preserve all63 task/13 requirement statuses;
Godot/artwork stay deferred.

**Current — physical ownership redesign (2026-10-31):** Ownership context/attempt traces now replay through complete reads, source edits, saved history, failure preservation and exact rollback.
[S05 owns the bounded contract and evidence](s05-adaptive-regions-and-partitions.md#bounded-qualification--physical-feedback-into-ownership-partitions-2026-10-31).
Full world-design, variety/scale, finer editing and streaming gates remain open;
all task/requirement statuses and Godot/artwork deferral are retained.

**Current — terrain-qualified partition replay (2026-10-31):** Source edits now
recompute actual candidate ownership/terrain qualification through the shared
owner. Whole-document reads, saved history, failed-edit preservation and exact
rollback pass with retained rejected/selected query witnesses. Two public CLI
worlds match SDK bytes. [S05 contract and evidence](s05-adaptive-regions-and-partitions.md#bounded-qualification--terrain-qualified-ownership-partition-search-2026-10-31).
Independent streaming and finer editing remain open; existing task/requirement
statuses and Godot/artwork deferral are preserved.

## Bounded qualification — regional input protections and origin pins (2026-10-31)

Selected world-source/recipe/catalog inputs resolve through semantic region IDs
and the shared JSON-pointer owner. Generated origins become actual terrain-intent
pins, retaining complete nested programs and all journeys under feasible edits.
Conflicting terrain/inputs, missing regions, attempted pin movement, cancellation,
checkpoint tampering and reporting outages retain authoritative state. Source
rollback, read/history replay, unrelated packet reuse, actual CLI parity and
neutral browser/SVG inspection pass. Six native scopes and12JSchecks qualify this
bounded API. Independent checks cover40nodes/24bodies/24paths and186segment/solid
comparisons; SVG retains six whole bodies and both regional directions.
See the [input](../../../examples/metadata/regional-developer-controls/README.md)
and [evidence](../../evidence/2026-10-31-regional-developer-controls-verification.json).

These controls are fixed for the session. Finer object-pose editing, in-session
control changes, partition/source/layout redesign, broad world variety/scale and
independent streaming remain open. All63task/13requirement statuses are retained;
Godot/artwork deferred. A pinned constrained-point upper bound does not assert
the component's maximum clearance or global infeasibility of other world designs.

## Acceptance recorded before implementation — developer regional protections and location pins (2026-10-31)

Acceptance before implementation: developers can protect selected world-source,
regional-recipe and regional-catalog values using semantic region IDs and the
existing JSON-pointer owner, and retain chosen regional origins during source
regeneration. Pinned positions must constrain actual terrain intent selection,
clearance and complete physical generation; post-generation comparison alone is
insufficient. Do not move a pin, resize/drop content or bypass terrain, access,
route, source, work or cancellation constraints to accept an edit.

Capture controls from detached accepted inputs/positions. Preserve them through
normal edits, atomic publication, checkpoints and full source/history replay.
Attribute input/pin conflicts, reject stale bases, missing regions, duplicate or
malformed controls and forged checkpoint protections. Failed/cancelled/conflicting
edits preserve accepted world/cache/history and publish nothing. Support unrelated
edits/reuse and explicit authored pins without city/game switches or Godot.

Use current session, intent, publication and operation-reporting owners. Verify
actual terrain edits that move an unlocked origin but preserve a locked one,
impossible terrain at a lock, complete nested sources/journeys, SDK/CLI parity,
neutral inspection, checkpoint tampering and source rollback. Generic per-object
pose editing, partition/source redesign and full professional-world design remain
separate gates; preserve all63task/13requirement statuses and Godot deferral.

## Bounded qualification — complete component assignments (2026-10-31)

The standalone physical-feedback scope now qualifies second-endpoint and
two-location redesign, fixed program preservation, complete source geometry,
source edits/restore/rollback and distinct exhaustion/limits. Three controls
retain35nodes/21bodies and22directed paths. Nineteen JS checks, two byte-identical
SDK/CLI worlds, four typed no-output refusals and214independent path/solid checks
pass. Shared cut-plane refinement preserves representable intersections without
dropping tiny cells; genuinely tiny requested geometry still refuses.
Existing physical/count/source-composition regressions also pass; see the
[evidence](../../evidence/2026-10-31-complete-component-assignments-verification.json).
Full world design, source/layout/partition redesign, scale, selective locks and
streaming remain open. All63tasks/13requirements keep statuses; Godot deferred.

## Bounded qualification — network-source redesign (2026-10-31)

An actual terrain-kind travel-cost source edit reselects regional links while
reusing all four complete local packets. Save/restore, a hard required-link
failure that preserves the accepted world/output, and exact rollback pass.
This extends source workflow evidence; selective locks and independent streaming
remain open. See [input](../../../examples/metadata/regional-networks/README.md)
and [evidence](../../evidence/2026-10-31-regional-networks-verification.json).
All63task/13requirement statuses are preserved.

## Bounded qualification — regional grammar source edits (2026-10-31)

A topology seed edit changes generated macro branches and physical journeys
while reusing all six exact local program packets. Actual source sessions,
complete saved restore, failure preservation and exact rollback pass. This does
not add selective locks or independent streaming. See the
[source example](../../../examples/metadata/regional-topology/README.md) and
[evidence](../../evidence/2026-10-31-regional-topology-verification.json).
All existing task/requirement statuses remain unchanged.

## Bounded qualification — nested upstream habitats in ownership (2026-10-31)

Explicit spatial context now delegates nested regions, source-demand minima and
local layouts to its one boundary-inheriting population source. Initial catalog
measurement marks source spatial qualification deferred; actual ownership analysis
qualifies complete upstream choices/layouts before exact counts and independent
consumer content. Source-only receipts preserve honest zero independent ranks/work.

Five controls retain184nodes/164bodies,30nested regions/30allocations,74discoveries
and110directed journeys. Source space-demand edit grows required discoveries21to32.
Native source/physical/history replay, exact rollback,33JSchecks, two byte-equal
SDK/CLI worlds, four typed no-output refusals and3278independent segment/solid
checks pass. Actual SVG preserves50bodies/400coordinates and22directed paths.
[Evidence and retained attempts](../../evidence/2026-10-31-nested-source-habitats-verification.json).

Shared-face outline identity now preserves source cells/area and exact loop
closure under the existing geometry predicate. A declared longest initial edge
policy follows accepted boundaries while all source/count/spacing/inset and
physical constraints remain hard. This is a technical composition qualification;
broader joint redesign, world design/variety/scale, developer locks and streaming
remain open. All63tasks/13requirements retain statuses; Godot/artwork deferred.

## Bounded qualification — qualified upstream program ownership (2026-10-31)

Actual upstream composition now qualifies its full source before downstream
expansion. Selected mixes/regions/layouts remain source-owned, preserving every
member, entrance, source dimension and journey. Consumers independently choose
only added content; analysis retains original source identities, failed/accepted
choices and exact habitat-driven quantities under captured cache/dependency limits.

Four controls retain48nodes/32bodies/28directed journeys and all10discoveries.
Native replay/edit/history rollback,37JSchecks, two byte-equal SDK/CLI worlds,
three typed no-output refusals and304independent segment/solid checks pass.
[Evidence and retained attempts](../../evidence/2026-10-31-source-program-ownership-verification.json).

This qualified the upstream prerequisite; the current qualification above
combines it with ownership-relative source contexts. General joint redesign, world design/variety/scale,
developer editing/locks and streaming remain open. All63tasks/13requirements and
statuses retained; Godot/artwork deferred.

## Bounded qualification — nested programs in actual ownership (2026-10-31)

Explicit purpose/source context now binds boundary-free terrain-intent templates
to exact computed ownership. Initial catalog analysis explicitly defers unknown
spatial selection; the shared joint owner rebuilds real region choices, demand
and complete local programs before accepting every physical body and journey.
Area layouts and relative frames reuse existing owners without source resizing,
arbitrary scattering, a mandatory world grid or reinstated city code.

Five controls retain165nodes/135bodies,45nested regions/45allocations and170directed
journeys. Independent checks cover3060segment/solid comparisons,135dimensions,
90original module offsets,177region pair intersections and15source-demand equations.
Two source edits/history restores produce exact original-world rollback. Native,
23JSchecks, two byte-identical SDK/CLI worlds, four typed no-output refusals and
actual browser/SVG geometry checks pass.
[Evidence and retained attempts](../../evidence/2026-10-31-nested-ownership-verification.json).

That context checkpoint required coupling/composition/capacity and excluded
population-source/terrain-demand phases. The current qualification above combines
those phases; broader joint redesign, complete world design/scale/editing, locks
and streaming remain open. All63tasks/13requirements and statuses retained; Godot/artwork deferred.

## Bounded qualification — spatial analysis and accepted source allocation (2026-10-31)

Direct surfaced recipes now analyze complete catalog choices and real terrain
region alternatives in ordinary generation order. Failed content-dependent region
analyses retain actual choices/reasons/work; exact bound generation reproduces
region/demand plans. Adapted source habitats are physically qualified by the
existing generator, and downstream quantities/layout use the accepted allocation.
Original sources, required members, entrances and journeys remain intact.

Four controls retain47nodes/34bodies and36directed paths. Independent checks cover
326segment/solid comparisons, three source-envelope equations, eight original
source dimension checks and all five required discoveries.440m²/430m² allocations
require three/two discoveries. Native source edit, hard-limit failure preservation,
history restore and exact rollback pass. Two SDK/CLI publications match byte for
byte; three typed CLI refusals publish nothing;14JSchecks pass.
[Evidence, tested binaries and retained failures](../../evidence/2026-10-31-spatial-program-analysis-verification.json).

Automatic ownership still forbids nested recipe Regions/LocalLayouts/RegionDemands;
an explicit ownership-relative context is the next integration. Unbound upstream
composition, broader redesign, locks, streaming and full design/scale gates remain
open. All63tasks/13requirements and statuses retained. Godot/artwork deferred.

## Bounded qualification — joint terrain and complete program search (2026-10-31)

Actual habitat measurement, exact terrain-derived mix quantities and complete
catalog choices now share the existing regional owner. Candidate habitat changes
rebuild the full nested decision topology; source-invariant failures prune only
their owning choices. Stable counts and source requirements still require full
physical placement and all local/regional journeys. The original work, attempt,
depth, cache and stage limits remain hard. Invalid data, dependency drift and
cancellation never become successful retries. Terrain-only sources inherit the
exact qualified habitat without a fabricated placement/building.

Four complete controls retain140nodes/131bodies,43discoveries/43nested markers
and90directed paths. The compound preserves48original base nodes/45bodies and
all source-phase nodes/routes; original marker sizes and0.4mactor widths remain
intact. Nine current-Core native scopes,7JSchecks, SDK/CLI byte equality, three
typed no-output refusals and independent count/source/body/path/SVG checks pass.
Source sessions retain both accepted edits and exact initial-world rollback.
[Evidence and retained failures](../../evidence/2026-10-31-regional-joint-verification.json).

The simultaneous search is bounded to declared supported inputs; consumer intent
region/source adaptation analysis and broader physical/partition redesign remain
open. The component policy qualifies a successful joint candidate; this increment
does not independently exercise joint failed-component repair. Selective locks,
streaming, world variety/scale/design acceptance and the full stage/core gate stay
IN PROGRESS. All63tasks/13requirements retain their statuses; Godot/artwork deferred.

## Bounded qualification — source-driven regional sessions (2026-10-31)

Acceptance before implementation: let a host start and edit a derived regional
world from its reusable `MetadataRegionalOwnershipRequest`, through the existing
transactional session owner. Freeze source before awaited reporting callbacks;
check edit identity and both accepted base hashes before expensive derivation.
Reuse the same ownership, generation, cache, checkpoint and atomic publication
owners; retain complete source and actual derived selection in source history.
SDK and CLI must produce the same world and accepted edit reports as the existing
explicit-definition route. Preserve failure/cancellation outcomes, original source
dimensions/functions/routes, work caps and existing publications. Verify newly
feasible preferred selection, unaffected regional reuse, exact source rollback,
fresh-process history replay, stale/infeasible/tampered source and original limit
refusals, plus actual shared log/audit/notification outcomes. Do not serialize
executable caches or turn derivation failure into an accepted edit.

Research checked2026-10-31: [Houdini TOP cooking](https://www.sidefx.com/docs/houdini/tops/cooking.html)
describes invalidating changed work and its dependants; [Unreal PCG generation
modes](https://dev.epicgames.com/documentation/en-us/unreal-engine/using-pcg-generation-modes-in-unreal-engine)
separates generation domains, hierarchical cached data and runtime streaming.
Design inference: retain dependency-scoped reuse, but always requalify global
journeys; source editing does not establish independent streaming or impose a
world grid. No third-party implementation or dependency is introduced.

Observed: the existing session now starts/edits from reusable ownership source,
with base validation before derivation and captured source before awaited callbacks.
The SDK and CLI retain exact original/preferred/rollback world and checkpoint
bytes, two accepted edits, two unaffected private packets, every required body,
function and full-width journey. Work/source/receipt/history tamper failures,
infeasible source, cancellation and existing output refuse transactionally.
Original explicit-definition and full regional-session regressions pass. Actual
callback mutation/cancellation, derivation failure attribution and committed
success-reporting failure are exercised. [Commands, hashes and limits](../../evidence/2026-10-31-regional-source-history-verification.json).
Selective locks, independent streaming, broader scale/design and full core
acceptance remain open. All63tasks/13requirements retained; Godot deferred.

**Previous qualification — portable complete population programs (2026-10-31):**
Explicit source context now inherits the caller's actual surface, origin and named
root anchors through the existing population owner. Static analysis retains both
source and consumer programs, source facts, relative layouts, access, connections
and installed provenance. Regional capacity measures complete root envelopes;
ordinary generation still qualifies every nested member, surroundings domain,
required open ground and original local/regional journey. No copied world poses,
source overrides, recursive sources or arbitrary placement are introduced.

The connected 336×224m control retains 48 nodes/45 physical sources and ten directed
journeys; the larger declared program retains 87/84 and sixteen journeys. Terrain
and source-size changes keep complete content. A one-region equipment-intent edit
preserves unrelated complete packets and replays source/history checkpoints.
Nine native scopes, 22 JS checks, public SDK/CLI equality/refusal, independent geometry
and actual browser/SVG inspection qualify this bounded complete composition.
[Reusable inputs](../../../examples/metadata/portable-population-world/README.md), [commands, source hashes and limits](../../evidence/2026-10-31-portable-population-verification.json).

This does not close broad world-design acceptance. Density-aware ownership,
terrain-dependent count/composition analysis, coupled repair, selective locks,
independent streaming and broad complete-world qualification remain open. All
original 63 tasks/13 requirements stay IN PROGRESS; Godot/artwork remain deferred.

**Previous qualification — source-domain content programs (2026-10-31):**
Source-domain declarations and complete traces now participate in existing placement fingerprints, native saved replay and source-history sessions. A 4-to-6 per-parent content edit equals clean generation; an invalid domain edit preserves accepted content/cache/history. This does not add independent domain-cell streaming, selective locks or automatic coupled repair.
[Evidence and limits](../../evidence/2026-10-31-source-domains-verification.json). All 63 tasks/13 requirements remain IN PROGRESS; Godot/artwork deferred.

**Previous qualification — source-derived game ground cover (2026-10-31):**
Optional game-authored ground rules now compose actual terrain, placed source
footprints/reservations, site features, semantic/local allocations and realized
corridors through the current shared area arrangement. The engine preserves whole
source geometry, parent ownership and material/rule provenance. Declared priority
resolves overlays; conflicting top-priority materials refuse. No objects are
scattered and reservations do not imply completed or populated ground.

Every domain area is accounted as covered, excluded or unresolved. Strict material
coverage refuses unresolved intent; partial scope keeps it visible. Ordinary
metadata and regional generation, installed-catalog SDK planning, source-history
sessions, native replay and public CLI use the same owner. Cells are vector
arrangement pieces, independent of gameplay grids or render tiles. The inspector
renders continuous compound fills and retains exact per-cell inspection targets,
with a separate layer toggle and source-derived SVG coordinates.

The larger-source control changes80→88m² of material footprint while preserving
all site members/features; a40m² void changes assigned cover to9960m². Complete
regional composition retains15 nodes,9 physical sources and10 directed journeys.
Irregular/partial/registered/allocation/trail/edit controls, saved replay, refusal
preservation, work/source/cell limits and cancellation pass. Nine native scopes,
28 JavaScript checks and three CLI publications qualify this bounded increment.
Independent equations inspect13 packets,1406 cells,77 source references,15 physical
body checks and1249 potential cell overlaps. [Exact evidence and limits](../../evidence/2026-10-31-ground-cover-verification.json).

Ground material coverage does not prove complete objects, planting, activity,
interactions, world design, artwork or runtime streaming. Dense combined boundary
scale remains bounded by the existing area kernel. Complete game-authored content
composition, coupled repair/analysis, density-aware ownership, selective locks,
streaming and broad core/design qualification remain open. All original63 tasks/
13 requirements remain IN PROGRESS; Godot/artwork remain deferred.

**Previous qualification — complete program analysis and demand-derived ownership (2026-10-31):**
The public `AnalyzeProgram` API and ordinary generation now share one catalog
expansion implementation. Analysis retains complete nested membership, authored
relative coordinates, capacity/variation/mix choices, connections and exact source
and installed dependency provenance without inventing unbound coordinates.
Analysis is explicitly not a placed or physically validated world.

Optional regional `Capacity` measures actual expanded root envelopes using the
existing placement/footprint owner. The declared maximum coverage derives area;
source radius derives anchor clearance. Authored minima remain hard and authored
weight is a bias multiplied by required area. Nested sources are retained and are
not double-counted. Ordinary generation verifies the analyzed members and source
choices, then still performs full placement, access and route validation.

The source-size edit derives240→360 required square units and proportional
ownership while preserving15 members,9 physical sources and10 directed local/
regional journeys in both worlds. Independent generation, CLI byte equality,
source-history replay and failed-edit preservation pass. Analysis also qualifies
platformer/space/dungeon hierarchy, relative layouts, complete capacity/mix/
variation choices, repeated root bindings, rotated registered source clearance,
capture/dependency changes, exact work, size bounds and cancellation.

Nine focused native scopes plus the strengthened analysis rerun,20 JavaScript
checks, two CLI generations and independent whole-source/path/terrain equations
qualify this increment. Browser inspection and downloaded SVG retain15 members,
9 whole source polygons and10 physical paths. One pre-existing test warning remains;
CLI is clean. [Exact commands, source hashes, failures and limits](../../evidence/2026-10-31-program-ownership-demand-verification.json).

Uniform-area ownership and simple regional polygons remain bounded capabilities.
The newer portable-population qualification adds static source/consumer analysis; terrain-demand/composition coupling and placed SourceWorld analysis still refuse.
Density-aware ownership, complete ground-content design, coupled repair, selective
locks, streaming and broad complete-world design qualification remain open. All
original63 tasks/13 requirements and the full core/design gate remain IN PROGRESS.
Godot/artwork remain deferred.

**Previous qualification — derived generation ownership and program anchors (2026-10-31):**
Complete regional program templates can now omit ownership/domain polygons, root
origins and selected root anchor coordinates. The common area kernel derives
weighted full-domain ownership; the existing terrain region owner derives actual
suitable anchors. The current regional generator still expands/places every nested
program and qualifies all local and regional journeys. Excluded terrain stays
owned without becoming playable. Ownership does not impose streets or storage cells.

Complete rectangular, weighted, moved-void and concave controls retain15 nodes/
9 sources each.4/8/16-region controls retain20/40/80 nodes and12/24/48 sources at
the same extent, source dimensions, widths and budgets. Independent requests,
source edits, clean equality, saved source history and refusal preservation pass.
The identical small-unit harness fails on the prior engine's implicit1-unit²
threshold and passes now, preserving a complete0.25-unit² metadata domain.

Nine native scopes,22 JS checks, three CLI successes/two expected refusals and
actual80-node browser/SVG inspection qualify this bounded increment. Independent
checks cover seven complete physical packets:120 whole source polygons,40 derived
anchor clearances,14597 source-height equations,146 directed paths and12544 solid-
edge clearances. The final SVG retains80 nodes,48 whole sources,62 physical paths
and80 labels. One pre-existing test warning remains; CLI is clean.
[Exact source, commands, retained failures and limits](../../evidence/2026-10-31-derived-ownership-verification.json).

Initial ownership uses explicit uniform-area weights and simple outer boundaries.
Automatic source-demand weights, density-aware ownership, complete ground-content
generation, coupled repair, selective regional locks, streaming and broad complete-
world design qualification remain open. All original63 tasks/13 requirements and
the full core/design gate remain IN PROGRESS. Godot/artwork deferred.

**Recorded acceptance — derived regional generation ownership (2026-10-31):**
Accept complete regional program templates without authored ownership polygons,
generation-domain polygons, root origins or the selected root anchor coordinates.
Use the common connected area kernel to divide the complete outer world domain
under explicit game-authored weights/minimum areas. These are generation ownership
masks, not land-use, traversability, streets or storage cells; excluded terrain
still belongs to an owner. Retain the original world terrain without alteration.

Derive each root anchor through the existing terrain/environment region owner
using the program's declared suitability/minimum area/clearance. Bind that anchor,
origin and domain into the existing regional generator without copying or replacing
content generation. Preserve all nested members, source sizes, ports and actual
outward/return journeys. Replay the source derivation during ordinary regional
validation, saved packet assembly and sessions. Retain full source/partition/anchor
proof and finite work/cell/depth/geometry limits; no arbitrary scattering.

Initial ownership partition is weighted uniform area on a simple outer boundary;
actual root anchors/content/routes remain terrain-qualified. Do not claim density-
aware ownership, exact program packing, general automatic ground-content coverage,
storage streaming or broad game-world design acceptance. Qualify concave domains,
protected voids, terrain-driven anchor response, source/weight edits, independent
request order, replay, transactional failure, budgets/cancellation and schematic
provenance through the current shared owners. Godot/artwork remain deferred.

**Verified bounded increment — shared-coordinate regional edit history (2026-10-31):**
The acceptance below passes on the current source/Core: complete10/11-node and
80/81-node nested programs, actual whole-packet/stage reuse, shared terrain and
ownership invalidation, changed dependency pins, explicit source rollback, source
history replay and atomic publication.32 accepted edits replay; the next refuses.
Stale world/definition, source-size, physical overlap, work, cancellation, concurrent
mutation, forged cache/history and existing-output failures preserve accepted state.
All15 create/edit/edit-publish/publish/restore success-sink failure cases expose
committed state; callbacks freeze inputs and late cancellation preserves commits.
13 current native scopes and12 saved JS checks pass; the CLI uses shared reporting
and the browser inspects the saved richer program. These bounded authored controls
do not close S08 or full core/design acceptance.
[Source-qualified evidence](../../evidence/2026-10-31-regional-session-verification.json).

The identical final harness reproduces a null-source checkpoint failure on prior
Core. Current Core validates null region/recipe/catalog rows before invoking the
installed historical resolver; all three typed refusals pass and accepted source
history remains intact. The initial verifier field-name/null-context mistakes and
premature test launch are retained as failed attempts, not passing evidence.

**Acceptance exercised:**
Add a regional session variant using the existing generation/cache, reporting and
atomic directory-publication owners. Accepted private packets may be retained only
when their complete regional context and registered dependencies still match.
Changed programs use the existing stage cache; shared surface or ownership changes
invalidate the affected contexts. Every edit requalifies complete cross-region
physical geometry and directed paths. Reports distinguish whole-packet reuse from
stage reuse and source replay performed during publication. Preserve source counts,
dimensions, widths, budgets and the declared world extent.

Require optimistic definition/world hashes, bounded attributed history, isolated snapshots,
rollback by an explicit source edit and deterministic save/replay. Failed edits,
cancellation, stale bases, concurrent operations, existing output and reporting
failures must preserve the correct accepted state. Checkpoints persist sources and
receipts, never executable caches; restore derives and compares every report.
CLI start/edit must use shared Log/Audit/Notification services and publish the same
combined/individual inspection packets with their checkpoint atomically. Qualify
complete nested programs, terrain/connection edits, no-op and richer edits, source
dependency invalidation and saved history tampering. Regional selective input-lock
authoring, automatic macro derivation/repair and the full core gate remain separate.

**Previous inspection integration (2026-10-31):**
Shared-coordinate publication now includes an exact combined schematic and full
individual packets. Current native/CLI read and assembly checks preserve the source
world, ownership and paths; browser/SVG evidence is recorded under S09. Regional
transactional history was still open at that checkpoint; the qualification above
adds it. Macro derivation and runtime streaming remain open; S08 is not closed.
[Evidence](../../evidence/2026-10-31-regional-inspection-verification.json).

**Previous qualification — independently generated shared-coordinate world regions (2026-10-31):**
The common metadata pipeline now generates a selected complete regional program,
persists its original-lattice terrain/source/build proof and assembles all declared
regions through the existing physical owners. Ownership polygons are independent
of storage cells. Whole seam-spanning sources retain one semantic owner and closed
neighbor references; different regions can reuse one recipe template/local IDs.
Current whole SDK, whole CLI and reversed saved-request assembly are byte-identical.

The complete10-node/6-source control retains nested members and actual outward/
return regional journeys; its richer edit retains11 nodes/7 sources and the exact
unchanged neighbor packet.4/8/16-region controls retain20/40/80 nodes,12/24/48
physical sources and6/14/30 directed regional journeys. Whole, reversed request,
declaration-order and complete saved replay match. Source dimensions, physical
sizes, counts and budgets remain declared; no grid or streets are imposed.

Eight current native scopes, six saved-JS tests, four CLI successes and seven
expected atomic CLI refusals pass. Independent equations verify2088 heights,
504 shared samples, all six baseline bodies, six directed paths and120 radius/
boundary checks. The same final harness fails on prior Core's missing final
cross-region dependency guard and passes on current Core. One existing test
warning remains; CLI is clean. Source/publisher/history failure attempts stay
preserved. Observed16-region generation allocated632956616 bytes in total and
the process reached192090112 bytes cumulative peak working set; these are
fixture observations, not universal performance or live-allocation ceilings.

At that generation checkpoint, regional history and automatic macro
ownership/coupled repair, independent storage-cell generation, runtime streaming
and the original63-task/13-requirement core/design gate remain open. Godot/artwork
deferred. No whole populated-world or visual acceptance is claimed.
[Current source, commands and limits](../../evidence/2026-10-31-regional-content-verification.json).

**Acceptance before independent shared-coordinate content generation — 2026-10-31:**
Add explicit generation regions to the common metadata pipeline. Generation
regions are game-authored ownership polygons with containing generation domains;
they are not storage-grid cells. Shared surface source and world coordinates have
one owner. A request must generate only the selected region's complete catalog
program, including source-sized nested structures and terrain capture. Reuse the
ordinary metadata generator and physical-source extraction; do not generate a
complete world then filter it, duplicate layout algorithms or revive city code.

Persist independently replayable source/build-bound region packets through the
existing JSON and atomic publication owners. Assembly requires every declared
region once, validates exact source provenance, rejects gaps/positive ownership
overlap, checks cross-region physical collisions and qualifies actual full-width
connections through the shared route owner. Seam-spanning members have one owner
and closed references to every touched ownership region. Source polygons, counts,
widths and limits remain unchanged. Independently requested/reloaded order and
whole generation must agree. Failed/cancelled publication and stale/tampered
packets refuse without replacing accepted output. Preserve compatible unaffected
packets across local content edits; requalify global collisions/connections.

Qualify irregular ownership, shared original terrain samples, structures and
routes crossing seams, both directions, local-content edits and exact finite
budgets. Record measured limits and missing gates. Continuous world streaming,
automatic cross-region repair and full world/design acceptance remain open until
separately proved; Godot/art deferred.

**Previous qualification — persisted bounded terrain capture (2026-10-31):**
Metadata generation now opts into saved original-lattice terrain windows through
the existing surface, session and publisher owners. The complete24-node/21-source/
7-program world stores1584 rather than263169 samples, retaining its world extent,
source dimensions, physical widths and budgets. Current full/capture geometry,
authored/network paths, costs and route work match exactly; source edits, saved
replay, checkpoints, rollback, cancellation and tamper refusal pass. Neighboring
captures share144 exact original samples.

Fourteen focused native scopes, seven JS tests, seven saved full-world JS packets
and both Release builds pass. Independent checks verify267057 heights,36 directed
paths,112 path segments and7280 radius/boundary checks. Actual browser inspection
qualifies saved terrain display, site details and SVG export. One existing test
nullable warning remains; CLI is clean. Expected CLI domain/precision refusals
preserve failed outcomes and publish nothing.

Logical membership regions can union across field planes while preserving exact
eligibility and source provenance; terrain-face consumers retain the default.
Original samples and whole-footprint/route checks remain authoritative. Bounded
source scans, early hard-anchor filtering and correctly scoped child dependencies
remove unrelated work; no counts, sizes or budgets were relaxed. The retained
exact-zero datum still rejects an attributed sub-resolution clamp sliver; this is
an explicit limit, not a successful case. Historical attempts remain preserved.

At that qualification independent content generation remained open. The current
generation-region increment above supersedes that gap for its declared bounds;
independent storage-cell/runtime streaming and the full core/design gate stay open.
Godot/artwork deferred.
[Qualified source, commands and limitations](../../evidence/2026-10-31-metadata-terrain-capture-verification.json).

**Acceptance before metadata terrain capture — 2026-10-31:**
Add an explicit versioned terrain-capture definition and replayed result to the
existing surface contract. The engine derives the original-lattice window/halo
from a declared sampling domain (defaulting to the unchanged world boundary).
Keep all required content, source sizes, world extent, traversal rules and existing
work limits. Save the normalized domain, source/build identity and actual samples;
never represent a bounded buffer as a complete field or silently widen a capture.

Generation, read validation, accepted edits, rollback, checkpoints, CLI publication
and exact JavaScript inspection must share the same surface owner. Reject stale/
changed captures and uncovered requested domains explicitly. Full fields remain
the current input for globally coupled algorithms; unsupported partial combinations
must fail rather than synthesize missing data. Verify complete physical content,
full-width routes, shared terrain samples, source edits/reload, failed edits and
tamper cases. The renderer must use original global sample indices, not rebased
origins, and must not generate missing terrain. Independently persisted content
chunks and native runtime streaming remain separate open requirements.

**Previous qualification — window-backed regional terrain queries (2026-10-31):**
The common height-query contract now distinguishes global lattice geometry from
full or bounded storage. Direct regional surface queries generate only required
original-cell windows/halos; immutable restrictions avoid full-field regeneration
and cloning. Both storage variants share interpolation, cell ownership and cuts.
An outside-field authored-platform regression now retains global geometry with
explicitly empty storage; the identical final harness fails on prior Core.

Nine current native scopes pass. Regional cases compare3375 exact samples,12
traces,16 footprints and four full-width routes with identical costs/work/audits,
plus the outside-field route control. Independent checks cover5920 heights,239
planar cost intervals and72 radius/boundary comparisons. Seven saved JS packets
retain24 nodes/21 physical sources and84 exact directed paths; three terrain JS
tests pass. Final Release builds pass with one existing test warning; CLI is clean.
The prior full CLI world stays byte-identical. No source dimensions, counts,
movement widths or budgets were reduced or raised.

At that earlier qualification metadata publication retained complete terrain.
The current capture increment above supersedes that persistence limit; independent
content generation remains open. All63
original tasks/13 requirements and whole-world design gates stay in progress;
Godot/artwork deferred.
[Qualified API, current source identities and limits](../../evidence/2026-10-31-regional-window-verification.json). This does not close the full stage.

**Acceptance before regional window-backed terrain consumption — 2026-10-31:**
Separate original-lattice geometry from sample storage at the existing height-query
contract. Full supplied/generated buffers and replayed generated windows remain
explicit variants; a window must never masquerade as a complete buffer. Both use
one interpolation, cell-ownership and segment/column-cut implementation. Missing
window samples must refuse instead of supplying zero or relocating the lattice.

Add direct regional construction at `WorldSurfaceQuery` for recipe-only terrain.
Validate/snapshot source declarations and every authored mask/patch/physical source,
then generate only the existing conservative regional cell window and halo. Retain
global indices, source provenance, constraints and budgets. Reuse validated immutable
height queries in `RestrictTo`; do not regenerate or clone the complete buffer.
Whole-field callers and globally coupled drainage/wind still require complete data.

Qualify exact samples, slopes, cuts, masks, full-width paths/costs/work, clear and
blocked terrain, physical obstacles, region seams/edges, fractional coordinates,
source mutation, repeated restriction, declared work/cancellation and bounded memory.
Compare with full-source regional queries and preserve required program content.
Public regional Core queries do not by themselves establish local metadata-session
generation, independently persisted content chunks or runtime streaming.

**Current — independent terrain-window generation (2026-10-31):**
The existing terrain owner now evaluates requested original-lattice windows with
optional one-cell halos, saved replay and complete order-independent assembly.
Whole/window generation shares the same height evaluator; the old CLI world is
byte-identical. Independent equations cover264258 heights and9522 repeated seam
references. A399-sample request from a263169-sample source allocated101672 bytes
in the measured case; this is not a universal performance target.

Three current native scopes, three saved-JavaScript checks and both Release builds
pass. Tests retain one prior warning; CLI has none. Missing coverage, altered/stale
windows, source/count/work limits and cancellation refuse. Assembly cannot exceed
the source work bound. Source dimensions and work ceilings remain unchanged.
Metadata surface/routing still uses full fields; independent content chunks,
regional terrain consumption and broader world/design acceptance remain open.
The original63 tasks/13 requirements stay in progress; Godot/artwork deferred.
[Qualified API, commands and limits](../../evidence/2026-10-31-terrain-window-verification.json). This does not close the full stage.

**Acceptance before independent terrain-window generation — 2026-10-31:**
Extend the existing `WorldTerrainGenerator`, not the chunk ownership index, with
versioned requests for original-lattice vertex windows and an optional one-cell
halo. Generate only requested samples; retain global origin, indices, gradients,
ordered operations, detail seed, source digest and implementation identity. Whole-
field generation must use the same height evaluator and remain byte-exact. Keep
the existing513-by513 source bound and work ceilings; charge each window's actual
sample work, including halos, before execution. No rebased-origin approximation.

Provide bounded replay and complete assembly through the same owner. Accept any
request/load order and matching overlap once; reject gaps, changed sources/builds,
stale/tampered values, invalid ranges, work exhaustion and cancellation. Assembly
must never fabricate missing samples. Verify independent coordinate/height equations,
fractional/negative origins, non-square steps, operation ordering, seam/corner halos,
complete exact reconstruction and measured bounded generation memory. Reuse current
serialization/publication boundaries when publishing a world; this API slice does
not replace world-content generation, globally coupled drainage, metadata sessions,
route planning or downstream runtime streaming. Keep those gates open.

**Current — complete connected multi-level metadata program (2026-10-30):**
Three independent physical domains retain48 rooms,48 fixtures,48 actual doorways
and96 directed local routes:99 nodes/96 physical sources. Rectangular first/last
levels deliberately share identical local IDs/worlds; a hexagonal middle level
remains distinct. Two explicit doorway-bound transitions support38 source steps
outward and38 back. These are independent coordinate frames, not inferred3D stairs.

Enlarging all16 middle-level fixtures from0.8×0.5 to1.2×0.8 preserves all counts,
other sources and budgets, reuses34/48 local stages and retains exact other levels.
CLI/SDK worlds and complete checkpoint bytes match; clean generation and restored
history equal accepted state. Missing selectors, duplicate edits and stale-base
edits refuse without partial packets. Independent checks cover five complete
packets,380 journey steps,835840 swept solid-edge clearances and exact source
polygons/endpoints. All initial local worlds equal their prior runtime packets.
Twenty new and three original saved-JavaScript journey/navigation cases pass,
including rejection of a wrong level when its local data/world hashes are equal.

Production C#/templates remain unchanged from the prior qualified697-file source
baseline. Existing Core and the unchanged public SDK host were executed together;
no new C# compilation is claimed. The current system lacked .NET10 Core runtime,
so a SHA512-verified Microsoft10.0.14 portable runtime was placed only inside the
workspace. SDK availability, live browser/design, artwork and Godot are not new
acceptance claims. Search-domain ground is not assembled room-floor coverage.
[Reusable definition/edit](../../../examples/metadata/multi-level-world/README.md) and [commands, source/binary identities and retained failures](../../evidence/2026-10-30-multi-level-world-verification.json).
The63-task/13-requirement core gate remains IN PROGRESS; Godot/artwork deferred.

**Current — composed-world developer lifecycle (2026-10-30):**
Independent-domain sessions now share the awaited operation runner with single-world
sessions. Source/checkpoint snapshots, structured identity/outcomes, rollback,
serialized awaited edits/publications and explicit committed-state recovery apply
through API and CLI. Restore reports its actual action without inventing historical
edit events. Generation geometry, required content and work/capacity bounds are unchanged.

The public-Core-only SDK example runs the outdoor/two-floor workflow. API/SDK/CLI
baseline and edited world/checkpoint bytes match. Each of six packets retains all
21 nodes, 14 physical sources, two transitions and both 11-step journeys. Independent
source expansion verifies 132 directed journey steps. Fifteen postcommit sink cases,
callback/checkpoint mutation, early/late cancellation, rollback and lock contention
pass. Four actual CLI refusals publish nothing; six affected native regression
scopes, six embedded-JavaScript packet checks and warning-free Release builds pass.
[Sources, commands, retained failed attempts and limits](../../evidence/2026-10-30-domain-lifecycle-verification.json).

This qualifies composed-world lifecycle reliability, not full professional-world
design, arbitrary geometry, unlimited scale or every game genre. The existing
63-task/13-requirement audit and core design gate remain IN PROGRESS. Godot/art deferred.

**Acceptance before composed-domain lifecycle work — 2026-10-30:**
Bring the existing domain collection session onto the same awaited operation
reporter as single-world sessions. Preserve the current generation/proposal/cache,
journey, checkpoint and atomic-directory owners. Freeze definitions, proposals and
checkpoint bytes before callbacks; correlate actor, definition/world/proposed and
checkpoint identities. Report create/edit/publish/restore and failure/cancellation.
Reject synchronous edits on reported sessions, serialize awaited mutations and
publication, retain both operation and sink failures, and expose the committed
domain session/output when only success reporting fails. Restore replays history
without fabricating new edits. Migrate the current CLI to that API. Qualify the
complete outdoor/two-floor program and its exact journeys, clean regeneration,
source edits, rollback, callback mutation, concurrent operations, sink outages,
late cancellation, replay/tampering and CLI publication/refusal. No geometry,
member count, source dimension, budget or supported-domain promise is relaxed.
Full composition/design, scale and Godot remain separate open gates.

**Physical population scale qualification — 2026-10-30:**
The complete 1,024-source case retains canonical chunk membership, unchanged native
budgets and exact geometry against the frozen baseline. Density edits equal clean
generation; failed/cancelled proposals preserve accepted state; complete checkpoint
history replays. This remains full-world generation with finite chunk references,
not independent local generation or unlimited streaming.
[Exact source/binary/command and measurement evidence](../../evidence/2026-10-30-population-scale-verification.json).
Stage/core remain IN PROGRESS; Godot/artwork deferred.

**Developer lifecycle qualification — 2026-10-30:**
A public SDK example now demonstrates complete source edits and checkpoint restore
for wilderness, generated interior, compound and topology-only worlds. All required
content and paths remain exact. Protected/stale/invalid edits, progress cancellation,
existing targets and failed reporting preserve their correct accepted/committed
state; restore records no fictitious new historical actions.
[Exact sources, commands, retained failures and limits](../../evidence/2026-10-30-session-workflow-verification.json).
Stage/core remain IN PROGRESS; Godot/artwork deferred.

**Current — original-lattice sampled regional dependencies (2026-10-30):**
Regional network searches now share one conservative sampled-cell window with a
one-cell halo for terrain features and private dependency fingerprints. Original
origins, steps, indices, heights and source provenance stay intact. Remote sample
edits outside a policy's window preserve its qualified search; local/shared-seam
edits invalidate the relevant policies. Full source validation still rejects an
invalid remote sample before reuse. Terrain recipes remain conservative whole-
recipe provenance dependencies. No source buffer is cropped or substituted.

The complete 24-node/21-physical-source case retains its original search budgets.
Eastern terrain increases measured walking ascent/descent costs under its original
permissive profile; a western slope reroutes the stricter supply network. Exact
undo restores the original source samples and paths. Warm worlds, work receipts,
checkpoint replay and actual CLI publication agree with clean generation.

Repeated sampled envelope checks use sufficient full-domain/mask geometry proofs
and exact original triangle gradients. Separated hazards permit acceptance; an
actual impassable interior witness proves rejection. Tangencies, tiny intersections
and potentially ambiguous boundary ownership use the existing exact area owner.
1,575 exact sample comparisons and 192 optimization/area-owner comparisons pass.
Independent integration covers 72 directed journeys, 208 complete-width envelopes
and 360 terrain/ascent/descent/guidance cost components.
[Commands, source hashes, retained failures and limits](../../evidence/2026-10-30-sampled-regions-verification.json).
[Delivered sampled recipes](../../../examples/metadata/regional-network-policies/README.md).

S04.8/S08.3 remain partial: dependency windows are conservative rectangles with a
halo; generated terrain recipe provenance and nonregional queries remain whole.
Parent stages/global source validation/publication replay may still execute, and
storage/streaming is not cell-local. The unchanged core requirement, variety and
mixed-settlement design gates remain open. Professional generator IN PROGRESS;
Godot/art deferred.

**Sampled regional dependency acceptance — 2026-10-30 (bounded qualification above):**
The first complete 24-node/21-physical-source sampled fixture exhausted its
existing route budget in repeated envelope arrangement checks. Retain the case
and budgets. Add a sufficient pure-field envelope proof using the shared original
cell window, exact triangle gradients, full domain containment and conservative
bad-triangle bounds. Mask intersection and strict interior impassable witnesses
can prove rejection. Tangency/uncertain cases and authored terrain overlays retain
the current exact area owner. Differentially check this
proof against actual area queries before claiming performance qualification.

Keep original sampled-field origin, step, indices, complete source buffer and exact
sampling arithmetic. Define one conservative regional cell window with a one-cell
halo for boundary/triangle ownership. Use that same window for both terrain-feature
extraction and the private route dependency fingerprint; omitting values from a key
while the solver still inspects them is forbidden. Include field header/dimensions,
all window sample vertices, complete terrain recipe provenance, region and other
query dependencies. Unbounded/nonregional queries retain complete-field behavior.
Qualify fractional/negative origins, grid/triangle seams, partial/outside field
domains, containing/concave regions, mutation/cancellation, real slope barriers,
remote/local/halo edits, actual network reuse, clean-world/work equality, checkpoint
and CLI replay. Do not raise budgets, remove required world content, substitute
remote heights or claim cropped storage/streaming. Parent-stage locality and
minimal triangular dependency sets remain open.

**Current — authored regional routing dependencies (2026-10-30):**
`MetadataNetworkRule.RoutingBoundary` now constrains the complete passage envelope
through the existing shared C# solver. `WorldSurfaceQuery.RestrictTo` snapshots that
domain and conservatively excludes disjoint terrain/exclusion/physical sources
from its query dependencies. Containing, crossing and touching sources remain in
full. Concave domains are supported; a region outside the world is rejected.
This is an explicit gameplay constraint, not an inference from selected paths.

At unchanged physical source poses and budgets, eastern water changes eastern
routes while the western search is reused. A western edit does the opposite;
removing water discovers the original clear passages. Shared-boundary terrain
invalidates both. Warm worlds and canonical work match clean generation exactly.
All 24 metadata nodes, seven parent programs and fourteen physical children remain.
Independent checks cover 84 directed journeys and 236 conservative passage
envelopes against region boundaries, solids, water and graph obligations.
Current CLI generation/edit/checkpoint replay and the delivered reusable recipes
agree with API world bytes. JavaScript preserves all exact source/path geometry.
[Commands, hashes and precise limits](../../evidence/2026-10-30-network-regions-verification.json).
[Reusable region-boundary examples](../../../examples/metadata/regional-network-policies/README.md).

S04.8/S08.3 remain partial: sampled heightfields conservatively retain complete
field dependencies, overlapping source polygons stay whole, and parent stages
may rerun. Publication/read validators still replay. Further sampled-cell and
parent-stage locality plus the unchanged core requirement/design audit remain
open. This does not complete the professional generator. Godot/art deferred.

**Regional dependency acceptance — 2026-10-30 (bounded qualification above):**
Add explicit optional routing boundaries to the existing shared network policy,
enforced by the ordinary solver for the complete passage envelope. Do not infer
dependencies from already selected paths. A detached regional surface query may
omit only terrain/exclusion/solid sources whose closed bounding boxes are disjoint
from the declared domain. Preserve containing/crossing/touching sources in full,
all provenance, original world content, and complete sampled-field dependencies.
Validate regional containment against concave world domains and preserve input
immutability. Existing policies without a boundary keep their current behavior.
Qualify full-width route containment, boundary violations, wet/dry shortcuts,
neighboring terrain/solid edits, unchanged-policy hits, warm/clean equality,
original budgets, rollback, checkpoint replay and current CLI publication.
Heightfield cell cropping and parent-stage locality remain separate open work;
this increment must not claim minimal terrain-region invalidation generally.

**Qualified increment — per-policy execution reuse (2026-10-30):**
The acceptance below now passes across allocation feedback and final generation,
independent domains, no-op retention, policy eviction/re-add, exact clean-world/work
equality, invalidation, rollback and actual checkpoint/CLI replay. The retained
reuse-cost case now completes under unchanged source sizes/budgets using shared
admissible search bounds and a qualified one-bend incumbent. Full query or solid
changes conservatively invalidate policy keys. Four entries / 1,028 records share
the existing 128 MiB cache bound; byte accounting/capacity propagation are tested,
but full 128 MiB stress is NOT RUN. Publication/read validators still replay.
[Exact commands, source hashes and corrections](../../evidence/2026-10-30-network-memo-verification.json).
S04.8/S08.3 remain partial: terrain-region selective invalidation and parent-stage
locality are not established. Historical gap evidence below is preserved; its
unchanged-policy search portion is superseded by this qualification. Core remains
IN PROGRESS; Godot deferred.

**Acceptance before network execution reuse — 2026-10-30:**
Extend the existing private accepted-state cache with bounded network-policy
memoization shared by placement feedback and final network generation. Key the
complete frozen route surface (including heightfield/walls), placed solid layer,
world/rule, exact scoped destinations and all canonical policy/profile/search/
reuse settings. Validate scope/count/entrances before a lookup. Terrain or solid
changes conservatively invalidate all affected complete-query keys; still-feasible
selected paths alone never prove unchanged optimum/selection. Regional clipping
or selective terrain dependency inference is not established by this increment.

Retain at most four current policy entries, matching the existing rule ceiling,
inside the unchanged 128 MiB accepted-cache byte bound. Failed speculative runs
must not mutate accepted entries. Copy results on return, retain active entries
through whole-stage/no-op reuse and remove omitted policies from accepted memo
state. Preserve fork/adopt behavior, cancellation and atomic rollback.

Preserve exact clean-generation world geometry, costs and canonical work receipts:
cached searches consume their original logical shared-budget charge, rather than
bypassing limits. Publish separate bounded search-execution evidence in session
reports, including computed/current/accepted reuse and exact hashes. No private
cache contents may be restored from JSON; checkpoints replay actual generation.
Qualify unchanged-policy edits, add/remove/re-add, no-op retention, both feedback
and final phases, terrain/solid/endpoint/profile/width/reuse/limit invalidation,
mutation isolation, failures/cancellation, byte bounds and checkpoint restoration.
Keep S04.8/S08.3 partial until broader terrain/region granularity is qualified.

**Observed remaining execution-granularity gap — 2026-10-30:**
The network-policy session removes one supply rule while keeping the field-path
policy, source geometry and poses unchanged. Its accepted-state report reuses
surface/content/source-position/region stages, but reruns allocation, access,
networks and downstream work. The ordinary network owner replans field paths.
Correct generation, checkpoint replay and rollback pass; this does not establish
S04.8 affected-region reuse. See the [actual source/receipt finding](../../evidence/2026-10-30-network-policies/incremental-gap.json).
Implement safe per-policy reuse at the existing execution-cache/network boundary
with complete input/physical dependencies, rather than accepting a still-feasible
old path after terrain changes that might permit cheaper alternatives. Preserve
all required content, atomic snapshots, explicit work/cancellation and the full
remaining S08 audit. No compatibility cache or rejected city fallback.

**Current dense persistence qualification — 2026-10-30:**
Current typed replay and independent ordered-byte/hash reconstruction preserve
all 22 accepted edits and 23 complete 263,169-sample terrain buffers. The dense
world uses 62 bounded parts (64,037,997 logical bytes); the domain collection
retains both physical/logical spaces and exact forward/return journeys. Chunk
seam, numeric-buffer protection, malformed/corrupt storage and cancellation
cases pass. Original per-file/document/work bounds remain fixed. [Current commands/source/history evidence](../../evidence/2026-10-30-environment-matrix-verification.json).
S08/core remain IN PROGRESS; no streaming-runtime or cross-platform claim.

**Current hierarchy replay qualification — 2026-10-30:**
All original 85/169/493/577-node programs now generate/reload with 20/40/108/128 child stages. Resumed assignments keep detached diagnostics/conflicts; later alternatives cannot mutate saved plans. Existing deterministic declaration order, source coordination, failed-edit/checkpoint preservation and budget refusals pass. The previously recorded 493-node stack failure is resolved.
[Source/command/measurement evidence](../../evidence/2026-10-30-heap-stages-verification.json).
Stage remains IN PROGRESS; Godot/artwork deferred.

**Earlier matrix evidence — 2026-10-30 (historical snapshot):**
Current source regenerates/reloads the 85- and 169-node scale programs. The 493-node program crashes with stack overflow before its saved packet completes; 577 is NOT RUN. Historical 493/577 evidence does not qualify the current binary. Preserve full targets; replace recursive stage chaining with bounded heap state.
[Source/command/failure/browser record](../../evidence/2026-10-30-core-matrix-verification.json).
Stage remains IN PROGRESS; Godot/artwork deferred.

**Provisional relation qualification — 2026-10-30:**
Provisional two-source, empty-domain, source-backtracking and repeated-parent worlds survive typed/file replay. Successful source edits equal clean generation and failed dependency edits retain accepted state. Candidate caches are local to a placement invocation and each branch enumerates only its own points.
[Current source/command/geometry evidence](../../evidence/2026-10-30-provisional-relations-verification.json).
Stage remains IN PROGRESS. Godot/artwork deferred.

**Analytic relation qualification — 2026-10-30:**
Fixed-anchor analytic placements now replay at typed/file boundaries. Successful anchor edits equal clean generation; impossible edits retain accepted state. Catalog counts and source dimensions remain unchanged.
[Evidence and limits](../../evidence/2026-10-30-relation-candidates-verification.json).
Stage remains IN PROGRESS; Godot/artwork deferred.

**Current coordination increment — 2026-10-30:**
Version-3 receipts replay complete-assignment digest guards as well as node-pose clauses. Successful route-offset edits equal clean generation and remove stale guards; failed edits preserve accepted state. The existing terminal chunk, cache and checkpoint owners remain in use.
Only version-3 receipts execute; earlier generated formats require regeneration.
[Evidence and limits](../../evidence/2026-10-30-complete-source-coordination-verification.json).
Stage remains IN PROGRESS. Godot and artwork remain deferred.

**Earlier node-clause increment — 2026-10-30 (receipt format superseded):**
Generated coordination clauses now identify allocated nodes, rather than local
demand names. The existing metadata placement owner checks global poses across
instantiated stages. Incomplete matches remain eligible; complete matches reject
before descendants are committed. Reusable programs are preserved. Ordinary
request-level demand nogoods remain authored local search constraints; generated
coordination no longer injects clauses there.

Version-2 receipts replace the old generated format without a compatibility
execution path. Nested source footprints are included in dependency invariants.
Unknown/unbound/duplicate members, invalid coordinates and oversized clauses
refuse; declaration bounds are checked before snapshot serialization. Existing
metadata work, attempt, clause and receipt limits remain fixed.

One/two repeated-parent references retain 4/7 nodes, 3/6 physical members and every
catalog/program instance. Parent frames and preferred first children stay exact;
second children move to satisfy 2m consumer spacing. Their 2/3 attempts account
for 21801/66729 of the original 200M planning ceiling. Independent source sizes,
parent containment and `sqrt(53)`m accepted child gaps pass.

Five focused C# scopes plus the zero-warning delivery rerun, six actual CLI
generations, atomic exhaustion refusal and nine JavaScript cases pass. Cross-stage
parent/child clauses, invalid members, demand-edit/clean equivalence, rollback and
checkpoint history are qualified. Root-pair and area cases remain supported.
The full baseline/richer references still retain 248/270 nodes, 219/241 required
population members, all prescribed centroids and 38 exact source directions.
[Sources, command versions and independent geometry](../../evidence/2026-10-30-nested-coordination-verification.json).

This qualifies nested spacing with the declared fixed-parent references, not
route/contour-dependent or general moving-parent repair, continuous optimal
packing or an exhaustive world solver. Next: source-dependent connection/content
constraints and purposeful constraint-derived candidates, followed by the broader
S10 terrain/structure/domain matrix. S06/S08/S09/S10 and the professional generator
remain IN PROGRESS. Godot and artwork remain deferred.

**Coordinated source and required population — 2026-10-30:**
The earlier spacing failure is resolved in the common C# pipeline. Source
layouts remain provisional until full consumer placement passes. A witnessed
source-root/area-member conflict creates a finite attributed pose nogood; source
placement is reconsidered while terrain, environment, region, allocation, root
and source-envelope dependencies stay fixed. Prescribed future centroids, source
dimensions, counts and required clearance are not changed. Accepted source nodes
and all nineteen source connections then remain exact.

Both original density references now pass: baseline has 248 nodes/247 physical
members and all 219 required population members; richer density has 270/269 and
all 241 members. Each uses three provisional source layouts, accounting for
25235930/25957340 placement work within the original 200M ceiling. Independent
linear-density integrals, weighted centroids, complete source-sized polygons,
one-meter root-envelope spacing and all 38 directed full-width paths pass.

Replayable receipts preserve original inputs and rejected/accepted source
identities. Provisional caches are isolated; only the accepted stage cache is
adopted, and terminal chunk identity includes the accepted receipt. Attempt/work
refusal, missing/tampered proof, multi-pose constraints, unsupported-socket refusal,
region/program-composition selection, successful demand edits, stale-guard removal,
no-op reuse, failed-edit rollback and checkpoint replay pass. Six focused C#
scopes plus extended session checks, three actual CLI generations, expected atomic
CLI refusal, three JavaScript cases and actual browser/SVG geometry have
[versioned evidence](../../evidence/2026-10-30-population-coordination-verification.json).

This qualifies invariant root-area spacing coordination, not a general solver
for source-source combinations, nested/moving parents, route/contour-derived
population or route-blockage repair. Those failures remain explicit; no arbitrary
repair, count reduction or global-optimality claim is introduced. Next: extend
shared coordinated constraints to those dependencies and continue the S10
structural/terrain/domain matrix. S05–S10 and the professional generator remain
IN PROGRESS. Godot and artwork remain deferred.

**Partitioned world and source-history persistence — 2026-10-29:**
The existing publishers/readers now share a streaming JSON codec: ordinary
compact JSON up to 8 MiB, then versioned manifests with ordered SHA256-addressed
1 MiB parts. Per-file and authored-input limits remain 16 MiB; reconstructed
generated documents/checkpoints are bounded separately at 256 MiB/256 parts.
The reader authenticates every part and the complete stream before typed source
validation. No native sample, required member, source history or work rule is
omitted, and no executable cache is loaded from disk.

The captured dense publication failure now succeeds with the same 263169 terrain/
drainage samples, 32768 m² minimum, 32-face cap, original work declarations and
complete source receipts. Its 64037963-byte world uses 62 parts; standalone flow
replay matches the original captured data. A 23982652-byte checkpoint preserves
all 22 accepted requests and 23 full terrain buffers, including the initial source.
A large two-domain/six-node collection retains physical-network and logical
forward/return journeys through the same root/child storage owner.

Strict corruption/order/size/path/cancellation cases, rehashed semantic tamper,
independent byte/source/history/SVG checks and actual browser part loading,
selection/layers/fit/export pass. Human property review is an explicit bounded
preview; stored properties remain complete. [Exact sources, commands and limits](../../evidence/2026-10-29-partitioned-storage-verification.json).

S08/S10 and the professional generator remain IN PROGRESS. The recorded dense
monolithic-file failure is resolved. Next: consolidate broader terrain/content
and structural-diversity qualification against the existing capability matrix,
addressing observed design/scale failures. Transport is not runtime spatial
streaming or whole-game/design acceptance. Godot and artwork remain deferred.

**Dense numeric-buffer/source-history qualification — 2026-10-29:**
Numeric arrays now form one authenticated authored change while complete initial
and accepted buffers remain saved. The 4096-entry limit is unchanged. Protected
individual samples and malformed array lengths still refuse before commit.
A full 513×513 terrain relocation preserves the 32768 m² minimum, counts, original
limits, clean equivalence and rollback; its two-edit source checkpoint restores
normally (15783839 bytes). Existing regeneration and domain-session tests pass.
The final 59-node physical level-edit/publication/checkpoint flow also passes.

That increment recorded a dense drainage publication refusal at the 16 MiB file
limit. The later partitioned transport above resolves it without omitted samples,
raised per-file guards or trusted cached geometry. S08 remains IN PROGRESS.
See [source/check evidence and remaining scope](../../evidence/2026-10-29-dense-regions-verification.json).

**Mixed terrain/interior/logical session — 2026-10-29:**
A retained-lake level edit in a three-domain collection recomputes its outdoor
network while every unchanged interior/progression stage reuses accepted cache.
All 19 nodes remain. Clean collection equality, failed/cancelled journey rollback,
post-failure cache recovery, strict publication/read and full source-history
checkpoint replay pass. Existing-output CLI refusal preserves all 32 packet files.
See [bounded verification and remaining gates](../../evidence/2026-10-29-mixed-domains-verification.json).

**Retained-water/lake-level geometry — 2026-10-29:**
The existing water owner now derives explicitly requested retained bodies from
retained drainage terminals, declared levels/bounds/kinds and strict positive
depth. Connected terrain extraction preserves distinct basins, islands, voids
and dry zero-depth saddles. Bed planes, depth/capacity and source receipts remain
separate from flat occupied water planes. Closed terrain shorelines are required
by default; explicit clipping retains unresolved/deficit witnesses. Rates do not
infer water volume or levels. Source terrain and existing stream provenance remain.

Analytic basin/level/island/capacity checks, actual region/exact-site/boat effects,
level/source edits versus clean generation, wind/route dependency closure,
failed/cancelled-edit rollback, strict publication and source-history restoration
pass. The larger level edit preserves 59 nodes/58 physical members, seven complete
compounds, 16 landscape members and 82 paths with the original 1.8m ridge,
25000 m² minimum, source geometry and budgets. A captured one-ULP contour seam
failure at level 1.17 was corrected using the common generated-event convention
and complete topology rebuild, retaining the requested level and all selected cells.

Seven affected C# scopes, current CLI, four JavaScript checks, actual browser
selection/layer/fit/SVG export and independent saved SVG coordinates pass.
An exact rational native-triangle oracle checks 12 saved cases/13 bodies, including
the full edited world's 52.559833024118575 m² area and 0.7883974953617774m³ geometric capacity.
See [exact source versions, checks and limits](../../evidence/2026-10-29-retained-water-verification.json).

S02/S08/S10 and the professional-generator objective remain IN PROGRESS. Next
qualification must widen terrain/domain/environment diversity and dense-field/
world-scale behavior through the existing shared owners. Dynamic storage/outflow,
broader retained-water policies and world/game design acceptance remain open;
production artwork and Godot are deferred. Technical schematics are not game art.

**Automatic wind recipe/session integration — 2026-10-29:**
Optional `WindMoisture` now derives after the effective occupied surface.
`wind-moisture` and `environment-input` stages preserve original authored
environment separately and feed existing region/allocation/suitability/path/
chunk consumers. Wind, evaporation, water and terrain edits invalidate the
dependent closure; authored-expression edits reuse wind derivation. Wind
reversal changes actual usable regions without dropping required destinations.

Source-bound receipts and environment markers replay on world/standalone read.
Publication also binds the original recipe's definition and authored environment,
rejecting mismatch before creating a directory. Population compilation preserves
source wind authority. Failed/cancelled proposals retain accepted state/cache;
edited worlds equal clean generation and complete source histories restore.

The larger automatic session retains 59 nodes/58 physical members, seven complete
compounds, 16 landscape members and all 82 directed paths across a wind edit.
Population-source and terrain-demand recomputation, clean equality, publication
and checkpoint restoration pass with the original 1.8m ridge, 25000 m² minimum,
source dimensions and work budgets. Eight affected regression scopes pass on
the integration binary; four are rechecked on the final publication-guard binary.
Current CLI, three JavaScript artifact checks and independent 25-node wind/field
composition checks pass. See [source revisions, checks and remaining gates](../../evidence/2026-10-29-wind-metadata-verification.json).

S02/S08/S10 and the overall generator remain IN PROGRESS. The later retained
water increment above now qualifies bounded lake geometry/levels. Wider domain,
environment, dense-field and scale/design gates remain open. No whole-game or
all-genres acceptance is inferred; Godot and artwork remain deferred.

**Connected full-world replay — 2026-10-29:** Density edits regenerate all 16/17
landscape members while preserving the seven-compound source and 82 paths. Clean
and edited results agree, exhausted search rolls back, and full source-history
checkpoints restore. Current chunk membership includes every node and directed
path. [Evidence](../../evidence/2026-10-29-connected-area-verification.json).
These 59/60-node cases do not establish maximum scale or streaming throughput;
S08/full generator remain IN PROGRESS and Godot remains deferred.


**Terrain-derived required program spaces — 2026-10-29:** `AreaBalanced` now
accepts required program intents without local coordinates. The shared area owner
derives density-weighted spaces and centroid anchors, allocating mass in proportion
to each minimum-area requirement. Minima remain hard constraints; ordinary
environment population retains equal-mass behavior. Canonical source and generated
area/weight provenance stay separate. [Evidence](../../evidence/2026-10-29-program-spaces-verification.json) records analytical uniform,
unequal/odd-weight, linear-density and holed geometry, nested scopes, invalid-
centroid/input/budget/cancel refusal, and exact work limits. Four seven-node/two-
structure physical programs retain source dimensions and a required interface
route; density changes positions while content identities, terrain and regions
survive. Strategy selection, source/tamper replay, clean/edit equality, rollback,
checkpoints, four CLI exports/refusal and fifteen Node checks pass. Existing area
geometry, six-compound/194-member/19-route population, terrain-demand, anchor and
joint-layout regressions pass. S03/S05/S07/S08/S09/S10 and the full generator remain
IN PROGRESS. This is a bounded intent-to-space grammar, not autonomous game-purpose
design or proof of occupied-world coverage. Godot/artwork/runtime and browser/
design acceptance remain deferred/unqualified.


**Generated anchor partitions — 2026-10-29:** `AnchorBisection` now derives a
recursive cut tree from protected required-program anchors. Callers supply no
cut tree or grid. The current local owner computes principal anchor spread,
chooses balanced separable groups and retains the generated tree beside unchanged
canonical source. Existing terrain/parent clipping, connectedness, minimum areas,
interfaces and full source-sized placement remain mandatory. [Evidence](../../evidence/2026-10-29-anchor-partitions-verification.json)
records analytical three-program areas/spans, holes/concavity, rotation/translation,
single/nested scopes, ID collisions, a 32-leaf bound case, disconnected/resolution/
budget/cancel refusal and source/tamper replay. A seven-node/two-structure program
retains its physical content and interface route, supports strategy selection,
edits/rollback and checkpoint replay. Direct/alternative CLI packets, refusal,
four new and six existing-strategy Node checks pass. Existing binary/cut/strategy
regressions and the six-compound/194-member/19-route area population pass. S05/S08/
S09/S10 remain IN PROGRESS. Anchors and mandatory game content remain authored
intent; this does not synthesize whole-world purposes, roads or arbitrary layouts.
Godot/artwork/runtime and browser/design acceptance remain deferred/unqualified.


**Bounded metadata publication recovery — 2026-10-29:** Real owned Windows
directory and child-file locks reproduce rename codes 32 and 5 respectively;
the child-file case matches the earlier access-denied message. Releasing the same
handle permits the rename. The shared metadata publisher now makes at most four
rename attempts, with 50/150/350 ms cancellable waits, only for those Windows codes
while staging exists and the target remains absent. Published `publication.json`
retains the commit attempt and earlier failures. Dependency identities are checked
before every attempt; cleanup preserves both the original and cleanup exceptions.
[Evidence](../../evidence/2026-10-29-metadata-publication-verification.json) covers real-lock recovery/exhaustion, cancellation, dependency-check
refusal, destination races, guarded cleanup, exact public single-world/three-domain
exports, session/adaptation regressions, CLI preservation of existing output and
three current Node checks. The earlier real holder remains unidentified. Recovery
from every filesystem error, mutable-plugin drift under load, maximum scale and
browser/design acceptance remain unqualified. S08/S09/S10 and the overall generator
remain IN PROGRESS. Godot/artwork/runtime work stays deferred.

**Bounded recursive cut adaptation — 2026-10-29:** The existing local adaptation
owner now supports `PartitionOffset` with an explicit `CutId`. It tests declared
normal offsets in the current nearest-change order and shared work/attempt ledger.
Tree topology, angles, required content, minimum areas, protected anchors and
source dimensions remain fixed during this search. [Evidence](../../evidence/2026-10-29-cut-adaptation-verification.json) records four
complete seven-node/two-structure cases: positive, reversed-normal, signed-negative
and oblique movement. Each retains two rejected settings before accepting the
third; exact work boundaries, regional translation, infeasible/invalid targets,
range/budget/cancel refusal, clean/edit equality, rollback and checkpoint replay
pass. Four CLI exports and fourteen current Node checks pass, including existing
strategy regressions. The direct partition, strategy and joint-layout C#
regressions pass. One CLI rename access denial recurred; its isolated rerun passed,
with the cause still unconfirmed. S05/S08/S09/S10 remain IN PROGRESS. Cut trees and
permitted search ranges are authored reusable intent; arbitrary topology synthesis,
world-scale throughput and browser/design acceptance remain open. Godot deferred.


**Transactional domain regeneration — 2026-10-29:** Domain sessions now reuse
the existing private stage cache per domain and revalidate collection transitions
and journeys on every edit. The three-domain fixture reuses 47/47 local stages for
a journey-purpose edit; a floor geometry edit recomputes 14/16 stages on that floor
while the other domains reuse all stages. Clean generation equality, isolated
snapshots, stale/failed/cancelled edits, checkpoint tamper refusal and atomic
publication rollback are checked. CLI start/edit publishes a source-history
checkpoint; restore regenerates sources and verifies reports instead of trusting
serialized cache data. [Evidence](../../evidence/2026-10-29-domain-session-verification.json) records the passing runs and a Windows
rename access-denied failure followed by a passing isolated run using the same
binaries. That publication failure's cause remains unconfirmed. S08/S09/S10 remain
IN PROGRESS: maximum-size stress, world-scale synthesis and design acceptance are
open. Godot stays deferred.

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

**Joint layout qualification (2026-10-29):** One adaptation owner now coordinates
multiple declared scopes under one ledger and 64-combination cap. A complete
13-node/four-structure/two-route world needs two changes together; either alone
fails. Numeric/numeric and numeric/strategy cases, independent four-state/area/
interface/source/anchor checks, exact work/attempts, invalid scopes/Cartesian cap,
source-edit/rollback/checkpoint replay, CLI and sixteen Node checks have
[evidence](../../evidence/2026-10-29-joint-layout-verification.json). Shared route-
binding normalization fixes unsorted multi-interface feedback replay. Affected
single-owner/route/frame/strategy regressions pass. Stage remains IN PROGRESS;
maximum-scope/nested combinations, broad synthesis/scale and visual acceptance
are unqualified. Godot remains deferred.

**Regional relationship qualification (2026-10-29):** Optional inclusive
minimum/maximum Euclidean anchor distances constrain required regional roles in
the existing bounded assignment owner. Protected anchors, whole components,
profiles/minima and complete physical programs remain intact. Independent 36-case/
104-assignment oracles, three-role constraints, exact work/refusal, complete 17-node/
11-route integration, edits/rollback/checkpoints, CLI and six Node checks have
[evidence](../../evidence/2026-10-29-region-relations-verification.json). Current
geometry, physical-feedback and local-strategy regressions pass. The stage remains
IN PROGRESS; these relations do not create roads or certify traversal distance,
autonomous design synthesis, world-scale throughput or visual acceptance. Godot deferred.

**Declared strategy qualification (2026-10-29):** Baseline-first ordered grid/
weighted options now share the existing adaptation work/attempt limits and physical
owners. Source validation runs for all declarations before candidate-dependent
geometry. Both strategy directions retain complete seven-node programs, and a
17-node/11-route case exhausts local options before changing regions. Independent
area/interface/source-size/anchor oracles, exact budgets, malformed unused option,
source-edit/rollback/checkpoint replay, CLI and nine Node checks have
[evidence](../../evidence/2026-10-29-layout-strategies-verification.json). Current
fixed/dial/frame regressions pass. The stage remains IN PROGRESS; broad strategy
synthesis, world-scale performance and visual acceptance remain open. Godot deferred.

**Region-relative content qualification (2026-10-29):** Original local program
and grid offsets can follow the computed regional seed. Placement consumes explicit
allocation-anchor bindings; source geometry, counts and protected absolute inputs
remain intact. Four complete grid/weighted terrain cases, nested relative/fixed
scopes, two 17-node/11-route reselections, independent coordinate/area oracles,
rotation/width adaptation, source-edit/checkpoint replay and bounded/invalid refusal
have [evidence](../../evidence/2026-10-29-regional-frames-verification.json). Three
CLI packets and twelve Node checks pass with current fixed-layout regressions.
The stage remains IN PROGRESS; this is authored relative grammar realization,
not autonomous intent/strategy synthesis, world-scale or visual acceptance.
Godot stays deferred.

**Local orientation increment (2026-10-29):** A `GridRotation` adaptation dial
retains the complete source-sized program and protected anchors. Opposite searches,
independent geometry, physical interfaces, checkpoint restore/rollback, bounded
refusal, actual CLI and seven Node checks have
[evidence](../../evidence/2026-10-29-layout-rotation-verification.json). The stage
remains IN PROGRESS; broad strategy/scale/visual acceptance is not established.
Godot stays deferred.

**Joint decision replay, 2026-10-29:** Composition receipts now retain selected
regional plans alongside catalog mixes. Original recipes contain no generated
exclusions; ranked requests and all failure/exhaustion decisions rederive from
source history. Lost receipts cannot legitimize a later excluded assignment.
Changing to a region-only source equals clean generation; an all-infeasible edit
retains the accepted snapshot. Required chunks and source-history checkpoint replay
pass with both decision families. [Evidence](../../evidence/2026-10-29-region-feedback-verification.json).
Finer composition reuse and broad concurrency/scale remain open. The previous
atomic-move sharing cause is not claimed fixed. S08 IN PROGRESS.

**Region-intent replay and reuse, 2026-10-29:** Region inputs, component candidates,
clearance bounds and selections replay from the actual terrain/environment. Root
anchor bindings preserve source templates separately from computed points. Direct
staged terrain edits reuse catalog content while recomputing regions/allocation;
optional whole-composition execution reaches the same nodes/routes. Impossible
region edits retain the accepted snapshot, and checkpoint history replays the
terrain change. [Evidence](../../evidence/2026-10-29-region-intent-verification.json).
One concurrent regression export hit atomic-move access-denied, cleaned staging
and published nothing; its isolated serial rerun passes. Sharing cause and wider
concurrent-publication resilience remain unqualified. S08 IN PROGRESS.

**Transactional composition, 2026-10-29:** Physically qualified composition is one
session stage. Source changes invalidate it; an all-infeasible edit leaves the
accepted world intact. Full source-history checkpoints replay both the original
and accepted configuration. Saved source inputs are cloned and source/dependency
identities are checked before search replay; changed receipts or selected positions
refuse reads. Existing chunk ownership includes the composition receipt in its
source digest. [Evidence](../../evidence/2026-10-29-program-composition-verification.json).
Finer composition-stage reuse and large-world scale remain open. S08 IN PROGRESS.

**Intent-selected content replay, 2026-10-29:** Program-mix demand parameters
invalidate content and physical allocation through the existing session owner.
A larger demand selects complete larger programs; an impossible demand leaves
the accepted snapshot intact. File reads recompute the optimization and member
witnesses; source-history checkpoint restoration matches the accepted snapshot.
[Evidence](../../evidence/2026-10-29-program-mix-verification.json).
This does not finish streaming or world-scale regeneration. S08 IN PROGRESS.

**Terrain-demand content invalidation, 2026-10-29:** The existing cache now records
`population-source → terrain-demand → content` before physical allocation. A
protected-strip terrain edit changes four derived source-sized members to three,
equals clean generation and invalidates content explicitly. A failed catalog
minimum edit preserves accepted state; checkpoint source history and saved demand/
member markers replay. [Evidence](../../evidence/2026-10-29-terrain-demand-verification.json).
This is quantity-aware whole-stage invalidation, not fine-grained streaming or
automatic saved-game migration. S08 stays IN PROGRESS.

**Larger prescribed worlds, 2026-10-28:** A 223-node/222-physical-member program
retains complete source content, nineteen source paths and thirteen contour
witnesses across terrain edits. Incremental output equals clean generation;
an oversized required-source edit preserves accepted state, and checkpoint
history replays. [Evidence](../../evidence/2026-10-28-prescribed-population-verification.json).
This is bounded whole-stage regeneration, not finer invalidation, world streaming
or complete S08 acceptance. Godot remains deferred.

Plan date: 2026-10-26. This document specifies future work; it is not an implementation-completion claim. Status and evidence are owned by the [master stage tracker](../../ROADMAP.md#master-stage-tracker). Requirements, decisions and shared delivery rules are in the [master plan](../../ADVANCED_WORLD_GENERATION_MASTER_PLAN.md#current-staged-plan).

Dependencies: [S01](s01-shared-world-contracts.md), [S02](s02-terrain-and-spatial-domains.md), [S04](s04-terrain-aware-connections.md), [S06](s06-nested-content-and-surroundings.md), [S07](s07-environment-and-gameplay-population.md)

Requirements: W 01, W 07, W 08, W 10, W 12. Responsible executor: Codex, including explicitly labeled self-review. Accountable product/design owner: Fahad. No external or independent reviewer is assigned or claimed.

## Purpose and current gap

Reusable world metadata and optional spatial domains require stable identities,
cross-boundary consistency and preservation of developer edits. Current supported
protocols meet the technical gate recorded above. Generation regions and storage
chunks remain separate; native runtime streaming is a downstream capability.

## Algorithm and design

Choose stable identities from semantic ownership and authored keys, with explicitly versioned derivation. Separate generation regions from storage/streaming chunks. A road, river or compound can cross multiple chunks while retaining one authoritative identity. Define boundary contracts before parallel or incremental generation: shared sampling, interfaces, ownership and dependency digests. Preserve authored overrides and diagnose conflicts after regeneration. Use named random streams only where an algorithm actually uses controlled variation; generation order must not affect unrelated results. Save versioned inputs, overrides, outputs, algorithm/build identities and validation reports through the current atomic publication boundary. No compatibility loader is retained for retired contracts; explicit one-time conversion, if needed, is separately scoped.


## Inputs and outputs

**Inputs:** Shared world contracts, dependency graph, qualified algorithm versions, chunk/boundary policy, explicit overrides and resource budgets.

**Outputs:** Deterministic world snapshots, chunk manifests, cross-boundary references, edit/invalidation records and atomic save/reload verification.

## Code and workflow owners

Extend existing package/export verification and operational services rather than adding another persistence framework. Locate current deterministic identity and serialization owners during S01. Keep WorldCreator independent of the consuming game runtime and leave runtime streaming to a qualified downstream adapter.

Named existing owners are inspection targets, not a commitment to keep every current API. Proposed boundaries become concrete in S01. Replace superseded behavior at its current owner, migrate all callers and remove the retired implementation; no compatibility aliases or parallel legacy path. Preserve unrelated changes and immutable historical evidence.

## Ordered implementation tasks

| Task | Work | Acceptance evidence |
| --- | --- | --- |
| S08.1 | Specify identity, numeric tolerance, build/version and reproducibility guarantees. | Bitwise equality is promised only for the declared environment; semantic comparisons specify their tolerances. |
| S08.2 | Implement chunk ownership and stable shared boundaries. | A route or structure crossing a seam has one identity and no duplicated physical member. |
| S08.3 | Implement dependency-based invalidation and edit conflict reporting. | Changing a terrain patch regenerates affected dependents while preserving unrelated authored content. |
| S08.4 | Persist snapshots, overrides and provenance using the current publication owner. | Failure/cancellation leaves the previously valid snapshot intact. |
| S08.5 | Exercise generation in different request/load orders with bounded memory. | Order does not alter qualified geometry or mandatory content. |
| S08.6 | Measure representative world sizes and record supported limits. | The report includes environment, actual peak memory/time, rejected sizes and remaining limitations. |

## Failure handling and operations

Validate inputs and required capabilities before expensive work. Apply explicit cancellation and finite work/resource budgets at stage boundaries and inside iterative work. Return attributed unsatisfied constraints instead of silent fallback, missing required content or partial success. Preserve prior accepted output on failure. Use the existing Log/Audit/Notification owners for applicable generation, export and review boundaries; UI consumers display actionable failures and cancellation. Record source/input identities, stage outcome and unresolved dependencies. Retry only after the cause or inputs change, with the same declared reproducibility guarantees.

Recovery is a coherent correction of the current owner and affected inputs, followed by focused checks. Do not restore a retired implementation as a runtime fallback. Artifact approval gates apply only to the relevant visual stages and exact revisions.

## Verification and acceptance cases

### Spatial ownership index — bounded qualification, 2026-10-27

[Executed evidence](../../evidence/2026-10-27-world-chunks-verification.json)
qualifies optional indexing through generation, replay, session edits and the
atomic publisher. The main packet retains 169 nodes and 14 directed paths as
183 unique members, with 307 cell references across 52 occupied cells. An
independent Python oracle checks ten saved cases: 785 source polygons, 66
directed paths, seven entrance approaches and reversible owner coordinates.
All 168 physical members in the browser match saved polygons exactly. Exact
seams, negative origins, rotation, reservations, topology-only sharing, load
order, stale plans, budgets and rollback are covered. A storage-size edit reuses
the fourteen existing stages and recomputes only the optional chunks stage.

The initial test incorrectly expected eight paths in the access fixture; its
source declares two bidirectional backbone edges plus one bidirectional authored
connection, giving six. The corrected check verifies those obligations and every
path ID. Earlier test errors and CLI failures are retained: concurrent CLI calls
contended on the shared operation log, and a subsequent resized publication had
a transient staging-folder access failure. Terminal runs and absent output were
confirmed before retry; sequential final CLI exports passed, while the expected
budget refusal left no target. Cross-process log concurrency is not qualified.

S08 remains IN PROGRESS. This does not establish independent chunk generation,
terrain paging, water/volume seams, native runtime streaming or broad solid-world
scale. The index covers metadata PlacementPlan geometry, site features, access
approaches and realized route/network paths; other world plans remain shared.

Add optional chunk indexing to the existing metadata generation/session/export
boundary. Preserve global geometry and stable node/path IDs. Each positioned node
and realised directed travel path has one canonical owning chunk and references
from all chunks touched by its full footprint/reservation/site features or swept
passage envelope. A chunk-local origin is a reversible coordinate frame, never a
placement instruction. Unpositioned metadata and terrain/regions/context remain
shared package dependencies, explicitly bound by the complete source-world hash.
This is an ownership index, not independent chunk generation or runtime streaming.

Use half-open cells for canonical point ownership and closed geometric overlap
for physical references, including negative coordinates and exact seam contacts.
No duplicated object assembly, clipped/shrunk footprints, omitted passage width
or inferred topology at crossings. Qualify a complete connected multi-stage world,
seam-spanning source polygons and routes, changed chunk size without geometry
changes, deterministic declaration/load order, stale/tampered output, finite work,
cancellation, session rollback, saved replay and atomic CLI publication.

### Whole-program scale qualification — bounded verification 2026-10-27

Exercise one connected exploration program at increasing sizes, with nested
walkable areas, activity sites and interaction pads. Preserve every catalog
polygon, parent and required count; replay saved output and independently check
containment, sibling separation and destination connectivity. Measure wall time,
managed allocations and process peak working set, explicitly distinguishing these
metrics. Authored anchors isolate hierarchy scaling; they do not qualify automatic
terrain adaptation, solid-building routing, expressive range or streaming.

The original 32-child-stage ceiling rejected a 169-node program before search.
Increasing it exposed a stack overflow at 493 nodes. The shared local solver now
keeps depth-first search frames on the heap and rejects impossible fixed-anchor
candidates before polygon queries. The current limits are 128 child stages and
1, 024 classified physical sites; local demands, aggregate work, solid exclusions
and total entrance budgets remain bounded. Explicitly allocate at least 129 stage
attempts for 128 child stages, since the root also counts.

[Executed verification](../../evidence/2026-10-27-world-scale-verification.json)
covers 85, 169, 493 and 577 complete nodes and saved replay. Independent checks
cover 1, 320 exact polygons, 1, 280 parent containments, 2, 500 sibling pairs, 72
directed routes and all 576 physical polygons in the largest browser packet.
Crowded anchors, oversized required content, 144 stages, small budgets and
cancellation reject. The actual CLI budget refusal publishes nothing. Local
generation/replay times and cumulative process peak memory are recorded, not
general performance promises. Chunks, seams, broad content/terrain scaling and
solid-building scale remain open; no whole-core or GameArts/Godot completion claim.

### Versioned selected dependencies

**Bounded qualification, 2026-10-27:** the same session now records selected layout
and registered policy configuration digests, measured code identities and actual
source definitions. Composed policies include imported versions. An explicit
generator replacement invalidates content and dependents. Before/after generation
and pre-publication checks reject observed drift. Selected unversioned extensions
fail; unused extensions do not block a session.

Schema-2 replay requires exact versions, optionally supplied by an application
resolver of already-installed implementations. Missing, wrong or null resolutions
fail. No fallback, saved-code loading or downloading is introduced. Fingerprints
are a trusted contract, not proof against hidden mutable state.

[Evidence](../../evidence/2026-10-27-world-dependencies-verification.json) covers
configuration/content updates, full-generation equality, transitive composition,
mid-run/publication refusal, rollback, actual registered CLI and exact JavaScript
output. S08 remains IN PROGRESS for chunks/seams, finer invalidation and scale.
Next is S07 purposeful population. The built-in-only paragraph below records the
earlier qualification, not the current external dependency limit.

### Executable layer invalidation gate

**Active implementation decision, 2026-10-27:** use an internal execution cache in
the existing `WorldMetadataGenerator`, with explicit stage dependencies and
input/output digests. Semantic `MetadataLayerPlanner` declarations remain a
different concern. Cache only accepted private session state, clone mutable
values at boundaries, and commit a proposed edit only after all dependent stages
succeed. Protect explicitly selected recipe/catalog JSON pointers; record exact
changes, stage execution/reuse and failures. Save source/edit history through the
existing atomic packet publisher; reload by replaying trusted engine operations,
never by trusting caller-supplied cached geometry. The first scheduling granularity
is whole computational stages, not spatial chunks. Registered policy/layout
implementations need version identities before incremental reuse is supported.
Verify terrain edits and independent presentation edits against full generation,
protected-input conflicts, cancellation, failed publication, reload and source
mutation isolation. Chunk seams and S08's wider exit gate remain separate.

Status: **BOUNDED BUILT-IN CASE QUALIFIED; STAGE IN PROGRESS**. [Execution evidence](../../evidence/2026-10-27-world-regeneration-verification.json) covers the fourteen-stage shared generator, full/incremental equality for complete physical compounds and a distinct topology-only journey, exact protected-input conflicts, attributed terrain failure, cancellation after terrain recomputation and during real file staging, existing-target failure, input/output mutation isolation, accepted-state preservation, checkpoint replay/tampering and actual CLI audit/nonpublication. The three generated CLI packets also pass exact JavaScript geometry/dependency checks. `MetadataLayerPlanner` still owns semantic dependencies; `MetadataExecutionCache` and `MetadataGenerationSession` own execution/transactions inside the existing generator. Registered external policies/custom layouts are explicitly refused until their implementation identities are versioned. Whole-stage reuse is not finer spatial invalidation; reload rebuilds caches by replaying source history. Chunk ownership, seams, representative scale and the wider exit gate remain open.

- Start from one accepted snapshot with saved input/output digests, algorithm identities and an authored override. Change one terrain operation through an explicit edit record.
- Calculate the affected dependency closure. Invalidate every dependent field, region, route and allocation actually consuming the changed output; preserve unrelated authored definitions and the override.
- Compare incremental output against full regeneration of the same edited inputs, within declared numeric guarantees. Merely changing timestamps or colors does not pass.
- Test an override that conflicts with the new terrain. Return an attributed conflict; do not silently move the protected object, retain a stale route or remove required parts.
- Interrupt during recomputation and publication. The previous accepted snapshot must remain usable; partially regenerated output must not masquerade as accepted. Reload and replay the edit record.

Use the existing generation/publication owners and a single scheduling owner selected during implementation. Chunking remains separately qualified; neither ProceduralToolkit's terrain example nor Bonsai's advertised size supplies local seam or performance evidence.

- Test seam-crossing routes, water, compounds and layered passages; compare shared interfaces independently.
- Test repeated seeds, different effective variation seeds, changed algorithm versions, input reorder and request-order permutations.
- Test conflicting edits, removed parents, missing chunks, corrupt manifests, interrupted output and cancellation.
- Compare bounded whole-world generation with supported chunk generation; three correct chunks are not evidence of an infinite world.

Run relevant checks with commands, environment, input/source digests and observed outcomes recorded. NOT RUN is not PASS. Existing tests are baseline evidence only. Keep implementation, local technical qualification, browser/native review, owner acceptance and unavailable CI/release gates distinct.

## Risks and boundaries

Cross-platform floating point and source ordering may affect reproducibility. Do not invent performance targets from unmeasured hardware or add distributed infrastructure without a consumer.

## Exit gate

The current [example](../../../examples/metadata/regeneration/README.md) provides
real `metadata-session start` and `metadata-session edit` commands through the
existing Log/Audit/Notification boundary. The same atomic publisher handles full
generation and accepted session packets. This is a coherent extension of that
owner, without a legacy compatibility loader. The initial qualification used
.NET 10.0.402/Windows; current build/runtime evidence is recorded above.
No cross-platform floating-point or universal performance threshold is claimed.
The initial qualification's first two test attempts
had test-source compilation errors, retained in the evidence; corrected builds
and all listed final checks passed.

S08 closes when qualified worlds preserve identities and edits across save/reload and supported generation orders, seams are verified, and measured resource limits are documented.

When the exit gate is evidenced, update the existing master tracker with actual evidence and the next dependency-ready task. Do not close this stage merely because these instructions or task descriptions exist.

## Owner-supplied implementation references — 2026-10-26

R 17/R 18 editable layers and R 21 staged volume editing motivate reproducible operation history and bounded dependency invalidation. R 19 motivates source identity and scale conversion in adapters. None establishes current chunk/streaming performance or native runtime acceptance. See [R 16–R 22 and the implementation priority](../../WORLD_GENERATION_RESEARCH.md#owner-supplied-implementation-references--2026-10-26). This records a design decision, not integration or runtime evidence.

## Research references

[R 08](../../WORLD_GENERATION_RESEARCH.md#r 08). These references motivate the proposed design; they do not certify this implementation or imply adoption of external code. The research record distinguishes accessed full text from abstracts and records relevant limits.

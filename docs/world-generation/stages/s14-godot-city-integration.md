# S14 — Native Godot integration and whole-city acceptance

Plan date: 2026-10-26. This document specifies future work; it is not an implementation-completion claim. Status and evidence are owned by the [master stage tracker](../../ROADMAP.md#master-stage-tracker). Requirements, decisions and shared delivery rules are in the [master plan](../../ADVANCED_WORLD_GENERATION_MASTER_PLAN.md#current-staged-plan).

Dependencies: [S08](s08-deterministic-chunks-and-persistence.md), [S09](s09-layered-schematic-and-export.md), [S10](s10-validation-diversity-and-core-gate.md), [S12](s12-mockup-to-world-map.md), [S13](s13-production-art-and-atlases.md)

Requirements: W03, W06, W07, W10, W11, W12, W13. Responsible executor: Codex, including explicitly labeled self-review. Accountable product/design owner: Fahad. No external or independent reviewer is assigned or claimed.

## Purpose and current gap

Current [animation-slice qualification](../../evidence/2026-10-27-component-animation-verification.json): consume approved canonical component bundles,
all frame masters, parent/mount hierarchy, facing draw order and the complete
sampled timeline through native AnimationPlayer. Require endpoint poses and every
duration-weighted frame transition; never silently omit a frame or invent an
unsampled pose. Explicit baked/discrete playback preserves source samples and
does not certify continuous authored curves between them. Native matrices, frame
rectangles, pivots, hierarchy, save/reload and live clock playback must be checked.
Production member integration is implemented in the same binding/export route.
Two facing variants, all nested parts, native clock/autoplay/loop, source pixels
and combined movement pass technical fixtures. Real artwork/full-flow acceptance
remains required. The source snapshot is stored losslessly through scene reload;
native transform precision is verified separately. Initial failed attempts and
premature inspections remain recorded rather than being treated as passes.

Current [movement-slice qualification](../../evidence/2026-10-27-native-movement-verification.json): compile source domain perimeter, declared solid
footprints and exclusions into native physics polygons; bake a separate native
navigation map for each explicit actor radius. Verify every declared journey by
native path queries and continuous circle sweeps, then save/reload and repeat in
a fresh process. Reject invalid sources/profiles and unreachable native journeys
without scene/receipt publication. This is exterior-domain movement only, not
door/interior/animation or complete-game acceptance.

Two actor radii and two journeys per radius are exercised. Actual source
collisions, continuous sweeps, endpoint rounding/connector checks, isolated native
maps, save/reload and fresh-process verification are recorded. Failed initial
precision/lock-test attempts remain evidence; the lock test now acquires its real
handle before invoking the production publication owner. This closes that narrow
native exterior-movement slice, not the full stage.

The [canonical CLI continuation](../../evidence/2026-10-27-canonical-cli-verification.json)
now reaches installed native compilation and fresh-process inspection from actual
canonical synthetic records, without test authority implementations. Ten targets,
six members and 42 terrain bindings survive; real artwork/gameplay acceptance is
still open. Host configuration does not configure a game or approve artwork.

The [canonical lifecycle bridge](../../evidence/2026-10-27-lifecycle-verification.json)
now supplies real read-only registry/authority calls for native frame consumption,
qualified with synthetic records. Schema 2 separates stable lifecycle/native
identities. Actual artwork execution and whole-world acceptance remain open.

Current [static native qualification](../../evidence/2026-10-27-native-static-verification.json):
six retained identities, three TileMapLayers, two native static sprite instances
and 42 explicit pattern-frame bindings. PNG decoding, nonzero atlas regions,
terrain joins, ground/meter transforms, native save/reload, fresh-process pixel/
ordering inspection and main-scene loading pass. External source/approval gates,
native refusal and Windows publication recovery are exercised. One native
compiler replaces the separate probe implementation. Fixture images and test
authorities are not actual approved city art. Collision/navigation, animation,
site contents, full-scale city fidelity, canonical execution and owner acceptance
remain open; no stage exit is claimed.

The existing structure-layer exporter and scene skeleton history do not demonstrate the requested complete playable city. Native Godot integration must consume the qualified map and production assets with actual installed-runtime evidence.

The final-map static binding packet was technically qualified at the S13
boundary ([evidence](../../evidence/2026-10-27-metadata-production-verification.json)).
It retains exact geometry and hash-pinned measured frames; it is not a production
TileSet or TileMapLayer compilation. The [current native-demand evidence](../../evidence/2026-10-27-tile-topology-verification.json)
now qualifies half-meter masks, explicit material grouping, installed Godot 4.7.2
terrain matching and PackedScene save/reload. Every selected side/corner, occupied
cell and meter transform is checked; missing-pattern/changed-input runs reject.
The 20/13/9 pattern counts came from transparent technical sources. Static frame
compilation is now qualified above; actual module artwork, exact collision/
navigation, animation and visual acceptance remain unfinished. No full-city gate
is closed by these technical probes or static test scenes.

## Algorithm and design

Implemented static-slice acceptance: revalidate the complete scene, measured-art assembly and
presentation plan; require every material group and every declared terrain
pattern to bind explicit verified frames shared by its channels. Copy exact image
bytes, retain provenance and produce native TileMapLayers plus every static
structure at its measured pivot/scale. Godot must decode source PNGs, reject
missing patterns or altered files, verify all cell joins and ground anchors, and
save/reload the complete static scene. Existing output and prior accepted scenes
must survive refusal. One native compiler owns both demand measurement and static
production compilation; no duplicate terrain solver or runtime fallback. This
still leaves animation, collision/navigation and actual art/city acceptance open.

Current slice acceptance: derive bounded presentation-cell masks from the shared
surface query and exact passage/site/access geometry at explicit meter resolution
and origin. Declare the center-classification error band; reject insufficient
resolution, missing target cells or disconnected passage masks rather than filling
gaps. Entrance approach strips must be explicit production demands. Use installed
Godot terrain APIs to measure required combinations, verify exact occupied cells
and native save/reload, and retain source digests. Technical probe textures are
temporary algorithm data, never production artwork or a city acceptance claim.
The native probe must fail on missing terrain combinations or altered occupancy.
Production asset integration, exact collision/navigation and actual art/visual
acceptance remain separate required tasks. This presentation grid does not alter
continuous world geometry or impose a street network.

Use Godot native TileMapLayer nodes and TileSets for appropriate terrain/surface/detail layers, with native collision, navigation, terrain connections and occlusion where applicable. Use native Sprite2D/scene instances for oversized or independently interactive structures and native AnimationPlayer/AnimationTree for moving parts as appropriate. Tile storage does not impose a square road layout: rasterize approved irregular geometry within declared tolerance and preserve route connectivity. Keep world-to-pixel/tile transforms explicit and honor installed-version coordinate limits through chunk-local origins when needed. Reuse native navigation services; validate movement for the declared agent profiles. Integrate the full city, including all districts and compound surroundings, then compare approved design and actual runtime.


## Inputs and outputs

**Inputs:** Qualified world package, full approved art/atlas packet, actual Godot version and target game configuration, agent profiles and runtime acceptance journeys.

**Outputs:** Whole-city native Godot project/scene, verified tile/structure bindings, movement/collision/animation evidence, visual comparison and final scope/remaining-gates report.

## Code and workflow owners

WorldCreator.Godot adapter and the target game project own compilation/runtime integration. Extend GodotStructureLayerExporter where appropriate; do not create custom replacements for native engine services. Canonical art manifests remain authoritative for assets.

Named existing owners are inspection targets, not a commitment to keep every current API. Proposed boundaries become concrete in S01. Replace superseded behavior at its current owner, migrate all callers and remove the retired implementation; no compatibility aliases or parallel legacy path. Preserve unrelated changes and immutable historical evidence.

## Ordered implementation tasks

| Task | Work | Acceptance evidence |
| --- | --- | --- |
| S14.1 | Verify installed Godot APIs and map-to-native coordinate/layer contracts. | Use actual supported version features, signed tile-coordinate bounds and explicit transforms. |
| S14.2 | Compile terrain, routes, grounds and detail into native TileMapLayers/TileSets. | Irregular geometry and district interfaces preserve their qualified shape and access. |
| S14.3 | Instantiate structures and moving components with native scene/animation support. | All required instances and bindings survive reload with correct pivots and scale. |
| S14.4 | Configure native collision, navigation and required interaction boundaries. | Visible routes and entrances are actually traversable by declared agents. |
| S14.5 | Run the complete city in the installed engine and exercise representative journeys. | Cross-district routes, compounds, boundaries, chunk transitions and animation work in runtime. |
| S14.6 | Compare runtime against approved district and city designs and reconcile final scope. | Owner visual/gameplay acceptance, local technical tests and any unavailable release gates are recorded separately. |

## Failure handling and operations

Validate inputs and required capabilities before expensive work. Apply explicit cancellation and finite work/resource budgets at stage boundaries and inside iterative work. Return attributed unsatisfied constraints instead of silent fallback, missing required content or partial success. Preserve prior accepted output on failure. Use the existing Log/Audit/Notification owners for applicable generation, export and review boundaries; UI consumers display actionable failures and cancellation. Record source/input identities, stage outcome and unresolved dependencies. Retry only after the cause or inputs change, with the same declared reproducibility guarantees.

Recovery is a coherent correction of the current owner and affected inputs, followed by focused checks. Do not restore a retired implementation as a runtime fallback. Artifact approval gates apply only to the relevant visual stages and exact revisions.

## Verification and acceptance cases

- Check native load/reload, asset import, missing references, tile seams and coordinate bounds.
- Traverse every required connected region and test narrow entrances, different levels, blocked areas and navigation clearance.
- Inspect district and whole-city views against approved maps, including count/identity reconciliation and animated components.
- Record measured runtime performance on actual hardware; C# compilation or a scene file alone is not native acceptance.

Run relevant checks with commands, environment, input/source digests and observed outcomes recorded. NOT RUN is not PASS. Existing tests are baseline evidence only. Keep implementation, local technical qualification, browser/native review, owner acceptance and unavailable CI/release gates distinct.

## Risks and boundaries

Navigation data is independent of visible tiles and must be validated. City acceptance qualifies the demonstration, not every supported genre. Cloud Actions remain unavailable and no publication is authorized by this plan.

## Exit gate

S14 closes when the entire declared city runs correctly in native Godot with complete approved art and recorded owner design acceptance. Missing hardware, assets, approval or runtime evidence stays open; no scene-skeleton completion claim.

When the exit gate is evidenced, update the existing master tracker with actual evidence and the next dependency-ready task. Do not close this stage merely because these instructions or task descriptions exist.

## Owner-supplied implementation references — 2026-10-26

Source refresh 2026-10-27: R22's current README lists Godot 4.4+ for `main` and marks intersection lane/edge curves forthcoming. Before any optional 3D adapter adoption, pin a compatible plugin revision, check native engine support, and exercise lane transitions through every required junction in Godot. A rendered intersection is insufficient. These checks are NOT RUN; the 2D delivery retains its native TileMapLayer path. See the [source-detail review](../../WORLD_GENERATION_RESEARCH.md#algorithm-selection-and-adoption-checks--2026-10-27).

R22 is a 3D road mesh/lane plugin candidate, not a substitute for the owner-requested 2D TileMapLayer route. Continue using Godot-native TileMapLayer/TileSet/navigation facilities. Any optional 3D consumer needs its own installed-version and runtime evidence; R19 voxel conversion and R21 custom rendering are not mandatory dependencies. See [R16–R22 and the implementation priority](../../WORLD_GENERATION_RESEARCH.md#owner-supplied-implementation-references--2026-10-26). This records a design decision, not integration or runtime evidence.

## Research references

[R12](../../WORLD_GENERATION_RESEARCH.md#r12), [R13](../../WORLD_GENERATION_RESEARCH.md#r13), [R14](../../WORLD_GENERATION_RESEARCH.md#r14), [R15](../../WORLD_GENERATION_RESEARCH.md#r15). These references motivate the proposed design; they do not certify this implementation or imply adoption of external code. The research record distinguishes accessed full text from abstracts and records relevant limits.

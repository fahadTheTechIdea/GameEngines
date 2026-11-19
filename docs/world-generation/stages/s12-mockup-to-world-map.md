# S12 — Faithful mockup capture and complete world-map compilation

Plan date: 2026-10-26. This document specifies future work; it is not an implementation-completion claim. Status and evidence are owned by the [master stage tracker](../../ROADMAP.md#master-stage-tracker). Requirements, decisions and shared delivery rules are in the [master plan](../../ADVANCED_WORLD_GENERATION_MASTER_PLAN.md#current-staged-plan).

Dependencies: [S11](s11-gamearts-mockups.md)

Requirements: W02, W04, W05, W06, W10, W11, W12. Responsible executor: Codex, including explicitly labeled self-review. Accountable product/design owner: Fahad. No external or independent reviewer is assigned or claimed.

## Purpose and current gap

Earlier extraction missed required structures and details. A visually similar approximation does not satisfy the request to obtain the actual map from the approved mockups. The owner explicitly requires stopping this capture pipeline if vision cannot reliably identify the structures.

## Algorithm and design

Inspect the actual approved images at usable scales and reconcile every required instance with the catalog manifest. Record source pixel evidence, ground versus roof interpretation, occlusion, uncertainty and calibration. Use explicit reviewed anchors and approved compositional grammars for repeated detail; do not infer hidden exact geometry from ambiguous pixels. Compile traced design into the shared world contracts and compare source overlays at district and whole-city scale. Exactness is bounded by declared capture/calibration tolerance and evidence. If required structures remain unidentifiable, stop dependent map/art compilation and report which sources or instances prevent completion; never substitute arbitrary placements or label a partial inventory complete.


## Inputs and outputs

**Inputs:** Approved mockups and content manifest, calibration, catalog/source variants, reviewed layer/region definitions and declared capture tolerances.

**Outputs:** Complete reconciled map/capture package, per-instance source evidence, uncertainty/missing list, overlay comparison and compiled shared-world geometry.

## Code and workflow owners

### Current slice: explicit terrain and route trace compilation

Add prescribed route polylines to the shared route owner. Preserve every point and
reverse traversal exactly, and use the existing full-width terrain/access and
junction checks with bounded work; blocked, wrong-endpoint or incompatible traces
must reject without route search fallback. Bind calibrated scene layers to all
declared terrain boundaries/patches/exclusions and configured explicit route links,
preserving source attributes and graph junctions. Logical junction nodes remain
metadata with an explicit non-art role; they cannot hide physical source content.

Acceptance: complete declared scene accounting, source/calibration provenance,
exact traced geometry and width, shared endpoint/junction topology, unchanged
physical poses/source dimensions, ordinary generator replay, negative cases,
atomic CLI export, source overlays and exact JavaScript rendering. Unsupported
network/interface/reuse trace modes must reject explicitly. Actual vision and
approved full-city artwork remain separate unverified gates.

[Technical verification](../../evidence/2026-10-27-scene-trace-verification.json)
now covers six unchanged members (two physical structures and three logical
junctions), four exact surface elements and four forward/reverse prescribed paths.
Unequal-scale camera widths and split-view topology pass. Blocked traces reject
even when ordinary search can detour. Normal engine/persistence/CLI validation,
source overlays and six exact rendered corridor segments pass. These synthetic
observations establish compiler behavior; S12's actual image and full-scope visual
exit gate remains open.

### Earlier slice: assembly from multiple calibrated views

Replace the single-image ground request with one current version-2 view collection.
Each view names an exact handoff detail/overview job, reconciled identities and a
calibrated ground layer from its own verified image revision. Require complete
per-job accounting and complete combined physical coverage. Every physical instance
has an explicitly authored owning view; that view fixes its origin and rotation.
All other observations of the instance must agree within the same bounded source
geometry/uncertainty tolerance. Never average views, duplicate repeated structures,
silently select a view or fill a missing district from an overview assumption.

Acceptance: detail-only assembly plus detail/overview cross-checks, per-image affine
calibration and provenance, deterministic declaration order, missing scope/owner,
conflicting origin/orientation/shape and stale-source refusal, shared aggregate work,
one ordinary generated world, per-view source overlays, atomic publication, persisted
replay, actual CLI and exact browser geometry. Migrate active callers and tests;
do not retain a version-1 execution fallback. Historical evidence stays historical.
Actual generated mockups, source interpretation and approval remain unverified.

[Current verification](../../evidence/2026-10-27-metadata-views-verification.json)
qualifies two synthetic programs with six source images, 14 retained metadata
members, 12 unique physical instances and 24 retained observations. Complete
detail-only assembly and detail/overview cross-checking preserve selected poses;
small discrepancies are measured, conflicting geometry/poses and missing scopes
reject. Persisted replay, order invariance, safe view filenames, actual CLI exports,
per-image overlays and ten exact browser polygons pass. Source pixels in these
fixtures test provenance and calibration contracts, not real structure recognition.
Two Windows directory-publication errors rolled back and succeeded on one retry
each; failed attempts remain in the evidence. Full source fidelity is still open.

### Earlier slice: calibrated ground layout through metadata placement

Use the qualified metadata handoff and reconciled identity capture plus a separate
ground-footprint annotation layer of the same exact image revision. Every physical
allocation, including containing compounds, must have an accepted ground polygon,
explicit source-local origin and orientation. Retain source dimensions, reservations,
site features, hierarchy and existing placement/terrain/access constraints. Add
zero-distance capture pins to the shared placement definition; regenerate through
`WorldMetadataGenerator`, never write renderer coordinates or synthesize a city.
Conflicting existing authored constraints must reject without relaxation.

Acceptance: full instance accounting; exact source/hash/calibration checks; a bounded
continuous boundary correspondence within declared tolerance and uncertainty;
cyclic/reversed/subdivided contours; translated/rotated and nested source geometry;
no roof substitution, resizing, omission or relocated anchor; complete engine replay,
atomic export, actual CLI, saved JavaScript geometry and source-overlay inspection.
This slice compiles declared footprints only. Source terrain/routes are retained and
revalidated, not newly inferred or claimed traced from the image. Actual vision,
approval, full route/terrain capture and S12 closure remain separate requirements.
The implemented compiler and export are now [technically qualified](../../evidence/2026-10-27-metadata-ground-verification.json)
on synthetic nested/registered programs: all 14 nodes and 12 physical footprints
remain, exact translated/rotated origins survive ordinary generation/replay, source
dimensions are retained, affine uncertainty is bounded, incompatible geometry and
placement publish nothing, and ten browser polygons match saved engine geometry.
The source overlay contains verified source pixels and compiled outlines. These
checks do not establish actual vision or close the full-map exit gate.

Extend current capture, inspection, MockupWorldDesign, terrain-query and compiler owners after their S01/S02 consolidation. Reuse the actual review UI and source-crop evidence. The C# engine remains the compiler; agent vision authors traceable observations.

Named existing owners are inspection targets, not a commitment to keep every current API. Proposed boundaries become concrete in S01. Replace superseded behavior at its current owner, migrate all callers and remove the retired implementation; no compatibility aliases or parallel legacy path. Preserve unrelated changes and immutable historical evidence.

## Ordered implementation tasks

| Task | Work | Acceptance evidence |
| --- | --- | --- |
| S12.1 | Establish image-to-world calibration and distinguish depicted roofs from physical footprints. | Dimensions are measured or explicitly authored, never invented as extracted facts. |
| S12.2 | Inspect every district and reconcile required structure instances and nested members. | Every required instance has a source anchor or an explicit unresolved entry. |
| S12.3 | Trace terrain, routes, boundaries, grounds and supported repeated-detail grammars. | Source ownership and exclusions survive compilation. |
| S12.4 | Stop dependent capture work when required structures cannot be identified reliably. | The failure report names missing instances and evidence; no vacant-space fallback or claimed exact map is produced. |
| S12.5 | Compile qualified observations through the current engine and compare overlays. | Physical and hierarchy constraints hold without silently relocating source anchors. |
| S12.6 | Review complete district and whole-city reconciliation before production art. | Coverage includes the declared city, not just an attractive reviewed region. |

## Failure handling and operations

Validate inputs and required capabilities before expensive work. Apply explicit cancellation and finite work/resource budgets at stage boundaries and inside iterative work. Return attributed unsatisfied constraints instead of silent fallback, missing required content or partial success. Preserve prior accepted output on failure. Use the existing Log/Audit/Notification owners for applicable generation, export and review boundaries; UI consumers display actionable failures and cancellation. Record source/input identities, stage outcome and unresolved dependencies. Retry only after the cause or inputs change, with the same declared reproducibility guarantees.

Recovery is a coherent correction of the current owner and affected inputs, followed by focused checks. Do not restore a retired implementation as a runtime fallback. Artifact approval gates apply only to the relevant visual stages and exact revisions.

## Verification and acceptance cases

- Compare required, observed, compiled and unresolved counts by district/type/parent, including duplicates and occlusions.
- Check calibration transforms against independent control points and report residual error.
- Validate footprint/route/terrain overlays, boundaries, crossings, access and source provenance.
- Keep the prior 33-versus-43 structure failure as a negative example; passing schema validation cannot resolve visual omissions.

Run relevant checks with commands, environment, input/source digests and observed outcomes recorded. NOT RUN is not PASS. Existing tests are baseline evidence only. Keep implementation, local technical qualification, browser/native review, owner acceptance and unavailable CI/release gates distinct.

## Risks and boundaries

A single raster image may not contain enough information for exact ground geometry. Additional approved views or authored clarification may be necessary; absent evidence is a blocking capture result, not permission to invent.

## Exit gate

S12 closes only when required map content is reconciled, geometric/capture tolerances pass and the faithful full-scope map is reviewed. Unresolved required structures block this stage.

When the exit gate is evidenced, update the existing master tracker with actual evidence and the next dependency-ready task. Do not close this stage merely because these instructions or task descriptions exist.

## Research references

[R15](../../WORLD_GENERATION_RESEARCH.md#r15). These references motivate the proposed design; they do not certify this implementation or imply adoption of external code. The research record distinguishes accessed full text from abstracts and records relevant limits.

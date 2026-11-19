# S11 — Building references, district mockups and whole-city design

Plan date: 2026-10-26. This document specifies future work; it is not an implementation-completion claim. Status and evidence are owned by the [master stage tracker](../../ROADMAP.md#master-stage-tracker). Requirements, decisions and shared delivery rules are in the [master plan](../../ADVANCED_WORLD_GENERATION_MASTER_PLAN.md#current-staged-plan).

Dependencies: [S10](s10-validation-diversity-and-core-gate.md)

Requirements: W05, W06, W11, W12. Responsible executor: Codex, including explicitly labeled self-review. Accountable product/design owner: Fahad. No external or independent reviewer is assigned or claimed.

## Purpose and current gap

This remains framework work. A reference demonstration under `examples` is not a
new commissioned game, and framework implementation does not start a game-name
wizard. Actual managed artwork still needs genuine owner-selected canonical art
settings and exact-artifact review; synthetic fixture settings/approvals cannot
be transferred to that visual acceptance gate.

The requested visual demonstration needs consistent buildings, complete district contents and a whole-city composition. Earlier small mockups and unapproved experiments cannot establish the complete intended city or a saved production art profile.

## Algorithm and design

### Consolidated review view — 2026-10-28

The example now provides a task-local read-only gallery of twenty proposed
candidates: eighteen references and the two count-correct scene details. It
excludes the failed 16-of-17 detail from approval proposals and displays that
failure separately. Canonical hashes and current exact versions are checked
before publication of the view. No forms, script approval callbacks or manifest
writes exist in this helper; canonical GameArts remains the sole approval owner.
HTTP checks cover all 21 images and 42 full-size links. Browser layout inspection
is NOT RUN: local browser use is disabled in Work settings. The agent did not
change that setting or route around it. Consolidated human review and count-
revision direction remain the next gates; no new artwork is approved here.
[Evidence](../../evidence/2026-10-28-consolidated-art-review.json).

### Actual eligible-detail execution — 2026-10-28

The host must evaluate each leaf's own reference dependencies rather than wait
for every world reference to be approved. Three townhouse-only leaves were
eligible and were actually generated, despite eighteen pending references.
The separate execution/readiness records retain exact source job/member IDs,
image hashes, tool prompts, input roles and current lifecycle states.

Five scene attempts were registered and audited. Codex visual self-review finds
17 bodies in old-center-frontages-01 v002 and 19 in outer-residential-04 v001;
both await owner review. Old-center-frontages-02 remains at 16 of 17 after its
one automatic correction, and awaits owner direction. Failure is not repaired
by removing a member, asserting an unseen house or approving a manifest flag.
Earlier seen candidates remain unapproved rather than being silently withdrawn.

This is actual limited art execution, not calibrated map extraction or complete
city acceptance. Lower-row doors/ground are occluded and are not claimed
visually measured. Whole-district/city composition, cross-view calibration,
production art and native runtime acceptance remain open. The world-generator
skill now clarifies per-job scheduling; post-edit generation behaviour evaluation
is NOT RUN. Prior actual calls provide evidence only for this session's actions.
[Artifact/source integrity evidence](../../evidence/2026-10-28-district-detail-integrity.json).

### Draft district execution briefs — 2026-10-28

The example now declares eight logical district interfaces in its catalogue.
C# expands their typed connections and validates weak connectivity of all six
district children. No route rules, road geometry or world positions are inferred.
The reference jobs and all 862 nodes remain unchanged in the replayed JSON
relative to the prior handoff.

An example-authored presentation policy assigns complete immediate site/subtree
roots to semantic detail views, with at most 24 building bodies per view. This
is a review-size policy, not a world-generation algorithm, global cap or grid.
The C# handoff owner validates unique coverage and dependencies: 23 unchanged
reference jobs, 22 detail jobs and seven overviews (six districts and the city).
Whole factory and residential/school site programmes stay intact. Every required
building and ground area has exactly one detail owner; the city retains all
context nodes as well.

The [draft briefs](../../../examples/modern-city-demo/visual-plan/README.md) bind
29 unexecuted scene/overview prompts to exact source members, reference images
and unresolved review gates. Current canonical settings/selection/audit must be
checked again at execution. Direct visual count checks for the first three eligible details are recorded
above. Cross-view landmarks, calibrated ground geometry and full map extraction
remain NOT RUN.
Conflicting views require design revision or more detail views, never omitted
members, arbitrary shifting/scaling or lower coverage.
[Focused evidence](../../evidence/2026-10-28-reference-city-multiview-verification.json)
proves metadata/brief integrity only; it does not approve artwork.

All 23 required concepts now have real registered image-tool candidates. House
v003, townhouse v001, apartment v001, clinic v001 and production hall v001 have
real owner approvals and canonical promotion/audit. Eighteen selected revisions
await review. Garden v002 corrects the first garden taper; several other ground
concepts still widen toward the front and are not calibrated rectangles.
The current source-replayed handoff is `art-handoff-v003.json`, retaining the
hall role correction and all members while adding logical access intent and
22 detail views beneath six district overviews and the city overview.
Hall fan, fountain water and swing seats are discovered, not separated/animated
production artwork. See the
[reference-set audit](../../evidence/2026-10-28-reference-set-audit.json).

### Full framework flow audit — 2026-10-27

This audit retains the requested complete flow; it does not redefine completion
around the technical static fixtures.

| Required transition | Current evidence | Required remaining evidence |
| --- | --- | --- |
| Required building types → individual references | `MetadataArtHandoff` retains complete nested demand and deduplicates reference jobs; dependency order is tested | Actual canonical reference images and exact-artifact review for every required type/part |
| References → districts individually | Explicit detail scopes retain required member identities and reference dependencies | Complete reviewed district image set, including compounds, surroundings and interfaces |
| Districts → whole-city mockup | Overview jobs depend on detail scopes; image extent is distinct from world extent | A reviewed complete overview linked to all district/detail views, with no missing scope |
| Mockups → map | Calibrated identity/ground/scene replay, source-sized footprints, continuous terrain/access/path checks | Actual vision-derived annotations and full required-instance reconciliation against the reviewed images; small synthetic scenes do not prove this |
| Map → sprites and tiles | Complete final-map demand and canonical CLI binding/native export now execute; exact hashes and revocation refuse stale art | Real canonical artwork for every required structure/part/surface/transition; sampled component attachment/animation is technically qualified, while actual animated artwork remains open |
| Art → Godot TileMapLayer | Native layers, measured static sprites, terrain joins, pixel/ground transforms, source collisions and per-radius journeys pass focused technical tests | Complete production artwork in the example, applicable native animation/interactions and whole-city visual comparison/owner acceptance |

Sampled component discovery/attachment and native animation consumption are now
qualified through the existing production owners. Unsampled continuous curves
are not claimed. Fahad accepted the canonical **test art profile** under
`examples/modern-city-demo` (version `b5db73aa73c2`); this is not a new game.
Actual house mockups v001/v002 were generated. The attached v002 was explicitly
rejected for projection and revoked. v001 remains unapproved. Fahad then selected
¾ overhead orthographic with roofs/walls visible; canonical version `99de9f248453`
records that material camera change. Fahad approved fresh house v003 and
townhouse-compact v001; both were canonically promoted and audited. Neither
earlier house view may drive districts, a map or production assets.
The [example inventory](../../../examples/modern-city-demo/inventory/README.md)
is now expanded and source-replayed by C#: 862 nodes, 369 buildings, 328 ground
areas, six districts, 23 reference jobs, six detail jobs and one overview.
Site constituents remain separate members. This is pre-layout inventory, not
complete visual detail, roads or map geometry. Synthetic confirmations remain
separate from real artwork review.

Follow canonical GameArtsSkills with the exact catalog/content manifest: separate references for required building types, district-by-district mockups, then an overview of the whole city. Reuse the actual selected consumer profile under `examples/modern-city-demo/.gamearts/project.json`; the framework root needs no game setup. Use the newly selected oblique camera and obtain actual reference review. Use a storyboard when the canonical workflow requires one. Keep shared style, scale, camera, terrain and district interfaces coherent. Separate world extent from image resolution; use overview plus district/detail sheets instead of assuming one image exposes every structure. Approval refers to exact artifacts and revisions.


## Inputs and outputs

**Inputs:** Qualified core pipeline, game profile, full catalog demand and district/terrain/route intent, existing genuine approvals and references.

**Outputs:** Approved style/profile and building references, complete district mockup set, whole-city overview, reconciled content inventory and artifact approval/provenance records.

## Code and workflow owners

### Capture identity reconciliation — current implementation slice

Extend the existing capture owner with explicit metadata instance bindings for a
detail or overview job. Acceptance: replay the complete handoff; verify exact PNG
bytes and annotation revision; retain nested parents; account for every required
visual instance and every annotation; report missing, reused, ambiguous, inferred,
occluded or unreviewed identities without dropping content. A partial compilation
selection cannot establish whole-job completeness. Exercise generic and registered
catalogs, non-city programs, cancellation, stale input and preserved output paths.
The report must distinguish identity accounting from ground geometry, actual visual
inspection, art approval and a faithfully compiled map. No game setup is required
to develop or test this framework API. The implemented boundary includes nearest
visual-parent validation and the canonical capture JSON reader.
[Focused verification](../../evidence/2026-10-27-metadata-capture-verification.json)
covers synthetic interior/registered inputs, a nested visual program, actual CLI
reports and source refusal. These checks qualify identity accounting only;
real image interpretation, ground-map compilation and S11 approval remain open.

### Metadata-to-art handoff — implementation qualification, 2026-10-27

The structure-first owner now accepts metadata directly alongside its supported
mockup-derived input, without converting through the retired city generator.
It replays the exact recipe/catalog/world before inventory planning. Game-authored
archetype roles and hierarchical visual scopes must account for every expanded
node; every visual member belongs to exactly one leaf scope. Retain nested parents,
source shapes/reservations, registered variants, routes and provenance. Group
containers remain in the inventory and cannot hide leaf content. Deduplicate
reference jobs by actual archetype/variant, then order detail mockups before their
containing overviews. Scope membership and job order are explicit dependencies,
not hardcoded city/district assumptions.

This handoff is a draft inventory/dependency packet. It grants no art approval,
does not invent camera/scale/settings and cannot execute image generation. The
canonical GameArts setup, storyboard, references and approval owners remain the
only execution path. Acceptance: complete generic and registered content, nested
ownership, exact source replay, missing/overlapping scope refusal, deterministic
job ordering, cancellation, stale-source refusal and atomic new-file publication;
exercise the actual CLI and preserve its operation events.

[Focused evidence](../../evidence/2026-10-27-art-handoff-verification.json) now
covers four programs with 84 retained members, complete nested scope ownership,
registered dimensions, differing authored shapes, dependency order, rejection of
missing/overlapping/cyclic/stale or forged input, cancellation and new-file
publication. Two actual CLI exports equal their API results; refusal leaves no
output and all operation streams retain the outcomes. The inventory does not
qualify image execution, visual reconciliation, production assets or S11 closure.

The owner clarified that this remains framework development. `examples/modern-city-demo`
is an example workspace, not a newly commissioned game; no game-name or art-settings
wizard is required to implement these APIs. The local routing skills now encode
that distinction. Artifact production for a configured consumer still follows
canonical GameArts. No profile or approval was fabricated during this work.

Canonical GameArtsSkills owns setup, mockup production and approval. WorldCreator owns the reusable content manifest and spatial intent. Existing experiments remain evidence only unless explicitly selected and approved. Do not fork the canonical art wizard.

Named existing owners are inspection targets, not a commitment to keep every current API. Proposed boundaries become concrete in S01. Replace superseded behavior at its current owner, migrate all callers and remove the retired implementation; no compatibility aliases or parallel legacy path. Preserve unrelated changes and immutable historical evidence.

## Ordered implementation tasks

| Task | Work | Acceptance evidence |
| --- | --- | --- |
| S11.1 | Inventory saved settings, references, experiments and exact approval state. | Generated images are not relabeled approved; missing profile/rights/scale facts remain explicit. |
| S11.2 | Complete canonical setup and review the concrete profile at the required gate. | Reuse owner answers; request only genuinely missing choices or required art approval. |
| S11.3 | Generate required building-type references individually from catalog demand. | The set follows the requested city rather than an arbitrary fixed list of six buildings. |
| S11.4 | Generate and reconcile each district mockup against its inventory and interfaces. | Nested facilities, surroundings and mandatory instances are counted and visually reviewable. |
| S11.5 | Compose the whole-city mockup from the district design and shared terrain/routes. | City scope and boundary continuity are visible; this is not a town-sized subset labeled a city. |
| S11.6 | Record exact revisions and approvals before capture/production dependencies proceed. | Unapproved alternatives stay outside the production source chain. |

## Failure handling and operations

Validate inputs and required capabilities before expensive work. Apply explicit cancellation and finite work/resource budgets at stage boundaries and inside iterative work. Return attributed unsatisfied constraints instead of silent fallback, missing required content or partial success. Preserve prior accepted output on failure. Use the existing Log/Audit/Notification owners for applicable generation, export and review boundaries; UI consumers display actionable failures and cancellation. Record source/input identities, stage outcome and unresolved dependencies. Retry only after the cause or inputs change, with the same declared reproducibility guarantees.

Recovery is a coherent correction of the current owner and affected inputs, followed by focused checks. Do not restore a retired implementation as a runtime fallback. Artifact approval gates apply only to the relevant visual stages and exact revisions.

## Verification and acceptance cases

- Reconcile required types and instances across references, district sheets and whole-city overview.
- Check shared boundaries, road entrances, terrain features, camera/scale and nested structures across images.
- Record image dimensions, source hashes, approval state and unresolved visual mismatches.
- Do not infer acceptance from successful image generation or from a catalog count without visible content.

Run relevant checks with commands, environment, input/source digests and observed outcomes recorded. NOT RUN is not PASS. Existing tests are baseline evidence only. Keep implementation, local technical qualification, browser/native review, owner acceptance and unavailable CI/release gates distinct.

## Risks and boundaries

Image generation can omit or merge structures. Revise the visual design and retain the missing-content report; do not change mandatory demand silently. Art approvals are future dependency gates, not a reason to stop the present planning task.

## Exit gate

S11 closes when the exact required references and full district/city mockups are reconciled and approved under the canonical art workflow.

When the exit gate is evidenced, update the existing master tracker with actual evidence and the next dependency-ready task. Do not close this stage merely because these instructions or task descriptions exist.

## Research references

[R15](../../WORLD_GENERATION_RESEARCH.md#r15). These references motivate the proposed design; they do not certify this implementation or imply adoption of external code. The research record distinguishes accessed full text from abstracts and records relevant limits.

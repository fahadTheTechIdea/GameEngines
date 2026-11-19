# Framework generation study

This example evaluates **24 declared cases** with the current C# engine: complete
inland/coastal compound programs, four generated interiors, north/south ridge
passages, four terrain-field variants, encounter/progression graphs, an exact
repeat, and eight controlled interior topology variants. It is a framework corpus. Godot and artwork remain deferred.

`study.json` holds complete typed input snapshots. `source-inputs.json` pins the
canonical example files and their original byte hashes. Existing example/catalog
owners remain authoritative; this folder introduces no new content families.
Typed serialization materializes current optional null/default fields while
preserving authored values, counts, dimensions, constraints and generation budgets.

Use `MetadataGenerationStudy` with the current generator and shared
`WorldOperationReporter`. `RunAsync` returns a measured report; `PublishAsync`
atomically writes `study.json`, `study-report.json` and successful normal world
packets under `cases/case-<id>/`. A rejected case has a report row and no world.
The CLI command is `study-metadata <study.json> <new-output-directory>`.

The command completes the **study** even when cases are rejected. Inspect
`GeneratedCases`, `RejectedCases`, `Outcome`, `RejectionStage` and `RejectionCode`.
Malformed envelopes, unexpected runtime/sink failures and cancellation abort
instead of appearing as normal rejected worlds. Existing targets are preserved.
Cancellation after atomic commit does not pretend publication was rolled back.

## Measurement scope

Reports retain all attempts, recipe/catalog/world hashes, engine/dependency
identities, hierarchy/graph/geometry fingerprints and per-cohort histograms with
generated, rejected, available and unavailable denominators. Cohorts must declare
the same axes and units. Inputs detach before callbacks. One attempt runs per case;
the runner never retries for prettier output or reduces required programs.

- Weak components and cycle rank describe the explicit multigraph, excluding
  containment. Branch fraction counts nodes with three or more distinct weak
  neighbors. Parallel typed links do not manufacture branching. These quantities
  do not certify physical reachability or progression.
- Route distance and detour use actual forward/reverse exterior, network and
  interior paths. Detour is path length / straight endpoint distance. Closed paths
  have no detour value; absent paths remain unavailable.
- Solid coverage measures classified AccessPlan solid-source unions divided by
  the finite domain area. It excludes reservations, colored allocations, generated
  interior walls, corridor surfaces and terrain. It is not total built density.
- Geometry fingerprints bind surface/source geometry, access, paths and generated
  interior walls/openings. Hierarchy fingerprints ignore instance IDs/placement
  while retaining kind/archetype and child multiplicity. Graph fingerprints retain
  declared identities; they are not graph-isomorphism tests.
- Timing excludes publication. Allocation deltas and peak working set are
  process-wide observations, not isolated per-case peaks or controlled benchmarks.

The current corpus produces 23 cases and retains the intentionally blocked-region
refusal. The
exact repeat remains visible. Similar metrics reveal limited variety; they do not
prove unlimited expressive range. No quantity is a universal aesthetic,
player-enjoyment or game-readiness score.

The method follows [Evaluating content generators](https://www.pcgbook.com/chapter12.pdf),
Shaker, Smith and Yannakakis, chapter 12 (R09 in the shared research record).
This small declared corpus is bounded diagnostic evidence, not a representative
statistical survey or player/design acceptance.

[Commands, independent checks, hashes and remaining gates](../../../docs/evidence/2026-10-30-generation-study-verification.json).

The eight controlled interior cases vary only two declared seeds, maximum
degree2/3 and extra connections0/1. All now generate with the original six exact
rooms and six fixtures, source dimensions, deck, movement rules and budgets.
This includes chains, branching and physical loops. A degree ceiling is not a
promise of branching. The two formerly unresolved branching-plus-loop requests
use staggered contact spans derived from two actual neighboring source faces.
Their original refusals remain retained in earlier evidence.

Independent checks cover96 source polygons,44 shared-boundary openings and88
directed routes. The complete24-case corpus keeps all21 previously successful
node/graph/terrain/access/path/interior/progression sets identical; placement
work/proofs change. All24 source pairs remain pinned. Final geometry, declared
movement widths and replay are qualified separately from owner design/runtime.
[Current contact-span evidence](../../../docs/evidence/2026-10-30-contact-span-verification.json).

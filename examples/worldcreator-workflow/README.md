# Public SDK world-authoring workflow

This runnable .NET example uses **public WorldCreator APIs only** to generate,
publish, edit and restore four structurally different game-world declarations.
The C# engine performs generation. Regional and composed-domain modes use the
same executable and their existing public session owners. No AI agent, artwork setup or Godot is needed.

The final supported-core qualification runs this exact public consumer on the
current engine: all four baseline/edit workflows publish complete source-verified
packets and restore their checkpoints. Nine independent saved-output checks pass.
The broader core report retains all 40 fixed requests and measured scale limits.
See [current evidence](../../docs/evidence/2026-11-02-s10-core-gate-verification.json).
These are reusable engine workflows, not production-art or native-runtime approval.

`workflow.json` selects complete input pairs from the existing generation-study
corpus. It introduces no new catalog family or engine genre switch:

| Pair | Source edit | Required nodes before → after |
| --- | --- | --- |
| wilderness | Move the ridge/pass while retaining the outpost/trail program. | 9 → 9 |
| interior | Rotate the generated room/corridor arrangement. | 13 → 13 |
| compound | Replace inland terrain with the coastal counterfactual, retaining all required compound and surroundings content. | 61 → 61 |
| topology | Select the declared alternative encounter/capacity program. | 25 → 7 |

The topology count changes because its second input explicitly requests a
different composition. It is not a fallback that drops required nodes. All eight
declared worlds retain their complete input scope: 198 nodes and 160 physical
sources across the eight packets. Graph-only content acquires no invented terrain.

## Developer flow

The example's `Program.cs` is the complete consumer implementation:

1. Read the reusable recipes/catalogs and install the shared operation reporter.
2. Call `MetadataGenerationSession.StartAsync` with optional input protections.
3. Inspect `session.Snapshot`, including world geometry and execution/reuse report.
4. Publish the baseline, including its schematic and source-history checkpoint.
5. Submit a source edit against the accepted recipe/catalog hashes through
   `ApplyAndPublishAsync`. Required protections and normal generation constraints
   apply before an edit commits.
6. Restore the checkpoint with the runtime reporter supplied again. Restore
   reconstructs source history; it does not trust a saved geometry cache or record
   historical edits as newly performed actions.

`WorldCreator.WorkflowExample.csproj` references `WorldCreator.Core` directly.
Its executable takes the workflow manifest and a **new output directory**. Each
case writes `baseline/`, `edited/`, `log.jsonl`, `audit.jsonl` and
`notifications.jsonl`. `workflow-results.json` pins the exact manifest, corpus,
engine module, world hashes and complete observed counts. Schematic pages inspect
the saved engine geometry. They do not generate replacement layouts.

The same SDK accepts game-authored recipes and catalogs; the supplied manifest is
a framework demonstration. Authors continue editing source inputs rather than
resolved placement bindings or cached world output.

## Regional generation and edits

`--regions <definition.json> <edit.json> <new-output-directory>` runs one regional
definition edit. `--region-sequence <definition.json> <edit-1.json>
[<edit-2.json> ...] <new-output-directory>` runs 1–32 edits in order.
`RegionalWorkflow.cs` uses `MetadataRegionalGenerationSession.StartAsync`,
`PublishAsync`, `ApplyAndPublishAsync` and `RestoreAsync`. It compares every
accepted edited world against clean generation of its effective definition.
The engine owns terrain qualification, complete source geometry, regional reuse,
global paths, validation and history. This example does not implement a second
generator.

The [regional overlay example](../metadata/regional-overlay-invalidation/README.md)
supplies `definition.json`, `west-overlay-edit.json`, `moved-overlay-edit.json` and
`shared-height-edit.json`. It keeps all 10 nodes and 6 physical sources, with two
regions and both directions of the regional journey. The sequence shows a local
overlay edit, an overlay moved between domains, and a shared height change.

Outputs contain `baseline/`, `edited/` for a single edit or `edit-0001/` onward
for a sequence, and the shared log/audit/notification streams.
`regional-workflow-results.json` pins input hashes, Core module, world identities,
complete counts, per-edit region reuse and final history replay.

## Procedural regional source workflow

The same SDK workflow also accepts reusable blueprints:

```text
dotnet run --project examples/worldcreator-workflow -- --regional-blueprints examples/metadata/regional-blueprint/blueprint.json examples/metadata/regional-blueprint/larger-modules.json <new-output-directory>
```

The first blueprint defines the baseline; each subsequent blueprint is a full
source revision. The consumer reads and hashes each input, expands it through
`MetadataRegionalOwnershipPlanner.ExpandBlueprint`, then gives the expanded
request to the existing regional session. It publishes each accepted revision,
checks clean regeneration and saved replay, and restores the final checkpoint.
The example revision enlarges one shared catalog module used twice in each of
four regions. The receipt identifies blueprint input hashes; the session history
stores the expanded requests, so retain the original blueprint files for editing.
Zero to 32 revisions are supported, and the output directory must be new.

## Metadata recipe revisions

`--metadata-sources <initial-recipe.json> <catalog.json> [<revised-recipe.json> ...] <new-output-directory>`
uses public Core APIs to generate a world and apply zero to 32 authored recipe
revisions. `MetadataSourceWorkflow.cs` uses a reported `MetadataGenerationSession`;
it publishes the initial result, applies each revision as an independent commit,
compares edited worlds with clean generation and published-source replay, and
restores the final source-history checkpoint. It writes `baseline/`, then
`edit-0001/` onward, operation logs and a final receipt with source/world hashes
and observed scope. Inputs are read before generation, existing output is refused,
and a later failure preserves earlier accepted publications without a final receipt.
The catalog stays fixed during this particular workflow; the session API supports
catalog edits separately.

The [shared-partition preferences](../metadata/shared-partition-preferences/README.md)
example contains `pinned-related-recipe.json`, `catalog.json` and
`revised-preference-recipe.json`. The tested revision moves a program while a
different program remains pinned and a declared distance, all physical sites and
the network remain required. These files are reusable framework examples, not a
specific game or art setup. This workflow does not turn partial declared scope
into a complete playable game.

`--regional-sources <initial-source.json> [<changed-source.json> ...] <new-output-directory>`
starts from a `MetadataRegionalOwnershipRequest` and accepts zero to 32 complete
authored source revisions. With no revisions it publishes and replays the initial
world and its checkpoint, so a developer can generate a first world directly.
For example, the framework inputs in
`../metadata/regional-networks/` provide `request.json` and `cost-request.json`:
the second changes terrain travel costs while preserving the declared regional
programs. A third argument containing `request.json` restores the initial source.
These inputs describe a framework test, not a fixed game or required genre.

To generate just the first example world from the repository root:

```text
dotnet run --project examples/worldcreator-workflow -- --regional-sources examples/metadata/regional-networks/request.json <new-output-directory>
```

Open `baseline/schematic.html` in the output directory to inspect the saved
world; `baseline/world-regions.json` contains its generated data.

`RegionalSourceWorkflow.cs` shows the public API: `StartDerivedAsync` plans
and generates the source; each `MetadataRegionalSourceEditRequest` uses the
accepted definition and world hashes, then `ApplyDerivedAndPublishAsync` plans,
validates and publishes the new world. The example compares each accepted result
to independent clean generation and source replay, then restores the final
checkpoint. It writes `baseline/`, `edit-0001/` onward, operation records, and
`regional-source-workflow-results.json` with input hashes, node/route counts,
reuse and replay results. Failed later revisions leave earlier accepted
publications in place; no receipt is written unless the sequence completes.

## Running this consumer

The project targets .NET 10 and references only the local Core project.
From the repository root a developer can run the manifest workflow with:

```text
dotnet run --project examples/worldcreator-workflow -- examples/worldcreator-workflow/workflow.json <new-output-directory>
```

The regional/domain switches replace the two manifest-mode arguments after `--`.
Agents performing this workflow execute the appropriate commands themselves.
`global.json` requests SDK 10.0.402 with `latestPatch` roll-forward; the current
scoped consumer build used SDK 10.0.403. An example build is not a full-solution gate.

All modes propagate Ctrl+C cancellation to input reads, generation, publication,
edits, restore and result writing. Cancellation returns exit 130; failures print
their exception and return 1. Existing output is refused. Manifest case references
and edit-file decoding are checked before generation; edit sequences are separate
commits, so an accepted earlier publication remains if a later edit fails or is
cancelled. A final workflow receipt is written only after the requested sequence
and restore complete. Engine operation failures are reported by the existing
session services; argument/file failures before session creation go to stderr.

## Runtime outcomes

The same executable accepts `--domains <definition.json> <edit.json>
<new-output-directory>` for composed worlds. `DomainWorkflow.cs` uses only public
Core APIs to create, publish, edit, restore and compare against clean generation.
For zero to 32 edits, the same owner accepts `--domain-sequence
<definition.json> [<edit-1.json> ...] <new-output-directory>`. With no edits,
it generates the first world, publishes and replays it, and restores its
baseline checkpoint. It reads every input before creating output, publishes the baseline, then applies
edits in supplied order through the same reported session. Each accepted world
must equal clean generation. The final checkpoint restores the entire history.
For example, generate the initial outdoor-and-floors framework world with:

```text
dotnet run --project examples/worldcreator-workflow -- --domain-sequence examples/metadata/world-domains/definition.json <new-output-directory>
```

For multiple edits, publications are `edit-0001/`, `edit-0002/`, and so on; a
single edit retains `edited/`. These are separate commits: a failed later edit
does not roll back an already accepted publication. The receipt includes every
input edit's hash, checked before and after reading. Receipt version 2 names
the final world and node counts even when no edit was supplied. See the complete
[source-size/terrain sequence](../metadata/source-terrain-morphology/README.md).
Domain-mode failures print the exception and return exit code 1 through the
entry adapter. Invalid edit counts refuse before input reads or output creation.
The complete outdoor/two-floor example is
`examples/metadata/domain-journeys/definition.json` with
`examples/metadata/domain-journeys/upper-width-edit.json`: all 21 nodes, explicit
transitions and complete forward/return journeys remain required while an upper
floor's source-sized modules change. Each publication includes its domain
schematics and checkpoint; `domain-workflow-results.json` pins the exact input
files, engine and observed scope. A composed-world success-report outage exposes
`MetadataDomainSessionReportingException` with the accepted session/output.

Reported sessions await their log/audit/notification services. Use `ApplyAsync`
for an in-memory edit or `ApplyAndPublishAsync` for a published edit; reported
sessions reject the synchronous `Apply` mutation so reporting cannot be bypassed.
The pure constructor/synchronous algorithm APIs remain available to engine hosts
that own their operation boundaries separately.

Failures and cancellation before commit retain the previous accepted session and
published packet. An atomic publication is the commit point; cancellation arriving
after it does not undo the completed action. If a success-report sink fails,
`MetadataSessionReportingException` exposes `Committed`, `Session`,
`OutputDirectory`, `Operation` and `CorrelationId`. Recover that accepted state and
repair reporting; do not automatically repeat the edit/publication. A failure of
both the operation and its failure-report sink retains both exceptions.

Actor labels come from the consuming host and do not independently authenticate a
human. Source/world/checkpoint hashes and sanitized stage/reason codes are logged;
complete source payloads and exception messages are not dumped into these events.

The full workflow and adversarial checks are recorded in
[source-qualified evidence](../../docs/evidence/2026-10-30-session-workflow-verification.json).
These bounded technical examples establish usable API/CLI/inspection paths;
professional whole-world composition/design, larger-world resource bounds,
remaining low-level/domain reporting, artwork and Godot remain separate gates.

Current consumer qualification covers the regional sequence/single-edit paths,
the four original recipe/catalog pairs and the complete 91-node domain composition.
It verifies existing-output preservation, input refusals, accepted earlier commits,
clean equality, replay and cancellation across all three modes. See the
[source-qualified entry evidence](../../docs/evidence/2026-11-01-developer-entry-verification.json).
The existing Core remains unchanged; this is a runnable developer entry path,
not acceptance of the complete professional-generator objective.

## Complete package verification

After baseline and edited publication, the consumer calls
`generator.ReadPublishedAsync(directory, token)` and compares the returned world
with the accepted snapshot. This reads the saved recipe and catalog, checks their
digests and replays the complete generation. It complements checkpoint history
restore: a coherent world document missing a requested member cannot pass by
repairing only its saved layer/geometry plans. The runnable workflow verifies all
eight packages and records `PublishedSourcesReplayed: true` for each case.

The read-only CLI equivalent is `verify-metadata <published-directory>`. It uses
normal registry discovery/reporting and preserves all input files on success,
refusal or cancellation. Verification checks agreement with supplied source files
and the current engine; it does not supply source approval or saved-game migration.
See [the current qualification](../../docs/evidence/2026-11-01-published-source-replay-verification.json).

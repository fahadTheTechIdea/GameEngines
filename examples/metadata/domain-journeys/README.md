# Qualified journey across three domains

Framework source definition, not a configured game. This extends the independent
outdoor/two-floor domain example with a required continuous forward/return journey.
All three complete seven-node local programs and two explicit transitions stay
unchanged. Both floors retain identical local coordinates and IDs in separate
domains.

`gated-corridor-definition.json` is a separate two-domain fixture with two
source routes sharing directed corridor segments. A key acquired at the start
is required on both source connections. Saved journey steps identify the source
route and capability state through each recorded junction, in both directions.
It can be generated with `generate-world-domains` and is a framework fixture,
not a game or a simulated barrier.

The journey starts at the outdoor second structure's west access approach. It uses
an existing qualified reverse local passage to the first structure's east approach,
the explicit `entry` transition, existing centre/doorway paths through the lower
floor, the `stairs` transition and existing upper-floor paths to the exit-room
opening. Forward and return each contain 11 source steps. Every physical segment
joins at an exact saved source coordinate in its current domain; transitions are
explicit coordinate/domain changes. No path, turn or bridge is invented at a line
intersection or to fill a gap.

`Journeys[].ConnectionKinds` selects allowed local/transition kinds. `Profiles`
selects the exact movement profile in each physical domain; radius, slope, kind
costs and directional height costs must match the qualified witnesses. Removing
local `door` connections leaves the domain graph connected but makes the journey
unreachable. A profile mismatch or missing return direction also refuses. No
partial collection is published on failure.

Tests independently expand each source segment and transition to verify exact
endpoint/domain continuity in both directions, unchanged child data, shared proof
budgets, tamper/read replay and logical-only paths without terrain. The current
`generate-world-domains` CLI exports this definition plus all local schematics and
a root journey summary. Godot, runtime movement, absent-route synthesis, continuous
3D connectors, broad scale and visual acceptance remain deferred/unqualified.

[Verification](../../../docs/evidence/2026-10-29-domain-journeys-verification.json).

## Domain generation sessions

`MetadataDomainGenerationSession` edits a collection through the existing generator.
For a host with shared operation services, use
`MetadataDomainGenerationSession.StartAsync(generator, definition, reporting)` and
await `ApplyAsync` or `ApplyAndPublishAsync`. `MetadataSessionReporting` supplies
the same Log/Audit/Notification services used by single-world sessions. Reported
sessions reject synchronous `Apply`; their awaited edits and publications cannot
interleave. Pure constructor/algorithm APIs remain available to hosts that own
their operation boundaries separately.

Each actual create/edit/publish/restore records correlated actor, source/world and
proposed-definition identities, stage/reason and reuse counts. Definitions,
proposals and checkpoint contents detach before callbacks. Runtime services must
be supplied again on restore; they are not serialized into the checkpoint.
Restore reports its own action and replays historical edits without recording
them as newly executed edits. Actor is host attribution, not authentication.

Precommit failure/cancellation retains the previous world, caches and history.
An atomic publication commits even if cancellation arrives afterward. If only
success reporting fails, `MetadataDomainSessionReportingException` exposes
`Committed=true`, `Session`, `OutputDirectory`, `Operation` and `CorrelationId`.
Recover that accepted state and repair reporting; do not retry an already committed
edit/publication. Both the original operation and a failed failure-report sink
remain available in an `AggregateException`. Async reporting does not schedule
generation CPU work on a background thread.

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

Aggregate current cache entries remain bounded at 128 MiB. Checkpoints use the
current partitioned transport: 16 MiB per file, 256 MiB reconstructed. These are
limits, not demonstrated maximum-size throughput or peak-memory guarantees.
Awaited operation lock contention, callback snapshots and early/late cancellation
are qualified; maximum-size exhaustion, broader concurrent stress and mid-algorithm
cancellation injection remain separate.

The three-domain/21-node fixture retains complete forward/return journeys.
Journey-purpose edits reuse 47/47 stages; an upper-floor geometry change reuses
2/16 upper stages and every lower/outdoor stage. API rollback, source-history
restore, tamper refusal and CLI replay passed. One CLI publication attempt hit a
Windows rename access denial, cleaned up without publishing, then passed in an
isolated run using identical binaries; the cause is unconfirmed.

[Domain session evidence](../../../docs/evidence/2026-10-29-domain-session-verification.json). The full generator remains IN PROGRESS;
Godot, production artwork, runtime and visual acceptance remain deferred.

[Composed lifecycle verification](../../../docs/evidence/2026-10-30-domain-lifecycle-verification.json) qualifies the current reported API/SDK/CLI workflow.
The runnable public SDK example and exact `upper-width-edit.json` source edit are
under [worldcreator-workflow](../../worldcreator-workflow/README.md). All required
nodes, source geometry and complete journeys are retained; the full core design
gate remains open.

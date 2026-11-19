# Protected world edits and executable regeneration

This example uses the complete two-outpost ridge world from
[`landform-routes`](../landform-routes/README.md). Its catalog and placement program
are protected inputs. Moving the pass preserves all nine nodes, eight physical
members, source dimensions and fixed anchors while changing the six physical paths.

The public command sequence is:

```text
metadata-session start recipe.json catalog.json protections.json <new-initial-directory>
metadata-session edit <initial-directory>/generation-session.json move-pass.json <new-moved-directory>
metadata-session edit <moved-directory>/generation-session.json presentation.json <new-review-directory>
```

The agent runs these commands; game developers can also use
`MetadataGenerationSession` directly. The retained packets are under
`generated/regeneration-initial`, `generated/regeneration-moved` and
`generated/regeneration-review`.

`generation-session.json` contains source inputs, protected values, accepted edit
history, build identity, changed JSON pointers and execution receipts. The initial
world executes fourteen stages. The pass edit reuses `content` and reruns thirteen
dependent stages. The profile-label edit reuses all fourteen computational stages;
input validation, document assembly and export verification still run.

`protected-conflict.json` attempts to move a protected anchor. It must fail with
`METADATA_PROTECTED_INPUT_CONFLICT`, publish no packet and preserve the accepted
checkpoint. A changed terrain that makes a protected compound impossible reports
the edit, failed stage and original constraint error. Retained protection IDs are
context for diagnosis, not proof that each protection caused the failure.

These are whole-stage dependencies, separate from semantic render layers and
spatial chunks. The cache is private to a live session. Reload replays source/edit
history to rebuild it; it does not trust or reuse serialized geometry. Thus this
CLI example is not a warm-cache performance benchmark across processes.
Only matching engine build identities reload. Unversioned external catalog
policies or custom layout implementations are refused for incremental sessions;
their ordinary generation APIs remain supported.

The same existing publisher stages all files before an atomic move into a new
directory. An edit-and-publish call updates accepted session state only after
publication succeeds. Existing packets are never overwritten. Checkpoints/input
authored payloads are limited to 16 MiB. Generated checkpoints use bounded
partitioned storage up to 256 MiB, preserving complete source history. Edit
history is limited to 32 accepted edits, protections to
64, changed leaf paths to 4096, and serialized cache entries to 128 MiB per run.
Those limits do not constitute a peak-memory or streaming benchmark.

The tests compare incremental output with full generation, exercise a distinct
geometry-free progression program, cancellation during recomputation and file
staging, stale edits, protected conflicts, replay tampering and caller mutation.
Versioned policies/layouts now have a separate
[qualified example](../versioned-dependencies/README.md). The retained ridge
packets above are historical schema-1/build-bound evidence; generate fresh
directories from their source inputs with the current schema-2 session API.
Chunk ownership/seams, finer invalidation, population, artwork and native Godot
acceptance remain separate open gates.

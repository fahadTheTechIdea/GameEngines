# Shared boundary between generated regions

This framework example requests three terrain-derived regions and complete
nested compound programs. Its `alpha`/`beta` relationship requires at least one
meter of continuous shared boundary. WorldCreator qualifies the actual partition
and retains the selected span; required content and declared terrain-qualified
connections remain separate hard constraints.

In a partitioned `WorldRegionRequest`, set `MinimumSharedSpan` on a
`WorldRegionIntentRelation`. The engine can reconsider permitted area cuts when
the span does not fit. A shared boundary is geometric: a game that needs travel
must also declare and qualify a connection. Requests without a region partition
reject this span constraint explicitly.

Generate with `WorldMetadataGenerator` or the existing `generate-metadata` CLI
using `recipe.json` and `catalog.json`. The example is a bounded framework test,
not a complete game or visual approval. See the
[world metadata contract](../../../docs/WORLD_METADATA.md) and
[verification](../../../docs/evidence/2026-11-01-region-shared-span-verification.json).

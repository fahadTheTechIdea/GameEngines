# Bounded recursive cut adaptation

Framework inputs for four complete physical programs, using the shared generator:

- `positive`: move a cut along a positive X normal.
- `negative`: reverse its normal/children and move toward negative X.
- `negative-steps`: use negative signed offsets along a positive X normal.
- `diagonal`: move along an oblique normal without changing its angle.

Each folder has `recipe.json` and `catalog.json`. Every baseline cannot fit its
required full-size content. The declared `PartitionOffset` dial targets `CutId`
and tests three bounded settings; two reject, then the third fits both complete
structures and all seven nodes. Footprints/reservations, required anchors and tree
topology are preserved. The current generator and CLI own all geometry and proofs.

Command: `generate-metadata <recipe.json> <catalog.json> <new-output-dir>`.
Exports contain metadata, attempt history, semantic layers and a JavaScript
schematic. No game name, city model, mockup, artwork or Godot is required.

See [contract](../../../docs/WORLD_METADATA.md#bounded-recursive-cut-adaptation)
and [verification](../../../docs/evidence/2026-10-29-cut-adaptation-verification.json).
This qualifies bounded local adaptation, not automatic topology synthesis,
whole-world design acceptance or a finished professional generator. Godot deferred.

# Regional developer controls

This framework reference protects a chosen location and selected reusable inputs
while regenerating actual terrain. It is a technical control, not a finished game
or a visual-design example. Godot and artwork remain deferred.

`request.json` declares two complete reusable programs in a100×80m domain.
`controls.json` protects the world boundary, the arrival program's placement
grammar and catalog, and its generated origin. `changed-request.json` adds a
terrain notch: ordinary generation moves the unlocked arrival origin, while the
controlled session retains its exact25,40origin and all five local nodes/three
physical sources. The whole world retains ten nodes, six sources and every local
and regional outward/return journey.

Use the existing SDK session owner:

```csharp
var session = await MetadataRegionalGenerationSession.StartDerivedAsync(
    generator, request, controls: controls);
var before = session.Snapshot;
var edit = new MetadataRegionalSourceEditRequest(
    "terrain", "Revise the declared terrain", "developer:terrain",
    before.World.DefinitionSha256, before.Report.WorldSha256, changedRequest);
await session.ApplyDerivedAndPublishAsync(edit, newOutputDirectory);
```

Source, controls and edit objects can be loaded through `WorldMetadataGenerator`.
The CLI offers `region-session start-derived` with optional `--controls`, then
`edit-derived` against the saved checkpoint and a current-base edit. The example
stores reusable requests, rather than assuming hashes from another engine build.

Pins constrain the terrain planner, with actual containment/clearance and full
physical qualification. They do not move objects after generation. Impossible
terrain, protected-content changes, missing regions and attempted pin movement
produce attributed failures; accepted world/cache/history remain intact and no
output is published. Unrelated recipe editing can reuse the exact protected local
packet. Reports/checkpoints retain definitions, input hashes and pin positions;
restore recaptures them and replays source history. Shared operation reporting
keeps generation failure distinct from an outage after publication has committed.

Initial controls are selected at session creation. The current
[protection-change workflow](../regional-protection-changes/README.md) adds explicit
in-session additions/releases while retaining initial authority and full history.
Graphical authoring, broader world design/variety/scale and native streaming remain
unfinished. See the
[contract](../../../docs/WORLD_METADATA.md) and
[qualified commands, hashes and limits](../../../docs/evidence/2026-10-31-regional-developer-controls-verification.json).

Input locks protect authored values, rather than freezing every terrain-derived
count or selected program variant. Exact regional-origin protection is distinct
from the shared [per-object placement controls](../pose-controls/README.md).

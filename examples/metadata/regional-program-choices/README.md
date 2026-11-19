# Complete program choice before regional allocation

This framework control declares three complete functional programs on shared
240×80m terrain. It is a technical generation test, not a finished world design.
Godot and artwork are deferred.

Each region must provide four function units, one full18×2m payload, one4×2m
storage body and their local return journey. Both catalog host options supply the
same required functions and children. The preferred16×12m host cannot contain
the fixed payload. The declared24×12m host can. The engine retains that exact
source-envelope failure, selects the other complete catalog program and measures
its original28×16m reservation before regional allocation. It never shrinks the
payload or changes the required functions. All regional return journeys retain
the authored0.4m actor width.

The request's `ProgramComposition` policy drives finite regional program choice.
The shared ranked mix owner analyzes candidates. Derived regional recipes bind
their full `Recipe.ProgramComposition.RequiredRanks`, so ordinary generation
cannot silently substitute another selection after capacity was measured.
Native read/export rederives the original request and every selected/failed
candidate. Only a proved pose-independent nested source envelope can prune a
local choice across unrelated regional choices.

The baseline retains12nodes/9bodies and all five round trips after four complete
candidate attempts. The larger-source control uses the declared28×12m host and
32×16m reservation, retaining the original payload/storage. Another control
composes this policy with regional component feedback. These are authored test
dimensions and requirements, not framework defaults.

```text
generate-derived-regional-world examples/metadata/regional-program-choices/request.json <new-output-directory>
```

The output contains complete metadata, the derived definition, selected prefixes,
failed source-envelope witnesses and JavaScript/SVG inspection in engine units.

Developers can keep the source request as their editable world definition:

```csharp
var session = await MetadataRegionalGenerationSession.StartDerivedAsync(
    generator, source, reporting, cancellationToken);
var before = session.Snapshot;
var edit = new MetadataRegionalSourceEditRequest(
    "revise-program", "Revise the required complete program", "game:program",
    before.World.DefinitionSha256, before.Report.WorldSha256, revisedSource);
await session.ApplyDerivedAndPublishAsync(edit, newOutputDirectory, cancellationToken);
```

The host supplies `source`, `revisedSource`, its existing generator/reporting
services and a new destination. The session derives the plan, checks full physical
content and travel, and commits the world/history together. Reverting submits a
previous source as a new attributed edit using the current base hashes. Existing
publications remain intact; failed edits never become accepted history. The CLI
also provides `region-session start-derived` and `region-session edit-derived`.
[Source-edit and exact rollback qualification](../../../docs/evidence/2026-10-31-regional-source-history-verification.json).

Full world design, simultaneous terrain-count/program-choice solving, region-intent
choice analysis and source layout adaptation remain open beyond this control.
[Contract](../../../docs/WORLD_METADATA.md) ·
[qualification](../../../docs/evidence/2026-10-31-regional-program-choices-verification.json).

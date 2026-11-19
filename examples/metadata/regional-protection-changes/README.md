# Edit regional protections without losing world history

This framework example retains complete nested compounds, actual shared terrain
and directed local/regional paths while changing the developer's protections.
It uses the existing metadata regional generator and session, without an art
profile, a named game or Godot.

The reusable [source](source.json) and [initial controls](controls.json) start with
three selected input locks and one generated arrival-anchor lock. The sequence is:

1. [Release the arrival lock and revise terrain](release-edit.json). The normal
   planner selects another valid location and keeps every required source/path.
2. [Lock the new arrival and terrain exclusions](relock-edit.json). The new lock
   captures the current accepted location, rather than the original position.
3. [Release every protection and restore the original source](release-all-edit.json).
4. [Protect the accepted purpose](lock-purpose-edit.json), preserving world geometry.

Both `MetadataRegionalEditRequest` and `MetadataRegionalSourceEditRequest` accept
optional `ProtectionChanges`:

```csharp
var before = session.Snapshot;
var change = new MetadataRegionalProtectionChange(
    ExpectedEditId: before.Report.EditId,
    ReleaseIds: ["location-origin"],
    Inputs: [],
    Anchors: []);
var edit = new MetadataRegionalSourceEditRequest(
    "release-anchor", "Allow the intended terrain revision", "developer:terrain",
    before.World.DefinitionSha256, before.Report.WorldSha256,
    revisedSource, change);
await session.ApplyDerivedAndPublishAsync(edit, newOutputDirectory, token);
```

The supplied edit JSON pins the tested accepted state. For a new session/build,
use its current definition/world hashes and `Report.EditId` as above. The additional
edit-ID guard matters: a protection-only edit can leave both world hashes unchanged.
The normal `region-session edit` and `edit-derived` commands consume these fields;
there is no separate protection store, command family or history protocol.

Additions use the same purpose/source-attributed input and anchor definitions as
initial controls. They capture the accepted world **before** applying the proposed
input edit. Surviving locks retain their original hashes/positions. Changing a
newly locked value in the same edit refuses; release it explicitly before changing
it. Duplicate targets/IDs, missing values and unknown releases refuse. Simultaneous
replacement of an active ID requires a new ID. At most64 protections may be active;
empty change requests refuse, while explicit all-release is valid.

Anchor additions require a derived source and become real planner pins. Direct
definition edits authenticate the supplied derived receipt before adding a pin;
they cannot use activation to repair forged geometry. Releasing a policy does not
erase explicit `PinnedAnchor` data. To allow automatic relocation, supply a source
edit that explicitly removes the relevant pin, as the release examples do.

`Report.Protections` describes the effective current policy. Checkpoint
`Protections` and `InitialReport.Protections` retain initial authority; each edit
stores its transition and resulting policy. Restore replays the whole sequence.
Failed/cancelled edits and failed publication preserve world, policy and history.
Reporting failure after commit exposes the committed session/output for recovery.
Returned snapshots cannot mutate the accepted policy.

See the [S08 owner](../../../docs/world-generation/stages/s08-deterministic-chunks-and-persistence.md)
and [commands, exact identities and limits](../../../docs/evidence/2026-11-01-regional-protection-changes-verification.json).
This is an SDK/CLI editing capability, not a graphical editor or qualification of
every world type/scale. The professional generator remains in progress; artwork
and Godot are deferred.

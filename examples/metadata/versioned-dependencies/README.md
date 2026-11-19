# Registered content in generation sessions

This bounded example uses an actual Modern structure, source dimensions, service
allocation and regional context. All four required nodes survive a profile-label
edit. It demonstrates registered dependencies, not a universal world layout.

```text
metadata-session start recipe.json catalog.json protections.json <new-initial-directory>
metadata-session edit <initial-directory>/generation-session.json edit.json <new-review-directory>
```

The CLI discovers the selected catalog through its existing registry. Retained
packets are `generated/dependencies-initial` and `generated/dependencies-review`.
Initial generation executes fourteen stages; the label edit reuses all fourteen.
Validation and publication replay still run. The complete catalog input is
protected, including required members and source selection.

Schema-2 checkpoints record content, catalog policy code/configuration and selected
layout identities. Replay requires the same engine build and dependency versions.
The API supports an explicit resolver of installed historical implementations;
the CLI rejects unavailable versions. No saved code is loaded or compatibility
implementation substituted. Packets remain historical evidence after build changes.

Integration tests also replace a configured layout and registered source variant,
compare with full generation, replay exact historical versions and reject changes
during generation/publication. Those updates use the API; this CLI example keeps
its registered version unchanged. See [evidence](../../../docs/evidence/2026-10-27-world-dependencies-verification.json).
Chunks, scale, purposeful population, artwork and native runtime remain open.

# Stage topology with an alternative tree

This game-independent example asks for two loops before a mandatory transition
and one after it, with maximum degree three. The first seeded tree cannot fit
those demands. WorldCreator searches alternate parent assignments within the
declared work budget and produces all eight destinations and ten connections.
Removing the transition disconnects entry from the goal.

A stage may also set `MaximumExtraConnections` to limit additional paths.
For example, setting it to `0` makes that stage a tree while other stages may
still use the rule's requested loop count. Stage minima and maxima must permit
the exact total; an impossible request is reported rather than weakened.

The input describes gameplay topology only; it does not invent terrain or
physical passages. Use the normal metadata generator with `recipe.json` and
`catalog.json`, then inspect the topology and progression plans. A bounded
search can report a work-budget failure instead of proving infeasibility.

The engine's public C# entry point is `WorldMetadataGenerator.Generate`. The
existing CLI also accepts `generate-metadata <recipe> <catalog> <new-output>`.
See [the contract](../../../docs/WORLD_METADATA.md#generated-progression-topology)
and [verification](../../../docs/evidence/2026-11-01-alternative-stage-trees-verification.json).

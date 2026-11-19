# Travel across an alternate region boundary

Two required regions share two separate nine-meter boundaries around a
protected hole. Water blocks the upper boundary for the declared traveler.
The engine preserves both actual spans, records the rejected upper attempt and
routes both directions through the lower span. Two complete source sites and
their required children remain intact.

`moved-recipe.json` moves the water to the lower boundary, so the same
required route uses the upper boundary. `blocked-recipe.json` blocks both and
must fail without producing a new world. The source edit/session test verifies
clean-generation equality, checkpoint restore and failed-edit preservation.

The route must explicitly bind to the shared-region relation.
`MaximumCrossingCandidates` bounds the search; a one-candidate request rejects
in this example rather than jumping across the hole or dropping content.
This game-independent input uses the public metadata generator and requires no
artwork or Godot. See the [contract](../../../docs/WORLD_METADATA.md) and
[verification](../../../docs/evidence/2026-11-01-region-alternate-spans-verification.json).

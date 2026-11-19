# Complete source-size and terrain counterfactuals

This framework example keeps the complete composed world: 91 nodes and 86 physical
members across terrain compounds/landscape, real outside structures, generated
rooms/content and logical progression. It needs no game setup, art or Godot.

The sequence contains three accepted states and one deliberately impossible edit:

1. `definition.json`: inland terrain with the exact 10×6 production hall.
2. `larger-source-edit.json`: replace that hall with an exact 14×10 source and a
   matching boundary/approach port. Required counts, parent sizes, recipes,
   movement rules and generation limits remain unchanged. Local approaches and
   paths must qualify around the larger hall; the source cannot shrink to fit.
3. `coastal-edit.json`: change only the consumer/source terrain patches to the
   coastal input, retaining the exact larger hall. Actual source placement changes.
4. `oversized-edit.json`: request an 80×60 hall inside the original required parent.
   The engine refuses with `NESTED_SOURCE_ENVELOPE_TOO_LARGE`; it neither omits the
   hall nor changes the parent, accepted world, checkpoint or published output.

The complete forward/return journey remains required in every accepted state:
terrain network → outdoor structure → entrance/rooms → logical progression.
The unrelated local and progression domains remain exact across both edits.
Domain transitions are explicit game rules between independent coordinate spaces;
they do not synthesize physical bridges or runtime transition behavior.

The public Core-only
[SDK host](../../worldcreator-workflow/README.md) accepts
`--domain-sequence <definition.json> <larger-source-edit.json> <coastal-edit.json>
<new-output-directory>`. It publishes `baseline/`, `edit-0001/`, `edit-0002/`,
durable operation records and a workflow receipt. Each edit commits separately.
A failed later edit does not undo an earlier committed publication. The same
sequence uses the existing CLI `domain-session start` followed by two
`domain-session edit` calls. The final checkpoint replays the complete history.

Native qualification retains all seven compound families, exact source dimensions,
all 51 local links and six bidirectional regional links, complete source nodes and
paths after population, clean-generation equality, replay and refusal rollback.
Independent checks measure hall sides/area/center/port and expand saved journey
steps. The API, CLI and public SDK produce identical worlds and checkpoints for
all three states. JavaScript inspects those saved worlds without changing layout.
See [commands, hashes and scope](../../../docs/evidence/2026-10-30-morphology-verification.json).

This is bounded technical qualification of a full declared program, not whole-game
design acceptance or proof of every morphology, genre and scale. Browser visual
review, production artwork, temporal gameplay and Godot remain separate work.

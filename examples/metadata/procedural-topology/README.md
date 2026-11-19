# Procedural progression topology

These inputs use the shared C# generator. No game name, artwork, city grid or Godot
project is required.

- `encounter`: 24 destinations in two game-authored stages. Twelve encounters
  precede a mandatory transition; nine follow it before the final objective. The
  engine creates 23 tree connections and three local loops, with maximum degree
  six. No edge list or positions are authored. Changing the seed changes branches
  and loops while retaining every destination and the mandatory transition.
- `north-inlet` and `south-inlet`: two complete expedition compounds retain eight
  physical constituents, exact footprints, hierarchy and access. Their inter-site
  connection comes from a topology rule and is realized by the existing terrain
  router. Two local access connections remain authored. Moving the inlet changes
  the physical route, without shrinking or dropping members.
- `loop-capacity`: six destinations and nine connections, including exactly four
  loops at maximum degree three. The earlier greedy selector rejected this
  feasible demand after choosing only three loops. Bounded constraint search
  retains the same seed, tree, members and degree limit and finds all four.

Use the existing `generate-metadata <recipe.json> <catalog.json> <new-output-dir>`
command. Each output contains the generated graph, progression witnesses, source
provenance and schematic. The focused `--world-topology` checks include independent
graph cuts, 16 seed variants, exact loop/degree accounting, terrain response,
saved-plan replay, stale-plan refusal and regeneration dependencies.
`--world-topology-capacity` exhaustively checks small-graph feasibility using a
separate edge-subset oracle, including impossible demands, rollback and work
refusal. Loop search uses the declared work limit; it never adds degree capacity
or drops required connections to make an input pass.

This increment generates logical stage topology and integrates it with existing
physical planning. It does not infer physical rooms, doors, interiors, gameplay
mechanics, production artwork or unrestricted world layouts from a graph. The
stage grammar and content demands remain game-authored. Godot is deferred.

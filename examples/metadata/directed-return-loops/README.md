# Directed return loops

The common topology generator builds a two-stage game-independent progression
with 24 required destinations, three additional connections and exactly two
one-way return links. Each return joins a descendant back to an ancestor along
a real generated tree path, creating a directed cycle. The intermediate gate
remains mandatory.

`regional-request.json` demonstrates a physical six-region world with one
return link. Return links stay inside a stage and do not guarantee a return
journey across the whole world. See the
[topology contract](../../../docs/WORLD_METADATA.md#generated-progression-topology).

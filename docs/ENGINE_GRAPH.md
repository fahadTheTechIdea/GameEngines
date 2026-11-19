# EngineCommon.Graph: the shared deterministic graph library

Dated 2026-10-29 (fortieth pass, work item 1 of `docs/ENGINE_ALGORITHM_REVIEW.md`). Project `src/EngineCommon/EngineCommon.Graph`, tests `src/EngineCommon/EngineCommon.Graph.Tests`, both in `GameFrameWork.slnx`. Used by three engines since: Economy (landed-cost field and routing, pass 41, reweighting in pass 44), WorldEvents (region graph and spread, pass 42) and Relationship (communities, standing, balance, pass 43). Pass 44 added `CsrGraph.Reweight` (section 9).

## 1. Contract

Pure library, `net8.0`, checked arithmetic, no reference to any other project. Everything is integer: node ids `int`, weights and capacities `long`, products and totals that can pass 64 bits are `Int128`. No float, no clock, no random source, no file access, no `throw`, no hash-ordered collection (guards in section 7).

| Piece | Entry point | Result (explain-friendly) |
|---|---|---|
| Graph | `GraphBuilder.Create` / `Add` / `AddUndirected` / `Build` -> `CsrGraph` | edges sorted by (from, to, weight, capacity); edge id = CSR position; the topology is immutable, the weights can be replaced as a set by `CsrGraph.Reweight` (section 9) |
| Reusable memory | `GraphScratch` | none; grows only when a bigger graph comes |
| BFS / k-hop reach | `Bfs.Run(graph, sources, maxDepth)` | hops, parent edge, visit order |
| Weak components | `Components.Run(graph)` | component = lowest node id in it, count |
| Shortest paths, weights >= 0 | `Dijkstra.Run(graph, source, target, scratch)` | distance, parent edge, settle order, `PathTo` (nodes, edges, cost) |
| Shortest paths, any weight | `BellmanFord.Run(graph, source, scratch)` | same tree, or the negative cycle (nodes, edges, cost) |
| Min-cost max-flow | `MinCostFlow.Run(graph, source, sink, maxFlow, maxAugmentations, scratch)` | flow, cost (`Int128`), flow per edge, every augmentation (amount, unit cost, arcs), `Decompose` into paths |
| Label propagation | `LabelPropagation.Run(graph, initialLabels, maxRounds, scratch)` | labels, community per node, rounds, changes per round, `Support` (agreeing neighbours) |
| Trust iteration | `TrustIteration.Run(graph, preTrust, alpha, maxIterations, epsilon, scratch)` | scores summing to Scale, iterations, residual, local trust per edge, `Contributors` |
| Topological order | `TopologicalSort.Run(graph, scratch)` | order (lowest index first), or the cycle (nodes) |
| Degree centrality | `DegreeTable.Run(graph)` / `Top(measure, k)` | in/out degree and weight, top-k nodes |

Every result has `Code` ("" accepted) and `Explain()`. A bad input is refused with a named code from `GraphCodes` (26 codes, each reached by `RefusalMatrixTests`; none is swallowed or turned into a default). Limits (`GraphLimits`): 200,000 nodes, 2,000,000 edges, |weight| and capacity <= 10^9, 1,000 rounds, 10,000 iterations, 1,000,000 augmentations, top-k <= 1,024. They are chosen so the arithmetic cannot overflow: a path is at most 2x10^14, and a flow's cost is `Int128`.

Directed edges; an undirected link is two edges (`AddUndirected`). Parallel edges and self-loops are allowed (a self-loop carries no flow and no shortest path). Flow needs cost (weight) >= 0 and capacity >= 0; Dijkstra needs weight >= 0 (`GRAPH_NEGATIVE_WEIGHT` otherwise; use Bellman-Ford).

## 2. Determinism rules

1. The graph is canonical: edges are sorted by (from, to, weight, capacity); two edges equal in all four are interchangeable. The same edges added in any order give the same graph (tested with shuffles).
2. Node ids are the caller's responsibility: engines sort their own string ids ordinally and map them to 0..n-1. Algorithms that depend on node order (Dijkstra tie-break, label propagation, rounding) depend on the node numbering, not on insertion order. A renumbering can change a tie-broken answer; it cannot change a distance, a flow value or a flow cost.
3. **Dijkstra tie-break.** Binary heap on (cost, node index): smallest cost first, lowest node index among equal costs. A node's parent is the first edge, in pop order then CSR order, that reaches its final cost; a later edge replaces it only when strictly cheaper. Settle order is recorded.
4. **Bellman-Ford.** Up to V passes in node order, edges in CSR order, stopping at a pass that changes nothing. A change in pass V means a reachable negative cycle: the first node relaxed in that pass is walked back V parent steps, the cycle is read off and rotated to start at its lowest node id. No distance is final then.
5. **Min-cost flow.** Successive shortest paths with node potentials; Dijkstra on reduced costs with the same (cost, node) tie-break; residual arcs of a node list its forward arcs before its reverse arcs, each by ascending edge id. Potentials rise by min(distance, distance to sink).
6. **Label propagation.** Asynchronous, ascending node order within a round, a change is seen at once by later nodes. A node adopts the label with the greatest positive-weight support among the nodes it points at; if its own label is among the heaviest it keeps it, otherwise the lowest heaviest label; no positive edge, no change. No random order and no random tie-break (the textbook version has both). No RNG at all, so no seeded roll is needed here.
7. **Trust iteration rounding.** Floors, then the units lost go one each to the entries with the largest remainder, ties to the lowest index; the same rule for every row of the local-trust matrix and every iterate, so every row and every vector sums to exactly Scale (10^9).
8. No iteration over a hash container anywhere; the only sorts are on keys that include the index.

## 3. Algorithms, complexity, bounds

| Algorithm | Time | Memory beyond the result | Stated bound |
|---|---|---|---|
| Build | O(E log E) | O(E) | once per graph |
| BFS | O(V + E) | none | `maxDepth` hops |
| Components | O((V + E) alpha(V)) | none | |
| Dijkstra | O((V + E) log E) | heap <= E + 1 entries | |
| Bellman-Ford | Θ(V·E) worst | none | refused when V·E·max|w| could pass half the long range (`GRAPH_BELLMAN_FORD_TOO_LARGE`); the 10,000 x 200,000 x 10^9 corner is 2x10^18, inside |
| Min-cost flow | O(augmentations x (V + E) log E) | O(V + E) residual | `maxAugmentations`; `Truncated` says the limit stopped it |
| Label propagation | O(rounds x E) | O(V) | `maxRounds` |
| Trust iteration | O(iterations x (E + V log V)) | O(V + E) | see below |
| Degrees | O(V + E); top-k O(V·k) | | k <= 1,024 |

**Trust iteration.** `t' = (1 - a) C^T t + a p`, C the edge weights with negatives clipped to 0 and each row normalised to Scale, a node with no positive edge trusting by `p` (its row is `p`), `a` in [0, Scale] the pre-trust weight, `p` the pre-trust vector (empty means uniform, Scale/V with the remainder to the lowest indices), start `t = p`. Products are 128-bit; every iterate is the floor of the exact `Int128` numerator over Scale^2 plus the remainder rule above. *Convergence argument (mine, not a cited theorem):* `C^T` preserves the L1 norm of a distribution, so the exact map contracts L1 distance by `(Scale - a)/Scale`; for `a > 0` the residual falls geometrically until it meets the rounding floor, at most about `2·V·Scale/a` parts (for `a = 0.2`, ten units per node). The tests assert `residual(k) <= 2·Scale·0.8^(k-1) + 10·V` on random graphs. With `a = 0` there is no such guarantee (EigenTrust needs an aperiodic, strongly connected matrix) and the run simply stops at the limit with `Converged = false`.

## 4. What was researched (2026-10-29)

Fetched and read (the fetch tool summarises with a small model; I did not see raw text):
- **Bellman-Ford** (Wikipedia): source distance 0, relax every edge |V|-1 times, one more scan detects a negative cycle reachable from the source and predecessor links locate a vertex on it; O(|V|·|E|). Implemented as stated (the cycle walk is the standard V-step predecessor walk).
- **EigenTrust** (Wikipedia): local trust `s_ij = sat - unsat`, negatives clipped, rows divided by their sum, iterate `t' = C^T t` from uniform until the change is small. The page has **no pre-trusted peers term**; the `a·p` mixing, dangling rows trusting `p`, and the largest-remainder fixed point are my design (the review's section 3 G already flagged pre-trust as not read).
- **Minimum-cost flow** (Wikipedia): the page does not define successive shortest paths nor integrality. The SSP method with node potentials and reduced costs, and "integer capacities give integer flows", are my knowledge of the standard method (Ahuja, Magnanti, Orlin); correctness here rests on the brute-force oracle tests, not on a cited page.
- Label propagation: from the review (section 3 G, R1): the textbook version shuffles order and breaks ties at random and so has no unique answer; this library fixes both. Nothing new was fetched in this pass.

## 5. Measured scale

Release, this machine, median of 15 repeats after a warm-up, three runs; full rows in `docs/measurements/graph-scale-2026-10-29.csv` (written by `ScaleTests`). Medians over the three runs (ms):

| Case | Run 1 | Run 2 | Run 3 |
|---|---|---|---|
| Build 10,000 nodes x 20 edges (200,000 edges) | 83.4 | 68.3 | 59.9 |
| Dijkstra full tree, same graph | 10.6 | 10.4 | 10.5 |
| Bellman-Ford full tree | 6.4 | 3.9 | 3.7 |
| BFS full reach | 0.69 | 0.46 | 0.43 |
| Components | 0.76 | 1.17 | 0.72 |
| Degrees plus top 10 | 1.23 | 0.82 | 0.80 |
| Min-cost flow, 200 markets, 400 routes plus 40 sources and 40 sinks (flow 1,794, 56 augmentations) | 0.55 | 0.35 | 0.31 |
| Label propagation, 1,000 actors x 20 edges (10 planted groups, 5 rounds, 7 communities) | 0.72 | 0.49 | 0.51 |
| Trust iteration, 1,000 actors x 20 edges, a = 0.15, 16 iterations | 18.4 | 7.9 | 7.5 |

Allocation after warm-up with a reused scratch and result: Dijkstra, trust iteration, BFS, components, Bellman-Ford: 0 bytes (asserted). Label propagation: 176 bytes (the per-round change counts). Min-cost flow: 9,856 bytes (its result and augmentation log; it is the one that allocates by design). The budgets in `ScaleTests` are about ten times the medians and are checked in Release only. Not measured: graphs at the 200,000-node / 2,000,000-edge limits; other machines; Debug.

A note on the review's estimates: it estimated label propagation at under 2 ms and trust iteration at 1 to 2 ms for 1,000 x 20; label propagation measured 0.5 ms; trust measured 7.5 to 18 ms because each iteration also sorts the remainders (V log V) to make the sum exact, so a 12-iteration standing run costs about 6 ms, not 1 to 2.

## 6. Tests

`EngineCommon.Graph.Tests` (xunit v3): hand-built graphs with worked answers (a 5-node Dijkstra, a 4-node flow with value 3 and cost 10, two triangles joined by a bridge, a 2-node trust fixed point solved on paper), plus independent oracles on fixed-seed (splitmix64) random graphs: reachability and hop distance by transitive closure for BFS and components, Floyd-Warshall for Dijkstra and Bellman-Ford distances and for negative-cycle presence, brute-force enumeration of every integer flow (capacities <= 2, <= 7 edges, 400 graphs) for min-cost flow value and cost, a dense `BigInteger` re-implementation of the stated trust arithmetic (exact equality on 150 graphs), and `LINQ` sorts for degree ranks. Properties: Dijkstra = Bellman-Ford = Floyd-Warshall; the same edges in any order give the same graph, tree, settle order, communities; flow conservation, capacity and cost recomputed from the edge flows; successive augmentation costs never fall; decomposition rebuilds the edge flows; a label-propagation result is a fixed point of itself and every node holds a label with maximal neighbour weight; trust scores sum to exactly Scale and rows to exactly Scale; the residual bound above. Limitation, stated: label propagation is **not** invariant under renumbering the nodes (the visiting order is the node order), so the permutation property tested is edge insertion order, not node relabelling.

Mutation check (2026-10-29): 261 operator mutants of the library (a deterministic sample of 504 candidates), runner outside the tree, files restored and checked by SHA-256 after each, the unmutated tree passing first. 229 killed (14 by timeout), 1 did not compile (not counted), 31 survived. 19 survivors were judged equivalent or unobservable one by one; 12 exposed missing tests (refused results not cleared, BFS source range, decomposition cancelling at the smallest flow and never reading the next node's edges, an undirected add at the edge limit, contributor amounts, a wide row of trust) and were closed; on re-run 11 died. One survives: which node of the last Bellman-Ford pass names the reported negative cycle. Seven source mutants injected for the guards: six killed, one did not compile.

## 7. Guards

`SourceGuardTests` run the shared Roslyn rules (`EngineCommon.TestSupport`) over the library: no clock or random source or float or file access, no catch, no throwing catch-all, no `ArgumentOutOfRangeException`, no `?? throw`; and, stricter, no `throw` at all and no `HashSet`/`Dictionary`/`Hashtable`/`SortedSet`/`ConcurrentDictionary`. Each rule is shown to fail on the library's own text with a forbidden word added. While writing it the guard found a **blind spot in the shared `SyntaxGuards.ForbiddenNames`**: it matched only plain identifiers, so `new HashSet<int>()` (a generic name) was invisible; it now matches generic names too. `DuplicationGuardTests` already scan every non-test project under `src/EngineCommon`, so the new project is covered by the one-locale-owner and one-channel-registry rules, and now also appears in its list of projects the scan must see.

## 8. Not done in this pass

- (Superseded: Economy, WorldEvents and Relationship now use the library; see the plan file, passes 41 to 44. Diplomacy, Combat and the remaining items of the review do not yet.)
- No betweenness centrality, triangle counts, Leiden/Louvain, multi-commodity flow, network simplex or cost scaling (the review defers or does not need them).
- No graph serialisation: the library builds from an engine's own data and has no save format of its own.
- Not measured at the stated limits, on other hardware, or under Debug.

## 9. Reweighting a graph (pass 44, 2026-10-29)

`CsrGraph.Reweight(ReadOnlySpan<long> weightsByInput)` replaces every weight and keeps the topology, for callers that run the same shape of graph many times with different costs (the Economy's landed-cost field runs one per good over the same markets and routes). `weightsByInput[n]` is the weight of the n-th edge **added to the builder**, whatever the edge ids have become, so the caller needs no knowledge of the CSR order. The graph keeps the permutation the builder's sort produced (one `int` per edge) for this.

Contract: after the call the graph is exactly the graph a fresh build with those weights would give: parallel edges (same source and target) are put back in (weight, capacity) order by an in-place insertion sort of each run (the sort key of the build; runs are short, and a run of one edge costs nothing), so edge ids, Dijkstra's tie-break and every result agree with a fresh build. `HasNegativeWeight` and `MaxAbsWeight` are recomputed. A wrong length (`GRAPH_WEIGHTS_LENGTH`, the 25th code) or a weight outside +-`MaxWeight` (`GRAPH_WEIGHT_RANGE`) is refused before anything is changed. No allocation. Edge ids held from before a reweight are not valid after it, and a graph must not be reweighted while another thread reads it (the type was immutable; it is now immutable except for this call, and the doc comment says so).

Tests (`ReweightTests`, plus the new code in `RefusalMatrixTests`): the oracle is a fresh build from the same edges with the new weights, compared edge by edge, by offsets, by the summary properties and by Dijkstra's whole tree, on 300 fixed-seed random graphs with parallel edges and self-loops, six reweights each (half of them with weights in 0..3 so ties between parallel edges are everywhere); insertion-order independence; zero allocation over 20 reweights of a 1,500-edge graph; negative weights; empty graphs; refusals change nothing; the parallel-edge reorder by hand. Mutation check in the plan file (pass 44).



## 10. Topological order (pass 46, 2026-10-29)

`TopologicalSort.Run(graph, scratch, reuse)` (file `TopologicalOrder.cs`, result `TopologicalOrderResult`) orders the nodes so that every edge points from an earlier node to a later one. It is Kahn's algorithm with the stated tie-break of the rest of the library: among the nodes whose predecessors are all placed, the **lowest node index goes first** (the same (cost, node) heap the shortest paths use, with every cost 0), so the order is the same for the same graph whatever order the edges were added in. A parallel edge counts once per edge toward the in-degree, so it is released once per edge. O((V + E) log V). Added for the Economy's production chains (work item 7), which needs the order **and** a named cycle, nothing more; no other piece of the library changed.

When nodes remain unplaced the graph has a cycle: the code is `GRAPH_CYCLE` (the 26th code), `Order` holds the nodes placed before the sort stopped, and `Cycle` names one cycle in edge direction, starting at its lowest node (a self-loop is a cycle of one). Every unplaced node has at least one unplaced predecessor, so walking back from the lowest unplaced node along the first unplaced predecessor must repeat a node; the repeated stretch is the cycle (reversed to edge direction and rotated). A null scratch is refused with `GRAPH_ARGUMENT_MISSING`. Reusing a result and a scratch allocates nothing after warm-up (asserted).

Tests (`TopologicalSortTests`, 6, plus two lines in `RefusalMatrixTests`): a hand-worked graph (3, 0, 1, 2, 4); the empty graph; a self-loop and a two-node cycle with their text; a cycle behind an acyclic prefix; 600 fixed-seed random graphs (half forward-only so both kinds are common: 150 or more acyclic, 100 or more cyclic are asserted) against two independent oracles, a Warshall transitive closure for "has a cycle" and a quadratic "lowest ready node" simulation for the exact order; every edge goes forward; a reported cycle is made of real edges, has distinct nodes and starts at its lowest; every placed node is neither on a cycle nor downstream of one; the same edges in another order give the same order and cycle. Mutation check: plan file, pass 46.

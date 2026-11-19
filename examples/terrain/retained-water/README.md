# Retained water and lake-level geometry

This optional capability extends `WorldWaterPlanner`; streams and retained bodies
share one source/replay/apply boundary. No city, artwork or game runtime is
required. The ten JSON files are actual C# drainage/water snapshots, not mockups.

Declare `WorldWaterDefinition.RetainedBasins` with a unique body ID, a retained
drainage terminal, purpose/source rule, level, boundary and inundatable terrain
kinds. `MinimumReceived` compares the selected terminal's receipt in the declared
drainage rate unit. It does not convert that rate into volume or choose a level.
An explicit level can describe a standing game-water body even without ongoing
inflow; there is no implicit ocean, carving, filling or discarded terrain.

The existing terrain-region owner extracts the strictly positive-depth connected
component containing the terminal. Other below-level depressions remain outside
that body. A zero-depth plateau or saddle is dry and cannot connect lakes. The
smallest representable positive depth expresses that strict constraint through
the existing field/region owners; this is not a new placement lattice.

`RequireTerrainShoreline` is the default policy. The original terrain must support
every boundary span at the declared level; open or unavailable boundaries refuse.
`ClipToBoundary` explicitly permits clipping at design/domain/void boundaries and
retains `BoundaryClipped`, unresolved-span and maximum-deficit witnesses. A clipped
body is not certified as a naturally closed lake. `ShorelineTolerance` is explicit
height precision (default 1e-8, permitted 0..0.001 in source units); it does not
change levels or terrain. Zero tolerance may refuse floating-point contour cases.

The output retains each bed face's original plane, depth extrema and geometric
capacity. Capacity is the affine integral of positive depth, in the source length
unit cubed, not a dynamic water budget. New occupied patches use the declared
constant level and zero gradients. Original heightfield/overlays remain available
in the drainage source. Land suitability/placement and water/land route profiles
query the actual occupied surface. Lake faces override stream faces at their
overlap while preserving both provenance records. Positive overlap between
distinct retained bodies refuses.

The shared four-ULP event convention resolves calculated contour seam identity
before publishing patch vertices. Original source coordinate anchors remain
distinct. Topology is rebuilt and all selected cells must remain in the body;
collapsed positive cells refuse. This is bounded floating-point geometry, not an
exact-arithmetic guarantee for arbitrary scales or sub-resolution features.

The ordinary water maximum-patch/work limits remain aggregate constraints; at
most 16 retained bodies may be declared. Dense or extensive lakes may exceed
those finite limits. They do not trigger a coarse mask or dropped-cell fallback.

Cases include an analytic V-basin, a higher level, explicit boundary clipping,
mixed streams/lake, a raised island, a protected void, distinct basins, a
zero-depth saddle and translation. The island case preserves the existing
sample-graph refusal without an explicit terminal for its isolated sampled top;
the accepted fixture declares that terminal without modifying terrain. This is
not a claim that finite sampled drainage proves continuous physical hydrology.

An independent rational native-triangle/edge oracle checks the saved areas,
capacities and connected selections. It also checks the larger protected-ridge
level edit. The latter retains seven complete compounds, 16 landscape members
and 82 directed paths (59 nodes/58 physical members), original terrain, minimum
area and budgets through clean generation, publication and checkpoint replay.

See [automatic metadata example](../../metadata/retained-water/README.md),
[verification](../../../docs/evidence/2026-10-29-retained-water-verification.json),
and [S02](../../../docs/world-generation/stages/s02-terrain-and-spatial-domains.md).
Wider terrain/domain/scale/design qualification and dynamic storage/outflow
simulation remain open; artwork and Godot remain deferred.

Concept reference: [Azgaar's feature documentation](https://github.com/Azgaar/Fantasy-Map-Generator/wiki/Geographical-Features-Overview)
distinguishes enclosed lake water from water reaching the map border (checked
2026-10-29). This framework uses its own fixed-level, protected-source contract
and shared geometry; no third-party implementation or dependency was adopted.

The current snapshots use shared exact convex-face merging. Adjacent faces merge
only when terrain provenance, terrain plane, all environmental affine values and
profile membership match exactly. The small analytic basin now has four bed
faces with unchanged area, capacity, source terrain, inflow and level. Different
planes, islands, voids and strict dry seams remain distinct. All ten snapshots
were regenerated through the current C# owner with unchanged source definitions.

# Physical fenced compound

This framework example composes a boundary from ordinary game-authored catalog
parts. There is no special fence/city generator and no artwork or Godot dependency.
The C# engine generates the source region and gate approach, then positions and
orients every panel/post from the accepted perimeter using the existing
`GeneratedBoundary` population grammar.

The complete program has67 metadata nodes and63 physical sources:30 panels,
31 posts, a compound reservation and a gate approach. Every panel is1.8 ×0.2m;
every post is0.2 ×0.2m. Counts and dimensions are authored, not inferred from a
generic label. The20 ×12m region supplies a64m perimeter, with one4m interval
between gate-edge post centers. The actual clear opening is3.8m; its declared
access interface reserves3.6m. The compound reservation is24 ×16m.

Two explicit perimeter spans leave that gate open. `Outside` with zero offset
allows the fence thickness to straddle the region boundary; the complete source
footprints must still fit inside the compound and avoid other content. Panels and
posts are declared solids. The gate marker has no invented collider.

The required journey runs from inside to outside and back at0.6m actor radius.
All fence solids participate in the normal route query. Closing the gate is an
explicit negative control: the barrier conflicts with the reserved gate approach,
and a separate swept-path check confirms that it blocks the actual journey.
A2m-radius actor also refuses. Failed edits preserve the accepted world/history.

`recipe.json` contains its automatic `PopulationSource`; the separate source files
make the authored base phase reviewable. The CLI consumes the recipe/catalog via
`generate-metadata` with a new output directory. Saved source-boundary/facing
witnesses, full geometry, routes and input hashes remain inspectable in the packet.
Translation moves the whole program while retaining every part.

This qualifies one complete authored boundary assembly and static traversability.
It does not automatically calculate counts for arbitrary perimeters, create gate
animation/opening interactions, or certify a complete game or3D physical model.

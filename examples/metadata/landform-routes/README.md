# Generated ridge-pass counterfactual

Both inputs use the same complete expedition catalog: two outposts, each with a
shelter, outdoor yard and solid supply cache. All eight physical members keep
their source dimensions, parent containment and fixed anchors. The root makes
nine metadata nodes. Three required bidirectional connections use six physical
paths, with a 0.6m passage width and maximum slope 0.2.

The only input difference is the two ridge control paths. They leave a flat pass
centered at y=12 or y=28. The engine generates a 29×21 heightfield over the same
irregular domain. No route waypoints or manually authored pass connections are
provided. The shared route planner derives feature bends from impassable terrain
triangles and validates each proposed edge against the complete swept width.
Imported heightfields use the same search capability. Routes retain free-angle
segments; terrain sample cells are not roads or settlement blocks.

The two fixed outpost anchors are (10,20) and (46,20). Required content is expanded
and physically allocated anew; this example does not qualify automatic settlement
topology or incremental dependency invalidation. The earlier compound experiment's
local partition and shared-border constraints are not part of this terrain-route
experiment. Both counterfactual inputs have identical scope and constraints.

Run the public `generate-metadata` command with either recipe/catalog pair and a
new output directory. The retained packets are `generated/landform-north-pass`
and `generated/landform-south-pass`. Their JavaScript schematics display saved
engine coordinates. Tests: `--world-landform-routes`, the JavaScript landform test,
and `docs/evidence/verify_landform_routes.py`.

The graph remains a finite search representation, with explicit candidate,
edge and work limits. A disconnected candidate graph is not a proof that no
continuous route exists. Complex terrain may exceed the declared budget; no
silent simplification, random search, forced grid or legacy generator is used.
Artwork, population and native Godot acceptance remain outside this example.

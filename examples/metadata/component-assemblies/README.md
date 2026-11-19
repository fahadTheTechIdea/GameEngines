# Catalog-owned component identities

This framework source expands two assemblies with two moving parts each: seven
nodes in total, with two `StaticAssembly` and four `MovingPart` declarations.
The ordinary `generate-metadata` API/CLI consumes `recipe.json` and `catalog.json`.
No game setup, mockup, artwork or coordinate layout is required.

`WorldMetadataArchetype.Component` owns the role, purpose and source rule. Every
expanded `WorldMetadataNode.Component` retains those exact facts. A moving part
must have a real parent; invalid roles and missing purpose/provenance reject.
Roles are independent of semantic kind and do not replace any required child,
physical footprint, access port or surrounding ground area. Source replay rejects
changed or missing component facts in a published world.

These declarations identify separate components for downstream consumers. They
do not define a motion curve, joint, animation, physics or animated swept
clearance. Physical generation still qualifies the declared static source pose.
Godot and artwork remain deferred.

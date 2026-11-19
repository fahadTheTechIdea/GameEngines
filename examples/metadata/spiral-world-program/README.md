# Spiral physical program

This game-independent fixture uses an explicitly authored expanding spiral to
place four solid game locations on a terrain surface. Source footprints, access
ports, collision and the required bidirectional journey are checked by the
existing physical owners. The engine places each required member; it does not
scan vacant cells or add decorative objects.

The spiral declares its center, starting radius, radial step and signed angular
step. A positive radial step expands; a negative step contracts as long as every
requested radius stays nonnegative. The metadata `spiral` layout can also use
two selected axes in higher-dimensional metadata. Physical population is 2D;
optional `Facing` can follow or turn relative to the continuous spiral tangent.

Use the recipe and catalog with `WorldMetadataGenerator` or `generate-metadata`.
This small fixture is not a complete game, art set or runtime integration.

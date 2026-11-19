# Protected terrain features

The two recipes share authored ridge, valley and landing paths. Each operation
declares a 4 m `DetailProtectionRadius`; the outer `Radius` controls the smooth
transition to ordinary procedural detail. Changing the detail seed varies
surrounding terrain while generated lattice samples within a protected corridor
retain the authored landform result.

Use `WorldMetadataGenerator` or `generate-metadata` for either recipe, and
`ReadPublishedAsync` or `verify-metadata` to replay the published source package.
This is an engine-independent terrain example, suitable as a layer in different
game worlds. It does not contain a complete game or visual assets.

The guarantee applies to generated samples. An interpolated point near the edge
can also depend on neighboring samples outside the corridor. Overlapping corridors
use the strongest protection. Radius must be positive and smaller than the
landform radius, and the terrain recipe must declare detail.

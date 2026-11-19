# Authored landform falloff

These game-independent recipes use the same terrain, physical site and required
bidirectional route. They differ only in the landform's `Falloff`: `Linear` or
`Quadratic`. The choice changes terrain samples and route costs; it does not
change the declared source dimensions or required content. `Smooth` is also
available and is the behavior when the property is omitted.

Use these files with the C# `WorldMetadataGenerator` or the `generate-metadata`
CLI operation. They demonstrate one small physical world, not an entire game,
visual art or runtime integration.

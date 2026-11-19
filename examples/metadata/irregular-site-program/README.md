# Irregular reserved-site program

This framework example declares one 8m × 10m solid module and two required
1m × 1m physical fixtures in its rear yard. The parent reserves a convex irregular
330m² plot. The engine calculates four declared surroundings: a 1.5m-wide entrance,
forecourt, rear work yard and side buffer. Each band retains its purpose/source
rule and clips to the actual inset plot. The optional cooling plant is explicitly
omitted because its full frontage depth does not fit.

There are four metadata nodes, three physical bodies and four ground features.
Grounds remain separate from physical members; the forecourt and entrance share
intentional circulation. Ground-feature sums are not populated-world coverage.
No source body, required child or access width is shrunk to fit the plot.

`recipe.json` and `catalog.json` are reusable current inputs. The 30-degree frame
rigidly rotates the complete source, plot, features and declared fixture sockets.
Use `WorldMetadataGenerator.Generate`/`GenerateFilesAsync`, or the existing
`generate-metadata` command. `edited-catalog.json` enlarges the actual plot to
370m² while retaining every source and nested part. The normal metadata session
handles edits, clean-generation equality, rollback, publication and history.

The ordinary catalog/placement declarations provide this program; no game name,
city model, mockup, artwork setup or Godot dependency is required. Actual qualified
packets are under `generated/irregular-sites-public`. Four rotations and the
edited state match SDK output. The prior engine refuses these irregular plots;
existing rectangular output remains exact. An excluded point outside the actual
plot but inside its bounds leaves all source geometry unchanged; a required-yard
exclusion rejects.

Supported site boundaries are convex, with a centered entrance on a Y extremum
before rotation and fixed rectangular source bodies. Concave plots, insufficient
frontage/access/depth, extending the fixed irregular reservation, and oversized
required children reject explicitly. Geometry work and cancellation are bounded.
General concave/irregular-building grammars and full game-world design remain
open; browser review, artwork and Godot are outside this qualification.
See [S06](../../../docs/world-generation/stages/s06-nested-content-and-surroundings.md)
and [the exact evidence](../../../docs/evidence/2026-11-01-irregular-site-grammar-verification.json).

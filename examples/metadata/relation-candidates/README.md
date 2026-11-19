# Analytic relation placement

Game-independent framework reference: two complete required catalog members
must each be exactly 5m from two authored anchors, with 1m envelope clearance.
The C# engine derives their positions `(3,-4)` and `(3,4)` from the constraints.
The catalog defines the 0.4m-square sources once; the request does not duplicate
or shrink them. No mockup, city subsystem, grid or game-specific code is required.

```powershell
dotnet run --project src/WorldCreator/WorldCreator.Cli -- generate-metadata examples/metadata/relation-candidates/recipe.json examples/metadata/relation-candidates/catalog.json generated/my-relation-world
```

Use `changed-anchor.json` for the counterfactual: the second anchor moves from
`(6,0)` to `(8,0)`, yielding `(4,-3)` and `(4,3)` with unchanged members/distances.
Metadata, SVG and the common JavaScript schematic are exported. C# owns the
calculation and typed replay. JavaScript presents saved geometry.

[Qualification, current source identities and limits](../../../docs/evidence/2026-10-30-relation-candidates-verification.json).
This is bounded analytic candidate search for fixed anchors. [Provisional relations](../provisional-relations/README.md) also use the shared owner.
Continuous optimal packing and the broader terrain/gameplay matrix remain open.
Godot and artwork remain deferred.

# Complete source-arrangement coordination

Framework reference for route-dependent content, independent of any game genre.
The source declares two exact 2m-square reservation sites and a connection.
The complete consumer adds one required 0.4m-square solid feature, with a 0.6m
reservation, 3m along the generated route and 2m to its left. Every member needs
1m envelope clearance. No catalog count, size, route radius or offset is relaxed.

The initial sites at `(10,5)` and `(7,5)` produce a feature too close to the second
site. The coordinator rejects that complete assignment by digest. It retains the
first pose and selects `(1,1)` for the second. The derived route changes; the
feature is then at `(8.070842286246123,1.9539615045991423)`. After commitment,
source members and the selected route remain exact. Two attempts account for
20649 placement-work units within the original 200M ceiling.

```powershell
dotnet run --project src/WorldCreator/WorldCreator.Cli -- generate-metadata examples/metadata/complete-source-coordination/recipe.json examples/metadata/complete-source-coordination/catalog.json generated/my-complete-source-reference
```

The output contains metadata, SVG and a JavaScript schematic. The C# engine
calculates geometry; JavaScript consumes it. Version-3 coordination receipts
replay original full inputs and guards. A separate test rejects one complete
18-member assignment across six repeated compounds, retaining partial poses.

[Verification and limits](../../../docs/evidence/2026-10-30-complete-source-coordination-verification.json).
This qualifies bounded candidate search, not continuous optimal packing or a
finished professional generator. Godot and artwork remain deferred.

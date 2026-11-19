# Environmental fields and point decisions

This example combines the generated landform surface with four authored observation points. C# derives elevation, slope, an authored temperature gradient and depth below a declared water level. A reusable resting-location profile requires dry, gentle ground and separately prefers a temperature target. The formulas and thresholds are game design inputs, not a physical simulation.

The current example has one eligible point and three rejected points. `RequireEligible=false` deliberately retains all observations for inspection. Selecting a marker shows exact values and rule decisions; green/red markers represent point checks only. No point was moved to improve its score, and no site footprint or placement was approved.

To require all selected points to pass, the input rule uses `RequireEligible=true`; the engine then rejects this example before publishing output. The normal CLI audit records that failure. The source recipe/catalog remain reusable:

```powershell
dotnet run --project src/WorldCreator/WorldCreator.Cli -- generate-metadata examples/metadata/environment-study/recipe.json examples/metadata/environment-study/catalog.json generated/environment-study-review
```

Use a new output directory. The saved plan includes exact observations and canonical definitions; read/export rederives them and rejects stale or altered decisions.

[API and limits](../../../docs/WORLD_METADATA.md#environmental-fields-and-point-suitability). [Verification](../../../docs/evidence/2026-10-26-world-environment-verification.json). Whole-footprint scalar constraints, purposeful destination/region placement, required nested content, hydrology where requested, approved artwork and native Godot remain unfinished.

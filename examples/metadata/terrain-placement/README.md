# Terrain-driven placement

This fixture asks the C# engine to place two required exploration sites inside an irregular region. Their positions are absent from the recipe. Each has a fixed polygon, dry/gentle terrain requirements and an altitude preference; a distance relationship and footprint separation govern the pair. A replayable ridge heightfield supplies terrain. The engine chooses qualified candidates and backtracks when required demands conflict.

Each site's catalog program includes two constituent stations. Their declared relative positions resolve after the parent is placed. A terrain-qualified bidirectional connection then joins the generated sites. The resulting packet has seven nodes, two reserved polygons and one generated connection.

Use `generate-metadata` with this folder's recipe/catalog and a new output directory. The current [schematic packet](../../../generated/terrain-placement/schematic.html) shows exact engine output. [Verification](../../../docs/evidence/2026-10-26-world-placement-verification.json) records actual tests, CLI outcomes and browser export.

This is a placement/network fixture, not a complete game or art mockup. Region extent is authored; the finite terrain candidate set is not continuous optimal packing. Constituent points still need physical shape allocation, access and collision checks. Automatic redesign after route failure, junctions, artwork and native Godot acceptance remain open.

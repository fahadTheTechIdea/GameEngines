# Bounded local orientation example

Framework algorithm fixture: two required structures and their four constituents,
not a configured game. Run through the existing `generate-metadata` command with
this directory's recipe and catalog. The C# engine owns all layout and geometry.

The baseline allocation cannot contain the complete 14×6 m structure and 16×8 m
reservation at its fixed site anchor. A declared `GridRotation` dial permits
-10, 0 and +10 degrees about the fixed origin. Baseline-first, nearest-step search
selects -10 degrees, preserving seven metadata nodes, two exact source structures,
all program/site anchors and the physical passage between them. The independent
shared-edge equation is cos(10°)x - sin(10°)y = 20; the first allocation area is
400/cos(10°). Outside wedges retain an explicit terrain-margin purpose.

The engine test also starts at -20 degrees with a positive ten-degree step and
reaches the same -10-degree geometry. Range/target/work/attempt refusal, altered
receipts and session rollback/restoration are tested. This is a local allocation
strategy; worlds are not required to use grids. General strategy synthesis,
large-world performance and visual acceptance remain open; Godot is deferred.

[Verification](../../../docs/evidence/2026-10-29-layout-rotation-verification.json).

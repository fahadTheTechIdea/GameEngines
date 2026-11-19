# Expedition outposts

This reusable metadata example declares two outposts, each with a shelter, an outdoor supply yard and a solid cache nested inside the yard. All eight members have catalog-authored physical polygons; six also have clearance reservations. These are explicit game design dimensions, not measured artwork.

The required program uses terrain-qualified power allocations and bounded influence adjustment. The initial split cannot contain the western outpost reservation; the accepted split moves from x=20 to x=22 without changing either outpost anchor or any source dimension. Internal paths connect shelter and yard entrances. The shared passage connects the nested yard entrances using their inherited allocation ownership and avoids every solid footprint.

The C# test command `--world-compounds` additionally exercises third-level cache endpoints, stage reordering, explicit child allocation overrides, cyclic ownership refusal, oversized required content, missing physical members and saved replay/tamper.

Generated review: `generated/expedition-outposts/schematic.html`. This is a complete declared two-outpost program inside a small surface fixture. It does not certify world diversity, whole-world density/composition, interiors, decorative population, production artwork or a playable Godot world.

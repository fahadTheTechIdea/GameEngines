# Mixed local growth and fixed world intent

These framework inputs generate a connected six-room interior alongside two
independently anchored outside structures. Each outside structure has its actual
2 × 2 meter source footprint and a 1.4-meter named access port. A regional route
or generated network, a catalog-owned entrance and room passages form a continuous
physical journey into the interior. One reusable room program adds two required
equipment members alongside each original fixture.

Each complete case retains **27 nodes and 26 physical sources**: six rooms, six
original fixtures, two outside structures, twelve additional equipment members and
one metadata root. The source phase retains 15 nodes/14 physical sources before
the equipment phase. Original wall, opening, interior, entrance and regional/network
path records remain exact when content is added.

The common growth owner now scopes growth to its root and declared link endpoints.
Other demands in the stage require consistent positive-weight exact anchor
relations. These fixed demands are placed first; grown source polygons must fit
around them. Every declared growth link must remain in the root component.
Missing/conflicting pins, disconnected link components and bodies blocking the
required root refuse. No source rescaling, member dropping, arbitrary relocation
or higher work/search limits are used.

`move-station-edit.json` changes an authored station position from x=28 to x=30.
`blocked-root-edit.json` intentionally places it at the required room-growth root;
that proposal must fail without replacing the accepted world/checkpoint.

The exact same complete input failed on the previous engine with
`WORLD_PLACEMENT_GROWTH_DISCONNECTED`. Current API/CLI world and checkpoint bytes
match. Twelve independent rectangle/candidate oracles verify mixed assignments;
independent saved-vertex graph checks prove forward/return journeys from the
outside station to the final room in four complete packets. Existing all-connected
branching/rotated/compact geometry remains unchanged.

[Verification](../../../docs/evidence/2026-10-30-mixed-growth-verification.json).
This is a bounded physical composition example, not a complete game or proof of
all genres/world scales. Full world design and core gates remain open. Godot,
runtime movement, production art and moving doors remain deferred/unqualified.

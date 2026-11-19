# Layered framework reference

These reusable inland/coastal inputs exercise WorldCreator without artwork or
Godot. They are a framework integration fixture, not a particular game or a
finished game world. Both use the same required catalog and dimensions.

The engine selects a terrain-suitable region and generates three macro spaces
and seven activity spaces from purpose, source rule and minimum-area intents.
No macro/activity anchor coordinates, grids or cut trees are authored. Constituent
program coordinates remain reusable source design: seven compounds each contain
two structures, a yard and two caches. All source members and local/regional paths
survive the downstream landscape population unchanged.

Landscape demand integrates the declared density `1 + 0.002*x` over its generated
support, then uses `floor(mass/250)` members with minimum sixteen. Both variants
produce eighteen; every actual member is accounted for without clamping. This is
not random scattering. The catalog requires the full program; infeasibility
refuses generation rather than shrinking source geometry or omitting parts.

Each result contains 61 nodes, 60 physical members, 35 local bidirectional routes,
six regional edges, 82 directed paths, 42 reachable interaction destinations and
143 canonical chunk members. Coastal terrain changes allocations and paths while
preserving mandatory content identities. Colored allocation boundaries are not
roads; the macro interface declares a shared span, not a physical passage.

Generate through the current CLI's `generate-metadata` command with a variant's
`recipe.json`, `catalog.json`, and a new output directory. The checked outputs for
this qualification are `generated/layered-world/inland` and `coastal`.

[Evidence](../../../docs/evidence/2026-10-29-layered-world-verification.json)
records builds, independent C#/Node checks, edits/rollback/replay, CLI export and
minimum-area refusal. Browser inspection covers initial rendering only; controls,
whole-world scale, genre diversity and owner design acceptance remain open.
The affine terrain/coastal mask is controlled input, not generated heightfield.
Allocated space does not certify occupied-world coverage. Godot remains deferred.

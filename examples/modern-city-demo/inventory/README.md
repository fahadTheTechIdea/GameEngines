# Framework reference-city inventory

This is authored example content for the reusable framework. Counts are a concrete
reference-test programme, not a universal definition of a city or a population claim.
No coordinates, street grid, mockup approval or completed world is inferred.

The existing C# `generate-metadata` command expands `recipe.json` and `catalog.json`.
The existing `structure-pipeline metadata` command replays those exact inputs and
validates `scopes.json`, publishing versioned handoffs. The current file is `art-handoff-v003.json`,
replaying `expanded-v003/world-metadata.json` and `scopes-v003.json`. Commands ran successfully;
logs are in `docs/evidence/2026-10-27-reference-city-{inventory,handoff}.log`.
The current handoff contains 862 members and 23 unchanged reference jobs (15
building types and eight ground-area concepts), now assigned to 22 detail views,
six district overviews and one dependent city overview. Eight authored logical
access intents connect the district graph; they are not physical paths.

| District | Buildings | Ground areas | Nested sites |
| --- | ---: | ---: | ---: |
| garden-neighborhood | 67 | 123 | 61 |
| mixed-center | 65 | 37 | 18 |
| old-center | 60 | 2 | 0 |
| outer-neighborhood | 92 | 104 | 51 |
| riverside | 36 | 50 | 24 |
| work-district | 49 | 12 | 4 |

Building sizes/variants resolve the registered Modern catalog. Single-building
reference scope is explicit where the source family is context dependent.
Production halls are an authored constituent archetype with no invented physical
measurements; their footprint must be calibrated from approved downstream work.
Each of four factory compounds retains two halls, two warehouses, administration,
maintenance, guardhouse, loading yard, utilities enclosure and pedestrian access.
Gardens, entrance walks, courtyards, playgrounds, parks and plazas remain members.

House v003, townhouse v001, apartment v001, clinic v001 and production hall v001
have real individual approvals in canonical GameArts. All 23 concepts now have
reference images; eighteen selected revisions remain pending review. Approval is owned by the lifecycle manifest,
not this document or the draft handoff. District images must resolve every required
member, including compound surroundings, with additional detail views when needed.
The overview indexes the complete city; its natural image resolution is not a
substitute for constituent detail or production pixels per metre.

Next gates: remaining references → districts individually → complete city overview
→ source-annotated, reconciled map → complete asset demand (including newly visible
roads, terrain transitions, vegetation, accessories and moving parts) → canonical
production art → full native Godot TileMapLayers and applicable movement/animation.
No road, terrain, accessory or interaction is certified by this pre-layout inventory.

Apartment, clinic and production-hall v001 are now approved and audited. The source-linked orchestration queue is saved in
`../.gamearts/inputs/reference-execution.json`; it does not grant approvals.

Current handoff is `art-handoff-v003.json`: it retains the hall role correction
and every source node/dimension, adds eight logical district-access intents, and
partitions image ownership into 22 detail views without splitting compounds.
Earlier handoffs/exports are historical evidence, not selected current inputs.

The other ten building candidates and all eight ground concepts have been
generated and registered. Garden v002 corrects its first revision's taper; other
ground candidates retain some widening and are not calibrated geometry.
Reference pixels, metadata hashes and lifecycle states were checked for all
23 current artifacts. Unreviewed references cannot drive district production.

The [draft visual briefs](../visual-plan/README.md) include exact per-view counts,
member identities, reference prerequisites and district/whole-city prompts. The
24-building detail budget is an example presentation choice, not a world/grid
constraint or a guarantee of image legibility. Verify the current inputs with
the example integrity checker; its output is recorded in
`../../../docs/evidence/2026-10-28-reference-city-multiview-verification.json`.

# Research-led game-world generation architecture

> Retired production path (2026-10-26): the owner rejected this city-first implementation. Its runtime generator, contracts, network/district/parcel orchestration and city CLI commands are removed. The content below is historical evidence, not current API instructions. Follow the [world master plan](ADVANCED_WORLD_GENERATION_MASTER_PLAN.md) and [active tracker](ROADMAP.md) for the replacement. No compatibility/legacy generator is retained.

Reviewed 2026-10-26. This is the current architecture decision and research record, not a claim that every stage works. It replaces the earlier realism-first proposal. WorldCreator generates renderer-neutral world metadata and spatial designs; games choose their scale, rules, structure programs and presentation.

## Findings from primary sources

| Source | Finding relevant to this engine | Decision |
| --- | --- | --- |
| [Dormans, Adventures in Level Design, 2010](https://www.pcgworkshop.com/archive/dormans2010adventures.pdf) | Mission structure and spatial structure serve different roles and are generated in separate stages. | Represent gameplay requirements before geometry. A connected street graph alone cannot establish that a game objective is reachable or correctly gated. |
| [Parish and Müller, Procedural Modeling of Cities, 2001](https://cgl.ethz.ch/Downloads/Publications/Papers/2001/p_Par01.pdf) | City generation separates global goals from local constraints and derives streets, lots and buildings in stages. | Keep authored region/district needs separate from geometry feasibility, but solve their assignment together. Do not let local iteration order decide the city's contents. |
| [Vanegas et al., Procedural Generation of Parcels, 2012](https://www.cs.purdue.edu/cgvlab/www/resources/papers/Vanegas-Eurographics-2012-Procedural_Generation_of_Parcels_in_Urban_Modeling.pdf) | Parcel subdivision is a distinct design problem, with street access, user-controlled dimensions and different subdivision styles. It considers skeleton and oriented bounding-box approaches. | Source dimensions, frontage, area and depth must inform subdivision. Complete land coverage alone is insufficient. Do not label a nearest-street partition as this paper's full algorithm. |
| [Shaker and Togelius, Search-Based PCG](https://www.pcgbook.com/chapter02.pdf) | Representations, search and content evaluation are separate concerns. | Hard validity constraints must precede soft composition preferences. Use bounded search with explicit failure, not unlimited retries or success-shaped fallback worlds. |
| [Shaker, Smith and Yannakakis, Evaluating Content Generators](https://www.pcgbook.com/chapter12.pdf) | Evaluation examines the range and quality of generated content rather than one attractive output. | Qualify multiple game-authored profiles and layouts, constraint failures, scale and repeatability. A passing fixture does not prove broad world variety. |
| [Gumin, WaveFunctionCollapse, original repository](https://github.com/mxgmn/WaveFunctionCollapse) | WFC enforces local patterns/adjacency; propagation can encounter contradictions. | Candidate downstream tool for compatible modular tiles. It is not by itself the owner of world hierarchy, global gameplay progression, source dimensions or required-content counts. No WFC dependency added. |

These are algorithmic references, not copied implementations. The decisions below are our engineering synthesis. No single technique above is established as best for every game world.

## Current engine observations

- C# owns reusable catalog expansion, source geometry, city street strategies, parcel compilation and validation. JavaScript renders exported meter geometry; it must not reposition the world.
- Metadata recipes already describe hierarchical locations and connections. The existing city route compiles connected streets, frontage parcels and nested structure programs. Preserve these current capabilities through their existing owners.
- The ordinary-city allocation loop selected each parcel independently using locally eligible weights and counts. It could not enforce district service counts or reserve scarce compatible sites. Replaced by `CityDistrictAllocator`; there is no retained greedy mode.
- Grid block targets and parcel frontage/depth rules are still independently authored. Required-site reservation is compact, but ordinary sites can have large residual land. The recent parcel-ground rules describe that land honestly; they do not establish good density or solve this coupling.
- Current metadata connectivity is not a mission solver. Full multi-region terrain/settlement co-generation, chunk continuity and game-state progression remain unproven.

## One generation pipeline, distinct responsibilities

1. **Game intent and reusable programs.** Select authored world scope, gameplay locations, interactions, mandatory counts, allowed connections, structure composition and optional visual references. Factories, clubs and other compounds use the same nested catalog mechanism; no parent-name-specific compiler.
2. **Logical world topology.** Derive region and location relationships and required traversals from game rules. Distinguish connection kinds and conditional access. Reject unsatisfied gameplay obligations before claiming a usable world.
3. **Spatial feasibility.** Each world-region grammar owns its appropriate geometry: settlements, interiors/dungeons and wilderness need different rules. Reuse current contracts where they express the needed semantics. Adding an implementation must not create a second legacy/current version of the same algorithm.
4. **Joint demand and layout.** Resolve exact source program dimensions first. Subdivision must account for useful frontage, permissible depth/area and intended density. Allocate content across all compatible sites, with hard counts and role restrictions before weighted preferences. Feed infeasibility back to an explicitly bounded design solve; do not enlarge every parcel or disguise residual land.
5. **Hierarchical composition.** Compile exact parent/child structures, working access, grounds, equipment and surroundings. Declared gardens and yards have purposes and ownership. Undeclared land stays measurable; a colored polygon does not count as a physical structure or completed artwork.
6. **Acceptance and export.** Validate geometric containment, occupancy, exact source sizes, movement/access, declared counts and hierarchy; qualify diversity and gameplay requirements separately. Persist reproducible inputs/output/build identity. Use existing API/CLI audit boundaries and renderer-neutral export. Godot and GameArts are downstream integrations when requested.

This ordering is the implementation direction. It does not claim that the unimplemented stages are present.

## Current replacement: district-wide constrained allocation

`CityLandUseRule.MinimumCount` and `MaximumCount` apply to ordinary parcels in each district. Explicit required sites are separately counted and do not consume these quotas. Null maximum permits all available ordinary parcels; zero forbids that type. Weights remain positive relative preferences, not guarantees.

`CityDistrictAllocator` solves a capacitated bipartite assignment between actual candidate parcels and their compatible source types. It first satisfies minima using alternating paths, then allocates remaining capacity by increasing weighted marginal load. An alternating path can move a flexible assignment without changing its source size or decreasing an already fulfilled type count. Types at maximum capacity cannot receive another parcel. Feasibility, search work, cancellation and invalid input have explicit outcomes. Stable IDs, candidate scarcity and ordinal ties remove input-enumeration dependence. The old per-parcel selection loop is removed.

The weighted objective is the sum of squared ordinary counts divided by their declared weights, subject to the hard eligibility and count bounds. Tests independently enumerate every assignment on 7,203 small constrained cases; larger-world optimality is not inferred from timing or selected screenshots. Requested and achieved counts are exported on district metadata. Existing package reproduction verifies the actual generated assignment.

This replaces content allocation, not block subdivision. It cannot repair an impossible street plan, guarantee every world genre, supply gameplay runtime, or prove attractive composition. The active master plan tracks those remaining requirements.

## Acceptance matrix for the wider work

- Dense urban, suburban, village and industrial game profiles must show distinct intentional spatial structures, not recolors of one grid.
- Mandatory content must survive constrained geometry; an impossible request must fail with a useful diagnostic and no published partial package.
- Nested compounds must preserve every declared member, grounds and access relationship.
- Gameplay graphs require explicit directed/conditional reachability checks; weak connectivity is insufficient.
- Source programs and district density must drive block/parcel design; report physical program occupancy separately from assigned ground area.
- Repeated generation must reproduce the same inputs and build; tests must cover profile variation, rotations, boundaries, budgets and cancellation.
- Native playability, art/mockup fidelity, large-world streaming and human visual acceptance remain separate, unrun gates unless actual evidence is recorded.

No new dependency, installation, cloud build or publication is part of this change.

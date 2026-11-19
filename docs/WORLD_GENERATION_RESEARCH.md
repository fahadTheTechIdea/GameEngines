# Research for a layered game-world generator

## Density-measure decision — 2026-10-31

[Gunzburger's author-hosted overview](https://people.sc.fsu.edu/~mgunzburger/cvt/cvt.html)
defines density-weighted centers of mass and describes spatial allocation uses
(accessed 2026-10-31). The framework reuses its existing affine mass integration
and bounded recursive bisection, rather than implementing CVT or importing code.
Density measures game-authored spatial demand; geometric area, original content
capacity and traversability remain separate hard constraints. Measurement uses
actual terrain/environment pieces, including clamps and dependent fields, and
must cover the entire owned mask. Exclusions remain owned but unavailable for
placement. Unknown terrain has no invented measure. This decision supports S05.5
and S07; it is rationale, not implementation or broad world-design acceptance.

Research date: 2026-10-26. This is the current cross-genre research and design rationale. The [master plan](ADVANCED_WORLD_GENERATION_MASTER_PLAN.md) owns requirements, [ROADMAP](ROADMAP.md#master-stage-tracker) owns stage status, and the linked stage documents own implementation tasks. The existing [city research](CITY_GENERATION_RESEARCH.md) remains an urban-specialist reference; it is not the world architecture.

## Conclusions and proposed architecture

There is no demonstrated single best algorithm for every game world. Use a shared layered world model and interchangeable algorithms selected by game-authored rules and spatial context. Keep gameplay topology, spatial representation, content composition and presentation distinct, with explicit dependencies between them. A city can mix planned grids, winding terrain-following routes, irregular quarters, campuses and open ground. A grid may instead represent tactical cells, dungeon partitions, chunks or terrain samples. Sampling on a grid must not force visible roads or region boundaries into a grid.

Terrain and gameplay intent inform region suitability and connection planning. Content dimensions and access requirements inform partitions before placement. Local layout failures can request a bounded redesign of their owning region; they must not silently shrink structures, relocate objects into arbitrary vacancies, invent a bridge or hide missing scope. Game readability and intended style take precedence over optional engineering simulation. These are proposed engineering decisions derived from the sources, not claims that the complete cited algorithms already exist in WorldCreator.

The owner's layer/color suggestion becomes a first-class requirement: retain independently inspectable semantic layers, their dependencies and versions; provide a colored composite schematic with labels, legends and per-layer toggles. Colors visualize the data and are never the only identifiers or the inputs from which geometry is reconstructed. Generation layers, elevation levels and Godot rendering layers are distinct concepts with explicit mappings.

## Roads across world scales

The owner's latest clarification is a mandatory design constraint: a city, county or country is not generated as a square street lattice. A local planned district may use one. The proposed synthesis first resolves purposeful destinations and terrain-feasible regional connections, then settlement approaches/primary routes, then locally chosen street patterns. Administrative boundaries and storage chunks do not imply road placement. Local urban blocks are derived from the resulting street network only where relevant. A roadless profile has no obligation to create streets or parcels.

R03 supports locally guided mixed street patterns; R04 motivates terrain-dependent connection costs; R05 motivates coupled settlement/road/site planning. Combining them into the hierarchy above is our engineering proposal, not a claim that one paper implements this complete game-world pipeline. See [S04](world-generation/stages/s04-terrain-aware-connections.md) and [S05](world-generation/stages/s05-adaptive-regions-and-partitions.md) for tasks, failure cases and exit gates.

## Algorithm selection

| Problem | Proposed primary method | Alternatives and boundary |
| --- | --- | --- |
| Terrain domains | Authored/generated fields with masks, height/slope queries and explicit feature controls | Heightfields for surfaces; layered/volumetric domains only through declared capabilities. Noise alone does not establish a coherent world. |
| Gameplay structure | Typed graphs and bounded grammar/constraint expansion | A connected spatial graph does not establish keys, progression, safe spawns or traversal feasibility. |
| Routes | Terrain-aware weighted graph search plus local directional guidance | Preserve game-authored anchors; account for route width and crossing levels. Straight lines, grids and radial patterns remain valid where explicitly suited. |
| Region/plot partition | Mix authored grids, oriented splits, polygon/frontage subdivision, graph embedding and constrained region growth | Choose by region purpose and terrain; Voronoi cells or distorted grids are not universal layouts. |
| Nested content | Reusable catalog programs, exact source dimensions, typed ownership and access ports | No per-building-name planner and no arbitrary vacant-cell fallback. |
| Repeated local detail | Explicit boundary/path/region grammars and constrained variants | Optional local adjacency solving; no random filler used to conceal unresolved land. |
| Tile assembly | Deterministic topology-to-tiles mapping; optional adjacency solver for eligible local variation | WFC is not the authority for global missions, source geometry or asset approval. |
| Quality | Hard invariant checks plus declared gameplay/composition metrics and human review | Do not optimize only occupancy or screenshot attractiveness. Report range, rejection rate, failures and missing capabilities. |

## Primary sources

Each entry records the scope actually accessed. Publication years come from the works, not search-engine crawl dates. No library or copied implementation is adopted by this research. Licensing/provenance review is required before importing any code.

## R01

**Constructive generation methods for dungeons and levels**, Shaker, Liapis, Togelius, Lopes and Bidarra, textbook chapter. [Author-hosted chapter](https://www.pcgbook.com/chapter03.pdf). Accessed full PDF text. The chapter distinguishes constructive methods and discusses topology, geometry and gameplay content. **Use:** compare partitioning, cellular and constructive techniques per declared region, rather than treating urban parcels as a universal model. **Limit:** a technique suitable for one level form is not a guarantee for every genre.

## R02

**Adventures in Level Design: Generating Missions and Spaces for Action Adventure Games**, Dormans, 2010. [Workshop paper](https://www.pcgworkshop.com/archive/dormans2010adventures.pdf). Indexed author abstract/text retrieved; direct PDF open timed out during this refresh. Mission and spatial structures are distinguished. **Use:** explicit gameplay/topology stage and later physical realization. **Limit:** do not claim an implementation of the paper or a complete mission solver from current metadata links.

## R03

**Interactive Procedural Street Modeling**, Chen, Esch, Wonka, Müller and Zhang, 2008. [Author-hosted paper](https://peterwonka.net/Publications/pdfs/2008.SG.Chen.InteractiveProceduralStreetModeling.pdf). Full PDF text accessed. It combines input maps, tensor-field design and graph editing to guide street generation. **Use:** local direction fields and mixed, editable patterns. **Limit:** its urban focus does not justify imposing street semantics on all worlds; building generation is not established by the street result.

## R04

**Procedural Generation of Roads**, Galin, Peytavie, Maréchal and Guérin, 2010. [Publisher abstract and supporting-results page](https://onlinelibrary.wiley.com/doi/10.1111/j.1467-8659.2009.01612.x). Abstract accessed. The method optimizes an anisotropic path cost incorporating terrain slope and obstacles. **Use:** terrain-sensitive connections with costs chosen for gameplay. **Limit:** full construction simulation and excavation are not default game requirements; no full-paper reproduction is claimed.

## R05

**Procedural Generation of Villages on Arbitrary Terrains**, Emilien, Bernhardt, Peytavie, Cani and Galin, 2012. [Author-hosted PDF](https://perso.liris.cnrs.fr/egalin/Articles/2012-villages.pdf), [institutional record](https://researchportal.ip-paris.fr/fr/publications/procedural-generation-of-villages-on-arbitrary-terrains/). Indexed author abstract and institutional metadata accessed; direct PDF timed out and the HAL copy denied access. The abstract describes suitability-driven settlement/road growth and terrain-adapted parcels/structures. **Use:** test different layouts for the same program on different terrain and allow content/network planning to influence one another. **Limit:** this is a settlement-specific reference, not a complete all-world solution.

## R06

**Procedural Generation of Parcels in Urban Modeling**, Vanegas et al., 2012. [Author project page](https://twak.org/project/parcels/), [author-hosted PDF](https://www.cs.purdue.edu/cgvlab/papers/aliaga/eg2012.pdf). Project abstract and indexed PDF sections accessed; full PDF exceeded the browser fetch-size limit. Subdivision is controlled by user attributes and styles, with skeleton and oriented-box approaches described in the retrieved sections. **Use:** a specialist partition strategy when frontage and parcels are appropriate. **Limit:** never apply its street-frontage requirements to caves, wilderness or space.

## R07

**Mapgen4**, Amit Patel / Red Blob Games, project published 2018, updated 2023. [Developer's project](https://www.redblobgames.com/maps/mapgen4/), [original source](https://github.com/redblobgames/mapgen4). Project page accessed. Its editable terrain features feed subsequent biome and river generation. **Use:** designer control and dependent terrain layers. **Limit:** not an adopted library, a physical simulation guarantee, or evidence that noise alone creates a playable world.

## R08

**The search-based approach**, Togelius and Shaker, textbook chapter. [Author-hosted chapter](https://www.pcgbook.com/chapter02.pdf). Full PDF text accessed. Content representation, search and evaluation are separate design choices. **Use:** bounded candidate evaluation with explicit hard constraints and recorded soft scores. **Limit:** a weighted score can reward the wrong outcome; roads consuming area must not masquerade as useful content density.

## R09

**Evaluating content generators**, Shaker, Smith and Yannakakis, textbook chapter. [Author-hosted chapter](https://www.pcgbook.com/chapter12.pdf). Full PDF text accessed. It discusses expressive range and player-centered evaluation. **Use:** compare multiple profiles and parameter ranges and include visual/gameplay review. **Limit:** deterministic output and passing geometry checks do not prove useful variety or acceptable design.

## R10

**WaveFunctionCollapse**, Maxim Gumin, original project. [Original implementation and explanation](https://github.com/mxgmn/WaveFunctionCollapse). Repository documentation accessed. It propagates local pattern constraints and may encounter contradictions. **Use:** evaluate only for local compatible tile/module variation after global planning. **Limit:** no assumption of global reachability, counts, terrain suitability or automatic contradiction recovery. No dependency added.

## R11

**Grammars and L-systems with applications to vegetation and levels**, Togelius, Shaker and Dormans. [Author-hosted chapter](https://www.pcgbook.com/chapter05.pdf). Full PDF text accessed. It discusses rule-based structures across vegetation and levels. **Use:** reusable, inspectable composition rules with controlled variation. **Limit:** rules still require geometry, budget and gameplay validation; grammar expansion is not art approval.

## R12

**Godot 4.6 TileMapLayer**, official documentation. [Class reference](https://docs.godotengine.org/en/4.6/classes/class_tilemaplayer.html). Accessed. TileMapLayer contains one tile layer; multiple nodes compose a map. Serialized cell coordinates are limited to signed 16-bit ranges. **Use:** native layers, bounded chunk-local cells and verified coordinate conversion. **Limit:** a tile storage grid does not require a grid-shaped town. Documentation support is not installed runtime acceptance.

## R13

**Godot 4.6 TileSets**, official documentation. [Using TileSets](https://docs.godotengine.org/en/4.6/tutorials/2d/using_tilesets.html). Accessed. Tile resources support rendering plus collision, navigation and occlusion data. **Use:** canonical atlas metadata and native terrain/tile resources. **Limit:** exact seams, pivots, collision and import settings still need real asset/runtime tests.

## R14

**Godot 4.6 navigation meshes**, official documentation. [Using navigation meshes](https://docs.godotengine.org/en/4.6/tutorials/navigation/navigation_using_navigationmeshes.html). Accessed. Navigation considers its own traversable data, independently of visual and physics shapes. **Use:** verify actual navigation and clearance after import. **Limit:** visible roads and collision shapes alone do not establish pathfinding correctness.

## R15

[GameArts integration contract](GAMEARTS_INTEGRATION_CONTRACT.md), current canonical GameArtsSkills mockup/project/storyboard skills and actual lifecycle tools remain the owners for art production. They require truthful profile/approval state, real reference conditioning and deterministic atlas assembly. This plan does not copy their standards or invent approvals. Current `.gamearts/` has experiments and outputs but no root `project.json`; existing experiments are not an owner-confirmed production profile.

## Research-to-stage mapping

R01/R02/R08 inform S01 and S03; R03–R07 inform S02, S04 and S05; R06/R11 inform S06/S07; R08/R09 inform S08/S10; R12–R14 inform S09/S14; R15 governs S11–S14. All stage algorithms are proposed until implementation, appropriate tests and required review evidence exist. Public-release or platform compliance is not inferred from this plan.

## Owner-supplied implementation references — 2026-10-26

### Application decision — refreshed 2026-10-27

### Reference roles and implementation boundaries

Reviewed the supplied official pages/READMEs on 2026-10-27. Acceptance for this research update is a concrete role for all seven references, a stage mapping, and an explicit distinction between documented features and proposed adoption. No third-party application was run and no implementation was imported.

| Reference | Documented capability to study | Proposed WorldCreator use | Boundary |
| --- | --- | --- | --- |
| [Mapgen4](https://www.redblobgames.com/maps/mapgen4/) | Paintable landforms with dependent rainfall, rivers and biomes | S02 terrain constraints and dependent field generation | Terrain mesh cells do not become streets or building plots. |
| [Azgaar](https://github.com/Azgaar/Fantasy-Map-Generator) | Editable fantasy maps; planned separation of state, generators, editors and rendering | S01–S05 semantic world layers and region/connection algorithms; S09 inspection | Its stated future architecture is not an already-completed integration; map symbols are not playable footprints. |
| [Inkarnate](https://inkarnate.com/) | Shape/line tools, textures, stamps and map export | S09 designer controls and readable layer composition | The product page does not establish a reusable procedural engine API. |
| [ProceduralToolkit](https://github.com/Syomus/ProceduralToolkit) | Polygon clipping/offsetting, tessellation, skeletons and building strategies | S05/S06 geometry and nested construction algorithm evaluation | Unity-specific types and dependencies require separate review before headless reuse. |
| [Arnis](https://github.com/louis-e/arnis) | OpenStreetMap/elevation conversion to game worlds | Optional source adapters in S01/S08/S14 | Imported geography is a distinct input route from fictional generation. |
| [Bonsai](https://github.com/scallyw4g/bonsai) | GPU voxel terrain, second-stage decoration and SDF editing | Optional volumetric domains and persistence in S02/S07/S08 | Alpha status and advertised scale are not local performance evidence. |
| [Godot Road Generator](https://github.com/TheDuckCow/godot-road-generator) | 3D road cross-sections, grouped control points and traffic support | Optional S14 realization of S04 connection output | Road meshes do not decide world layout or prove lane continuity. |

Our synthesis keeps world topology independent of its spatial representation. Surface worlds, interiors and topology-only games select the layers they need. Generation dependencies support bounded feedback and protected authored edits. C# owns saved geometry; the JavaScript schematic exposes semantic colors and exact coordinates. Local grids are explicit regional policies. This is a design decision, not a feature claim about any one reference.

### Transfer assumptions to test

Source inspection refreshed on 2026-10-27 for the seven supplied URLs. These are proposed adaptation checks, not newly implemented features:

- **Drainage needs declared sinks.** Mapgen4's [`assignDownslope`](https://github.com/redblobgames/mapgen4/blob/main/map.ts) seeds priority traversal from deep ocean triangles. WorldCreator must define outlets, retained basins and disconnected land components explicitly before using this approach on inland or enclosed domains. Preserve authored heights unless an explicit terrain-edit policy allows modification. See S02's optional environmental derivation contract.
- **Sparse links are candidates.** Azgaar's [`calculateUrquhartEdges`](https://github.com/Azgaar/Fantasy-Map-Generator/blob/master/src/model/economy/routes-generator.ts) removes longest Delaunay-triangle edges before terrain path search. For a game, required journeys and movement constraints remain authoritative; Euclidean sparsity alone cannot certify connectivity around barriers. S04 owns the comparison and rejection rules.
- **Presentation must not alter world facts.** Inkarnate informs editing interactions; Azgaar's documented model/render separation reinforces the existing C# geometry/JavaScript display boundary. A color toggle must not regenerate, relocate or omit saved structures. Editor operations change declared inputs and trigger validated regeneration.
- **Optional backends stay optional.** ProceduralToolkit's Unity geometry wrappers, Arnis's geographic reconstruction, Bonsai's voxel runtime and the Godot road plugin serve different execution models. They are algorithm/adapter references; none defines the shared world schema. Roadless, indoor and topology-only recipes must remain supported independently.

Current source inspection finds bounded height operations in `WorldTerrainGenerator` and derived scalar queries in `WorldEnvironmentQuery`; those contracts do not implement Mapgen4-style rainfall transport or drainage. Existing evidence for generated landforms must not be relabeled as environmental simulation. No third-party application was run or dependency added in this review.

### Algorithm selection and adoption checks — 2026-10-27

This source refresh makes the existing stage mapping actionable. Observations below come from upstream source/documentation; the WorldCreator decisions are proposals. Branch URLs are mutable inspection locators, not pinned dependencies. Any future source reuse must record the exact revision, files and applicable notices before adoption.

| Source observation | WorldCreator decision and acceptance |
| --- | --- |
| Mapgen4's [`map.ts`](https://github.com/redblobgames/mapgen4/blob/main/map.ts) separates mountain-distance expansion, seeded elevation detail, wind-ordered rainfall, priority-queue drainage direction and accumulated flow. | Keep terrain operations in `WorldTerrainGenerator` and queries in the shared surface owners. Add environmental relationships only for selected capabilities. First qualify a moved generated ridge/pass against unchanged required content; optional drainage needs explicit outlets and cycle/flow checks. S02/S10. |
| Azgaar's [route source](https://github.com/Azgaar/Fantasy-Map-Generator/blob/master/src/model/economy/routes-generator.ts) separates Urquhart candidate links from terrain costs and reused segments. Water paths restrict river/port approaches. | Compare candidate topology strategies within `MetadataNetworkPlanner`; retain full-width qualification in `WorldSurfaceRoutePlanner`. Connection mode and valid interfaces govern transitions. Do not copy its habitability constants into every game's movement rules. S04. |
| ProceduralToolkit's [samples](https://github.com/Syomus/ProceduralToolkit#samples) separate facade/roof planning from construction. Its low-poly terrain example explicitly lacks chunk support. | Study polygon and composition algorithms for S05/S06; require independent seam/identity tests for S08. A mesh sample does not qualify world streaming. |
| Bonsai's [README](https://github.com/scallyw4g/bonsai) lists erosion and biomes under its wishlist. | Keep volume editing as an optional future capability. Do not treat those listed aspirations as implemented environment generation or measured scalability. S02/S07/S08. |

`MetadataLayerPlanner` owns semantic dependencies and membership; generation sessions own executable invalidation. The master tracker now records bounded generated-landform and edit/replay evidence through those existing owners. Wider environmental dependencies, spatial chunks, scale and complete editable-world acceptance remain open. This reference review does not rerun or extend that implementation evidence.

Adoption details also need file-level review. [Arnis's README](https://github.com/louis-e/arnis#copyright-license-information) identifies Apache-2.0 generally but specifically identifies `src/luanti_block_map.rs` as LGPL-2.1-or-later-derived code. Record that exception if the optional adapter ever uses it; a repository-wide label is insufficient. No Arnis code, geography or asset is imported here. Inkarnate remains an editing reference; this review does not grant rights to redistribute its stamps. The [Godot Road Generator version table](https://github.com/TheDuckCow/godot-road-generator#version-support) currently targets Godot 4.4+ on `main`, and its intersection table still marks lane/edge curves forthcoming. A selected version must pass actual junction traversal, not merely mesh inspection.

Rechecked the seven owner URLs and Mapgen4's developer explanation. Use complementary capabilities, rather than selecting one project as the entire engine. Mapgen4 and Azgaar are the principal surface-world algorithm references; Inkarnate informs editing, ProceduralToolkit informs geometry, Arnis informs optional source import, Bonsai informs a future volume backend, and Godot Road Generator informs optional downstream 3D road realization. The detailed observations and boundaries remain in R16–R22 below.

The implementation decision is **editable terrain/domain → derived environment → gameplay destinations and regions → traversable connections → local layouts → nested catalog content → detail → colored review/export**. These are dependencies with bounded feedback, not an unconditional linear pass. A changed ridge can invalidate a route; a required compound that cannot fit can force region reconsideration. Stages must declare their inputs, affected outputs and protected authored edits. A renderer must not repair failed geometry.

For roads, select required destinations before choosing candidate links; route against terrain and passage constraints, then realize compatible shared corridors and explicit junctions. A visible intersection is not evidence of connectivity. A local grid is a selected layout policy with a boundary, never the domain representation or world-wide default. Roadless profiles do not acquire roads merely to divide land.

The [S10 reference-driven acceptance matrix](world-generation/stages/s10-validation-diversity-and-core-gate.md#reference-driven-acceptance-matrix) turns these decisions into counterfactual checks. Its recorded bounded cases and remaining unqualified cases must be distinguished; the complete matrix is not qualified. This refresh imported no code or assets and ran no third-party applications. Source inspection is not integration or performance evidence.

The owner supplied all seven projects below during network implementation. Their official pages, documentation and selected source files were inspected; none was installed, executed, imported or copied into runtime code. Statements about third-party capabilities below are source observations, not local runtime qualification. The design decisions are our proposed applications. These references change the next priority to generated terrain and suitability, rather than extending the authored-point route demonstration indefinitely.

### R16 — Inkarnate

[Official product page](https://inkarnate.com/). It presents line/shape tools, textures, stamps, cloneable maps and high-resolution exports. **Application:** reference for S09/S11 editing and composition: named layers, deliberate shapes, style-consistent assets and an understandable map review. **Boundary:** the inspected page supplies neither an engine API nor procedural-generation source. It does not establish a reusable runtime dependency or permission to bundle its assets. Canonical GameArts still owns the requested artwork workflow.

### R17 — Mapgen4 implementation (extends R07)

[Repository](https://github.com/redblobgames/mapgen4), [developer explanation](https://www.redblobgames.com/maps/mapgen4/), [generation source](https://github.com/redblobgames/mapgen4/blob/main/map.ts). The project combines editable terrain constraints with derived environmental layers. The inspected source separates elevation arrays, mountain distance, humidity/rainfall, downslope and flow; it uses seeded noise and mesh connectivity. The repository identifies Apache-2.0 licensing.

**Application:** S01/S02 need a real generated terrain-field representation and dependent regeneration. Designer constraints such as coastlines, ridges and valleys remain editable; rivers/biomes derive from field relationships. A terrain sampling mesh must not become visible street blocks. **Boundary:** preserve purposeful game constraints; do not treat stochastic terrain variation as permission to scatter required structures. The current authored planar patches and quadratic footprint checks are not a sufficient high-resolution terrain backend.

### R18 — Azgaar Fantasy Map Generator

[Repository](https://github.com/Azgaar/Fantasy-Map-Generator), [README](https://github.com/Azgaar/Fantasy-Map-Generator/blob/master/README.md), [MIT license](https://github.com/Azgaar/Fantasy-Map-Generator/blob/master/LICENSE). The README describes a **future** separation of world state, generators, editors and pure rendering; that architectural transition is not claimed complete.

Selected code inspected:

- [Heightmap generation](https://github.com/Azgaar/Fantasy-Map-Generator/blob/master/src/model/physical/heightmap-generator.ts): composable hill, pit, range, trough, strait, mask and smoothing operations. **Application:** explicit, replayable terrain operations in S02, with bounded seed variation and authored constraints.
- [Biome generation](https://github.com/Azgaar/Fantasy-Map-Generator/blob/master/src/model/physical/biomes-generator.ts): elevation, temperature, precipitation and river-related moisture feed classification; biome data also includes habitability and travel cost. **Application:** separate semantic kind, placement suitability, traversal cost and display color rather than infer one from another.
- [Settlement generation](https://github.com/Azgaar/Fantasy-Map-Generator/blob/master/src/model/political/burgs-generator.ts): scored candidates and spatial separation influence settlement selection, with spacing relaxation and randomization. **Application:** S03/S05 select purposeful locations using game-authored suitability and relationships. **Boundary:** no automatic relaxation of hard requirements, reduction of required counts or unbounded retry in WorldCreator.
- [Route generation](https://github.com/Azgaar/Fantasy-Map-Generator/blob/master/src/model/economy/routes-generator.ts): Delaunay-derived Urquhart candidates are distinct from terrain path costs, which consider land, habitability, height and existing connections. **Application:** compare sparse graph strategies and compatible corridor reuse in S04. Our current nearest-neighbor graph remains a bounded implemented candidate strategy, not an implementation of Azgaar's algorithm. A sparse graph alone cannot establish required connectivity or physical junctions.

**Boundary:** fantasy states/cultures, renderer conventions and regional-scale symbols do not become mandatory concepts or playable footprints in the shared game engine. Third-party internal dependencies need their own provenance review before any reuse.

### R19 — Arnis

[Repository and README](https://github.com/louis-e/arnis). It describes transforming OpenStreetMap and elevation data into detailed worlds for Minecraft and other supported targets; the repository identifies Apache-2.0 licensing. **Application:** study a source-adapter boundary for S01/S08/S14: retain geographic/source identity, compile typed features and convert scale only at the target adapter. **Boundary:** real-geography reconstruction is different from inventing a fictional world. It cannot replace the requested procedural generator or justify a mandatory voxel grid. Source-data rights and target-specific geometry remain separate from the repository's code license.

### Network source review — 2026-10-27

Reopened all seven owner links and inspected [Azgaar's route source](https://github.com/Azgaar/Fantasy-Map-Generator/blob/master/src/model/economy/routes-generator.ts). Observed: `calculateUrquhartEdges` selects candidate links, `getLandPathCost` discounts existing connections, and `getRouteSegments` separates newly needed segments from reused ones. These are separate decisions, not a grid layout algorithm. Our application is an explicit shared-segment model with ordered journey references and independently qualified junctions; see the [S04 contract](world-generation/stages/s04-terrain-aware-connections.md#active-physical-corridor-realization--2026-10-27).

The [Godot Road Generator README](https://github.com/TheDuckCow/godot-road-generator/blob/main/README.md) documents cross-section interpolation and procedural intersections but marks intersection RoadLane/edge-curve support as forthcoming. Therefore visible intersection meshes do not establish navigable lane continuity. This downstream limitation must remain an actual runtime gate if that adapter is selected. No dependency, implementation or stage completion follows from this review.

### R20 — ProceduralToolkit

[Repository documentation](https://github.com/Syomus/ProceduralToolkit), [MIT license](https://github.com/Syomus/ProceduralToolkit/blob/master/LICENSE.md). The Unity library documents geometry primitives, clipping/offsetting wrappers, tessellation, straight skeletons and strategy-based building construction. **Application:** evaluate its algorithm organization and underlying geometry libraries for S05/S06 polygon allocation and compositional construction; keep the pure geometry owner separate from engine rendering. **Boundary:** the toolkit uses Unity types and is not a drop-in headless .NET/Godot dependency. Its wrappers and bundled libraries must be evaluated separately; do not recreate Godot-native services or introduce Unity merely to obtain a helper.

### R21 — Bonsai

[Repository and README](https://github.com/scallyw4g/bonsai). It documents GPU terrain generation, staged terrain decoration, SDF-based editing and an alpha rewrite; the repository identifies WTFPL licensing. **Application:** S01/S02/S07/S08 should distinguish volumetric fields, generation, detail and edit persistence. A volume/chunk backend is a separate capability from the current bounded surface route planner. **Boundary:** README scale/performance claims are not our benchmarks. No custom renderer/GPU engine is adopted, and a voxel backend is not forced on the 2D TileMapLayer delivery.

### R22 — Godot Road Generator

[Repository](https://github.com/TheDuckCow/godot-road-generator), [README](https://github.com/TheDuckCow/godot-road-generator/blob/main/README.md). The documented 3D plugin provides cross-section-based roads, grouped road points, intersections, lane-following paths and terrain integration. The README identifies MIT licensing and explicitly says the project is not feature complete. **Application:** S04/S14 must keep route intent, junctions, cross-sections and downstream mesh/lane realization separate. Evaluate the native Godot extension only for an actual 3D road consumer. **Boundary:** it does not replace regional terrain/destination generation or the requested 2D native TileMapLayer pipeline. Installed-version/runtime qualification is still required before use.

### R23 — Scalar contours as a purposeful population grammar (2026-10-28)

[VTK contour filter](https://vtk.org/doc/nightly/release/9.5/html/classvtkContourFilter.html)
documents extracting isolines from scalar datasets. Its
[contour-label example](https://examples.vtk.org/site/Cxx/Visualization/LabelContours/)
distinguishes generated line segments from connected polylines. **Application:**
S07 uses the existing terrain/environment affine pieces to extract specified
field contours, join shared seams, select a component from declared regional
intent and sample required catalog content through the common arc-length owner.
This is our bounded planar implementation; VTK is neither installed nor imported.
Plateaus, branches and ambiguous components refuse rather than implying a unique
curve. No source performance claim is used as GameFrameWork evidence.

### R24 — Principal-axis mass partitioning for population (2026-10-29)

[Sandia's Zoltan developer guide](https://sandialabs.github.io/Zoltan/dev_html/dev_rib.html)
describes recursively splitting geometric domains using inertial directions.
**Application:** S07 adapts principal-axis cuts to continuous area with
analytically integrated, game-authored density and required integer member counts.
Leaves use weighted centroids and the existing physical qualification owners.
This is a bounded planar population grammar, not parallel load balancing or an
import of Zoltan. No dependency or third-party code was added. Source performance
claims are not our throughput evidence; actual timings and limitations remain in
the increment's verification record.

### R25 — Cost-aware graph search and incumbent feature bounds (2026-10-29)

[Red Blob Games' primary A* introduction](https://www.redblobgames.com/pathfinding/a-star/introduction.html)
separates a game's world representation from its search graph and explains cost
plus heuristic search. Retrieved 2026-10-29. No third-party code or dependency was
imported. **Framework extension:** use a fully qualified incumbent as an upper
bound; a proposed feature whose admissible start→feature→goal bound exceeds it
cannot improve that path. Lowest-cost mode retains every remaining eligible
feature or refuses the candidate budget. First-feasible mode makes no minimum
claim. The extension/proof is our design inference; the source does not certify
it or our performance. Independent full-feature Dijkstra and exact circular
clearance cases qualify the current bounded implementation in
[local evidence](evidence/2026-10-29-physical-routing-verification.json).

### R26 — Bounded integer composition of complete programs (2026-10-29)

[Google OR-Tools' integer-programming example](https://developers.google.com/optimization/mip/mip_example)
describes integer variables, constraints and objectives (retrieved 2026-10-29).
Framework application: whole-program counts are bounded integers; net capacity
balances and declared budget are constraints, with cost/count/canonical objectives.
The current bounded decimal enumeration and its optimistic suffix bounds are our
implementation; no solver dependency or third-party source was imported. This
reference does not establish resource delivery or physical feasibility. Ninety
independent exhaustive oracle cases and full selected-program physical execution
have [local evidence](evidence/2026-10-29-program-mix-verification.json).

The subsequent [physical-composition increment](evidence/2026-10-29-program-composition-verification.json)
is our design extension: enumerate exact local optima with prior vector exclusions,
then search deterministic parent-first configurations through the actual physical
pipeline. Failed descendants backtrack; engine budget failures are not infeasibility.
Fifteen independently sorted exhaustive vectors and two complete physical programs
qualify this bounded extension. It is not a claim about OR-Tools behavior or a
minimum aggregate price over changing world hierarchies.

### R27 — Bounded interior anchors for terrain components (2026-10-29)

[Mapbox Polylabel's primary algorithm description](https://github.com/mapbox/polylabel)
uses signed boundary distance, a priority queue and distance-plus-cell-radius bounds
to approximate an interior point farthest from a polygon's outline. Retrieved
2026-10-29. Our C# adaptation uses existing convex-piece terrain unions and real
exterior/hole edges, with binary rectangular subdivision, aggregate work limits and
saved lower/upper witnesses. No source code, asset or dependency was imported;
upstream performance/guarantee statements are not our acceptance evidence.
Independent analytic and terrain-contour cases, exhaustive small component
assignment graphs and complete physical programs have
[local evidence](evidence/2026-10-29-region-intent-verification.json).

The subsequent [joint physical-feedback increment](evidence/2026-10-29-region-feedback-verification.json)
uses the existing exact assignment order with prior-vector exclusions. The same
composition cursor exhausts regional choices before earlier catalog decisions,
while each physical/layout owner qualifies unchanged source contents. This is our
bounded implementation extension, with independent full assignment-order checks
and five complete CLI fixtures; it is not an upstream algorithm/performance claim
or an aggregate optimal-design guarantee.

### Resulting implementation priority (historical sequence)

The sequence below records the original research recommendation. Current implementation progress and the next action are owned by [ROADMAP](ROADMAP.md#master-stage-tracker); do not restart already-qualified terrain or network slices from this historical sequence. Physical interfaces and bounded local repartition now have focused evidence. Complete physical constituent programs and broader reference-driven world-profile tests remain open.

1. Finish qualification of the already-started bounded destination-network slice; do not treat it as world completion.
2. S01/S02: implement a scalable terrain representation and replayable landform operations, with stable field ownership, spatial queries, constraints and derived environmental layers. Replace the authored-patch-only assumption for generated terrain; no global visible block grid.
3. S03/S05: use those fields for region/destination suitability and purposeful placement with required content and explicit failure. Then S04 connects the derived destinations, handles compatible corridor reuse and qualifies junctions.
4. S06–S10: nested content, environmental detail, persistence, colored inspection and varied game-world acceptance. C# owns generation; JavaScript displays its data without repacking physical geometry.
5. S11–S14 remain the owner's building-type references → district mockups → complete city mockup → faithful map → canonical GameArts assets → native Godot TileMapLayers chain.

This is refinement of the existing fourteen-stage plan, not a parallel replacement plan or completed dependency adoption. Source review is not proof that any referenced tool has been successfully integrated.

### Reference refresh and implementation checkpoint — 2026-10-26

Reopened all seven owner URLs and their project descriptions. Their roles above remain distinct: Mapgen4 terrain editing/derived terrain layers; Azgaar editable world data separated from generators/renderers; Inkarnate composition tools; ProceduralToolkit geometry/construction strategies; Arnis geographic reconstruction; Bonsai optional volumetric generation; Godot Road Generator downstream 3D road realization. These are design references, not a universal replacement dependency. No third-party code or assets were imported.

The common engine now has a locally exercised hierarchy slice: catalog-defined shapes, terrain-qualified child placement and child-driven parent backtracking. See [evidence](evidence/2026-10-26-world-hierarchy-verification.json). It advances composition but does not implement these projects' complete algorithms, hydrology, arbitrary volume worlds, access/capacity or road junction realization. Keep those gaps visible in the master tracker.


### Optional sampled drainage decision — 2026-10-29

Primary source documentation inspected: RichDEM [flow metrics](https://richdem.readthedocs.io/en/latest/flow_metrics.html), [flat resolution](https://richdem.readthedocs.io/en/latest/flat_resolution.html), and [flow accumulation](https://richdem.readthedocs.io/en/latest/flow_accumulation.html). The metrics documentation distinguishes single-receiver convergence from split-flow methods and describes D8 steepest descent. The flat-resolution documentation distinguishes modifying elevations from adjusting routing metrics. The accumulation documentation supports explicit source weights and fractional downstream absorption.

Framework decision: preserve authored terrain and use the shared heightfield's triangle-edge graph, explicit permitted terminals, strict non-uphill edge profiles and reverse shortest paths. Positive path distance resolves flats without an artificial elevation gradient. This is our bounded game routing grammar; it is not D8, Barnes flat resolution, Priority-Flood or an implementation imported from RichDEM. Unknown basins/components refuse instead of automatic filling or ocean creation. Directional sampling bias, single receivers, explicit per-sample rates and the absence of hydraulic/water-geometry semantics are documented in the [examples](../examples/terrain/drainage/README.md). No third-party code, library or assets were adopted. Core qualification does not complete S02.7's downstream integration.


### Occupied water and numerical geometry decision — 2026-10-29

Reviewed primary sources: [Shewchuk's robust predicates overview](https://www.cs.cmu.edu/~quake/robust.html) and [CGAL's kernel robustness discussion](https://doc.cgal.org/latest/Kernel_23/index.html#Kernel_23Robustness). Both distinguish floating-point geometric decisions from exact arithmetic guarantees. Exact predicates and exact constructed coordinates are separate concerns; near-zero numerical results need careful treatment.

Implementation decision: use the existing conservative circular envelope and structured surface-face sweep. Construct symmetric rays from shared magnitudes, canonicalize only calculated crossing events within four ULPs at the operation-coordinate scale, and preserve distinct authored event values. A two-ULP authored-boundary test guards that separation. Retain common topology cleanup and explicit resolution/budget failures. This is a bounded numerical convention qualified by current fixtures, not Shewchuk/CGAL code, an exact-arithmetic kernel, or proof of robustness for all ill-conditioned geometry. No external implementation/dependency was adopted. Future exact construction work remains separate from the current water capability.

Water widths and threshold selection are explicit game inputs. Generated wet faces inherit underlying terrain planes and source precedence; no physical erosion, discharge-width calibration, lake-level calculation or ocean initialization is inferred. See [core API/limits](../examples/terrain/water/README.md).

## Source-footprint anchor decision — 2026-10-29

Reviewed primary documentation: [CGAL 2D Minkowski sums](https://doc.cgal.org/Manual/latest/doc_html/cgal_manual/Minkowski_sum_2/Chapter_main.html).
It connects polygon sums and boundary-edge construction with translational
motion planning. We use that configuration-space viewpoint to derive finite
translation-contact segments from a required source footprint and its allowed
support. Projections/intersections and declared clearance offsets supply
candidates; the shared full-union geometry owner verifies every accepted pose.
No CGAL code/dependency or exact kernel is imported. This is our bounded
floating-point feature search, with explicit numerical/resource refusal; it is
not CGAL's complete Minkowski-sum implementation or a continuous optimality proof.
The source polygon, forbidden areas and placement constraints remain unchanged.

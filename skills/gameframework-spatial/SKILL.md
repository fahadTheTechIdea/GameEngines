---
name: gameframework-spatial
description: "Decide where spatial work belongs in GameFrameWork: simulation truth (deterministic paths, distances, line of sight, fog or maps that enter the state hash or replay) belongs to SimEngine's existing map/navigation contracts, presentation belongs to Godot, and SpatialEngine is only a parked numeric sketch. Use when a game needs pathfinding, sight or a map, or when tempted to reimplement Godot navigation."
---

# Spatial work in GameFrameWork

Reference: `docs/engines/SpatialEngine.html`; design record `docs/SPATIAL_ENGINE.md`.

## Status of SpatialEngine

SpatialEngine is **parked** (2026-10-28). Only a sketch exists under `src/SpatialEngine/SpatialEngine.Core`: `Cell`, `Point` (millimetres), `TerrainDef`, `SpatialCodes`, and `Metric.Distance` (exact integer floor of Euclidean distance). There is no map reader, pathfinding, line of sight, queries, save, tests, CLI or solution reference. Do not describe it as working and do not build on it without a recorded decision.

## The routing rule

1. **If the geometry affects simulation** — movement, reachability, distance, line of sight, fog, occupancy, resources in space, regions — it belongs to **SimEngine** and must participate in its one clock, stable IDs and `Sync` hash.
   Use: `MapLayer`/`MapLayer<T>`, `TileMap`, `ISpatialPresence`, `SpatialRegionDef`, `NavGrid`/`PathSearch`, `Mover`/`IMotion`, `ISpatialLayer`, spatial resource cells and the existing visibility contracts.
2. **If it is only presentation** — rendering, camera, animation, input, visual effects — use **Godot** natively. Do not reimplement navigation or physics in game code.
3. **Never add a third spatial authority.** Two sources of truth for position or sight desynchronize save, replay and clients.

## Forbidden patterns

- A second pathfinder or navigation grid beside `NavGrid`/`PathSearch`.
- A copied position/presence store beside the native entity presence or point-space provider.
- Floating-point gameplay geometry; use millimetres/exact fixed point.
- Host-side geometry interpretation or a game assembly reading private map internals.
- Using the SpatialEngine sketch as a dependency and claiming it is implemented.

## If the work resumes

Record the decision in `docs/SPATIAL_ENGINE.md` first (extend SimEngine vs separate kernel). If separate, add the project to the solution, then implement reader/validator → queries (path, sight, distance) → checkpoint → tests, each with coded refusals. Acceptance is NOT RUN until those exist and pass.

## Where the working implementation lives

`src/SimEngine/src/Beep.SimEngine/Map` (layers, tile map, navigation, placement), `Kernel/EntityMotion.cs` and `Core/Spatial*` (presence, regions, resources, interactions); `Views` for scoped reads. The SimEngine skill (`gameframework-simengine`) governs how to extend them.

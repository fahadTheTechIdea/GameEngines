---
name: gameframework-spatial
description: "Decide where spatial work belongs in GameFrameWork: simulation truth (deterministic paths, distances, line of sight, fog or maps that enter the state hash or replay) belongs to SimEngine's existing map/navigation contracts, presentation belongs to Godot, and SpatialEngine is only a parked numeric sketch. Use when a game needs pathfinding, sight or a map, or when tempted to reimplement Godot navigation."
---
# gameframework-spatial

The canonical skill is `skills/gameframework-spatial/SKILL.md` (fuller reference: `docs/engines/SpatialEngine.html`). Read that skill and follow it. This file only routes agents to it; it holds no engine facts of its own, so `skills/README.md` and the canonical skill win over anything else.

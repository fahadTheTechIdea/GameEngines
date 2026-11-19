# Joint region and program composition fixtures

These are framework fixtures, not configured games or production artwork. Every
recipe uses the same current C# generation pipeline with finite constraints and
complete catalog constituents. Root placement is a deliberately authored local
row; the region strategy is not a global grid/world policy.

| Fixture | Purpose | Qualified result |
| --- | --- | --- |
| `terrain-reselection` | The higher-clearance component cannot hold the complete row; a lower-clearance component can. | 17 nodes, 16 physical members, 11 routes, 2 attempts. |
| `region-only` | Fixed required programs use regional composition without arithmetic mixing. | 17 nodes, 16 physical members, 11 routes, 2 attempts; mix work is zero. |
| `joint-backtracking` | Both regions fail the cheaper program mix; a different complete mix fits the first region. | 9 nodes, 8 physical members, 5 routes, 4 attempts; capacity 8 and minimum 2 programs retained. |
| `protected-layout` | A fixed globally eligible local anchor must belong to the chosen region. | 17 nodes, 11 routes; anchor remains at `(120, 0)`. |
| `adapted-layout` | Region selection composes with authorized width adaptation. | 17 nodes, 11 routes; width 100 refuses and 110 qualifies, with the anchor unchanged. |

The alternate narrow program is explicitly authored with full source geometry,
a separate yard access/facing definition and nested storage before generation.
No source dimensions or required contents are reduced during search. This fixture
does not claim a joint price optimum, autonomous intent, broad genre/design
acceptance or large-world performance. Godot stays deferred.

[Evidence and actual execution commands](../../../docs/evidence/2026-10-29-region-feedback-verification.json)
cover independent assignment order/exhaustion, full physical oracles, source/history
replay, rejected inputs/budgets, CLI publication and current JS witness checks.

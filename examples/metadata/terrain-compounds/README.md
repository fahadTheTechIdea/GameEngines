# Terrain response with complete physical compounds

The north-inlet and south-inlet inputs differ only in the water patch's position. Both use the same expedition catalog, eight physical members, protected anchors, local allocation rules, entrance widths and three required bidirectional connections. The irregular outer domain has area 784 m²; the inlet removes 8 m². No global street grid is declared.

Independent expected results: the north inlet leaves the shared span y=8..20, so its midpoint crossing is (22,14). The south inlet leaves y=0..12, producing (22,6). Physical content stays fixed, while routes and terrain-connected allocation shapes change. The initial allocation still fails and the permitted power-influence adjustment preserves every required member.

The interface search allows 10 million work units per leg. Three crossing candidates, three legs and two directions consume at most 180 million declared units; four internal path directions at 5 million retain the existing 200-million cap. The earlier 5-million interface-leg attempt exhausted its budget and is retained in evidence. Budgets are finite; no failed constraint is relaxed.

`--world-terrain-compounds` checks source identity, exact areas/spans, full-width solid/water avoidance, full footprint/reservation exclusion, unchanged hierarchy, stale terrain refusal and export/reload. Cross-domain water and water over a required fixed site fail without publishing a packet. Generated reviews are under `generated/terrain-compounds-north` and `generated/terrain-compounds-south`.

This is a controlled surface-terrain comparison, not a complete world, a hydrology simulation, a diversity proof, approved art or native Godot runtime acceptance.

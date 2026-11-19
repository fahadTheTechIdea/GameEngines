# Constructed floor intent

This framework example retains the full terrain-plus-three-level program:
160 nodes, 156 physical sources, 48 rooms and 48 fixtures, at the existing 0.3m
traversal radius and unchanged placement/search budgets. It is not a configured
game. Each interior declares at least 90% of its assembled floor clear of all
generated walls and solid contents. The rectangular levels require 420m²; the
hexagonal middle level starts at 330m².

`definition.json` is the baseline. `coastal-edit.json` changes only the exterior
shoreline. `stricter-floor-edit.json` raises the middle-level minimum to 335m²;
the physical sources are unchanged. Both edits use actual native definition
hashes. Use the existing domain-session CLI or the public workflow example's
`--domain-sequence` entry point; generation needs no artwork or Godot setup.

`insufficient-floor-edit.json` is a required refusal after the stricter edit.
It enlarges the same 16 middle-level fixtures from 0.8×0.5m to 1.2×0.8m. This still
meets the independently declared 95% ground-use rule, which subtracts solid source
bodies only. Once generated walls are included, it fails the constructed-floor
minimum. The accepted world and published directory must survive the failed edit.
No required members are dropped or resized to recover success.

Floor requirements use native squared world units and a dimensionless fraction.
Both minimums must pass; they are not actor-radius clearance or terrain-suitability
certificates. Floors remain independent coordinate domains with explicit portals;
the example does not establish fitting interiors into exterior envelopes, stairs,
production art, runtime behavior or broad whole-world design acceptance.

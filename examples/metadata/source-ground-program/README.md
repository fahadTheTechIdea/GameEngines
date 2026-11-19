# Ground-aware complete program selection

This framework program combines terrain-driven content counts, nested catalog
parts, authored source alternatives, local travel and a required regional journey.
It is engine-independent metadata; it is not a configured game or art profile.

Both requests generate two regions with two complete compounds per region. Each
compound retains one required physical child. The catalog requires at least 60%
open activity ground inside every compound's physical boundary.

`request.json` offers 3.2 m and 4 m square parents with the same 2.4 m square child.
The smaller parent fits the child's dimensions but cannot provide the required
open ground. The engine selects the authored 4 m alternative, leaving 64% open.
All ten nodes and eight physical sources remain, along with declared travel.

`child-choices-request.json` also offers an authored 2 m child supplying the same
required function. The engine can retain the preferred 3.2 m parent with that
child, leaving 60.9375% open. Source dimensions are fixed for each authored
variant; the generator does not resize a child or discard required content.

Use `MetadataRegionalOwnershipPlanner.Plan(request)` followed by
`MetadataRegionalWorldGenerator.Generate(definition)`, or the CLI command
`generate-derived-regional-world <request.json> <new-output-directory>`.
Regional publication/readback and source-edit sessions use the existing Core APIs.

The unplaced source preflight reuses exact envelope and ground-capacity owners.
Only proven, fixed catalog sources contribute to its necessary area bound.
Typed attempt witnesses retain the ground, contributing solids and constraints.
Source-only pruning keys include every contributing source and intermediate
containment ancestor, with geometry, occupancy and placement template. A changed
source path can be reconsidered. Exact whole-world qualification still runs.

This is a bounded framework example, not proof of arbitrary world design or
unlimited packing. Broad design/variety and live-agent gates remain open.
Artwork and Godot are deferred.

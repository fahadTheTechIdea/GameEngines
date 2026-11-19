# Authored object placement controls

Reusable framework inputs for the shared C# metadata generator. No game setup,
mockup, artwork or Godot project is required.

`Placement.PoseControls` targets an expanded semantic `NodeId`, with an ID,
purpose, source rule, required `Position`, explicit `Frame` and optional
`RotationDegrees`. `World` uses that metadata domain's coordinates. `Stage`
follows the placement parent's translation; it keeps the existing world axes.
A parent's rotation does not rotate its descendants' coordinate system.

The orientation replaces that member's binding default before resolving the full
source footprint, reservation, ports and site features. Positions constrain
placement. Existing exact anchors, growth-root intent, terrain, containment,
spacing, population facing, preserved source-world geometry and required journeys
remain hard. Conflicts refuse; controls never resize or drop content.

The four inputs cover:

- `controlled`: five complete expedition compounds, one fixed domain position,
  one rotated compound and one rotated shelter with an authored work-area position.
- `larger-source`: every shelter grows from 4 to 5 meters while all three controls
  remain. A boundary-derived contact span fits the full work area inside the
  rotated compound, preserving the declared gap and all 15 required connections.
- `absolute-child`: the child's domain position makes the shared solver reconsider
  its parent's initially preferred position.
- `stage-child`: a selected child offset follows its moved, rotated parent while
  retaining the stage's existing axes.

Use the existing `WorldMetadataGenerator` or `generate-metadata` command with each
recipe/catalog pair. Read `PlacementPlan.PoseResults` for the actual stage, demand,
domain position and effective orientation of every control. The schematic shows
this intent when its object is selected; it never relocates saved geometry.

Apply control changes through ordinary `MetadataGenerationSession` edits. Accepted
controls persist through source changes and checkpoint replay. Set the list to
`null` to explicitly return to ordinary procedural choices. Missing targets,
duplicate controls, conflicting terrain/anchors, invalid inputs and exhausted
budgets preserve accepted state. An empty list is invalid, rather than a no-op.

These are finite 2D placement controls, not a full interactive editor or independent
streaming. Other coordinate domains, broader design quality and world variety
remain separately qualified. Godot and artwork remain deferred.

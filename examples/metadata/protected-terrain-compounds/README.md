# Protected terrain with complete compounds

Two metadata recipes generate the same two nested compounds on an irregular
terrain domain with an inlet and three required bidirectional paths. A generated
terrain corridor is protected from detail variation. The seeds change surrounding
terrain while preserving source dimensions, all nine nodes and eight physical
sources. The route planner checks full-width paths against the inlet and solids.

Use `WorldMetadataGenerator.GenerateFilesAsync` or `generate-metadata` with
either recipe and the shared catalog. `ReadPublishedAsync` and `verify-metadata`
replay the source package. This is a reusable framework example; it is not a
complete game, visual production package or Godot project.

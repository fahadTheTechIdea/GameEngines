# Branching encounter journey

This geometry-free game program contains an entry, two alternative encounters with
one nested reward each, a relay, a vault and an exit. All nine nodes survive catalog
expansion. Six directed `advance` relationships allow either branch, but every
route to the exit must visit the relay before the vault.

`ProgressionRules` selects a scope, start, goals, traversable connection kinds and
ordered mandatory milestones. C# validates all-path milestone requirements and
saves one deterministic witness per goal. Read/export repeats that validation.
The JavaScript schematic checks and displays the saved witness in diagram units;
it does not invent terrain or physical navigation.

Generate with the existing `generate-metadata` command using `recipe.json` and
`catalog.json` and a new output directory. Focused checks are
`dotnet run --project tests/WorldCreator.Tests -- --world-progression` and
`node --test tests/javascript/world-progression.test.cjs` after producing
`generated/encounter-journey`.

Coverage remains partial: inventory, locks, combat, reward collection, physical
room geometry and playable runtime are separate requirements. A reachable reward
interaction is not implied by containment alone.

---
name: gameframework-alchemy
description: "Use GameFrameWork's AlchemyEngine for combination and discovery: validated element and recipe content, order-free or ordered matching with catalysts and environment tags, exact-fraction chance from a seed and attempt, and a discovery book with save and hash. Use when a game has crafting by experiment, potions, research by combining or recipe discovery."
---

# AlchemyEngine

Reference: `docs/engines/AlchemyEngine.html`; research in `docs/ALCHEMY_ENGINE.md`.
Code: `src/AlchemyEngine/AlchemyEngine.Core`; components `src/GameRuntime/GameRuntime.Alchemy`, `GameRuntime.AlchemySim`.

## Boundary

- Engine owns: content validation, recipe matching, chance, the discovery book, its canonical hash and save.
- Game owns: inventory, quantities, UI, rewards, unlock pacing and what an element is worth.
- Never invent a result. Bad content or a bad request raises a coded refusal; nothing is repaired.

## Loading content

```
var content = AlchemyContentLoader.Load(json);   // strict format 1
var rules   = new AlchemyRules(content);
var book    = new DiscoveryBook(content);        // knows starting elements only
```

- Elements need `id`, `name`, `properties`, `starting`; at least one is starting.
- A recipe has `inputs` (consumed), `catalysts` (present, kept), `environment` tags, `outputs`, `ordered`, and an exact `chance {numerator, denominator}`.
- Refused at load: duplicates, unknown elements, empty inputs/outputs, bad chance, catalyst also an input, ambiguous recipes, unreachable elements.

## Combining

```
var result = rules.Combine(items, new CombineContext(seed, book.Attempts, environment));
// Refused | NoReaction | Fizzled | Reacted
```

- Max 8 items; tags are 1–64 chars of `a-z 0-9 . _ -`; environment is deduplicated and sorted.
- Unordered matching uses the sorted multiset of inputs + catalysts; `ordered` compares non-catalyst inputs in sequence.
- Chance is `SHA256("alchemy-roll|seed|attempt|recipe|items|environment") % denominator < numerator`. Never use `Random` or a clock.
- `WhatDoes(items, env)` previews the match without rolling; `WhatMakes(elementId)` lists producing recipes.

## Discovery book

- `CheckUsable(items)` → `ALCHEMY_COMBINE_NOT_DISCOVERED` when the player lacks an item.
- `Record(result)`: refusal teaches nothing; fizzle costs an attempt but stays untried; a reaction adds outputs and the recipe.
- `LearnRecipe(id)` teaches externally and is not an attempt.
- `Hash()` is the per-tick journal fingerprint. `book.Save()` / `DiscoveryBook.Load(content, bytes)` are digest-checked; never edit saved bytes.

## Runtime component

`AlchemyComponent(rules, book, seed, id)` queues `CombineRequested` and `RecipeLearnedExternally`; answers in `Update` in arrival order. Emits `alchemy.combined`, `alchemy.combine-refused`, `alchemy.discovery-made`, and `alchemy.knowledge-restored` at `Init`. Declare `AlchemyEventCatalog` before `Init`. A checkpoint is consistent only when `IsQuiet`.

## SimEngine bridge

`GameRuntime.AlchemySim` maps discovered recipes into SimEngine content: `SimRecipeSource.Alchemy`, `RecipeUnlockComponent`, `SimProcessor`, `DiscoverRecipeEffect`. The workshop proof game is under `GameRuntime.AlchemySim.Headless/Workshop`.

Tests: `dotnet run --project src/GameRuntime/GameRuntime.Tests -c Release` (`AlchemyEngineTests.cs`).

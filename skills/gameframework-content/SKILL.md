---
name: gameframework-content
description: "Use GameFrameWork's GameContent catalogs for game data: versioned packs of resources, recipes, stations, skills, eras, items, formations and stats, validated whole with units, conservation and cross-pack duplicate rules, plus converters to EconomyEngine, AlchemyEngine, SimEngine, CombatEngine and characters. Use when a game needs items, resources, recipes, prices or units defined once and shared by several engines."
---

# GameContent

Reference: `docs/engines/GameContent.html`; catalog record `docs/RESOURCE_RECIPE_CATALOG.md`.
Code: `src/GameContent/GameContent.Catalog` (+ `.Economy`, `.Alchemy`, `.Sim`, `.Combat`, `.Characters`), CLI `GameContent.Cli`.

## Rule

Data is authored once, in packs, and converted into each engine's own document. Never retype an item's numbers in a second engine.

## Packs

```
src/GameContent/Packs/<id>/
  pack.json        manifest (format 1, id, version, world, genres, dependencies, license, locales, documents)
  resources.json   resources
  recipes.json     recipes
```

- Every id inside a pack starts with `<pack>.` (`CATALOG_ID_NAMESPACE`).
- Dependencies load first and are version-checked; cycles are refused.
- Shipped packs: `core`, `ages`, `arms-*`, `alchemy`, `city`, `colony`, `farming`, `kitchen`, `trading`, `rpg`, `space`, `war`, …
- The shared thing belongs in a pack siblings depend on (usually `core`). `CatalogLibrary.CheckDuplicates` enforces it across the library.

## Loading

```
var source = new DirectoryCatalogSource("src/GameContent/Packs");
CatalogLoadResult r = CatalogLoader.Load(source, "core");
if (r.Set is null) { foreach (var d in r.Report.Items) Console.WriteLine(d); return; }
CatalogSet set = r.Set;                       // dependency-first, Root is the requested pack
set.TryResource("core.iron-ingot", out var iron);
```

Findings are `CatalogDiagnostic(Code, Message, Location, Severity)`; a pack is accepted whole or refused whole. Codes are `CATALOG_*`.

## Resources

`id`, `name`, `class` (raw/intermediate/product/consumable/currency/energy/fluid/gas/information/labor/population/tool/equipment/unique), `unit` (UCUM from `CatalogUnits`), `origin` (natural/crafted), `stack.max`, `grade`, `perishable`, `durability`, `currency`, `cpc`, `tags`, typed `properties` (e.g. `base-price`).
Units must match the class; a misspelt property is refused, not ignored.

## Recipes

`id`, `name`, `kind` (extract/harvest/grow/transform/smelt/refine/craft/assemble/disassemble/cook/brew/combine/upgrade/repair/construct/research/trade/upkeep/consume/decay/loot), `inputs`, `catalysts`, `tools`, `outputs`, `byproducts`, `drops` (exact chance), `station {id, level}`, `skill {id, level}`, `environment` tags, `duration {value, unit}`, `quality` (inheritInput/skill), `reverse`.
Rules: no empty inputs/outputs, tools cannot also be inputs, reverse recipes must be reciprocal, station/skill levels exist, quality is consistent.

## Validation groups

Identity/references, units/values, recipes, graph and economy (reachability, cycles, mass conservation), eras (ordered, non-overlapping spans), formations (equipment exists/usable, training levels), balance warnings.

## Converters

| Target | Call |
|---|---|
| EconomyEngine | `CatalogToEconomy.Convert(set, new EconomyConversionOptions(...))` → goods/recipes JSON + names + report |
| AlchemyEngine | `CatalogToAlchemy.Convert(set)` → `AlchemyContent` + report |
| SimEngine | `CatalogToSim.Convert(set, ownership)` → content/module documents, stations, vehicles, quality |
| CombatEngine | `CatalogCombat.Weapon/Ammo/Group` → `WeaponSpec`, `AmmoSpec`, `Group` |
| Characters | `CatalogCharacters.Document(set, formations, template)` |

A conversion never invents data: read `ConversionReport` (`Reduced`/`Refused` notes) before trusting output.

## Scaffold and generator

`CatalogScaffold` / `new-pack` writes a valid skeleton. `GameContent.Generator/PackGenerator` builds the shipped packs from `Data*.cs`; generated packs are validated like any other. `CatalogWriter` round-trips a model to canonical JSON.

## CLI

```
GameContent.Cli list <packs>
GameContent.Cli validate <packs> <pack>
GameContent.Cli validate-all <packs>
GameContent.Cli stats <packs> <pack>
GameContent.Cli alchemy|sim|economy <packs> <pack> [--notes] [--out dir]
GameContent.Cli new-pack <packs> <id> <genre> <author>
```

Exit 0 done, 1 refused, 2 usage/file problem. Always run `validate-all` before shipping a pack set.

Tests: `dotnet run --project src/GameContent/GameContent.Tests -c Release`.

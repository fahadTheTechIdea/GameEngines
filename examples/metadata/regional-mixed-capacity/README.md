# Regional programs with varied capacity

`request.json` describes a reusable three-region world. Each region selects an
authored complete compound, and the regional planner measures that exact source
before reserving ownership. The compact and wide sources occupy 120 and 180 m²
and provide 7 and 10 units respectively. All nested members and bidirectional
inter-region journeys remain required.

`mixed-recipe.json` and `mixed-catalog.json` show a separate complete program
with three selected sources of two different sizes and capacities. Their combined
capacity is 24 units for a requirement of 20. Unplaced analysis and actual
placement retain each original source polygon.

Use `generate-derived-regional-world` for the regional request, or
`generate-metadata` for the mixed recipe and catalog. These are framework metadata
examples; they do not define a particular game, artwork or Godot runtime.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameContent.Catalog;

public static partial class CatalogToSim
{
    private const string FeedPort = "catalog.feed", ToolPort = "catalog.tool", ProductPort = "catalog.product";
    private const decimal Store = 10_000_000m;
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };

    private static string Json(JsonNode node) => node.ToJsonString(Indented);

    private static JsonObject Version(string id, params string[] requires)
    {
        var module = new JsonObject { ["id"] = id, ["version"] = "1.0.0" };
        if (requires.Length > 0) module["requires"] = new JsonArray(requires.Select(r => (JsonNode)new JsonObject { ["id"] = r, ["version"] = "1.0.0" }).ToArray());
        return module;
    }

    /// <summary>The module list the generated world needs. A consumer's <c>GameDefinition</c> must register code modules with exactly these ids and requirements.</summary>
    private static string Manifest(bool motion)
    {
        JsonObject Module(string id, string[] documents, params string[] requires)
        {
            var module = Version(id, requires);
            module["documents"] = new JsonArray(documents.Select(d => (JsonNode)d).ToArray());
            return module;
        }
        var modules = new List<JsonObject>
        {
            Module("sim.access", ["open-access.json"]),
            Module("sim.inventory", ["resources.json"]),
            Module("sim.operations", []),
            Module("sim.transformations", ["recipes.json"], "sim.access", "sim.inventory", "sim.operations"),
            Module("sim.world-setup", []),
        };
        if (motion)
        {
            modules.Add(Module("sim.motion", []));
            modules.Add(Module("sim.tile-navigation", ["navigation.json"], "sim.motion"));
        }
        modules.Add(Module(WorldModuleId, ["world.json"], motion ? ["sim.access", "sim.transformations", "sim.world-setup", "sim.motion", "sim.tile-navigation"] : ["sim.access", "sim.transformations", "sim.world-setup"]));
        return Json(new JsonObject { ["schema"] = 1, ["modules"] = new JsonArray(modules.Select(m => (JsonNode)m).ToArray()) });
    }

    private static string ResourcesDocument(List<CatalogResource> resources)
    {
        var used = resources.Select(r => SimUnits.BaseOf(r.Unit)).DistinctBy(b => b.Id).OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
        var units = new JsonArray();
        var limits = new JsonObject();
        foreach (var unit in used)
        {
            units.Add(new JsonObject
            {
                ["id"] = unit.Id, ["dimension"] = unit.Dimension, ["scale"] = new JsonObject { ["numerator"] = 1, ["denominator"] = 1 }, ["discrete"] = false,
            });
            limits[unit.Id] = Store;
        }
        var list = new JsonArray();
        foreach (var resource in resources)
        {
            var unit = SimUnits.BaseOf(resource.Unit);
            list.Add(new JsonObject
            {
                ["id"] = resource.Id,
                ["unit"] = unit.Id,
                ["category"] = Names<ResourceClass>.Of(resource.Class),
                ["phase"] = resource.Class == ResourceClass.Fluid ? "liquid" : resource.Class == ResourceClass.Gas ? "gas" : "solid",
                ["traits"] = new JsonArray(resource.Tags.Concat(resource.Cpc is null ? [] : ["cpc-" + resource.Cpc]).Select(t => (JsonNode)t).ToArray()),
                ["capacity"] = Capacity(used),
            });
        }
        var document = new JsonObject
        {
            ["units"] = units,
            ["capacity_policies"] = new JsonArray { new JsonObject { ["id"] = "catalog.store", ["limits"] = limits } },
            ["resources"] = list,
        };
        var graded = resources.Where(r => r.Grade is not null).ToList();
        if (graded.Count > 0)
        {
            // A grade is a whole-number axis 0..levels-1 in one shared unit (no capacity limit: it is not a resource's measure).
            units.Add(new JsonObject { ["id"] = GradeUnit, ["dimension"] = "grade", ["scale"] = new JsonObject { ["numerator"] = 1, ["denominator"] = 1 }, ["discrete"] = true });
            document["resource_qualities"] = new JsonArray(graded.Select(r => (JsonNode)new JsonObject
            {
                ["id"] = QualityId(r.Id), ["resource"] = r.Id, ["unit"] = GradeUnit, ["minimum"] = 0, ["maximum"] = r.Grade!.Levels.Count - 1,
            }).ToArray());
        }
        return Json(document);
    }

    /// <summary>Every resource occupies one unit of every dimension in use, so the one generous store policy can measure any mix of resources.</summary>
    private static JsonObject Capacity(List<SimUnits.Base> used)
    {
        var measures = new JsonObject();
        foreach (var unit in used) measures[unit.Id] = 1;
        return measures;
    }

    private static JsonObject Bill(CatalogSet set, IEnumerable<(string Resource, decimal Amount)> lines)
    {
        var bill = new JsonObject();
        foreach (var group in lines.GroupBy(l => l.Resource, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            bill[group.Key] = Converted(set, group.Key, group.Sum(l => l.Amount));
        return bill;
    }

    private static JsonObject Ports(IEnumerable<string> resources, string port)
    {
        var ports = new JsonObject();
        foreach (var resource in resources.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) ports[resource] = port;
        return ports;
    }

    private static string RecipesDocument(CatalogSet set, List<CatalogRecipe> recipes)
    {
        var transformations = new JsonArray();
        foreach (var recipe in recipes)
        {
            // Catalysts are held for the run and given back whole. A tool is held too and wears 1/uses of one item per run, which is
            // the engine's own partial tool consumption.
            var held = recipe.Catalysts.Select(c => (c.Resource, c.Amount)).Concat(recipe.Tools.Select(t => (t.Resource, 1m))).ToList();
            var wear = new JsonObject();
            foreach (var tool in recipe.Tools.OrderBy(t => t.Resource, StringComparer.Ordinal)) wear[tool.Resource] = Math.Round(1m / tool.Uses, 9);
            var made = recipe.Outputs.Concat(recipe.Byproducts).Select(o => (o.Resource, o.Max)).ToList();
            var (numerator, denominator) = Fraction(recipe.Duration.Value);
            var transformation = new JsonObject
            {
                ["id"] = recipe.Id,
                ["mode"] = "batch",
                ["cancel_policy"] = "return_inputs",
                ["category"] = Names<RecipeKind>.Of(recipe.Kind),
                ["inputs"] = Bill(set, recipe.Inputs.Select(i => (i.Resource, i.Amount))),
                ["tools"] = Bill(set, held),
                ["tool_consumption"] = wear,
                ["outputs"] = Bill(set, made),
                ["operating_inputs"] = new JsonArray(),
                ["input_ports"] = Ports(recipe.Inputs.Select(i => i.Resource), FeedPort),
                ["tool_ports"] = Ports(held.Select(h => h.Resource), ToolPort),
                ["output_ports"] = Ports(made.Select(m => m.Resource), ProductPort),
                ["operating_ports"] = new JsonObject(),
                ["duration"] = new JsonObject { ["numerator"] = numerator, ["denominator"] = denominator, ["unit"] = DurationUnit(recipe.Duration.Unit) },
            };
            if (recipe.Quality == QualityMode.InheritInput)
            {
                var output = recipe.Outputs.Select(o => o.Resource).First(r => Graded(set, r) is not null);
                var source = InheritSource(set, recipe, Graded(set, output)!)!;
                transformation["output_qualities"] = new JsonObject { [QualityId(output)] = new JsonObject { ["source"] = QualityId(source), ["rounding"] = "floor" } };
            }
            transformations.Add(transformation);
        }
        return Json(new JsonObject { ["transformations"] = transformations });
    }


    // Indexed by TimeUnit in declaration order.
    private static readonly string[] DurationUnits = ["second", "minute", "hour", "day"];

    private static string DurationUnit(TimeUnit unit) => DurationUnits[(int)unit];

    /// <summary>The value as a reduced fraction over a power of ten (the converter has already checked it needs at most 9 decimals).</summary>
    private static (long Numerator, long Denominator) Fraction(decimal value)
    {
        var scaled = (long)(value * 1_000_000_000m);
        var divisor = Gcd(scaled, 1_000_000_000L);
        return (scaled / divisor, 1_000_000_000L / divisor);
    }

    private static long Gcd(long a, long b) => b == 0 ? a : Gcd(b, a % b);

    private static string WorldDocument(List<CatalogRecipe> recipes, List<Host> hosts, List<Vehicle> vehicles)
    {
        var entities = new JsonArray();
        var setups = new JsonArray();
        foreach (var vehicle in vehicles) entities.Add(VehicleEntity(vehicle));
        foreach (var host in hosts)
        {
            entities.Add(new JsonObject
            {
                ["id"] = host.Id,
                ["kind"] = "structure",
                ["components"] = new JsonObject
                {
                    ["inventory_port"] = new JsonArray { new JsonObject { ["id"] = FeedPort }, new JsonObject { ["id"] = ToolPort }, new JsonObject { ["id"] = ProductPort } },
                    ["storage"] = new JsonObject { ["capacity_policy"] = "catalog.store", ["public"] = false, ["initial_stock"] = new JsonObject() },
                    ["transformer"] = new JsonObject
                    {
                        ["automatic_supply"] = false,
                        ["wip_capacity"] = "catalog.store",
                        ["transformations"] = new JsonArray(recipes.Where(host.Runs).Select(r => (JsonNode)r.Id).ToArray()),
                        ["states"] = new JsonObject { ["working"] = "catalog.working", ["stopped"] = "catalog.blocked" },
                        ["operation"] = new JsonObject
                        {
                            ["id"] = "catalog.operation", ["enabled"] = true, ["target"] = 1, ["minimum"] = 1, ["maximum"] = 1, ["unit"] = "unit.work-rate",
                        },
                    },
                },
            });
            setups.Add(new JsonObject { ["id"] = host.Id, ["after"] = new JsonArray(), ["entity"] = host.Id, ["company"] = 0, ["placement"] = null, ["home"] = null });
        }
        return Json(new JsonObject
        {
            ["game_modes"] = new JsonArray { new JsonObject { ["id"] = "catalog.mode", ["name"] = "Catalog resource-only world", ["properties"] = new JsonObject() } },
            ["time_definitions"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "catalog.time",
                    ["quantum"] = new JsonObject { ["numerator"] = 1, ["denominator"] = 1, ["unit"] = "second" },
                    ["frame_ticks"] = 1, ["ticks_per_second"] = 30, ["horizon"] = null,
                },
            },
            ["entity_states"] = new JsonArray
            {
                new JsonObject { ["id"] = "catalog.idle", ["state"] = "catalog.state.idle", ["priority"] = 0, ["default"] = true },
                new JsonObject { ["id"] = "catalog.working", ["state"] = "catalog.state.working", ["priority"] = 20 },
                new JsonObject { ["id"] = "catalog.blocked", ["state"] = "catalog.state.blocked", ["priority"] = 60 },
            },
            ["tile_spaces"] = vehicles.Count > 0 ? new JsonArray { new JsonObject { ["id"] = TileSpace, ["width"] = 32, ["height"] = 32 } } : new JsonArray(),
            ["entities"] = entities,
            ["entity_setups"] = setups,
            ["world_setup_profiles"] = new JsonArray { new JsonObject { ["id"] = "catalog.start", ["entries"] = new JsonArray(hosts.Select(h => (JsonNode)h.Id).ToArray()), ["includes"] = new JsonArray() } },
            ["scenarios"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "catalog", ["game_mode"] = "catalog.mode", ["mode_overrides"] = new JsonObject(), ["time"] = "catalog.time", ["space"] = vehicles.Count > 0 ? TileSpace : null,
                    ["modules"] = vehicles.Count > 0 ? new JsonArray { WorldModuleId, "sim.world-setup", "sim.transformations", "sim.motion", "sim.tile-navigation" } : new JsonArray { WorldModuleId, "sim.world-setup", "sim.transformations" },
                    ["seats"] = 1, ["default_seats"] = 1,
                    ["settings"] = new JsonObject { ["sim.world-setup"] = new JsonObject { ["profile"] = "catalog.start" } },
                },
            },
        });
    }
}

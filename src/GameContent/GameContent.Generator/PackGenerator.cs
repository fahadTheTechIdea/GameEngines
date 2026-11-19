using System.Globalization;
using System.Text;

namespace GameContent.Generator;

/// <summary>
/// Writes the equipment, vehicle, era, ammunition and formation packs from the authoring tables in <c>Data*.cs</c>. The tables are
/// the input; the pack JSON is the output and a test regenerates it and compares byte for byte, so the two cannot drift.
/// </summary>
public static class PackGenerator
{
    /// <summary>Generates every pack the tables describe into <paramref name="packs"/> and returns their ids.</summary>
    public static IReadOnlyList<string> Generate(string packs)
    {
        var generated = new List<string>();
        var inv = CultureInfo.InvariantCulture;
        
        // ---- known prices of materials that already exist in other packs ------------------------------------------------------------------
        var price = new Dictionary<string, decimal>
        {
            ["core.water"] = 0.05m, ["core.stone"] = 0.1m, ["core.sand"] = 0.05m, ["core.clay"] = 0.1m, ["core.wood"] = 0.5m, ["core.fiber"] = 0.3m, ["core.coal"] = 0.8m,
            ["core.salt"] = 2m, ["core.crude-oil"] = 0.6m, ["core.electricity"] = 0.12m, ["core.labor"] = 10m, ["core.plank"] = 2m, ["core.charcoal"] = 1.5m, ["core.brick"] = 1.5m,
            ["core.glass"] = 3m, ["core.rope"] = 2m, ["core.cloth"] = 5m, ["core.iron-ingot"] = 4m, ["core.copper-ingot"] = 6m, ["core.tin-ingot"] = 8m, ["core.bronze-ingot"] = 9m,
            ["core.steel-ingot"] = 8m, ["core.pickaxe"] = 40m, ["core.axe"] = 30m, ["core.hammer"] = 25m, ["core.grain"] = 0.4m, ["core.flour"] = 0.7m, ["core.people"] = 0m,
            ["mining.aluminium-ingot"] = 6m, ["mining.nickel-ingot"] = 9m, ["mining.lead-ingot"] = 3m, ["mining.zinc-ingot"] = 3m, ["mining.brass-ingot"] = 6m,
            ["mining.nitrate"] = 1.5m, ["mining.sulphur"] = 0.5m, ["mining.diesel"] = 1.2m,
            ["naval.deck-gun"] = 300m, ["naval.sail"] = 80m, ["naval.mast"] = 60m, ["naval.anchor"] = 150m, ["naval.canvas"] = 3m, ["naval.oak"] = 0.6m, ["naval.hemp"] = 1.5m,
            ["naval.pine-tar"] = 4m, ["naval.rigging"] = 160m,
            ["electronics.control-board"] = 40m, ["electronics.power-pack"] = 90m, ["electronics.led"] = 0.1m, ["electronics.lens"] = 1.2m, ["electronics.sensor"] = 120m,
            ["electronics.display"] = 25m, ["electronics.cell"] = 4m,
        };
        
        // ids whose unit is a whole-number count; an amount of these must be a whole number
        var discrete = new HashSet<string>
        {
            "core.plank", "core.brick", "core.rope", "core.pickaxe", "core.axe", "core.hammer", "naval.deck-gun", "naval.sail", "naval.mast", "naval.anchor", "naval.canvas", "naval.rigging",
            "electronics.control-board", "electronics.power-pack", "electronics.led", "electronics.lens", "electronics.sensor", "electronics.display", "electronics.cell",
        };
        
        string N(decimal value) => value.ToString("0.######", inv);
        string Q(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        
        // ---- materials ---------------------------------------------------------------------------------------------------------------------
        var eraId = new Dictionary<string, string>();
        foreach (var e in Data.AgesEras) eraId[e.Key] = e.Id;
        foreach (var e in Data.HorizonEras) eraId[e.Key] = e.Id;
        
        var matterUnit = new Dictionary<string, string>();
        var matterLines = Data.Materials.Concat(Data.AmmoMaterials).Concat(Data.AltMaterials.Concat(Data.AltMaterials2).Select(l => "ALT:" + l)).ToList();
        var parsedMaterials = new List<(string Slug, string Name, string Cls, string Unit, string Origin, string Price, string Era, string Ingredients, string Station, decimal Out, string Kind, int Minutes, bool Alt)>();
        foreach (var raw in matterLines)
        {
            var alt = raw.StartsWith("ALT:", StringComparison.Ordinal);
            var f = (alt ? raw[4..] : raw).Split('|');
            if (f.Length != 12) throw new Exception("bad material line: " + raw);
            parsedMaterials.Add((f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], decimal.Parse(f[9], inv), f[10], int.Parse(f[11], inv), alt));
            matterUnit["matter." + f[0]] = f[3];
        }
        foreach (var (id, unit) in matterUnit) if (unit == "{each}") discrete.Add(id);
        
        decimal Cost(string ingredients, string context)
        {
            decimal total = 0;
            foreach (var part in ingredients.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var (id, amount) = Split(part);
                if (!price.TryGetValue(id, out var p)) throw new Exception("no price for " + id + " in " + context);
                if (discrete.Contains(id) && amount != decimal.Truncate(amount)) throw new Exception("fractional amount of a counted resource " + id + " in " + context);
                total += p * amount;
            }
            return total;
        }
        
        (string Id, decimal Amount) Split(string part)
        {
            var at = part.LastIndexOf(':');
            return (part[..at], decimal.Parse(part[(at + 1)..], inv));
        }
        
        // natural prices first
        foreach (var m in parsedMaterials.Where(m => m.Origin == "natural")) price["matter." + m.Slug] = decimal.Parse(m.Price, inv);
        // crafted prices in order of definition (a part is defined before what uses it); the first line of a slug sets the price
        foreach (var m in parsedMaterials.Where(m => m.Origin == "crafted" && !m.Alt))
        {
            var cost = Cost(m.Ingredients, "matter." + m.Slug);
            price["matter." + m.Slug] = Math.Max(0.05m, Math.Round(cost / m.Out * 2m, 2));
        }
        foreach (var m in parsedMaterials.Where(m => m.Alt)) Cost(m.Ingredients, "alt matter." + m.Slug);
        
        // ---- helpers for JSON ----------------------------------------------------------------------------------------------------------------
        string Lines(string ingredients) => string.Join(", ", ingredients.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p =>
        {
            var (id, amount) = Split(p);
            return "{ \"resource\": " + Q(id) + ", \"amount\": " + N(amount) + " }";
        }));
        
        string Duration(int minutes) => minutes % 1440 == 0 && minutes >= 1440 ? "{ \"value\": " + minutes / 1440 + ", \"unit\": \"d\" }"
            : minutes % 60 == 0 && minutes >= 60 ? "{ \"value\": " + minutes / 60 + ", \"unit\": \"h\" }" : "{ \"value\": " + Math.Max(1, minutes) + ", \"unit\": \"min\" }";
        
        string StationJson(string station)
        {
            if (station == "-") return "null";
            var (id, level) = Split(station);
            return "{ \"id\": " + Q(id) + ", \"level\": " + (int)level + " }";
        }
        
        string RecipeJson(string id, string name, string kind, string inputs, string tools, string outputs, string station, int minutes) =>
            "{ \"id\": " + Q(id) + ", \"name\": " + Q(name) + ", \"kind\": " + Q(kind) + ", \"inputs\": [" + inputs + "], \"catalysts\": [], \"tools\": [" + tools + "], \"outputs\": [" + outputs +
            "], \"byproducts\": [], \"drops\": [], \"station\": " + StationJson(station) + ", \"skill\": null, \"environment\": [], \"duration\": " + Duration(minutes) + ", \"quality\": null, \"reverse\": null }";
        
        string ResourceJson(string id, string name, string cls, string unit, string origin, decimal stack, decimal? uses, string cpc, string tags, decimal basePrice) =>
            "{ \"id\": " + Q(id) + ", \"name\": " + Q(name) + ", \"class\": " + Q(cls) + ", \"unit\": " + Q(unit) + ", \"origin\": " + Q(origin) + ", \"stack\": { \"max\": " + (int)stack +
            " }, \"grade\": null, \"perishable\": null, \"durability\": " + (uses is null ? "null" : "{ \"uses\": " + (int)uses + " }") + ", \"currency\": null, \"cpc\": " + Q(cpc) +
            ", \"tags\": [" + string.Join(", ", tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Q)) + "], \"properties\": { \"base-price\": " + N(basePrice) + " } }";
        
        string Manifest(string id, string title, string world, string genre, IEnumerable<string> deps) =>
            "{\n  \"format\": 1,\n  \"id\": " + Q(id) + ",\n  \"version\": \"1.0.0\",\n  \"title\": " + Q(title) + ",\n  \"language\": \"en\",\n  \"world\": " + Q(world) + ",\n  \"genres\": [" + Q(genre) +
            "],\n  \"dependencies\": [" + string.Join(", ", deps.Distinct().Order(StringComparer.Ordinal).Select(d => "{ \"id\": " + Q(d) + ", \"version\": \"1.0.0\" }")) +
            "],\n  \"license\": { \"spdx\": \"CC0-1.0\", \"provenance\": \"original\", \"author\": \"GameFrameWork\" },\n  \"locales\": [],\n  \"documents\": [\"resources.json\", \"recipes.json\"]\n}\n";
        
        void Write(string pack, string file, string text)
        {
            var folder = Path.Combine(packs, pack);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, file), text);
            if (!generated.Contains(pack)) generated.Add(pack);
        }
        
        string Join(IEnumerable<string> lines, string indent = "    ") => string.Join(",\n", lines.Select(l => indent + l));
        
        string Document(string pack, IEnumerable<(string Name, IEnumerable<string> Lines)> sections)
        {
            var parts = sections.Where(s => s.Lines.Any()).Select(s => "  \"" + s.Name + "\": [\n" + Join(s.Lines) + "\n  ]");
            return "{\n  \"format\": 1,\n  \"pack\": " + Q(pack) + ",\n" + string.Join(",\n", parts) + "\n}\n";
        }
        
        string PackOf(string id) => id[..id.IndexOf('.')];

        var statNames = new Dictionary<string, string>
        {
            ["dmg"] = "damage", ["dt"] = "damage-type", ["rng"] = "range", ["rof"] = "rate-of-fire", ["acc"] = "accuracy", ["pen"] = "penetration", ["spl"] = "splash-radius",
            ["arm"] = "armor-rating", ["str"] = "structure", ["wt"] = "weight", ["spd"] = "speed", ["trv"] = "operating-range", ["ceil"] = "ceiling", ["rel"] = "reliability", ["eff"] = "efficiency",
        };

        string StatJson(string stats) => string.Join(", ", stats.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s =>
        {
            var kv = s.Split('=');
            return Q("gear." + statNames[kv[0]]) + ": " + (kv[0] == "dt" ? Q(kv[1]) : N(decimal.Parse(kv[1], inv)));
        }));

        var tierOf = Data.AgesEras.ToDictionary(e => e.Key, e => e.Tier);
        foreach (var e in Data.HorizonEras) tierOf[e.Key] = e.Tier;
        var penetrationFactor = new Dictionary<string, decimal> { ["slash"] = 0.4m, ["pierce"] = 0.6m, ["blunt"] = 0.3m, ["ballistic"] = 0.8m, ["blast"] = 0.5m, ["energy"] = 0.9m, ["fire"] = 0.2m };

        // Authoring convenience: the numbers combat needs (accuracy, penetration, structure) are written out into the JSON for every
        // fighting item, derived here once from range, damage type and weight when the table does not give them. The catalog keeps the result.
        string Complete(string cls, string eraKey, string stats)
        {
            var kv = stats.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Split('=')).ToDictionary(p => p[0], p => p[1]);
            decimal D(string key) => kv.TryGetValue(key, out var value) ? decimal.Parse(value, inv) : 0m;
            var tier = tierOf[eraKey];
            if (kv.ContainsKey("dmg"))
            {
                var range = D("rng");
                var accuracy = (range <= 3 ? 85 : range <= 30 ? 75 : range <= 150 ? 68 : range <= 600 ? 60 : 55) + (tier >= 10 ? 5 : 0) + (tier >= 13 ? 5 : 0) - (cls == "weapon" ? 0 : 5);
                if (!kv.ContainsKey("acc")) kv["acc"] = accuracy.ToString(inv);
                if (!kv.ContainsKey("pen")) kv["pen"] = Math.Round(D("dmg") * penetrationFactor[kv.TryGetValue("dt", out var type) ? type : "blunt"], 0).ToString(inv);
            }
            if (cls is "vehicle-land" or "vehicle-sea" or "vehicle-air" or "vehicle-space" or "mount" or "structure-unit" && !kv.ContainsKey("str"))
            {
                var divisor = cls == "mount" ? 5m : cls == "structure-unit" ? 10m : 20m;
                kv["str"] = Math.Min(1000000000m, Math.Max(20m, Math.Round(D("wt") / divisor, 0))).ToString(inv);
            }
            return string.Join(",", kv.Select(p => p.Key + "=" + p.Value));
        }
        
        // ---- gear: the stat vocabulary and skills ---------------------------------------------------------------------------------------------
        {
            var stats = Data.Stats.Select(s => "{ \"id\": " + Q("gear." + s.Slug) + ", \"name\": " + Q(s.Name) + ", \"dimension\": " + Q(s.Dimension) + ", \"unit\": " + Q(s.Unit) + ", \"kind\": " + Q(s.Kind) +
                ", \"min\": " + (s.Min ?? "null") + ", \"max\": " + (s.Max ?? "null") + ", \"values\": [" + string.Join(", ", s.Values.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Q)) +
                "], \"classes\": [" + string.Join(", ", s.Classes.Split(',').Select(Q)) + "] }");
            var skills = Data.Skills.Select(s => { var p = s.Split('|'); return "{ \"id\": " + Q("gear." + p[0]) + ", \"name\": " + Q(p[1]) + ", \"max\": 10 }"; });
            Write("gear", "pack.json", Manifest("gear", "Gear vocabulary: stats and crew skills of weapons, armor, tools and vehicles", "timeless", "equipment", []));
            Write("gear", "resources.json", Document("gear", [("stats", stats), ("skills", skills)]));
            Write("gear", "recipes.json", "{\n  \"format\": 1,\n  \"pack\": \"gear\",\n  \"recipes\": []\n}\n");
            // The manifest lists recipes.json; an empty recipes array is valid.
        }
        
        // ---- matter: materials for arms ---------------------------------------------------------------------------------------------------------
        var matterAvailability = new List<(string Id, string Era)>();
        {
            var resources = new List<string>();
            var recipes = new List<string>();
            var seen = new HashSet<string>();
            foreach (var m in parsedMaterials)
            {
                var id = "matter." + m.Slug;
                var isNatural = m.Origin == "natural";
                if (seen.Add(id))
                {
                    var cls = m.Cls;
                    var cpc = isNatural ? "0" : "3";
                    resources.Add(ResourceJson(id, m.Name, cls, m.Unit, m.Origin, cls == "consumable" ? 500 : 200, null, cpc, isNatural ? "material,natural" : "material", price[id]));
                    if (m.Era != "-") matterAvailability.Add((id, m.Era));
                }
                if (!isNatural)
                {
                    var recipeId = m.Alt ? "matter.make-" + m.Slug + "-alt" : "matter.make-" + m.Slug;
                    recipes.Add(RecipeJson(recipeId, (m.Alt ? "Make " : "Make ") + m.Name.ToLowerInvariant() + (m.Alt ? " (another way)" : ""), m.Kind, Lines(m.Ingredients), "", "{ \"resource\": " + Q(id) + ", \"amount\": " + N(m.Out) + " }", m.Station, m.Minutes));
                    if (m.Era != "-") matterAvailability.Add((recipeId, m.Era));
                }
            }
            var referenced = parsedMaterials.Where(m => m.Ingredients != "-").SelectMany(m => m.Ingredients.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => PackOf(Split(p).Id)).Concat(m.Station == "-" ? [] : [PackOf(Split(m.Station).Id)])).Where(p => p != "matter");
            var ammoItems = Data.AmmoItems.Select(l => { var p = l.Split('|'); return "{ \"resource\": " + Q("matter." + p[0]) + ", \"class\": \"ammunition\", \"stats\": { " + StatJson(p[1]) + " }, \"slots\": [], \"cargo\": [], \"crew\": [], \"fuel\": null, \"ammo\": null, \"upkeep\": [], \"replaces\": null }"; }).ToList();
            Write("matter", "pack.json", Manifest("matter", "Arms materials: flint, hide, powder, alloys, engines and rare metals", "timeless", "equipment", referenced.Append("gear")));
            Write("matter", "resources.json", Document("matter", [("resources", resources), ("items", ammoItems)]));
            Write("matter", "recipes.json", Document("matter", [("recipes", recipes)]));
        }
        
        // ---- ages and horizons -------------------------------------------------------------------------------------------------------------------
        {
            var externalGates = new (string Id, string Era)[]
            {
                ("core.bronze-ingot", "bronze"), ("core.iron-ingot", "iron"), ("core.steel-ingot", "medieval"), ("mining.lead-ingot", "classical"), ("mining.brass-ingot", "classical"),
                ("mining.nitrate", "renaissance"), ("core.copper-ingot", "copper"), ("mining.aluminium-ingot", "industrial"), ("mining.nickel-ingot", "industrial"), ("mining.diesel", "industrial"), ("naval.deck-gun", "gunpowder"),
                ("electronics.control-board", "atomic"), ("electronics.power-pack", "atomic"), ("electronics.lens", "atomic"), ("electronics.led", "atomic"), ("electronics.display", "atomic"),
                ("electronics.sensor", "atomic"), ("electronics.cell", "atomic"),
            };
            string Avail(string id, string era) => "{ \"id\": " + Q(id) + ", \"from\": " + Q(eraId[era]) + ", \"until\": null }";
            var ageEras = Data.AgesEras.Select(e => "{ \"id\": " + Q(e.Id) + ", \"name\": " + Q(e.Name) + ", \"order\": " + e.Order + ", \"tier\": " + e.Tier + ", \"label\": " + Q(e.Label) + ", \"span\": { \"from\": " + e.From + ", \"to\": " + e.To + " } }");
            var ageAvail = externalGates.Select(g => Avail(g.Id, g.Era)).Concat(matterAvailability.Where(a => Data.AgesEras.Any(e => e.Key == a.Era)).Select(a => Avail(a.Id, a.Era)));
            Write("ages", "pack.json", Manifest("ages", "Ages of history: Stone Age to modern age, and when common materials appear", "historical", "eras", ["core", "mining", "naval", "electronics", "matter"]));
            Write("ages", "resources.json", Document("ages", [("eras", ageEras), ("availability", ageAvail)]));
            Write("ages", "recipes.json", "{\n  \"format\": 1,\n  \"pack\": \"ages\",\n  \"recipes\": []\n}\n");
        
            var horizonEras = Data.HorizonEras.Select(e => "{ \"id\": " + Q(e.Id) + ", \"name\": " + Q(e.Name) + ", \"order\": " + e.Order + ", \"tier\": " + e.Tier + ", \"label\": " + Q(e.Label) + ", \"span\": null }");
            var horizonAvail = matterAvailability.Where(a => Data.HorizonEras.Any(e => e.Key == a.Era)).Select(a => Avail(a.Id, a.Era));
            Write("horizons", "pack.json", Manifest("horizons", "Horizons: the near and far future, and when their materials appear", "fictional", "eras", ["matter"]));
            Write("horizons", "resources.json", Document("horizons", [("eras", horizonEras), ("availability", horizonAvail)]));
            Write("horizons", "recipes.json", "{\n  \"format\": 1,\n  \"pack\": \"horizons\",\n  \"recipes\": []\n}\n");
        }
        
        // ---- arms packs ------------------------------------------------------------------------------------------------------------------------------
        var packIdOf = new Dictionary<string, string> { ["ancient"] = "arms-ancient", ["medieval"] = "arms-medieval", ["modern"] = "arms-modern", ["future"] = "arms-future", ["fantasy"] = "arms-fantasy" };
        var worldOf = new Dictionary<string, string> { ["ancient"] = "historical", ["medieval"] = "historical", ["modern"] = "historical", ["future"] = "fictional", ["fantasy"] = "fictional" };
        var titleOf = new Dictionary<string, string>
        {
            ["ancient"] = "Ancient arms: stone, bronze, iron and classical weapons, armor, tools, boats",
            ["medieval"] = "Medieval and gunpowder arms: swords, bows, plate, cannon and sailing warships",
            ["modern"] = "Industrial and modern arms: rifles, locomotives, tanks, ships and aircraft",
            ["future"] = "Future arms: railguns, exo-frames, drones, starfighters and colony ships",
            ["fantasy"] = "Fantasy arms: mithril, runes, dragon-scale and sky galleons",
        };
        string Resolve(string reference) => reference.Contains('.') && packIdOf.ContainsKey(reference[..reference.IndexOf('.')]) ? packIdOf[reference[..reference.IndexOf('.')]] + reference[reference.IndexOf('.')..] : reference;
        
        var allItems = Data.Items.Concat(Data.MoreItems).Concat(Data.FutureItems).Concat(Data.FantasyItems).ToList();
        var itemCost = new Dictionary<string, decimal>();
        foreach (var group in allItems.GroupBy(l => l.Split('|')[0]))
        {
            var pack = packIdOf[group.Key];
            var resources = new List<string>();
            var recipes = new List<string>();
            var availability = new List<string>();
            var items = new List<string>();
            var deps = new HashSet<string> { "core", "gear" };
            foreach (var line in group)
            {
                var f = line.Split('|');
                if (f.Length != 12) throw new Exception("bad item line: " + line);
                var (key, era, slug, name, cls, replaces, stats, dura, ingredients, station, minutes, extra) = (f[0], f[1], f[2], f[3], f[4], f[5], f[6], decimal.Parse(f[7], inv), f[8], f[9], int.Parse(f[10], inv), f[11]);
                var id = pack + "." + slug;
                var natural = extra.Split(';').Contains("natural");
                deps.Add("ages");
                if (Data.HorizonEras.Any(e => e.Key == era)) deps.Add("horizons");
                var resourceClass = natural ? "unique" : cls == "tool" ? "tool" : "equipment";
        
                decimal cost = 0;
                if (!natural)
                {
                    ingredients = ingredients.Replace("matter.", "matter.");
                    cost = Cost(ingredients, id);
                    foreach (var part in ingredients.Split(';')) deps.Add(PackOf(Split(part).Id));
                    deps.Add(PackOf(Split(station).Id));
                }
                var itemPrice = natural ? 500m : Math.Max(1m, Math.Round(cost * 2.2m, 0));
                price[id] = itemPrice;
                resources.Add(ResourceJson(id, name, resourceClass, "{each}", natural ? "natural" : "crafted", 1, dura > 0 ? dura : null, natural ? "0" : "4", cls + "," + era, itemPrice));
                availability.Add("{ \"id\": " + Q(id) + ", \"from\": " + Q(eraId[era]) + ", \"until\": null }");
                if (!natural)
                {
                    var kind = cls is "vehicle-land" or "vehicle-air" ? "assemble" : cls is "vehicle-sea" or "vehicle-space" or "structure-unit" ? "construct" : "craft";
                    var toolPart = "";
                    var recipeId = pack + ".make-" + slug;
                    recipes.Add(RecipeJson(recipeId, "Make: " + name, kind, Lines(ingredients), toolPart, "{ \"resource\": " + Q(id) + ", \"amount\": 1 }", station, minutes));
                    availability.Add("{ \"id\": " + Q(recipeId) + ", \"from\": " + Q(eraId[era]) + ", \"until\": null }");
                }
        
                // item
                var statJson = StatJson(Complete(cls, era, stats));
                string slots = "", cargo = "", crew = "", fuel = "null", ammo = "null", upkeep = "";
                foreach (var part in extra.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part == "natural") continue;
                    var kv = part.Split('=');
                    switch (kv[0])
                    {
                        case "slots": slots = string.Join(", ", kv[1].Split(',').Select(s => { var p = s.Split(':'); return "{ \"slot\": " + Q(p[0]) + ", \"count\": " + p[1] + " }"; })); break;
                        case "cargo": cargo = string.Join(", ", kv[1].Split(',').Select(s => { var p = s.Split(':'); return "{ \"dimension\": " + Q(p[0]) + ", \"amount\": " + p[1] + " }"; })); break;
                        case "crew": crew = string.Join(", ", kv[1].Split(',').Select(s => { var p = s.Split(':'); var lc = p[1].Split('x'); return "{ \"skill\": " + Q("gear." + p[0]) + ", \"level\": " + lc[0] + ", \"count\": " + lc[1] + " }"; })); break;
                        case "fuel": { var p = kv[1].Split(':'); fuel = "{ \"resource\": " + Q(p[0]) + ", \"capacity\": " + p[1] + ", \"burn-per-hour\": " + p[2] + " }"; deps.Add(PackOf(p[0])); break; }
                        case "ammo": { var p = kv[1].Split(':'); ammo = "{ \"resource\": " + Q(p[0]) + ", \"capacity\": " + p[1] + " }"; deps.Add(PackOf(p[0])); break; }
                        case "upkeep": upkeep = string.Join(", ", kv[1].Split(',').Select(s => { var p = s.Split(':'); deps.Add(PackOf(p[0])); return "{ \"resource\": " + Q(p[0]) + ", \"per-day\": " + p[1] + " }"; })); break;
                        default: throw new Exception("extra " + kv[0]);
                    }
                }
                var replacesId = replaces == "-" ? null : Resolve(replaces);
                if (replacesId is not null) deps.Add(PackOf(replacesId));
                items.Add("{ \"resource\": " + Q(id) + ", \"class\": " + Q(cls) + ", \"stats\": { " + statJson + " }, \"slots\": [" + slots + "], \"cargo\": [" + cargo + "], \"crew\": [" + crew + "], \"fuel\": " + fuel + ", \"ammo\": " + ammo +
                    ", \"upkeep\": [" + upkeep + "], \"replaces\": " + (replacesId is null ? "null" : Q(replacesId)) + " }");
            }

            // ---- formations of this pack
            var units = new List<string>();
            foreach (var line in Data.Units.Where(l => l.StartsWith(group.Key + "|", StringComparison.Ordinal)))
            {
                var f = line.Split('|');
                if (f.Length != 12) throw new Exception("bad unit line: " + line);
                var (era, slug, name, role, training, strength, equipment, crew, upkeepSpec, replaces, minutes) = (f[1], f[2], f[3], f[4], f[5], int.Parse(f[6], inv), f[7], f[8], f[9], f[10], int.Parse(f[11], inv));
                var id = pack + "." + slug;
                deps.Add("ages");
                deps.Add("military");
                if (Data.HorizonEras.Any(e => e.Key == era)) deps.Add("horizons");
                var resolved = string.Join(";", equipment.Split(';').Select(part => { var (item, count) = Split(part); return Resolve(item) + ":" + N(count); }));
                foreach (var part in resolved.Split(';')) deps.Add(PackOf(Split(part).Id));
                decimal cost = 0;
                foreach (var part in resolved.Split(';')) cost += price[Split(part).Id] * Split(part).Amount;
                var unitPrice = Math.Max(1m, Math.Round(cost * 1.5m, 0));
                price[id] = unitPrice;
                resources.Add(ResourceJson(id, name, "unique", "{each}", "crafted", 1, null, "9", "formation," + role, unitPrice));
                availability.Add("{ \"id\": " + Q(id) + ", \"from\": " + Q(eraId[era]) + ", \"until\": null }");
                var recipeId = pack + ".raise-" + slug;
                recipes.Add(RecipeJson(recipeId, "Raise: " + name, "assemble", Lines("core.people:" + strength + ";" + resolved), "", "{ \"resource\": " + Q(id) + ", \"amount\": 1 }", "military.barracks:1", minutes));
                availability.Add("{ \"id\": " + Q(recipeId) + ", \"from\": " + Q(eraId[era]) + ", \"until\": null }");
                var equipmentJson = string.Join(", ", resolved.Split(';').Select(part => { var (item, count) = Split(part); return "{ \"item\": " + Q(item) + ", \"count\": " + (int)count + " }"; }));
                var crewJson = crew == "-" ? "" : string.Join(", ", crew.Split(';').Select(s => { var p = s.Split(':'); var lc = p[1].Split('x'); return "{ \"skill\": " + Q("gear." + p[0]) + ", \"level\": " + lc[0] + ", \"count\": " + lc[1] + " }"; }));
                var upkeepJson = upkeepSpec == "-" ? "" : string.Join(", ", upkeepSpec.Split(';').Select(s => { var p = s.Split(':'); deps.Add(PackOf(p[0])); return "{ \"resource\": " + Q(p[0]) + ", \"per-day\": " + p[1] + " }"; }));
                var replacesId = replaces == "-" ? null : Resolve(replaces);
                if (replacesId is not null) deps.Add(PackOf(replacesId));
                units.Add("{ \"resource\": " + Q(id) + ", \"strength\": " + strength + ", \"role\": " + Q(role) + ", \"training\": " + Q(training) + ", \"equipment\": [" + equipmentJson + "], \"crew\": [" + crewJson +
                    "], \"upkeep\": [" + upkeepJson + "], \"replaces\": " + (replacesId is null ? "null" : Q(replacesId)) + " }");
            }
            deps.Remove(pack);
            Write(pack, "pack.json", Manifest(pack, titleOf[group.Key], worldOf[group.Key], group.Key + "-arms", deps));
            Write(pack, "resources.json", Document(pack, [("resources", resources), ("availability", availability), ("items", items), ("units", units)]));
            Write(pack, "recipes.json", Document(pack, [("recipes", recipes)]));
        }
        return generated;
    }
}

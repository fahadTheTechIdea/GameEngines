using GameContent.Catalog;

namespace GameContent.Tests;

/// <summary>Builds small inline packs for the refusal tests, so each rule is exercised by the smallest pack that breaks it.</summary>
internal static class PackBuilder
{
    public static string Res(string id, string cls = "raw", string unit = "kg", string origin = "natural", string stack = "10", string grade = "null",
        string perishable = "null", string durability = "null", string currency = "null", string cpc = "null", string tags = "", string properties = "") =>
        $$"""{ "id": "{{id}}", "name": "{{id}}", "class": "{{cls}}", "unit": "{{unit}}", "origin": "{{origin}}", "stack": { "max": {{stack}} }, "grade": {{grade}}, "perishable": {{perishable}}, "durability": {{durability}}, "currency": {{currency}}, "cpc": {{cpc}}, "tags": [{{tags}}], "properties": {{{properties}}} }""";

    public static string Line(string resource, string amount) => $$"""{ "resource": "{{resource}}", "amount": {{amount}} }""";

    public static string Rec(string id, string kind = "craft", string inputs = "", string catalysts = "", string tools = "", string outputs = "", string byproducts = "",
        string drops = "", string station = "null", string skill = "null", string environment = "", string duration = """{ "value": 5, "unit": "s" }""",
        string quality = "null", string reverse = "null") =>
        $$"""{ "id": "{{id}}", "name": "{{id}}", "kind": "{{kind}}", "inputs": [{{inputs}}], "catalysts": [{{catalysts}}], "tools": [{{tools}}], "outputs": [{{outputs}}], "byproducts": [{{byproducts}}], "drops": [{{drops}}], "station": {{station}}, "skill": {{skill}}, "environment": [{{environment}}], "duration": {{duration}}, "quality": {{quality}}, "reverse": {{reverse}} }""";

    public static string Document(string pack, string resources = "", string recipes = "", string stations = "", string skills = "")
    {
        var parts = new List<string> { "\"format\": 1", $"\"pack\": \"{pack}\"" };
        if (resources.Length > 0) parts.Add($"\"resources\": [{resources}]");
        if (stations.Length > 0) parts.Add($"\"stations\": [{stations}]");
        if (skills.Length > 0) parts.Add($"\"skills\": [{skills}]");
        if (recipes.Length > 0) parts.Add($"\"recipes\": [{recipes}]");
        return "{ " + string.Join(", ", parts) + " }";
    }

    public static string Manifest(string id, string dependencies = "", string documents = "\"doc.json\"", string version = "1.0.0", string provenance = "original", string locales = "", string world = "timeless") =>
        $$"""{ "format": 1, "id": "{{id}}", "version": "{{version}}", "title": "{{id}}", "language": "en", "world": "{{world}}", "genres": ["test"], "dependencies": [{{dependencies}}], "license": { "spdx": "CC0-1.0", "provenance": "{{provenance}}", "author": "tests" }, "locales": [{{locales}}], "documents": [{{documents}}] }""";

    /// <summary>One pack "t" with the given resources and recipes, and nothing else.</summary>
    public static MemoryCatalogSource Single(string resources, string recipes = "", string stations = "", string skills = "") => new(new Dictionary<string, string>
    {
        ["t/pack.json"] = Manifest("t"),
        ["t/doc.json"] = Document("t", resources, recipes, stations, skills),
    });

    public static CatalogLoadResult Load(MemoryCatalogSource source, string pack = "t") => CatalogLoader.Load(source, pack);

    public static CatalogReport Check(string resources, string recipes = "", string stations = "", string skills = "") => Load(Single(resources, recipes, stations, skills)).Report;

    /// <summary>Two resources, a (natural) and b (crafted), for tests that need a minimal valid base.</summary>
    public static string AB => Res("t.a") + "," + Res("t.b", origin: "crafted");
}

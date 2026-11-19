using System.Text.RegularExpressions;

namespace GameContent.Catalog;

/// <summary>
/// Writes the folder of a new pack that already validates: a manifest that depends on <c>core</c>, one resource made from a core
/// ingot at a core station, and the recipe that makes it. The author replaces the sample with the genre's real content.
/// </summary>
public static partial class CatalogScaffold
{
    [GeneratedRegex("^[a-z][a-z0-9-]{0,31}$")]
    private static partial Regex Slug();

    /// <summary>Creates <c>&lt;packs&gt;/&lt;id&gt;/</c> with its three files. Refuses an existing folder rather than overwrite anything.</summary>
    public static IReadOnlyList<string> Create(string packsFolder, string id, string genre, string author)
    {
        ArgumentException.ThrowIfNullOrEmpty(packsFolder);
        foreach (var (name, value) in new[] { ("pack id", id), ("genre", genre) })
            if (!Slug().IsMatch(value)) throw new ArgumentException(name + " '" + value + "' must be lower-case letters, digits and dashes, starting with a letter (at most 32)", nameof(id));
        if (string.IsNullOrWhiteSpace(author) || author.Contains('"') || author.Contains('\\')) throw new ArgumentException("the author must be a plain, non-empty name", nameof(author));
        var folder = Path.Combine(packsFolder, id);
        if (Directory.Exists(folder)) throw new IOException("pack folder already exists: " + folder);

        var manifest = $$"""
            {
              "format": 1,
              "id": "{{id}}",
              "version": "0.1.0",
              "title": "{{id}} pack",
              "language": "en",
              "world": "timeless",
              "genres": ["{{genre}}"],
              "dependencies": [{ "id": "core", "version": "1.0.0" }],
              "license": { "spdx": "CC0-1.0", "provenance": "original", "author": "{{author}}" },
              "locales": [],
              "documents": ["resources.json", "recipes.json"]
            }
            """;
        var resources = $$"""
            {
              "format": 1,
              "pack": "{{id}}",
              "resources": [
                { "id": "{{id}}.sample-part", "name": "Sample part", "class": "intermediate", "unit": "{each}", "origin": "crafted", "stack": { "max": 100 }, "grade": null, "perishable": null, "durability": null, "currency": null, "cpc": "4", "tags": ["sample"], "properties": {} }
              ]
            }
            """;
        var recipes = $$"""
            {
              "format": 1,
              "pack": "{{id}}",
              "recipes": [
                { "id": "{{id}}.forge-sample-part", "name": "Forge a sample part", "kind": "craft", "inputs": [{ "resource": "core.iron-ingot", "amount": 1 }], "catalysts": [], "tools": [], "outputs": [{ "resource": "{{id}}.sample-part", "amount": 2 }], "byproducts": [], "drops": [], "station": { "id": "core.workbench", "level": 1 }, "skill": null, "environment": [], "duration": { "value": 30, "unit": "s" }, "quality": null, "reverse": null }
              ]
            }
            """;
        Directory.CreateDirectory(folder);
        var written = new List<string>();
        foreach (var (file, text) in new[] { ("pack.json", manifest), ("resources.json", resources), ("recipes.json", recipes) })
        {
            var path = Path.Combine(folder, file);
            File.WriteAllText(path, text + "\n");
            written.Add(path);
        }
        return written;
    }
}

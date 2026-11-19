using System.Text.Json;

namespace AlchemyEngine;

/// <summary>
/// The one place alchemy content is read and checked. Anything wrong is a refusal with a code naming the first problem;
/// nothing is repaired, defaulted or skipped.
/// </summary>
public static partial class AlchemyContentLoader
{
    private const int Format = 1;
    private const int MaxDenominator = 1_000_000;

    public static AlchemyContent Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument document;
        try { document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false }); }
        catch (JsonException exception) { throw ContentJson.Refuse(AlchemyCodes.ContentFormat, "not valid JSON: " + exception.Message); }
        using (document)
        {
            var root = ContentJson.Object(document.RootElement, "content", "format", "elements", "recipes");
            if (ContentJson.Int(root, "format", "content") != Format)
                throw ContentJson.Refuse(AlchemyCodes.ContentFormat, "unsupported content format");
            var elements = ReadElements(root.GetProperty("elements"));
            var recipes = ReadRecipes(root.GetProperty("recipes"));
            Validate(elements, recipes);
            return new AlchemyContent(elements, recipes);
        }
    }

    private static List<ElementDef> ReadElements(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array) throw ContentJson.Refuse(AlchemyCodes.ContentFormat, "elements must be an array");
        var list = new List<ElementDef>();
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var where = "elements[" + index++ + "]";
            ContentJson.Object(item, where, "id", "name", "properties", "starting");
            var id = ContentJson.RequireId(ContentJson.String(item, "id", where), where);
            list.Add(new ElementDef(id, ContentJson.String(item, "name", where), ContentJson.Ids(item, "properties", where), ContentJson.Bool(item, "starting", where)));
        }
        return list;
    }

    private static List<RecipeDef> ReadRecipes(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array) throw ContentJson.Refuse(AlchemyCodes.ContentFormat, "recipes must be an array");
        var list = new List<RecipeDef>();
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var where = "recipes[" + index++ + "]";
            ContentJson.Object(item, where, "id", "inputs", "catalysts", "environment", "outputs", "ordered", "chance");
            var id = ContentJson.RequireId(ContentJson.String(item, "id", where), where);
            var chance = ContentJson.Object(item.GetProperty("chance"), where + ".chance", "numerator", "denominator");
            list.Add(new RecipeDef(
                id,
                ContentJson.Ids(item, "inputs", where),
                ContentJson.Ids(item, "catalysts", where),
                ContentJson.Ids(item, "environment", where),
                ContentJson.Ids(item, "outputs", where),
                ContentJson.Bool(item, "ordered", where),
                ContentJson.Int(chance, "numerator", where + ".chance"),
                ContentJson.Int(chance, "denominator", where + ".chance")));
        }
        return list;
    }
}

using System.Text.Json.Nodes;

namespace EconomyEngine.Tests;

/// <summary>Edits of the sample document, by path, for the tests that need a document that is wrong in one place.</summary>
internal static class Edit
{
    /// <summary>The sample with the edit applied to its parsed form.</summary>
    public static string Doc(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(Fixture.TownText)!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    public static JsonObject Obj(JsonObject root, string array, int index) => root[array]!.AsArray()[index]!.AsObject();

    public static JsonObject ById(JsonObject root, string array, string id) => root[array]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);

    public static JsonObject Market(JsonObject root, string id) => ById(root, "markets", id);

    /// <summary>The pair of a good in a market.</summary>
    public static JsonObject Pair(JsonObject root, string market, string good) => Market(root, market)["pairs"]!.AsArray().Select(n => n!.AsObject()).First(o => o["good"]!.GetValue<string>() == good);

    public static JsonNode Copy(JsonNode node) => JsonNode.Parse(node.ToJsonString())!;
}

using System.Text.Json.Nodes;

namespace RelationshipEngine.Tests;

/// <summary>Edits of the sample document, by path, for the tests that need a document that is wrong in one place.</summary>
internal static class Edit
{
    /// <summary>The sample with the edit applied to its parsed form.</summary>
    public static string Doc(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(Fixture.VillageText)!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    public static JsonObject Obj(JsonObject root, string array, int index) => root[array]!.AsArray()[index]!.AsObject();

    public static JsonObject Interaction(JsonObject root, string id) => root["interactions"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);

    public static JsonObject Memory(JsonObject root, string id) => root["memories"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);

    public static JsonObject Moodlet(JsonObject root, string id) => root["moodlets"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);

    public static JsonObject Need(JsonObject root, string id) => root["needs"]!.AsArray().Select(n => n!.AsObject()).First(o => o["id"]!.GetValue<string>() == id);

    /// <summary>A copy of a node, so a document can repeat an entry.</summary>
    public static JsonNode Copy(JsonNode node) => JsonNode.Parse(node.ToJsonString())!;
}

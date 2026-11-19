using System.Text;
using System.Text.Json;
using AlchemyEngine;
using Beep.SimEngine;
using Beep.SimEngine.Content;

namespace GameRuntime;

public static partial class SimRecipeSource
{
    /// <summary>
    /// Writes the alchemy content document for the bridged recipes and loads it through the alchemy loader, so its own checks
    /// (ambiguous combinations, unreachable elements, bad ids) apply and refuse with their own codes. Elements are the engine's resources;
    /// a resource no bridged recipe makes is a starting element. A recipe's inputs are the resources it consumes, its catalysts the
    /// tools it holds, its outputs the resources it makes. There are no quantities in this document.
    /// </summary>
    private static AlchemyContent AlchemyContentOf(ResolvedContent content, IReadOnlyList<SimRecipe> recipes)
    {
        var made = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipe in recipes)
            foreach (var line in recipe.Facts.Outputs.Lines) made.Add(content.ResourceName(line.Resource));

        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteNumber("format", 1);
            json.WriteStartArray("elements");
            foreach (var resource in content.Resources)
            {
                json.WriteStartObject();
                json.WriteString("id", resource.Id);
                json.WriteString("name", resource.Id);
                json.WriteStartArray("properties");
                foreach (var property in PropertiesOf(resource)) json.WriteStringValue(property);
                json.WriteEndArray();
                json.WriteBoolean("starting", !made.Contains(resource.Id));
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteStartArray("recipes");
            foreach (var recipe in recipes)
            {
                json.WriteStartObject();
                json.WriteString("id", recipe.Id);
                WriteResources(json, "inputs", content, recipe.Facts.Inputs);
                WriteResources(json, "catalysts", content, recipe.Facts.Tools);
                json.WriteStartArray("environment");
                json.WriteEndArray();
                WriteResources(json, "outputs", content, recipe.Facts.Outputs);
                json.WriteBoolean("ordered", false);
                json.WriteStartObject("chance");
                json.WriteNumber("numerator", 1);
                json.WriteNumber("denominator", 1);
                json.WriteEndObject();
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return AlchemyContentLoader.Load(Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static void WriteResources(Utf8JsonWriter json, string name, ResolvedContent content, Bill bill)
    {
        json.WriteStartArray(name);
        foreach (var line in bill.Lines) json.WriteStringValue(content.ResourceName(line.Resource));
        json.WriteEndArray();
    }

    /// <summary>The authored category, phase and traits of a resource, as descriptive tags. Names are the engine's resource ids.</summary>
    private static IEnumerable<string> PropertiesOf(IResourceContentDefinition resource)
    {
        if (resource is not ResourceDef definition) return Array.Empty<string>();
        var tags = new SortedSet<string>(StringComparer.Ordinal) { definition.Category };
        if (!definition.Phase.IsNone) tags.Add(definition.Phase.Id);
        foreach (var trait in definition.Traits) tags.Add(trait);
        return tags;
    }
}

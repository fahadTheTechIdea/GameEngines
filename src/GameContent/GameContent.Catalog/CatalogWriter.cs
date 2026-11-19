using System.Globalization;
using System.Text.Json;

namespace GameContent.Catalog;

/// <summary>
/// The canonical JSON of one pack's documents, written from the model in a fixed order. Reading it back and writing again gives the
/// same bytes, so a pack's meaning is independent of how its files were formatted.
/// </summary>
public static partial class CatalogWriter
{
    /// <summary>One canonical document with everything the pack defines. Property values sort by name.</summary>
    public static string Document(CatalogPackData pack)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteNumber("format", 1);
            json.WriteString("pack", pack.Id);
            json.WriteStartArray("resources");
            foreach (var resource in pack.Resources) Resource(json, resource);
            json.WriteEndArray();
            json.WriteStartArray("stations");
            foreach (var station in pack.Stations) { json.WriteStartObject(); json.WriteString("id", station.Id); json.WriteString("name", station.Name); json.WriteNumber("levels", station.Levels); json.WriteEndObject(); }
            json.WriteEndArray();
            json.WriteStartArray("skills");
            foreach (var skill in pack.Skills) { json.WriteStartObject(); json.WriteString("id", skill.Id); json.WriteString("name", skill.Name); json.WriteNumber("max", skill.Max); json.WriteEndObject(); }
            json.WriteEndArray();
            json.WriteStartArray("recipes");
            foreach (var recipe in pack.Recipes) Recipe(json, recipe);
            json.WriteEndArray();
            Equipment(json, pack);
            json.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Resource(Utf8JsonWriter json, CatalogResource r)
    {
        json.WriteStartObject();
        json.WriteString("id", r.Id);
        json.WriteString("name", r.Name);
        json.WriteString("class", Names<ResourceClass>.Of(r.Class));
        json.WriteString("unit", r.Unit);
        json.WriteString("origin", Names<ResourceOrigin>.Of(r.Origin));
        json.WriteStartObject("stack"); json.WriteNumber("max", r.StackMax); json.WriteEndObject();
        if (r.Grade is null) json.WriteNull("grade");
        else
        {
            json.WriteStartObject("grade");
            json.WriteString("scheme", Names<GradeScheme>.Of(r.Grade.Scheme));
            json.WriteStartArray("levels");
            foreach (var level in r.Grade.Levels) json.WriteStringValue(level);
            json.WriteEndArray();
            json.WriteEndObject();
        }
        if (r.Perishable is null) json.WriteNull("perishable");
        else
        {
            json.WriteStartObject("perishable");
            json.WriteNumber("ticks", r.Perishable.Ticks);
            if (r.Perishable.Result is null) json.WriteNull("result"); else json.WriteString("result", r.Perishable.Result);
            json.WriteEndObject();
        }
        if (r.DurabilityUses is null) json.WriteNull("durability");
        else { json.WriteStartObject("durability"); json.WriteNumber("uses", r.DurabilityUses.Value); json.WriteEndObject(); }
        if (r.Currency is null) json.WriteNull("currency");
        else { json.WriteStartObject("currency"); json.WriteString("kind", Names<CurrencyKind>.Of(r.Currency.Kind)); json.WriteString("code", r.Currency.Code); json.WriteEndObject(); }
        if (r.Cpc is null) json.WriteNull("cpc"); else json.WriteString("cpc", r.Cpc);
        json.WriteStartArray("tags");
        foreach (var tag in r.Tags) json.WriteStringValue(tag);
        json.WriteEndArray();
        json.WriteStartObject("properties");
        foreach (var (name, value) in r.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (value is string text) json.WriteString(name, text);
            else if (value is decimal number) json.WriteNumber(name, number);
            else json.WriteBoolean(name, (bool)value);
        }
        json.WriteEndObject();
        json.WriteEndObject();
    }

    private static void Recipe(Utf8JsonWriter json, CatalogRecipe r)
    {
        json.WriteStartObject();
        json.WriteString("id", r.Id);
        json.WriteString("name", r.Name);
        json.WriteString("kind", Names<RecipeKind>.Of(r.Kind));
        Lines(json, "inputs", r.Inputs);
        Lines(json, "catalysts", r.Catalysts);
        json.WriteStartArray("tools");
        foreach (var tool in r.Tools) { json.WriteStartObject(); json.WriteString("resource", tool.Resource); json.WriteNumber("uses", tool.Uses); json.WriteEndObject(); }
        json.WriteEndArray();
        Outputs(json, "outputs", r.Outputs);
        Outputs(json, "byproducts", r.Byproducts);
        json.WriteStartArray("drops");
        foreach (var drop in r.Drops)
        {
            json.WriteStartObject();
            json.WriteString("resource", drop.Resource);
            Amount(json, drop.Min, drop.Max);
            json.WriteString("chance", drop.Numerator.ToString(CultureInfo.InvariantCulture) + "/" + drop.Denominator.ToString(CultureInfo.InvariantCulture));
            json.WriteEndObject();
        }
        json.WriteEndArray();
        if (r.Station is null) json.WriteNull("station");
        else { json.WriteStartObject("station"); json.WriteString("id", r.Station.Station); json.WriteNumber("level", r.Station.Level); json.WriteEndObject(); }
        if (r.Skill is null) json.WriteNull("skill");
        else { json.WriteStartObject("skill"); json.WriteString("id", r.Skill.Skill); json.WriteNumber("level", r.Skill.Level); json.WriteEndObject(); }
        json.WriteStartArray("environment");
        foreach (var tag in r.Environment) json.WriteStringValue(tag);
        json.WriteEndArray();
        json.WriteStartObject("duration"); json.WriteNumber("value", r.Duration.Value); json.WriteString("unit", Names<TimeUnit>.Of(r.Duration.Unit)); json.WriteEndObject();
        if (r.Quality is null) json.WriteNull("quality"); else json.WriteString("quality", Names<QualityMode>.Of(r.Quality.Value));
        if (r.Reverse is null) json.WriteNull("reverse"); else json.WriteString("reverse", r.Reverse);
        json.WriteEndObject();
    }

    private static void Lines(Utf8JsonWriter json, string name, IEnumerable<Ingredient> lines)
    {
        json.WriteStartArray(name);
        foreach (var line in lines) { json.WriteStartObject(); json.WriteString("resource", line.Resource); json.WriteNumber("amount", line.Amount); json.WriteEndObject(); }
        json.WriteEndArray();
    }

    private static void Outputs(Utf8JsonWriter json, string name, IEnumerable<OutputLine> lines)
    {
        json.WriteStartArray(name);
        foreach (var line in lines) { json.WriteStartObject(); json.WriteString("resource", line.Resource); Amount(json, line.Min, line.Max); json.WriteEndObject(); }
        json.WriteEndArray();
    }

    private static void Amount(Utf8JsonWriter json, decimal min, decimal max)
    {
        if (min == max) { json.WriteNumber("amount", min); return; }
        json.WriteStartObject("amount");
        json.WriteNumber("min", min);
        json.WriteNumber("max", max);
        json.WriteEndObject();
    }
}

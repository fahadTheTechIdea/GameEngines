using System.Reflection;
using System.Text.Json;
using ScenarioEngine.Content;
using ScenarioEngine.Contracts;

namespace GameContent.Catalog;

/// <summary>
/// Structural validation against the standard's JSON Schemas, using the repo's one schema evaluator
/// (<see cref="JsonSchemaContractValidator"/>). The schemas are the files in <c>schemas/catalog/</c>, embedded at build time.
/// </summary>
public static class CatalogSchemas
{
    private static readonly JsonSchemaContractValidator Validator = new();
    private static readonly JsonElement PackSchema = Load("catalog.pack.schema.json");
    private static readonly JsonElement DocumentSchema = Load("catalog.document.schema.json");
    private static readonly JsonElement LocaleSchema = Load("catalog.locale.schema.json");

    public static void ValidatePack(JsonElement value, string file, CatalogReport report) => Validate(PackSchema, value, file, report);

    public static void ValidateDocument(JsonElement value, string file, CatalogReport report) => Validate(DocumentSchema, value, file, report);

    public static void ValidateLocale(JsonElement value, string file, CatalogReport report) => Validate(LocaleSchema, value, file, report);

    // The schema library builds a schema lazily on first use; evaluating the same schema from several threads at once was seen to let an
    // invalid document through, so evaluation is serialized.
    private static readonly object Gate = new();

    private static void Validate(JsonElement schema, JsonElement value, string file, CatalogReport report)
    {
        ValidationReport result;
        lock (Gate) result = Validator.Validate(schema, value, file);
        foreach (var diagnostic in result.Diagnostics)
            report.Add(CatalogCodes.Schema, diagnostic.Message, file + diagnostic.Pointer);
    }

    private static JsonElement Load(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("the catalog schema '" + name + "' is not embedded in the assembly");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }
}

using System.Text.Json;
using DialogueEngine;
using EngineCommon;

namespace GameRuntime;

/// <summary>
/// The people who may speak in a dialogue document: the archetypes of the catalog's character document (the one
/// <c>CatalogCharacters.Document</c> writes), plus any extra ids the game names, such as a narrator. The dialogue engine never invents a speaker.
/// </summary>
public sealed class CatalogSpeakers : ISpeakerCatalog
{
    public const string BadDocument = "DIALOGUE_SPEAKERS_DOCUMENT";

    private readonly HashSet<string> _ids;

    private CatalogSpeakers(HashSet<string> ids) { _ids = ids; }

    public bool HasSpeaker(string id) => _ids.Contains(id);

    public IReadOnlyCollection<string> Ids => _ids;

    /// <param name="charactersDocument">A JSON document with a <c>character_archetypes</c> array whose entries have an <c>id</c>.</param>
    /// <param name="extra">Speakers that are not catalog characters.</param>
    public static CatalogSpeakers FromCharacters(string charactersDocument, params string[] extra)
    {
        ArgumentNullException.ThrowIfNull(charactersDocument);
        var ids = new HashSet<string>(extra, StringComparer.Ordinal);
        try
        {
            using var document = JsonDocument.Parse(charactersDocument);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("character_archetypes", out var archetypes) || archetypes.ValueKind != JsonValueKind.Array)
                throw new EngineRefusedException(BadDocument, "the character document has no character_archetypes array");
            foreach (var archetype in archetypes.EnumerateArray())
            {
                if (archetype.ValueKind != JsonValueKind.Object || !archetype.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(id.GetString()))
                    throw new EngineRefusedException(BadDocument, "a character archetype has no id");
                ids.Add(id.GetString()!);
            }
        }
        catch (JsonException exception)
        {
            throw new EngineRefusedException(BadDocument, "the character document is not JSON: " + exception.Message);
        }
        return new CatalogSpeakers(ids);
    }
}
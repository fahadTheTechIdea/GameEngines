using System.Text;
using System.Text.Json;

namespace GameContent.Catalog;

/// <summary>
/// The bridge from the catalog's formations to the character factory's definitions, and the only code that reads a formation for this
/// purpose. A formation becomes one archetype (its people, with the stat and skill ranges its training level selects from the template)
/// and one factory formation (the same strength, role and equipment, the catalog unit it came from). Nothing about headcount or
/// equipment is stated twice: change the catalog and regenerate.
/// </summary>
public static class CatalogCharacters
{
    /// <summary>The id of the archetype generated for a catalog formation.</summary>
    public static string ArchetypeId(string formation) => formation + ".member";

    /// <summary>A JSON document with the <c>character_archetypes</c> and <c>character_formations</c> sections for the given catalog formations.</summary>
    public static string Document(CatalogSet set, IReadOnlyList<string> formations, CharacterTemplate template)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(formations);
        ArgumentNullException.ThrowIfNull(template);
        Check(template);
        var units = new List<CatalogFormation>();
        foreach (var id in formations)
        {
            if (!set.TryUnit(id, out var unit)) throw new CharacterAdaptRefusedException(CharacterAdaptCodes.NotAFormation, "'" + id + "' is not a formation");
            units.Add(unit);
        }

        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteStartArray("character_archetypes");
            foreach (var unit in units) Archetype(json, unit, template);
            json.WriteEndArray();
            json.WriteStartArray("character_formations");
            foreach (var unit in units) Formation(json, unit, template);
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Check(CharacterTemplate t)
    {
        if (string.IsNullOrWhiteSpace(t.IndividualEntity) || string.IsNullOrWhiteSpace(t.SquadEntity) || string.IsNullOrWhiteSpace(t.GivenNames) || string.IsNullOrWhiteSpace(t.FamilyNames))
            throw new CharacterAdaptRefusedException(CharacterAdaptCodes.BadTemplate, "the template needs entity and name pool ids");
        if (t.MaxSpawn < 1 || t.MaxSquads < 1 || t.TraitMin < 0 || t.TraitMin > t.TraitMax || t.TraitMax > t.Traits.Count)
            throw new CharacterAdaptRefusedException(CharacterAdaptCodes.BadTemplate, "the template limits are inconsistent");
    }

    private static void Archetype(Utf8JsonWriter json, CatalogFormation unit, CharacterTemplate t)
    {
        json.WriteStartObject();
        json.WriteString("id", ArchetypeId(unit.Resource));
        json.WriteString("entity", t.IndividualEntity);
        json.WriteNumber("max_spawn", t.MaxSpawn);
        json.WriteString("given_names", t.GivenNames);
        json.WriteString("family_names", t.FamilyNames);
        Ranges(json, "stats", "stat", t.Stats, unit.Training);
        Ranges(json, "skills", "skill", t.Skills, unit.Training);
        json.WriteStartObject("traits");
        json.WriteNumber("min", t.TraitMin);
        json.WriteNumber("max", t.TraitMax);
        json.WriteStartArray("pool");
        foreach (var trait in t.Traits)
        {
            json.WriteStartObject();
            json.WriteString("trait", trait.Id);
            json.WriteNumber("weight", trait.Weight);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteEndObject();
        json.WriteStartArray("equipment");
        json.WriteEndArray();
        json.WriteStartArray("roles");
        json.WriteStringValue(unit.Role);
        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void Ranges(Utf8JsonWriter json, string section, string key, IReadOnlyList<StatPlan> plans, TrainingLevel training)
    {
        json.WriteStartArray(section);
        foreach (var plan in plans)
        {
            var range = plan.Ranges.For(training);
            json.WriteStartObject();
            json.WriteString(key, plan.Id);
            json.WriteNumber("min", range.Min);
            json.WriteNumber("max", range.Max);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void Formation(Utf8JsonWriter json, CatalogFormation unit, CharacterTemplate t)
    {
        json.WriteStartObject();
        json.WriteString("id", unit.Resource);
        json.WriteString("squad_entity", t.SquadEntity);
        json.WriteNumber("max_squads", t.MaxSquads);
        json.WriteString("catalog_unit", unit.Resource);
        json.WriteStartArray("members");
        json.WriteStartObject();
        json.WriteString("archetype", ArchetypeId(unit.Resource));
        json.WriteNumber("count", unit.Strength);
        json.WriteEndObject();
        json.WriteEndArray();
        json.WriteStartArray("equipment");
        foreach (var equipment in unit.Equipment)
        {
            json.WriteStartObject();
            json.WriteString("item", equipment.Item);
            json.WriteNumber("count", equipment.Count);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteEndObject();
    }
}

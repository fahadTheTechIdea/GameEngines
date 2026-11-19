using System.Text.Json;

namespace GameContent.Catalog;

public static partial class CatalogWriter
{
    private static void Equipment(Utf8JsonWriter json, CatalogPackData pack)
    {
        json.WriteStartArray("eras");
        foreach (var era in pack.Eras)
        {
            json.WriteStartObject();
            json.WriteString("id", era.Id);
            json.WriteString("name", era.Name);
            json.WriteNumber("order", era.Order);
            json.WriteNumber("tier", era.Tier);
            json.WriteString("label", Names<PeriodLabel>.Of(era.Label));
            if (era.Span is null) json.WriteNull("span");
            else { json.WriteStartObject("span"); json.WriteNumber("from", era.Span.From); json.WriteNumber("to", era.Span.To); json.WriteEndObject(); }
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteStartArray("availability");
        foreach (var entry in pack.Availability)
        {
            json.WriteStartObject();
            json.WriteString("id", entry.Id);
            json.WriteString("from", entry.From);
            if (entry.Until is null) json.WriteNull("until"); else json.WriteString("until", entry.Until);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteStartArray("stats");
        foreach (var stat in pack.Stats)
        {
            json.WriteStartObject();
            json.WriteString("id", stat.Id);
            json.WriteString("name", stat.Name);
            json.WriteString("dimension", stat.Dimension);
            json.WriteString("unit", stat.Unit);
            json.WriteString("kind", Names<StatKind>.Of(stat.Kind));
            if (stat.Min is null) json.WriteNull("min"); else json.WriteNumber("min", stat.Min.Value);
            if (stat.Max is null) json.WriteNull("max"); else json.WriteNumber("max", stat.Max.Value);
            json.WriteStartArray("values");
            foreach (var value in stat.Values) json.WriteStringValue(value);
            json.WriteEndArray();
            json.WriteStartArray("classes");
            foreach (var itemClass in stat.Classes) json.WriteStringValue(Names<ItemClass>.Of(itemClass));
            json.WriteEndArray();
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteStartArray("items");
        foreach (var item in pack.Items) Item(json, item);
        json.WriteEndArray();
        json.WriteStartArray("units");
        foreach (var unit in pack.Units)
        {
            json.WriteStartObject();
            json.WriteString("resource", unit.Resource);
            json.WriteNumber("strength", unit.Strength);
            json.WriteString("role", unit.Role);
            json.WriteString("training", Names<TrainingLevel>.Of(unit.Training));
            json.WriteStartArray("equipment");
            foreach (var equipment in unit.Equipment) { json.WriteStartObject(); json.WriteString("item", equipment.Item); json.WriteNumber("count", equipment.Count); json.WriteEndObject(); }
            json.WriteEndArray();
            json.WriteStartArray("crew");
            foreach (var crew in unit.Crew) { json.WriteStartObject(); json.WriteString("skill", crew.Skill); json.WriteNumber("level", crew.Level); json.WriteNumber("count", crew.Count); json.WriteEndObject(); }
            json.WriteEndArray();
            json.WriteStartArray("upkeep");
            foreach (var upkeep in unit.Upkeep) { json.WriteStartObject(); json.WriteString("resource", upkeep.Resource); json.WriteNumber("per-day", upkeep.PerDay); json.WriteEndObject(); }
            json.WriteEndArray();
            if (unit.Replaces is null) json.WriteNull("replaces"); else json.WriteString("replaces", unit.Replaces);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void Item(Utf8JsonWriter json, CatalogItem item)
    {
        json.WriteStartObject();
        json.WriteString("resource", item.Resource);
        json.WriteString("class", Names<ItemClass>.Of(item.Class));
        json.WriteStartObject("stats");
        foreach (var (name, value) in item.Stats.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            if (value is string text) json.WriteString(name, text); else json.WriteNumber(name, (decimal)value);
        }
        json.WriteEndObject();
        json.WriteStartArray("slots");
        foreach (var slot in item.Slots) { json.WriteStartObject(); json.WriteString("slot", slot.Slot); json.WriteNumber("count", slot.Count); json.WriteEndObject(); }
        json.WriteEndArray();
        json.WriteStartArray("cargo");
        foreach (var cargo in item.Cargo) { json.WriteStartObject(); json.WriteString("dimension", cargo.Dimension); json.WriteNumber("amount", cargo.Amount); json.WriteEndObject(); }
        json.WriteEndArray();
        json.WriteStartArray("crew");
        foreach (var crew in item.Crew) { json.WriteStartObject(); json.WriteString("skill", crew.Skill); json.WriteNumber("level", crew.Level); json.WriteNumber("count", crew.Count); json.WriteEndObject(); }
        json.WriteEndArray();
        if (item.Fuel is null) json.WriteNull("fuel");
        else { json.WriteStartObject("fuel"); json.WriteString("resource", item.Fuel.Resource); json.WriteNumber("capacity", item.Fuel.Capacity); json.WriteNumber("burn-per-hour", item.Fuel.BurnPerHour); json.WriteEndObject(); }
        if (item.Ammo is null) json.WriteNull("ammo");
        else { json.WriteStartObject("ammo"); json.WriteString("resource", item.Ammo.Resource); json.WriteNumber("capacity", item.Ammo.Capacity); json.WriteEndObject(); }
        json.WriteStartArray("upkeep");
        foreach (var upkeep in item.Upkeep) { json.WriteStartObject(); json.WriteString("resource", upkeep.Resource); json.WriteNumber("per-day", upkeep.PerDay); json.WriteEndObject(); }
        json.WriteEndArray();
        if (item.Replaces is null) json.WriteNull("replaces"); else json.WriteString("replaces", item.Replaces);
        json.WriteEndObject();
    }
}

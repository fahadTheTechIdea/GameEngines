using System.Text.Json;

namespace GameContent.Catalog;

public static partial class CatalogReader
{
    private static CatalogEra Era(JsonElement e)
    {
        var span = e.GetProperty("span");
        return new CatalogEra(
            e.GetProperty("id").GetString()!, e.GetProperty("name").GetString()!, e.GetProperty("order").GetInt32(), e.GetProperty("tier").GetInt32(),
            Names<PeriodLabel>.Parse(e.GetProperty("label").GetString()!),
            span.ValueKind == JsonValueKind.Null ? null : new EraSpan(span.GetProperty("from").GetInt32(), span.GetProperty("to").GetInt32()));
    }

    private static CatalogStat Stat(JsonElement e) => new(
        e.GetProperty("id").GetString()!, e.GetProperty("name").GetString()!, e.GetProperty("dimension").GetString()!, e.GetProperty("unit").GetString()!,
        Names<StatKind>.Parse(e.GetProperty("kind").GetString()!),
        NullableNumber(e.GetProperty("min")), NullableNumber(e.GetProperty("max")),
        Strings(e.GetProperty("values")),
        e.GetProperty("classes").EnumerateArray().Select(c => Names<ItemClass>.Parse(c.GetString()!)).ToList());

    private static CatalogItem Item(JsonElement e)
    {
        var fuel = e.GetProperty("fuel");
        var ammo = e.GetProperty("ammo");
        return new CatalogItem(
            e.GetProperty("resource").GetString()!,
            Names<ItemClass>.Parse(e.GetProperty("class").GetString()!),
            e.GetProperty("stats").EnumerateObject().ToDictionary(p => p.Name, p => Property(p.Value), StringComparer.Ordinal),
            e.GetProperty("slots").EnumerateArray().Select(s => new ItemSlot(s.GetProperty("slot").GetString()!, s.GetProperty("count").GetInt32())).ToList(),
            e.GetProperty("cargo").EnumerateArray().Select(c => new ItemCargo(c.GetProperty("dimension").GetString()!, c.GetProperty("amount").GetDecimal())).ToList(),
            e.GetProperty("crew").EnumerateArray().Select(c => new ItemCrew(c.GetProperty("skill").GetString()!, c.GetProperty("level").GetInt32(), c.GetProperty("count").GetInt32())).ToList(),
            fuel.ValueKind == JsonValueKind.Null ? null : new ItemFuel(fuel.GetProperty("resource").GetString()!, fuel.GetProperty("capacity").GetDecimal(), fuel.GetProperty("burn-per-hour").GetDecimal()),
            ammo.ValueKind == JsonValueKind.Null ? null : new ItemAmmo(ammo.GetProperty("resource").GetString()!, ammo.GetProperty("capacity").GetDecimal()),
            e.GetProperty("upkeep").EnumerateArray().Select(u => new ItemUpkeep(u.GetProperty("resource").GetString()!, u.GetProperty("per-day").GetDecimal())).ToList(),
            NullableString(e.GetProperty("replaces")));
    }


    private static CatalogFormation Unit(JsonElement e) => new(
        e.GetProperty("resource").GetString()!, e.GetProperty("strength").GetInt32(), e.GetProperty("role").GetString()!,
        Names<TrainingLevel>.Parse(e.GetProperty("training").GetString()!),
        e.GetProperty("equipment").EnumerateArray().Select(q => new UnitEquipment(q.GetProperty("item").GetString()!, q.GetProperty("count").GetInt32())).ToList(),
        e.GetProperty("crew").EnumerateArray().Select(c => new ItemCrew(c.GetProperty("skill").GetString()!, c.GetProperty("level").GetInt32(), c.GetProperty("count").GetInt32())).ToList(),
        e.GetProperty("upkeep").EnumerateArray().Select(u => new ItemUpkeep(u.GetProperty("resource").GetString()!, u.GetProperty("per-day").GetDecimal())).ToList(),
        NullableString(e.GetProperty("replaces")));
    private static decimal? NullableNumber(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetDecimal();
}

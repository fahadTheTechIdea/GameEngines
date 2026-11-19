namespace GameContent.Catalog;

public static partial class CatalogValidator
{
    private static void CheckResources(CatalogSet set, CatalogReport report)
    {
        foreach (var pack in set.Packs)
            foreach (var resource in pack.Resources)
            {
                if (!CatalogUnits.TryGet(resource.Unit, out var unit))
                    report.Add(CatalogCodes.UnknownUnit, "unit '" + resource.Unit + "' is not in the supported UCUM table", resource.Id);
                else if (!CatalogUnits.DimensionsOf(resource.Class).Contains(unit.Dimension))
                    report.Add(CatalogCodes.UnitClass, "a " + Names<ResourceClass>.Of(resource.Class) + " resource cannot be measured in '" + resource.Unit + "' (" + unit.Dimension + ")", resource.Id);

                if (resource.Class == ResourceClass.Currency && resource.Currency is null)
                    report.Add(CatalogCodes.Currency, "a currency resource must say whether it is an ISO 4217 currency or a fictional one", resource.Id);
                if (resource.Class != ResourceClass.Currency && resource.Currency is not null)
                    report.Add(CatalogCodes.Currency, "only a currency resource may carry a currency code", resource.Id);

                if (resource.Class == ResourceClass.Unique && resource.StackMax != 1)
                    report.Add(CatalogCodes.Stack, "a unique item cannot stack", resource.Id);
                if (resource.DurabilityUses is not null && resource.Class is not (ResourceClass.Tool or ResourceClass.Equipment or ResourceClass.Unique))
                    report.Add(CatalogCodes.Durability, "only a tool, equipment or unique item wears out", resource.Id);

                if (resource.Properties.TryGetValue(CatalogPrices.Key, out var price) && !(price is decimal amount && amount > 0))
                    report.Add(CatalogCodes.BadPrice, "'" + CatalogPrices.Key + "' must be a positive number: the value of one unit in the reference currency", resource.Id);

                if (resource.Perishable is { Result: { } result })
                {
                    if (result == resource.Id) report.Add(CatalogCodes.Perishable, "a resource cannot spoil into itself", resource.Id);
                    else Reference(pack, result, "spoil result", resource.Id, set.TryResource(result, out _), report);
                }
            }
    }
}

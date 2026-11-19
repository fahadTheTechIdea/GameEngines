namespace GameContent.Catalog;

public static partial class CatalogValidator
{
    private static int OrderOf(CatalogSet set, string eraId) => set.TryEra(eraId, out var era) ? era.Order : int.MinValue;

    private static bool ExistsAnywhere(CatalogSet set, string id) =>
        set.TryResource(id, out _) || set.TryRecipe(id, out _) || set.TryStation(id, out _) || set.TrySkill(id, out _);

    /// <summary>
    /// Eras and availability as written: a timeless pack has none; a historical pack orders its eras on real years and stops at the
    /// present; the order, tier and period label of all eras in the library agree; every availability names things and eras that exist.
    /// </summary>
    private static void CheckEras(CatalogSet set, CatalogReport report)
    {
        foreach (var pack in set.Packs)
        {
            if (pack.World == WorldKind.Timeless && pack.Eras.Count + pack.Availability.Count > 0)
                report.Add(CatalogCodes.EraWorld, "a timeless pack defines no eras and no availability; declare the pack historical or fictional", pack.Id);
            foreach (var era in pack.Eras)
            {
                if (pack.World == WorldKind.Historical && era.Span is null) report.Add(CatalogCodes.EraSpan, "a historical pack gives every era its years", era.Id);
                if (pack.World == WorldKind.Fictional && era.Span is not null) report.Add(CatalogCodes.EraSpan, "a fictional pack's eras have no real years", era.Id);
                if (era.Span is { } span && span.From >= span.To) report.Add(CatalogCodes.EraSpan, "the span " + span.From + " to " + span.To + " does not run forward", era.Id);
                if (pack.World == WorldKind.Historical && era.Label > PeriodLabel.Information)
                    report.Add(CatalogCodes.EraWorld, "'" + Names<PeriodLabel>.Of(era.Label) + "' lies beyond the present, which a historical world cannot reach (it stops at the information age)", era.Id);
            }
            foreach (var entry in pack.Availability) CheckAvailability(set, pack, entry, report);
        }

        var eras = set.Eras.OrderBy(e => e.Order).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
        foreach (var group in eras.GroupBy(e => e.Order).Where(g => g.Count() > 1))
            report.Add(CatalogCodes.EraOrder, "eras " + string.Join(", ", group.Select(e => e.Id)) + " share the order " + group.Key, group.First().Id);
        for (var i = 1; i < eras.Count; i++)
        {
            var (before, after) = (eras[i - 1], eras[i]);
            if (before.Order == after.Order) continue;
            if (after.Tier < before.Tier) report.Add(CatalogCodes.EraOrder, "tier " + after.Tier + " comes after tier " + before.Tier + " in time", after.Id);
            if (after.Label < before.Label) report.Add(CatalogCodes.EraOrder, "the period '" + Names<PeriodLabel>.Of(after.Label) + "' comes after '" + Names<PeriodLabel>.Of(before.Label) + "' in time", after.Id);
        }
        var dated = eras.Where(e => e.Span is not null).ToList();
        for (var i = 1; i < dated.Count; i++)
            if (dated[i].Span!.From < dated[i - 1].Span!.To)
                report.Add(CatalogCodes.EraSpan, "its years start before '" + dated[i - 1].Id + "' ends", dated[i].Id);

        foreach (var group in set.Availability.GroupBy(a => a.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
            report.Add(CatalogCodes.AvailabilityDuplicate, "'" + group.Key + "' is given availability " + group.Count() + " times", group.Key);
    }

    private static void CheckAvailability(CatalogSet set, CatalogPackData pack, CatalogAvailability entry, CatalogReport report)
    {
        Reference(pack, entry.Id, "availability target", entry.Id, ExistsAnywhere(set, entry.Id), report);
        var from = Reference(pack, entry.From, "era", entry.Id, set.TryEra(entry.From, out _), report);
        var until = entry.Until is null || Reference(pack, entry.Until, "era", entry.Id, set.TryEra(entry.Until, out _), report);
        if (from && until && entry.Until is not null && OrderOf(set, entry.From) > OrderOf(set, entry.Until))
            report.Add(CatalogCodes.EraWindow, "'" + entry.From + "' comes after '" + entry.Until + "'", entry.Id);
        if (pack.World == WorldKind.Historical)
            foreach (var eraId in new[] { entry.From, entry.Until }.OfType<string>())
                if (set.TryEra(eraId, out var era) && era.Label > PeriodLabel.Information)
                    report.Add(CatalogCodes.EraWorld, "a historical pack cannot place '" + entry.Id + "' in the " + Names<PeriodLabel>.Of(era.Label) + " era '" + eraId + "'", entry.Id);
    }

    /// <summary>
    /// The era rules between things: a recipe cannot start before an ingredient, tool or station exists or after it is gone; it cannot
    /// make something before that thing exists; and a thing cannot exist before any recipe could make it. A thing with no entry is timeless.
    /// </summary>
    private static void CheckAnachronism(CatalogSet set, CatalogReport report)
    {
        int? Start(string id) => set.TryAvailability(id, out var entry) ? OrderOf(set, entry.From) : null;
        int? End(string id) => set.TryAvailability(id, out var entry) && entry.Until is not null ? OrderOf(set, entry.Until) : null;

        foreach (var recipe in set.Recipes)
        {
            if (Start(recipe.Id) is not { } start) continue;
            var needs = recipe.Inputs.Select(i => i.Resource).Concat(recipe.Catalysts.Select(c => c.Resource)).Concat(recipe.Tools.Select(t => t.Resource));
            if (recipe.Station is { } station) needs = needs.Append(station.Station);
            foreach (var need in needs.Distinct(StringComparer.Ordinal))
            {
                if (Start(need) is { } needed && start < needed)
                    report.Add(CatalogCodes.EraAnachronism, "starts before '" + need + "' exists", recipe.Id);
                if (End(need) is { } gone && gone < start)
                    report.Add(CatalogCodes.EraAnachronism, "needs '" + need + "', which is gone before this recipe starts", recipe.Id);
            }
            foreach (var made in Made(recipe).Distinct(StringComparer.Ordinal))
                if (Start(made) is { } exists && start < exists)
                    report.Add(CatalogCodes.EraAnachronism, "makes '" + made + "' before it exists", recipe.Id);
        }

        foreach (var item in set.Items)
        {
            if (Start(item.Resource) is not { } itemStart || item.Ammo is null || Start(item.Ammo.Resource) is not { } ammoStart) continue;
            if (itemStart < ammoStart) report.Add(CatalogCodes.EraAnachronism, "fires '" + item.Ammo.Resource + "', which does not exist yet", item.Resource);
        }

        var producers = new Dictionary<string, List<CatalogRecipe>>(StringComparer.Ordinal);
        foreach (var recipe in set.Recipes)
            foreach (var made in Made(recipe).Distinct(StringComparer.Ordinal))
            {
                if (!producers.TryGetValue(made, out var list)) producers[made] = list = new List<CatalogRecipe>();
                list.Add(recipe);
            }
        foreach (var resource in set.Resources)
        {
            if (Start(resource.Id) is not { } exists || !producers.TryGetValue(resource.Id, out var makers)) continue;
            if (makers.All(m => Start(m.Id) is not null) && makers.Min(m => Start(m.Id)!.Value) > exists)
                report.Add(CatalogCodes.EraAnachronism, "exists from an era before any recipe that makes it", resource.Id);
        }
    }

    private static IEnumerable<string> Made(CatalogRecipe recipe) =>
        recipe.Outputs.Select(o => o.Resource).Concat(recipe.Byproducts.Select(b => b.Resource)).Concat(recipe.Drops.Select(d => d.Resource));
}

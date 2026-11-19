using AlchemyEngine;

namespace GameContent.Catalog;

/// <summary>
/// Turns a validated pack into AlchemyEngine content. Alchemy is a discovery layer over kinds of things, so a recipe becomes a
/// combination of resource kinds: consumed resources are inputs, kept catalysts and held tools are catalysts, products and byproducts
/// are outputs, station, skill and environment become environment tags. Amounts, durations and quality are not modelled and are
/// reported as reductions; recipes alchemy cannot express are refused with a code. Natural resources are the starting elements.
/// </summary>
public static partial class CatalogToAlchemy
{
    private sealed record Candidate(CatalogRecipe Recipe, string[] Inputs, string[] Catalysts, string[] Outputs, string[] Environment, int Numerator = 1, int Denominator = 1, string? Suffix = null)
    {
        public string Id => Suffix is null ? Recipe.Id : Recipe.Id + Suffix;
        public string[] Vessel => Inputs.Concat(Catalysts).ToArray();
    }

    public static AlchemyConversion Convert(CatalogSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var report = new ConversionReport();
        var candidates = new List<Candidate>();
        foreach (var recipe in set.Recipes.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            candidates.AddRange(Shape(set, recipe, report));
        }
        // Spoilage is time acting on a resource. Alchemy has no clock, so the game offers the tag 'time-passes' to let it happen.
        foreach (var resource in set.Resources.Where(r => r.Perishable?.Result is not null).OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var spoil = new CatalogRecipe(resource.Id + ".spoil", resource.Name + " spoils", RecipeKind.Decay, [new Ingredient(resource.Id, 1)], [], [], [new OutputLine(resource.Perishable!.Result!, 1, 1)], [], [], null, null, [], new Duration(0, TimeUnit.S), null, null);
            candidates.Add(new Candidate(spoil, [resource.Id], [], [resource.Perishable.Result!], ["time-passes"]));
            report.Reduce(AlchemyConversionCodes.SpoilageAsReaction, spoil.Id, "perishable '" + resource.Id + "' becomes a reaction needing the environment tag 'time-passes'; the " + resource.Perishable.Ticks + "-tick shelf life is not modelled");
        }
        candidates = Disambiguate(candidates, report);
        if (set.Items.Any())
            report.Reduce(AlchemyConversionCodes.ItemStats, "pack", set.Items.Count() + " items: their stats, slots, cargo, crew, fuel, ammunition and upkeep are not modelled; the item class becomes the property 'item-<class>'");
        if (set.Resources.Any(r => r.StackMax > 1 || r.DurabilityUses is not null || r.Currency is not null || r.Grade is not null))
            report.Reduce(AlchemyConversionCodes.ResourceFields, "pack", "stack sizes, durability, currency codes and grades are not modelled: an alchemy element is a kind of thing; its class, tags and CPC section become properties");

        var elements = set.Resources.ToDictionary(r => r.Id, StringComparer.Ordinal);
        candidates = DropUnreachable(candidates, elements, report);
        var json = AlchemyDocument(elements.Values.OrderBy(r => r.Id, StringComparer.Ordinal), candidates, set);
        var content = AlchemyContentLoader.Load(json);
        return new AlchemyConversion(content, report, candidates.Count, elements.Count);
    }

    /// <summary>The alchemy forms of one recipe: usually one reaction, plus one per chance drop that cannot ride on the main reaction; none if it is refused.</summary>
    private static List<Candidate> Shape(CatalogSet set, CatalogRecipe recipe, ConversionReport report)
    {
        var guaranteed = recipe.Outputs.Concat(recipe.Byproducts).Select(o => o.Resource).Distinct(StringComparer.Ordinal).ToList();
        var inputs = recipe.Inputs.Select(i => i.Resource).ToArray();
        var catalysts = recipe.Catalysts.Select(c => c.Resource).Concat(recipe.Tools.Select(t => t.Resource)).Distinct(StringComparer.Ordinal).ToArray();
        if (inputs.Length + catalysts.Length > AlchemyRules.MaxItems)
        {
            report.Refuse(AlchemyConversionCodes.VesselTooBig, recipe.Id, (inputs.Length + catalysts.Length) + " kinds exceed the vessel limit of " + AlchemyRules.MaxItems);
            return [];
        }
        if (inputs.Length == 0)
        {
            // Gathering, extraction and loot consume nothing. Alchemy needs something in the vessel, so the tool or catalyst is used on
            // its own; with neither, the bare hands (a generated starting element) do the work.
            if (catalysts.Length == 0) { catalysts = [Hand]; report.Reduce(AlchemyConversionCodes.HandCatalyst, recipe.Id, "nothing is consumed and no tool is held, so the bare hands '" + Hand + "' are the catalyst"); }
            else report.Reduce(AlchemyConversionCodes.GatheringAsToolUse, recipe.Id, "nothing is consumed; the held tool or catalyst is used on its own");
        }

        var environment = recipe.Environment.ToList();
        if (recipe.Station is { } station)
        {
            environment.Add("at-" + Local(station.Station) + (station.Level > 1 ? "-l" + station.Level : ""));
            report.Reduce(AlchemyConversionCodes.StationAsEnvironment, recipe.Id, "station '" + station.Station + "' level " + station.Level + " becomes an environment tag the game must offer");
        }
        if (recipe.Skill is { } skill)
        {
            environment.Add("skill-" + Local(skill.Skill) + "-" + skill.Level);
            report.Reduce(AlchemyConversionCodes.SkillAsEnvironment, recipe.Id, "skill '" + skill.Skill + "' level " + skill.Level + " becomes an environment tag the game must offer");
        }
        report.Reduce(AlchemyConversionCodes.Amounts, recipe.Id, "amounts and ranges are not modelled: alchemy combines kinds");
        if (recipe.Duration.Value > 0) report.Reduce(AlchemyConversionCodes.Duration, recipe.Id, "duration is not modelled");
        if (recipe.Tools.Count > 0) report.Reduce(AlchemyConversionCodes.ToolsAsCatalysts, recipe.Id, "held tools become catalysts; wear is not modelled");
        if (recipe.Byproducts.Count > 0) report.Reduce(AlchemyConversionCodes.ByproductsAsOutputs, recipe.Id, "byproducts become ordinary outputs");
        if (recipe.Quality is not null) report.Reduce(AlchemyConversionCodes.QualityNotModelled, recipe.Id, "quality outcomes are not modelled");
        if (recipe.Reverse is not null) report.Reduce(AlchemyConversionCodes.ReverseNotModelled, recipe.Id, "the reverse link is not modelled");
        if (set.TryAvailability(recipe.Id, out var window))
        {
            environment.Add("era-" + Local(window.From));
            report.Reduce(AlchemyConversionCodes.EraAsEnvironment, recipe.Id, "available from era '" + window.From + "': the game offers the environment tag 'era-" + Local(window.From) + "' while that era or a later one is on" + (window.Until is null ? "" : "; the end era '" + window.Until + "' is not enforced"));
        }
        var tags = environment.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        var forms = new List<Candidate>();
        if (recipe.Drops.Count == 1 && guaranteed.Count == 0)
        {
            // Loot: the lone drop is the whole result, so alchemy's one chance per recipe is exactly the drop's chance.
            var drop = recipe.Drops[0];
            forms.Add(new Candidate(recipe, inputs, catalysts, [drop.Resource], tags, drop.Numerator, drop.Denominator));
            report.Reduce(AlchemyConversionCodes.DropAsRecipeChance, recipe.Id, "the lone drop '" + drop.Resource + "' becomes the recipe's chance " + drop.Numerator + "/" + drop.Denominator + "; its amount is not modelled");
            return forms;
        }
        if (guaranteed.Count > 0 || recipe.Drops.Count == 0)
            forms.Add(new Candidate(recipe, inputs, catalysts, guaranteed.ToArray(), tags));
        if (guaranteed.Count == 0 && recipe.Drops.Count == 0)
            report.Reduce(AlchemyConversionCodes.ConsumptionOnly, recipe.Id, "the inputs are consumed and nothing is made; the effect of consuming (need, upkeep, payment) is not modelled");
        foreach (var drop in recipe.Drops)
        {
            // Alchemy has one chance per reaction, so a drop beside other products is its own reaction over the same vessel, chosen by tag.
            forms.Add(new Candidate(recipe, inputs, catalysts, [drop.Resource], tags, drop.Numerator, drop.Denominator, ".drop-" + Local(drop.Resource)));
            report.Reduce(AlchemyConversionCodes.DropAsSeparateReaction, recipe.Id + ".drop-" + Local(drop.Resource), "the drop '" + drop.Resource + "' (" + drop.Numerator + "/" + drop.Denominator + ") is its own reaction over the same vessel, chosen by an environment tag; alchemy cannot give it together with the main products");
        }
        return forms;
    }

    /// <summary>The generated starting element that stands in for bare hands when a recipe consumes nothing and holds no tool.</summary>
    public const string Hand = "world.hand";

    private static string Local(string id) => id[(id.IndexOf('.') + 1)..];
}

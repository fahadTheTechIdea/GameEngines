using System.Text.Json;

namespace CombatEngine;

/// <summary>Reads the rules JSON (format 1). Every finding refuses the whole document with a code; nothing is repaired.</summary>
public static class CombatRulesLoader
{
    public static CombatRules Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument document;
        try { document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false }); }
        catch (JsonException exception) { throw RulesJson.Refuse(CombatCodes.RulesFormat, "the rules are not valid JSON: " + exception.Message); }
        using (document)
        {
            var root = RulesJson.Object(document.RootElement, "rules", "format", "armor_constant", "minimum_damage", "non_piercing_cut_percent", "hit_chance", "personnel_health",
                "damage_types", "range_bands", "cover", "training", "suppression", "rout", "splash", "limits", "morale", "terrain", "movement");
            if (RulesJson.Int(root, "format", "rules", 1, 1_000_000) != 2) throw RulesJson.Refuse(CombatCodes.RulesFormat, "unsupported format: this engine reads format 2");

            var hit = RulesJson.Object(root.GetProperty("hit_chance"), "rules.hit_chance", "floor_permille", "ceiling_permille");
            var floor = RulesJson.Int(hit, "floor_permille", "rules.hit_chance", 1, 999);
            var ceiling = RulesJson.Int(hit, "ceiling_permille", "rules.hit_chance", 2, 1000);
            if (floor >= ceiling) throw RulesJson.Refuse(CombatCodes.RulesRange, "rules.hit_chance: the floor " + floor + " must be below the ceiling " + ceiling);

            var suppression = RulesJson.Object(root.GetProperty("suppression"), "rules.suppression", "hits", "accuracy_percent");
            var rout = RulesJson.Object(root.GetProperty("rout"), "rules.rout", "strength_percent");
            var splash = RulesJson.Object(root.GetProperty("splash"), "rules.splash", "spacing_milli", "max_targets", "damage_percent");
            var limits = RulesJson.Object(root.GetProperty("limits"), "rules.limits", "max_engagement_seconds", "max_shots_per_battery");
            var morale = RulesJson.Object(root.GetProperty("morale"), "rules.morale", "casualty_loss_percent", "suppression_loss_permille", "recovery_permille", "rout_below_permille");
            var movement = RulesJson.Object(root.GetProperty("movement"), "rules.movement", "foot_speed_milli_per_second");

            return new CombatRules(
                RulesJson.Int(root, "armor_constant", "rules", 1, 1_000_000),
                RulesJson.Int(root, "minimum_damage", "rules", 1, 1000),
                RulesJson.Int(root, "non_piercing_cut_percent", "rules", 0, 100),
                floor, ceiling,
                RulesJson.Int(root, "personnel_health", "rules", 1, 1_000_000),
                DamageTypes(root),
                Bands(root),
                Cover(root),
                Training(root),
                RulesJson.Int(suppression, "hits", "rules.suppression", 1, 1_000_000),
                RulesJson.Int(suppression, "accuracy_percent", "rules.suppression", 1, 100),
                RulesJson.Int(rout, "strength_percent", "rules.rout", 0, 99),
                RulesJson.Int(splash, "spacing_milli", "rules.splash", 1, 1_000_000),
                RulesJson.Int(splash, "max_targets", "rules.splash", 1, 1000),
                RulesJson.Int(splash, "damage_percent", "rules.splash", 1, 100),
                RulesJson.Int(limits, "max_engagement_seconds", "rules.limits", 1, 86_400),
                RulesJson.Int(limits, "max_shots_per_battery", "rules.limits", 1, 10_000_000),
                new MoraleRules(RulesJson.Int(morale, "casualty_loss_percent", "rules.morale", 0, 1000), RulesJson.Int(morale, "suppression_loss_permille", "rules.morale", 0, 1000),
                    RulesJson.Int(morale, "recovery_permille", "rules.morale", 0, 1000), RulesJson.Int(morale, "rout_below_permille", "rules.morale", 0, 999)),
                Terrain(root),
                RulesJson.Int(movement, "foot_speed_milli_per_second", "rules.movement", 1, 1_000_000));
        }
    }

    private static List<DamageTypeRule> DamageTypes(JsonElement root)
    {
        var list = new List<DamageTypeRule>();
        foreach (var item in RulesJson.Items(root, "damage_types", "rules"))
        {
            var where = "rules.damage_types";
            RulesJson.Object(item, where, "id", "armor_factor_percent", "armor_checked");
            list.Add(new DamageTypeRule(RulesJson.Id(item, "id", where), RulesJson.Int(item, "armor_factor_percent", where, 0, 1000), RulesJson.Bool(item, "armor_checked", where)));
        }
        RejectDuplicates(list.Select(t => t.Id), "damage type");
        return list;
    }

    private static List<RangeBand> Bands(JsonElement root)
    {
        var list = new List<RangeBand>();
        foreach (var item in RulesJson.Items(root, "range_bands", "rules"))
        {
            RulesJson.Object(item, "rules.range_bands", "up_to_percent", "accuracy_percent");
            list.Add(new RangeBand(RulesJson.Int(item, "up_to_percent", "rules.range_bands", 1, 100), RulesJson.Int(item, "accuracy_percent", "rules.range_bands", 1, 200)));
        }
        for (var i = 1; i < list.Count; i++)
            if (list[i].UpToPercent <= list[i - 1].UpToPercent) throw RulesJson.Refuse(CombatCodes.RulesBands, "range bands must have rising limits, but " + list[i].UpToPercent + " follows " + list[i - 1].UpToPercent);
        if (list[^1].UpToPercent != 100) throw RulesJson.Refuse(CombatCodes.RulesBands, "the last range band must reach 100 percent of the range, not " + list[^1].UpToPercent);
        return list;
    }

    private static List<CoverRule> Cover(JsonElement root)
    {
        var list = new List<CoverRule>();
        foreach (var item in RulesJson.Items(root, "cover", "rules"))
        {
            RulesJson.Object(item, "rules.cover", "id", "accuracy_percent", "damage_percent");
            list.Add(new CoverRule(RulesJson.Id(item, "id", "rules.cover"), RulesJson.Int(item, "accuracy_percent", "rules.cover", 1, 200), RulesJson.Int(item, "damage_percent", "rules.cover", 1, 200)));
        }
        RejectDuplicates(list.Select(c => c.Id), "cover level");
        return list;
    }

    private static List<TerrainRule> Terrain(JsonElement root)
    {
        var list = new List<TerrainRule>();
        foreach (var item in RulesJson.Items(root, "terrain", "rules"))
        {
            RulesJson.Object(item, "rules.terrain", "id", "accuracy_percent", "movement_percent");
            list.Add(new TerrainRule(RulesJson.Id(item, "id", "rules.terrain"), RulesJson.Int(item, "accuracy_percent", "rules.terrain", 1, 200), RulesJson.Int(item, "movement_percent", "rules.terrain", 1, 300)));
        }
        RejectDuplicates(list.Select(t => t.Id), "terrain");
        return list;
    }

    private static List<TrainingRule> Training(JsonElement root)
    {
        var list = new List<TrainingRule>();
        foreach (var item in RulesJson.Items(root, "training", "rules"))
        {
            RulesJson.Object(item, "rules.training", "id", "accuracy_percent", "damage_percent", "morale_permille");
            list.Add(new TrainingRule(RulesJson.Id(item, "id", "rules.training"), RulesJson.Int(item, "accuracy_percent", "rules.training", 1, 200), RulesJson.Int(item, "damage_percent", "rules.training", 1, 200), RulesJson.Int(item, "morale_permille", "rules.training", 1, 1000)));
        }
        RejectDuplicates(list.Select(t => t.Id), "training level");
        return list;
    }

    private static void RejectDuplicates(IEnumerable<string> ids, string what)
    {
        foreach (var group in ids.GroupBy(i => i, StringComparer.Ordinal).Where(g => g.Count() > 1))
            throw RulesJson.Refuse(CombatCodes.RulesDuplicate, "the " + what + " '" + group.Key + "' is defined twice");
    }
}

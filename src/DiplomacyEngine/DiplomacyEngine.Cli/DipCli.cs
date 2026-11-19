using System.Globalization;
using EngineCommon;
using ScenarioEngine.Contracts;
using DiplomacyEngine;

/// <summary>
/// The diplomacy authoring tool. <c>check</c> reads a document the way a game does and says whether it is accepted, or which rule refused it and where; <c>simulate</c> lives a number of steps on the document's own seasons
/// with scripted calls, and prints what happened; <c>explain</c> says why a proposal would be accepted, where a regard comes from, how a mission would go, why a coalition stands or not, or how a realm stands, in a save that
/// <c>simulate</c> wrote; <c>coverage</c> says how much of a document each locale table covers. Exit codes: 0 done (for <c>coverage</c>, every table complete; for <c>simulate</c>, every scripted call carried out), 1 refused
/// or incomplete (the code or the lists are printed), 2 wrong command line, scenario or file problem.
/// </summary>
public static partial class DipCli
{
    public const string Usage =
        "usage: DiplomacyEngine.Cli check <diplomacy.json>\n" +
        "       DiplomacyEngine.Cli simulate <diplomacy.json> <scenario.json> <steps> [--save <state.json>] [--verbose]\n" +
        "       DiplomacyEngine.Cli explain <diplomacy.json> <state.json> proposal <from> <to> <type> <days> <amount>  [--fact <id>=<true|false|number|text>]...\n" +
        "       DiplomacyEngine.Cli explain <diplomacy.json> <state.json> regard <holder> <subject>\n" +
        "       DiplomacyEngine.Cli explain <diplomacy.json> <state.json> mission <agent> <target> <mission>\n" +
        "       DiplomacyEngine.Cli explain <diplomacy.json> <state.json> coalition <target>\n" +
        "       DiplomacyEngine.Cli explain <diplomacy.json> <state.json> realm <realm>\n" +
        "       DiplomacyEngine.Cli coverage <diplomacy.json> <locale.json> [<locale.json> ...]\n" +
        "exit: 0 done | 1 refused or incomplete (the code or the lists are printed) | 2 usage, scenario or file problem";

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count < 2) return Wrong(error, Usage);
        try
        {
            switch (args[0])
            {
                case "check" when args.Count == 2: return Check(File.ReadAllText(args[1]), output);
                case "simulate" when args.Count >= 4: return Simulate(File.ReadAllText(args[1]), args, output, error);
                case "explain" when args.Count >= 4: return Explain(File.ReadAllText(args[1]), args, output, error);
                case "coverage" when args.Count >= 3: return Coverage(File.ReadAllText(args[1]), args.Skip(2).Select(File.ReadAllText).ToList(), output);
            }
        }
        catch (IOException exception) { return Wrong(error, "file problem: " + exception.Message); }
        catch (UnauthorizedAccessException exception) { return Wrong(error, "file problem: " + exception.Message); }
        return Wrong(error, Usage);
    }

    private static int Wrong(TextWriter error, string message)
    {
        error.WriteLine(message);
        return 2;
    }

    private static int Refused(TextWriter output, EngineRefusedException refusal)
    {
        output.WriteLine("refused " + refusal.Code + ": " + refusal.Message);
        return 1;
    }

    private static int Check(string document, TextWriter output)
    {
        DipSet set;
        try { set = DipLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        output.WriteLine($"ok {set.Id} digest {set.Digest} steps-per-day {set.StepsPerDay} realms {set.Realms.Count} ideologies {set.Ideologies.Count} traits {set.Traits.Count} types {set.Types.Count} casus-belli {set.CasusBelli.Count} " +
            $"missions {set.Missions.Count} deeds {set.Deeds.Count} agents {set.Agents.Count} host-facts {set.HostFacts.Count} text-keys {DipNames.RequiredKeys(set).Count}");
        output.WriteLine($"seasons {string.Join(",", set.Seasons.Select(s => s.Id + ":" + s.WarCost))} signals {string.Join(",", set.Signals)} shocks {string.Join(",", set.Shocks)}");
        foreach (var realm in set.Realms)
            output.WriteLine($"realm {realm.Id} {EnumNames.Name(realm.Control)} {set.Ideologies[realm.Ideology].Id} might {realm.Might} stability {realm.Stability} legitimacy {realm.Legitimacy} factions {string.Join(",", realm.Factions.Select(f => f.Id + ":" + f.Share))}");
        foreach (var type in set.Types)
            output.WriteLine($"type {type.Id} {EnumNames.Name(type.Consent)} clauses {string.Join(",", type.Clauses.Select(c => EnumNames.Name(c.Kind)))} term {(type.Permanent ? "none or " : "")}{type.MinTerm}-{type.MaxTerm} days cancel {EnumNames.Name(type.Cancel)}{(type.EndsWar ? " ends-war" : "")}");
        foreach (var cb in set.CasusBelli)
            output.WriteLine($"casus-belli {cb.Id} {EnumNames.Name(cb.Source)} infamy {cb.Infamy} honour {cb.Honour}");
        return 0;
    }

    private static FactValue ParseFact(string text)
    {
        if (text == "true") return FactValue.From(true);
        if (text == "false") return FactValue.From(false);
        return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? FactValue.From(number) : FactValue.From(text);
    }

    private static int Explain(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            var set = DipLoader.Load(document);
            var runtime = DipSave.Open(set, File.ReadAllBytes(args[2]));
            var subject = args[3];
            var positional = subject switch { "proposal" => 5, "regard" => 2, "mission" => 3, "coalition" => 1, "realm" => 1, _ => -1 };
            if (positional < 0 || args.Count < 4 + positional) return Wrong(error, Usage);
            var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
            for (var i = 4 + positional; i < args.Count; i += 2)
            {
                var at = i + 1 < args.Count && args[i] == "--fact" ? args[i + 1].IndexOf('=', StringComparison.Ordinal) : -1;
                if (at < 1) return Wrong(error, "--fact needs id=value\n" + Usage);
                facts[args[i + 1][..at]] = ParseFact(args[i + 1][(at + 1)..]);
            }
            switch (subject)
            {
                case "proposal":
                    if (!int.TryParse(args[7], NumberStyles.None, CultureInfo.InvariantCulture, out var days) || !long.TryParse(args[8], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount)) return Wrong(error, "<days> and <amount> are whole numbers\n" + Usage);
                    output.WriteLine(DipExplain.Proposal(runtime, args[4], args[5], args[6], days, amount, facts));
                    return 0;
                case "regard": output.WriteLine(DipExplain.Regard(runtime, args[4], args[5])); return 0;
                case "mission": output.WriteLine(DipExplain.Mission(runtime, args[4], args[5], args[6])); return 0;
                case "coalition": output.WriteLine(DipExplain.Coalition(runtime, args[4])); return 0;
                case "realm": output.WriteLine(DipExplain.Realm(runtime, args[4])); return 0;
            }
        }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        return Wrong(error, Usage);
    }

    private static int Coverage(string document, IReadOnlyList<string> tables, TextWriter output)
    {
        DipSet set;
        try { set = DipLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        var complete = true;
        foreach (var text in tables)
        {
            LocaleTable table;
            try { table = LocaleTable.Load(text); }
            catch (EngineRefusedException refusal) { return Refused(output, refusal); }
            var report = DipNames.Coverage(set, table);
            output.WriteLine($"{report.Locale}: {report.Translated}/{report.Total} ({report.Percent}%) missing {report.Missing.Count} unknown {report.Unknown.Count} problems {report.Problems.Count}");
            foreach (var key in report.Missing) output.WriteLine("  missing " + key);
            foreach (var key in report.Unknown) output.WriteLine("  unknown " + key);
            foreach (var problem in report.Problems) output.WriteLine("  problem " + problem.Key + ": " + problem.Why);
            complete &= report.IsComplete;
        }
        return complete ? 0 : 1;
    }
}

using System.Globalization;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;

/// <summary>
/// The world-events authoring tool. <c>check</c> reads a document the way a game does and says whether it is accepted, or which rule refused it and where; <c>simulate</c> lives a number of steps on the document's own
/// seasons with scripted calls, and prints what happened; <c>explain</c> says why a hazard did or did not start, a card can or cannot be drawn, how a disease stands or where the director is, in a save that
/// <c>simulate</c> wrote; <c>coverage</c> says how much of a document each locale table covers. Exit codes: 0 done (for <c>coverage</c>, every table complete; for <c>simulate</c>, every scripted call carried out), 1 refused
/// or incomplete (the code or the lists are printed), 2 wrong command line, scenario or file problem.
/// </summary>
public static partial class WorldCli
{
    public const string Usage =
        "usage: WorldEventsEngine.Cli check <world.json>\n" +
        "       WorldEventsEngine.Cli simulate <world.json> <scenario.json> <steps> [--save <state.json>] [--verbose]\n" +
        "       WorldEventsEngine.Cli simulate <world.json> <scenario.json> [<steps>] --seeds <n> [--from-seed <s>] [--days <d>] [--metrics <a,b,...>] [--rows] [--onsets]\n" +
        "       WorldEventsEngine.Cli simulate <world.json> <scenario.json> [<steps>] --rare <hazard>:<rung> [--predraw | --conditional] [--effort <n> | --efforts <a,b,c> | --efforts auto [--pilot-effort <e>] [--pilot-replications <p>]] [--replications <r>] [--from-seed <s>] [--days <d>] [--rows]\n" +
        "       WorldEventsEngine.Cli explain <world.json> <state.json> hazard <hazard> <region>  [--fact <id>=<true|false|number|text>]...\n" +
        "       WorldEventsEngine.Cli explain <world.json> <state.json> card <card> [<region>]    [--fact <id>=<true|false|number|text>]...\n" +
        "       WorldEventsEngine.Cli explain <world.json> <state.json> disease <disease> <region>\n" +
        "       WorldEventsEngine.Cli explain <world.json> <state.json> weather <region>\n" +
        "       WorldEventsEngine.Cli explain <world.json> <state.json> director\n" +
        "       WorldEventsEngine.Cli coverage <world.json> <locale.json> [<locale.json> ...]\n" +
        "exit: 0 done | 1 refused or incomplete (the code or the lists are printed) | 2 usage, scenario or file problem";

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count < 2) return Wrong(error, Usage);
        try
        {
            switch (args[0])
            {
                case "check" when args.Count == 2: return Check(File.ReadAllText(args[1]), output);
                case "simulate" when args.Count >= 3 && args.Contains("--seeds"): return SimulateSeeds(File.ReadAllText(args[1]), args, output, error);
                case "simulate" when args.Count >= 3 && args.Contains("--rare"): return SimulateRare(File.ReadAllText(args[1]), args, output, error);
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
        WorldSet set;
        try { set = WorldLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        output.WriteLine($"ok {set.Id} digest {set.Digest} steps-per-day {set.StepsPerDay} year {set.YearSteps} regions {set.Regions.Count} links {set.Links.Count} zones {set.Zones.Count} weathers {set.Weathers.Count} hazards {set.Hazards.Count} " +
            $"diseases {set.Diseases.Count} interventions {set.Interventions.Count} cards {set.Cards.Count} groups {set.Groups.Count} host-facts {set.HostFacts.Count} text-keys {WorldNames.RequiredKeys(set).Count}");
        output.WriteLine($"seasons {string.Join(",", set.Seasons.Select(s => s.Id + ":" + s.Steps))} storytellers {string.Join(",", set.Storytellers.Select(s => s.Id))} difficulties {string.Join(",", set.Difficulties.Select(d => d.Id))} start {set.Storytellers[set.StartStoryteller].Id}/{set.Difficulties[set.StartDifficulty].Id}");
        foreach (var hazard in set.Hazards)
            output.WriteLine($"hazard {hazard.Id} {Words.Name(hazard.Kind)} rate {hazard.BasePpm} ppm{(hazard.Paced ? " paced" : "")}{(hazard.When is null ? "" : " conditional")} rungs {string.Join(",", hazard.Ladder.Select(r => r.Id + ":" + r.TailPpm))} reach {hazard.ReachHops}");
        foreach (var disease in set.Diseases)
            output.WriteLine($"disease {disease.Id} {Words.Name(disease.Model)} beta {disease.BetaPpm} ppm incubation {disease.Incubation} infectious {disease.Infectious} waning {disease.Waning} fatality {disease.Fatality}{(disease.Paced ? " paced" : "")}");
        foreach (var card in set.Cards)
            output.WriteLine($"card {card.Id} {Words.Name(card.Tier)} {(card.Global ? "global" : "regional")} weight {card.Weight} cooldown {card.Cooldown} chain {card.Chain.Count}");
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
            var set = WorldLoader.Load(document);
            var runtime = WorldSave.Open(set, File.ReadAllBytes(args[2]));
            var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
            var subject = args[3];
            var positional = subject switch { "hazard" => 2, "disease" => 2, "weather" => 1, "card" => args.Count >= 6 && args[5] != "--fact" ? 2 : 1, "director" => 0, _ => -1 };
            if (positional < 0 || args.Count < 4 + positional) return Wrong(error, Usage);
            for (var i = 4 + positional; i < args.Count; i += 2)
            {
                var at = i + 1 < args.Count && args[i] == "--fact" ? args[i + 1].IndexOf('=', StringComparison.Ordinal) : -1;
                if (at < 1) return Wrong(error, "--fact needs id=value\n" + Usage);
                facts[args[i + 1][..at]] = ParseFact(args[i + 1][(at + 1)..]);
            }
            switch (subject)
            {
                case "hazard": output.WriteLine(WorldExplain.Hazard(runtime, args[4], args[5], facts)); return 0;
                case "card": output.WriteLine(WorldExplain.Card(runtime, args[4], positional == 2 ? args[5] : "", facts)); return 0;
                case "disease": output.WriteLine(WorldExplain.Disease(runtime, args[4], args[5])); return 0;
                case "weather": output.WriteLine(WorldExplain.Weather(runtime, args[4])); return 0;
                case "director": output.WriteLine(WorldExplain.Director(runtime)); return 0;
            }
        }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        return Wrong(error, Usage);
    }

    private static int Coverage(string document, IReadOnlyList<string> tables, TextWriter output)
    {
        WorldSet set;
        try { set = WorldLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        var complete = true;
        foreach (var text in tables)
        {
            LocaleTable table;
            try { table = LocaleTable.Load(text); }
            catch (EngineRefusedException refusal) { return Refused(output, refusal); }
            var report = WorldNames.Coverage(set, table);
            output.WriteLine($"{report.Locale}: {report.Translated}/{report.Total} ({report.Percent}%) missing {report.Missing.Count} unknown {report.Unknown.Count} problems {report.Problems.Count}");
            foreach (var key in report.Missing) output.WriteLine("  missing " + key);
            foreach (var key in report.Unknown) output.WriteLine("  unknown " + key);
            foreach (var problem in report.Problems) output.WriteLine("  problem " + problem.Key + ": " + problem.Why);
            complete &= report.IsComplete;
        }
        return complete ? 0 : 1;
    }
}

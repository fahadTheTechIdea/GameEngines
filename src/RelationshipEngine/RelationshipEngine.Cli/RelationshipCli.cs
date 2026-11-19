using System.Globalization;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;

/// <summary>
/// The relationship authoring tool. <c>check</c> reads a document the way a game does and says whether it is accepted, or which rule refused it and where; <c>simulate</c> lives a scenario (actors, their daily
/// routines, scripted interactions) for a number of days and prints what happened, then the state; <c>explain</c> says why an actor's mood, an opinion of another, or an interaction between two is what it is, in a
/// save that <c>simulate</c> wrote; <c>coverage</c> says how much of a document each locale table covers. Exit codes: 0 done (for <c>coverage</c>, every table complete; for <c>simulate</c>, every scripted
/// interaction carried out), 1 refused or incomplete (the code or the lists are printed), 2 wrong command line, scenario or file problem.
/// </summary>
public static partial class RelationshipCli
{
    public const string Usage =
        "usage: RelationshipEngine.Cli check <relationship.json>\n" +
        "       RelationshipEngine.Cli simulate <relationship.json> <scenario.json> <days> [--save <state.json>] [--resume <state.json>] [--verbose]\n" +
        "       RelationshipEngine.Cli simulate <relationship.json> <scenario.json> [<days>] --seeds <n> [--from-seed <s>] [--days <d>] [--metrics <a,b,...>] [--rows]\n" +
        "       RelationshipEngine.Cli explain <relationship.json> <state.json> mood<actor>\n" +
        "       RelationshipEngine.Cli explain <relationship.json> <state.json> opinion <from> <to> <axis>\n" +
        "       RelationshipEngine.Cli explain <relationship.json> <state.json> interaction <actor> <target> <interaction> [--fact <id>=<true|false|number|text>]...\n" +
        "       RelationshipEngine.Cli coverage <relationship.json> <locale.json> [<locale.json> ...]\n" +
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
                case "simulate" when args.Count >= 4: return Simulate(File.ReadAllText(args[1]), args, output, error);
                case "explain" when args.Count >= 5: return Explain(File.ReadAllText(args[1]), args, output, error);
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
        RelationshipSet set;
        try { set = RelationshipLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        output.WriteLine($"ok {set.Id} digest {set.Digest} seed {set.Seed} steps-per-day {set.StepsPerDay} axes {set.Axes.Count} needs {set.Needs.Count} sources {set.Sources.Count} moodlets {set.Moodlets.Count} memories {set.Memories.Count} " +
            $"traits {set.Traits.Count} ladders {set.Ladders.Count} bonds {set.Bonds.Count} gifts {set.Gifts.Count} interactions {set.Interactions.Count} host-facts {set.HostFacts.Count} facts {set.Facts.Count} text-keys {RelationshipNames.RequiredKeys(set).Count}");
        output.WriteLine($"limits actors {set.Limits.MaxActors} edges-per-actor {set.Limits.MaxEdgesPerActor} memories-per-edge {set.Limits.MaxMemoriesPerEdge} moodlets {set.Limits.MaxMoodlets}");
        foreach (var need in set.Needs) output.WriteLine($"need {need.Id} max {need.Max} decay-per-day {need.DecayPerDay} critical {need.Critical} rearm {need.Rearm} rules {need.DecayModifiers.Count}");
        foreach (var interaction in set.Interactions)
            output.WriteLine($"interaction {interaction.Id} cooldown {interaction.Cooldown}{(interaction.Gift ? " gift" : "")} desire {interaction.Desire.Count} accept {interaction.Accept.Count} effects {interaction.Effects.Count}/{interaction.RefusedEffects.Count}{(interaction.Witness is null ? "" : " witnesses " + interaction.Witness.Max)}");
        var social = set.Social;
        output.WriteLine($"social communities {(social.Communities.Enabled ? "on" : "off")} on {set.Axes[social.Communities.Axis].Id} above {social.Communities.Above} | standing {(social.Standing.Enabled ? "on" : "off")} on {set.Axes[social.Standing.Axis].Id} above {social.Standing.Above} alpha {social.Standing.AlphaPermille} iterations {social.Standing.Iterations} | balance {(social.Balance.Enabled ? "on" : "off")} on {set.Axes[social.Balance.Axis].Id} like {social.Balance.Like} dislike {social.Balance.Dislike} strength {social.Balance.StrengthPermille}");
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
            var set = RelationshipLoader.Load(document);
            var runtime = RelationshipSave.Open(set, File.ReadAllBytes(args[2]));
            switch (args[3])
            {
                case "mood" when args.Count == 5:
                    output.WriteLine(RelationshipExplain.Mood(runtime, args[4]).Text());
                    return 0;
                case "opinion" when args.Count == 7:
                    output.WriteLine(RelationshipExplain.Opinion(runtime, args[4], args[5], args[6]).Text());
                    return 0;
                case "community" when args.Count == 5:
                    output.WriteLine(RelationshipExplain.Community(runtime, args[4]));
                    return 0;
                case "standing" when args.Count == 5:
                    output.WriteLine(RelationshipExplain.Standing(runtime, args[4]));
                    return 0;
                case "balance" when args.Count == 6:
                    output.WriteLine(RelationshipExplain.Balance(runtime, args[4], args[5]));
                    return 0;
                case "interaction" when args.Count >= 7:
                    var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
                    for (var i = 7; i < args.Count; i += 2)
                    {
                        var at = i + 1 < args.Count && args[i] == "--fact" ? args[i + 1].IndexOf('=', StringComparison.Ordinal) : -1;
                        if (at < 1) return Wrong(error, "--fact needs id=value\n" + Usage);
                        facts[args[i + 1][..at]] = ParseFact(args[i + 1][(at + 1)..]);
                    }
                    output.WriteLine(RelationshipExplain.Interaction(runtime, args[4], args[5], args[6], facts));
                    return 0;
            }
        }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        return Wrong(error, Usage);
    }

    private static int Coverage(string document, IReadOnlyList<string> tables, TextWriter output)
    {
        RelationshipSet set;
        try { set = RelationshipLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        var complete = true;
        foreach (var text in tables)
        {
            LocaleTable table;
            try { table = LocaleTable.Load(text); }
            catch (EngineRefusedException refusal) { return Refused(output, refusal); }
            var report = RelationshipNames.Coverage(set, table);
            output.WriteLine($"{report.Locale}: {report.Translated}/{report.Total} ({report.Percent}%) missing {report.Missing.Count} unknown {report.Unknown.Count} problems {report.Problems.Count}");
            foreach (var key in report.Missing) output.WriteLine("  missing " + key);
            foreach (var key in report.Unknown) output.WriteLine("  unknown " + key);
            foreach (var problem in report.Problems) output.WriteLine("  problem " + problem.Key + ": " + problem.Why);
            complete &= report.IsComplete;
        }
        return complete ? 0 : 1;
    }
}

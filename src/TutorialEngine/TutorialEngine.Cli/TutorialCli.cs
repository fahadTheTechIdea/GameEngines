using System.Globalization;
using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;

/// <summary>
/// The tutorial authoring tool. <c>check</c> reads a document the way a game does and says whether it is accepted, or which rule refused it and where; <c>simulate</c> plays a scripted player (what they
/// do, and when) against the tutorial for a number of steps and prints what the tutorial did, then the state; <c>explain</c> says why a step is being taught or waits, why a tip shows or does not, or where the
/// journey is, in a save that <c>simulate</c> wrote; <c>coverage</c> says how much of a document each locale table covers. Exit codes: 0 done (for <c>coverage</c>, every table complete; for <c>simulate</c>,
/// every scripted action carried out), 1 refused or incomplete (the code or the lists are printed), 2 wrong command line, scenario or file problem.
/// </summary>
public static partial class TutorialCli
{
    public const string Usage =
        "usage: TutorialEngine.Cli check <tutorial.json>\n" +
        "       TutorialEngine.Cli simulate <tutorial.json> <scenario.json> <steps> [--save <state.json>] [--verbose]\n" +
        "       TutorialEngine.Cli explain <tutorial.json> <state.json> step <step>   [--fact <id>=<true|false|number|text>]...\n" +
        "       TutorialEngine.Cli explain <tutorial.json> <state.json> tip <tip>     [--fact <id>=<true|false|number|text>]...\n" +
        "       TutorialEngine.Cli explain <tutorial.json> <state.json> journey\n" +
        "       TutorialEngine.Cli coverage <tutorial.json> <locale.json> [<locale.json> ...]\n" +
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
        TutorialSet set;
        try { set = TutorialLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        output.WriteLine($"ok {set.Id} digest {set.Digest} steps-per-day {set.StepsPerDay} steps {set.Steps.Count} tips {set.Tips.Count} events {set.Events.Count} skills {set.Skills.Count} inputs {set.Inputs.Count} " +
            $"host-facts {set.HostFacts.Count} facts {set.Facts.Count} text-keys {TutorialNames.RequiredKeys(set).Count}");
        output.WriteLine($"limits active-steps {set.Limits.MaxActiveSteps} visible-tips {set.Limits.MaxVisibleTips} tip-gap {set.Limits.TipGap}");
        output.WriteLine($"journey languages {string.Join(",", set.Journey.Languages)} default {set.Journey.DefaultLanguage} pages {set.Journey.Pages.Count} entries {string.Join(",", set.Journey.Entries)}");
        foreach (var step in set.Steps)
            output.WriteLine($"step {step.Id} requires {step.Requires.Count}/{step.RequiresAny.Count}{(step.When is null ? "" : " branch")}{(step.Skippable ? " skippable" : "")}{(step.Skill < 0 ? "" : " teaches " + set.Skills[step.Skill].Id)} hints {step.Hints.Count}");
        foreach (var tip in set.Tips)
            output.WriteLine($"tip {tip.Id} cooldown {tip.Cooldown} max-shows {tip.MaxShows} duration {tip.Duration} priority {tip.Priority}");
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
            var set = TutorialLoader.Load(document);
            var runtime = TutorialSave.Open(set, File.ReadAllBytes(args[2]));
            var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
            var first = args[3] == "journey" ? 4 : 5;
            for (var i = first; i < args.Count; i += 2)
            {
                var at = i + 1 < args.Count && args[i] == "--fact" ? args[i + 1].IndexOf('=', StringComparison.Ordinal) : -1;
                if (at < 1) return Wrong(error, "--fact needs id=value\n" + Usage);
                facts[args[i + 1][..at]] = ParseFact(args[i + 1][(at + 1)..]);
            }
            switch (args[3])
            {
                case "step" when args.Count >= 5:
                    output.WriteLine(TutorialExplain.Step(runtime, args[4], facts));
                    return 0;
                case "tip" when args.Count >= 5:
                    output.WriteLine(TutorialExplain.Tip(runtime, args[4], facts));
                    return 0;
                case "journey" when args.Count == 4:
                    output.WriteLine(TutorialExplain.Journey(runtime));
                    return 0;
            }
        }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        return Wrong(error, Usage);
    }

    private static int Coverage(string document, IReadOnlyList<string> tables, TextWriter output)
    {
        TutorialSet set;
        try { set = TutorialLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        var complete = true;
        var loaded = new List<LocaleTable>();
        foreach (var text in tables)
        {
            LocaleTable table;
            try { table = LocaleTable.Load(text); }
            catch (EngineRefusedException refusal) { return Refused(output, refusal); }
            loaded.Add(table);
            var report = TutorialNames.Coverage(set, table);
            output.WriteLine($"{report.Locale}: {report.Translated}/{report.Total} ({report.Percent}%) missing {report.Missing.Count} unknown {report.Unknown.Count} problems {report.Problems.Count}");
            foreach (var key in report.Missing) output.WriteLine("  missing " + key);
            foreach (var key in report.Unknown) output.WriteLine("  unknown " + key);
            foreach (var problem in report.Problems) output.WriteLine("  problem " + problem.Key + ": " + problem.Why);
            complete &= report.IsComplete;
        }
        var (covered, missing) = TutorialNames.LanguageSupport(set, loaded);
        output.WriteLine("journey languages with a table: " + (covered.Count == 0 ? "none" : string.Join(", ", covered)));
        // Informational: a language without a table falls back to the default language through the locale chain, so it is reported but does not make the coverage incomplete.
        if (missing.Count > 0) output.WriteLine("journey languages without a table: " + string.Join(", ", missing));
        return complete ? 0 : 1;
    }
}

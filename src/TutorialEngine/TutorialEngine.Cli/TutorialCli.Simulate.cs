using System.Globalization;
using System.Text.Json;
using EngineCommon;
using ScenarioEngine.Contracts;
using TutorialEngine;

public static partial class TutorialCli
{
    /// <summary>One scripted action: at this step, do this (with a text and a number where the action takes them).</summary>
    private sealed record Action(long At, string Do, string Arg, long N);

    private static readonly StrictJson ScenarioJson = new(TutorialCodes.ForDocument);

    private static readonly string[] ActionNames =
        ["language", "next", "back", "skip-story", "entry", "replay-story", "intent", "start", "observe", "touch", "skip-step", "skip-tutorial", "replay", "dismiss", "reduced-motion", "text-scale", "tips", "reset-tips", "resume", "fact"];

    /// <summary>Reads the scenario: the host facts at the start, then the actions in the order given (steps never go back).</summary>
    private static (Dictionary<string, FactValue> Facts, List<Action> Script) ReadScenario(string text)
    {
        using var document = ScenarioJson.Parse(text, "the scenario");
        var root = ScenarioJson.Object(document.RootElement, "the scenario", ["facts", "script"]);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        foreach (var property in root.GetProperty("facts").EnumerateObject())
        {
            try { facts[property.Name] = ConditionLogic.ParseValue(property.Value); }
            catch (ScenarioContractException exception) { throw ScenarioJson.Refuse(TutorialCodes.Format, "scenario fact '" + property.Name + "': " + exception.Message); }
        }
        var script = new List<Action>();
        long previous = 0;
        foreach (var item in ScenarioJson.Array(root, "script", "the scenario", 0, 1_000_000).EnumerateArray())
        {
            var where = "script[" + script.Count + "]";
            ScenarioJson.Object(item, where, ["at", "do"], "arg", "n");
            var at = ScenarioJson.Long(item, "at", where, 0, 1_000_000_000_000);
            if (at < previous) throw ScenarioJson.Refuse(TutorialCodes.Range, where + ".at goes back from " + previous + " to " + at);
            previous = at;
            var action = ScenarioJson.Text(item, "do", where, 32);
            if (!ActionNames.Contains(action)) throw ScenarioJson.Refuse(TutorialCodes.Format, where + ".do '" + action + "' must be one of " + string.Join(", ", ActionNames));
            script.Add(new Action(at, action, ScenarioJson.Has(item, "arg") ? ScenarioJson.Text(item, "arg", where, 4096) : "", ScenarioJson.Has(item, "n") ? ScenarioJson.Long(item, "n", where, -1_000_000_000_000, 1_000_000_000_000) : 1));
        }
        return (facts, script);
    }

    /// <summary>Performs one scripted action; returns what it changed.</summary>
    private static TutorialChanges Perform(TutorialRuntime runtime, Action action)
    {
        var none = new TutorialChanges();
        switch (action.Do)
        {
            case "language": return runtime.SelectLanguage(action.Arg);
            case "next": return runtime.StoryNext();
            case "back": return runtime.StoryBack();
            case "skip-story": return runtime.StorySkip();
            case "entry": return runtime.ChooseEntry(action.Arg);
            case "replay-story": return runtime.ReplayStory();
            case "intent": return runtime.SetIntent(action.Arg);
            case "start": return runtime.Start();
            case "observe": return runtime.Observe(action.Arg, (int)Math.Clamp(action.N, int.MinValue, int.MaxValue));
            case "skip-step": return runtime.SkipStep(action.Arg);
            case "skip-tutorial": return runtime.SkipTutorial();
            case "replay": return runtime.Replay();
            case "dismiss": return runtime.DismissTip(action.Arg);
            case "reduced-motion": return runtime.SetReducedMotion(action.N != 0);
            case "text-scale": return runtime.SetTextScale((int)Math.Clamp(action.N, int.MinValue, int.MaxValue));
            case "tips": return runtime.SetTipsEnabled(action.N != 0);
            case "reset-tips": return runtime.ResetTips();
            case "touch": runtime.Touch(); return none;
            case "resume": runtime.Resume(); return none;
        }
        throw new InvalidOperationException("unknown scripted action " + action.Do);
    }

    private static void Print(TextWriter output, TutorialRuntime runtime, long step, TutorialChanges changes)
    {
        var set = runtime.Set;
        foreach (var c in changes.Steps) output.WriteLine($"{step} step {set.Steps[c.Step].Id} {TutorialFacts.StatusName(c.From)} -> {TutorialFacts.StatusName(c.To)}");
        foreach (var c in changes.Hints) output.WriteLine($"{step} hint {set.Steps[c.Step].Id} level {c.Level}");
        foreach (var c in changes.Tips) output.WriteLine($"{step} tip {set.Tips[c.Tip].Id} {c.Change.ToString().ToLowerInvariant()} shows {c.Shows}");
        foreach (var c in changes.Skills) output.WriteLine($"{step} skill {set.Skills[c.Skill].Id} mastered");
        foreach (var c in changes.Phases) output.WriteLine($"{step} tutorial {TutorialFacts.PhaseName(c.From)} -> {TutorialFacts.PhaseName(c.To)}");
        foreach (var c in changes.Journeys) output.WriteLine($"{step} journey {TutorialFacts.JourneyName(c.From)} -> {TutorialFacts.JourneyName(c.To)} page {c.Page + 1} language {(c.Language.Length == 0 ? "-" : c.Language)}");
        foreach (var c in changes.Settings) output.WriteLine($"{step} setting {c.Name} {c.Value}");
    }

    private static int Simulate(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        TutorialSet set;
        try { set = TutorialLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        if (!long.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var steps) || steps < 1 || steps > 100_000_000) return Wrong(error, "<steps> must be a whole number from 1 to 100000000\n" + Usage);
        string? save = null;
        var verbose = false;
        for (var i = 4; i < args.Count; i++)
        {
            if (args[i] == "--save" && i + 1 < args.Count) save = args[++i];
            else if (args[i] == "--verbose") verbose = true;
            else return Wrong(error, "unknown or incomplete option '" + args[i] + "'\n" + Usage);
        }
        Dictionary<string, FactValue> facts;
        List<Action> script;
        try { (facts, script) = ReadScenario(File.ReadAllText(args[2])); }
        catch (EngineRefusedException refusal) { return Wrong(error, "scenario problem " + refusal.Code + ": " + refusal.Message); }
        catch (IOException exception) { return Wrong(error, "file problem: " + exception.Message); }
        var runtime = TutorialRuntime.Create(set);
        var next = 0;
        var refused = 0;
        var events = 0;
        try
        {
            for (long step = 0; step <= steps; step++)
            {
                var changes = new TutorialChanges();
                runtime.MoveTo(step);
                while (next < script.Count && script[next].At == step)
                {
                    var action = script[next++];
                    try
                    {
                        if (action.Do == "fact") SetFact(facts, action.Arg);
                        else changes = Merge(changes, Perform(runtime, action));
                    }
                    catch (EngineRefusedException refusal)
                    {
                        refused++;
                        output.WriteLine($"{step} refused {action.Do} {action.Arg}: {refusal.Code}");
                    }
                }
                changes = Merge(changes, runtime.Evaluate(facts));
                events += changes.Steps.Count + changes.Hints.Count + changes.Tips.Count + changes.Skills.Count + changes.Phases.Count + changes.Journeys.Count + changes.Settings.Count;
                if (verbose || changes.Any) Print(output, runtime, step, changes);
            }
        }
        catch (EngineRefusedException refusal)
        {
            // A fact the scenario did not give, or one of the wrong type, is the scenario's mistake: the tool says which and stops.
            return Wrong(error, "scenario problem " + refusal.Code + ": " + refusal.Message);
        }
        if (next < script.Count) output.WriteLine($"{script.Count - next} scripted actions are after step {steps} and were not carried out");
        output.WriteLine($"end step {runtime.Step} tutorial {TutorialFacts.PhaseName(runtime.Phase)} progress {runtime.ProgressPermille()} journey {TutorialFacts.JourneyName(runtime.Journey.State)} events {events} refused {refused} state {runtime.StateDigest()}");
        if (save is not null) File.WriteAllBytes(save, TutorialSave.Write(runtime));
        return refused == 0 && next == script.Count ? 0 : 1;
    }

    /// <summary>A scripted change of a host fact: the arg is <c>id=value</c>.</summary>
    private static void SetFact(Dictionary<string, FactValue> facts, string text)
    {
        var at = text.IndexOf('=', StringComparison.Ordinal);
        if (at < 1) throw new EngineRefusedException(TutorialCodes.FactInvalid, "a fact change is id=value, not '" + text + "'");
        facts[text[..at]] = ParseFact(text[(at + 1)..]);
    }

    private static TutorialChanges Merge(TutorialChanges into, TutorialChanges more)
    {
        into.Steps.AddRange(more.Steps); into.Hints.AddRange(more.Hints); into.Tips.AddRange(more.Tips); into.Skills.AddRange(more.Skills);
        into.Phases.AddRange(more.Phases); into.Journeys.AddRange(more.Journeys); into.Settings.AddRange(more.Settings);
        return into;
    }
}

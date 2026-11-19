using System.Globalization;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;

public static partial class WorldCli
{
    /// <summary>One scripted call: at this step, before it is lived, do this. <c>Arg</c> holds the names the call takes joined by ':' (intervene: region:intervention; hazard: region:hazard:severity; outbreak: region:disease; card: card or card:region), <c>N</c> its number.</summary>
    internal sealed record Call(long At, string Do, string Arg, long N);

    private static readonly StrictJson ScenarioJson = new(WorldCodes.ForDocument);

    private static readonly string[] CallNames = ["intervene", "storyteller", "difficulty", "hazard", "outbreak", "card", "fact"];

    /// <summary>Reads the scenario: the host facts at the start, then the calls in the order given (steps never go back).</summary>
    internal static (Dictionary<string, FactValue> Facts, List<Call> Script) ReadScenario(string text)
    {
        using var document = ScenarioJson.Parse(text, "the scenario");
        var root = ScenarioJson.Object(document.RootElement, "the scenario", ["facts", "script"]);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        foreach (var property in root.GetProperty("facts").EnumerateObject())
        {
            try { facts[property.Name] = ConditionLogic.ParseValue(property.Value); }
            catch (ScenarioContractException exception) { throw ScenarioJson.Refuse(WorldCodes.Format, "scenario fact '" + property.Name + "': " + exception.Message); }
        }
        var script = new List<Call>();
        long previous = 0;
        foreach (var item in ScenarioJson.Array(root, "script", "the scenario", 0, 1_000_000).EnumerateArray())
        {
            var where = "script[" + script.Count + "]";
            ScenarioJson.Object(item, where, ["at", "do"], "arg", "n");
            var at = ScenarioJson.Long(item, "at", where, 0, 1_000_000_000_000);
            if (at < previous) throw ScenarioJson.Refuse(WorldCodes.Range, where + ".at goes back from " + previous + " to " + at);
            previous = at;
            var call = ScenarioJson.Text(item, "do", where, 32);
            if (!CallNames.Contains(call)) throw ScenarioJson.Refuse(WorldCodes.Format, where + ".do '" + call + "' must be one of " + string.Join(", ", CallNames));
            script.Add(new Call(at, call, ScenarioJson.Has(item, "arg") ? ScenarioJson.Text(item, "arg", where, 4096) : "", ScenarioJson.Has(item, "n") ? ScenarioJson.Long(item, "n", where, -1_000_000_000_000, 1_000_000_000_000) : 1));
        }
        return (facts, script);
    }

    private static string Part(string[] parts, int index) => index < parts.Length ? parts[index] : "";

    private static WorldChanges Perform(WorldRuntime runtime, Call call)
    {
        var parts = call.Arg.Split(':');
        switch (call.Do)
        {
            case "intervene": return runtime.Intervene(Part(parts, 0), Part(parts, 1), call.N != 0);
            case "storyteller": return runtime.UseStoryteller(call.Arg);
            case "difficulty": return runtime.UseDifficulty(call.Arg);
            case "hazard": return runtime.BeginHazard(Part(parts, 0), Part(parts, 1), Part(parts, 2));
            case "outbreak": return runtime.SeedOutbreak(Part(parts, 0), Part(parts, 1), call.N);
            case "card": return runtime.PlayCard(Part(parts, 0), Part(parts, 1));
        }
        throw new InvalidOperationException("unknown scripted call " + call.Do);
    }

    /// <summary>
    /// The one place a scenario is lived: for each step the scripted calls due at it, then the step. A refused call is reported to <paramref name="Report"/> and counted; the changes of the step (calls first, then the step) go to
    /// <paramref name="stepped"/> together with how many of them came from the scripted calls (the rest are the step's own). Returns how many calls were refused and how many of the script were carried out or refused (the rest lie at or after the
    /// horizon). A fact the scenario lacks refuses the whole run and is the caller's to report. A run can start part way: <paramref name="from"/> is the first step to live and <paramref name="next"/> the first scripted call not yet made (a
    /// branch of a checkpoint carries both), and it can stop early when <paramref name="stop"/> says so after a step (a splitting stage ends at the first moment its level is reached).
    /// </summary>
    internal static (int Refused, int Next) Live(WorldRuntime runtime, Dictionary<string, FactValue> facts, List<Call> script, long steps, Action<long, Call, EngineRefusedException> Report, Action<long, List<WorldChange>, int> stepped,
        long from = 0, int next = 0, Func<bool>? stop = null)
    {
        var refused = 0;
        for (var step = from; step < steps; step++)
        {
            var changes = new List<WorldChange>();
            while (next < script.Count && script[next].At == step)
            {
                var call = script[next++];
                try
                {
                    if (call.Do == "fact") SetFact(facts, call.Arg);
                    else changes.AddRange(Perform(runtime, call).Items);
                }
                catch (EngineRefusedException refusal)
                {
                    refused++;
                    Report(step, call, refusal);
                }
            }
            var fromStep = changes.Count;
            changes.AddRange(runtime.Advance(step, runtime.CycleSeasonAt, facts).Items);
            stepped(step, changes, fromStep);
            if (stop is not null && stop()) break;
        }
        return (refused, next);
    }

    private static int Simulate(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        WorldSet set;
        try { set = WorldLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        if (!long.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var steps) || steps < 1 || steps > 10_000_000) return Wrong(error, "<steps> must be a whole number from 1 to 10000000\n" + Usage);
        string? save = null;
        var verbose = false;
        for (var i = 4; i < args.Count; i++)
        {
            if (args[i] == "--save" && i + 1 < args.Count) save = args[++i];
            else if (args[i] == "--verbose") verbose = true;
            else return Wrong(error, "unknown or incomplete option '" + args[i] + "'\n" + Usage);
        }
        Dictionary<string, FactValue> facts;
        List<Call> script;
        try { (facts, script) = ReadScenario(File.ReadAllText(args[2])); }
        catch (EngineRefusedException refusal) { return Wrong(error, "scenario problem " + refusal.Code + ": " + refusal.Message); }
        catch (IOException exception) { return Wrong(error, "file problem: " + exception.Message); }
        var runtime = WorldRuntime.Create(set);
        var next = 0;
        var refused = 0;
        var counts = new SortedDictionary<ChangeKind, long>();
        try
        {
            (refused, next) = Live(runtime, facts, script, steps,
                (step, call, refusal) => output.WriteLine($"{step} refused {call.Do} {call.Arg}: {refusal.Code}"),
                (_, changes, _) =>
                {
                    foreach (var change in changes)
                    {
                        counts[change.Kind] = counts.GetValueOrDefault(change.Kind) + 1;
                        if (verbose || change.Kind != ChangeKind.Weather) output.WriteLine(WorldChangeText.Line(set, change));
                    }
                });
        }
        catch (EngineRefusedException refusal)
        {
            // A fact the scenario did not give, or one of the wrong type, is the scenario's mistake: the tool says which and stops.
            return Wrong(error, "scenario problem " + refusal.Code + ": " + refusal.Message);
        }
        if (next < script.Count) output.WriteLine($"{script.Count - next} scripted calls are at or after step {steps} and were not carried out");
        output.WriteLine($"end step {runtime.Step} season {(runtime.Season < 0 ? "-" : set.Seasons[runtime.Season].Id)} stress {runtime.Stress} director {Words.Name(runtime.Phase)} hazards-going {runtime.ActiveHazards.Count} played {runtime.Played} held-back {runtime.Suppressed} dropped {runtime.Dropped} " +
            $"changes {string.Join(",", counts.Select(c => Words.Name(c.Key) + ":" + c.Value))} refused {refused} state {runtime.StateDigest()}");
        if (save is not null) File.WriteAllBytes(save, WorldSave.Write(runtime));
        return refused == 0 && next == script.Count ? 0 : 1;
    }

    /// <summary>A scripted change of a host fact: the arg is <c>id=value</c>.</summary>
    private static void SetFact(Dictionary<string, FactValue> facts, string text)
    {
        var at = text.IndexOf('=', StringComparison.Ordinal);
        if (at < 1) throw new EngineRefusedException(WorldCodes.FactInvalid, "a fact change is id=value, not '" + text + "'");
        facts[text[..at]] = ParseFact(text[(at + 1)..]);
    }
}

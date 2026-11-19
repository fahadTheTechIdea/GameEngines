using System.Globalization;
using EngineCommon;
using ScenarioEngine.Contracts;
using DiplomacyEngine;

public static partial class DipCli
{
    /// <summary>One scripted call: at this step, before it is lived, do this. <c>Arg</c> holds the names the call takes joined by ':' and <c>N</c> its number (see <see cref="CallNames"/>).</summary>
    private sealed record Call(long At, string Do, string Arg, long N);

    private static readonly StrictJson ScenarioJson = new(DipCodes.ForDocument);

    private static readonly string[] CallNames =
        ["propose", "respond", "withdraw", "cancel", "war", "join", "leave", "score", "fulfil", "mission", "recruit", "release", "grant", "deed", "happen", "succession", "might", "leader", "fact"];

    private static (Dictionary<string, FactValue> Facts, List<Call> Script) ReadScenario(string text)
    {
        using var document = ScenarioJson.Parse(text, "the scenario");
        var root = ScenarioJson.Object(document.RootElement, "the scenario", ["facts", "script"]);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        foreach (var property in root.GetProperty("facts").EnumerateObject())
        {
            try { facts[property.Name] = ConditionLogic.ParseValue(property.Value); }
            catch (ScenarioContractException exception) { throw ScenarioJson.Refuse(DipCodes.Format, "scenario fact '" + property.Name + "': " + exception.Message); }
        }
        var script = new List<Call>();
        long previous = 0;
        foreach (var item in ScenarioJson.Array(root, "script", "the scenario", 0, 1_000_000).EnumerateArray())
        {
            var where = "script[" + script.Count + "]";
            ScenarioJson.Object(item, where, ["at", "do"], "arg", "n");
            var at = ScenarioJson.Long(item, "at", where, 0, 1_000_000_000_000);
            if (at < previous) throw ScenarioJson.Refuse(DipCodes.Range, where + ".at goes back from " + previous + " to " + at);
            previous = at;
            var call = ScenarioJson.Text(item, "do", where, 32);
            if (!CallNames.Contains(call)) throw ScenarioJson.Refuse(DipCodes.Format, where + ".do '" + call + "' must be one of " + string.Join(", ", CallNames));
            script.Add(new Call(at, call, ScenarioJson.Has(item, "arg") ? ScenarioJson.Text(item, "arg", where, 4096) : "", ScenarioJson.Has(item, "n") ? ScenarioJson.Long(item, "n", where, -1_000_000_000_000, 1_000_000_000_000) : 1));
        }
        return (facts, script);
    }

    private static string Part(string[] parts, int index) => index < parts.Length ? parts[index] : "";

    private static long Id(string[] parts, int index)
    {
        if (!long.TryParse(Part(parts, index), NumberStyles.None, CultureInfo.InvariantCulture, out var id)) throw new EngineRefusedException(DipCodes.Format, "'" + Part(parts, index) + "' is not an id (a whole number)");
        return id;
    }

    private static int Whole(string[] parts, int index)
    {
        if (!int.TryParse(Part(parts, index), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) throw new EngineRefusedException(DipCodes.Format, "'" + Part(parts, index) + "' is not a whole number");
        return number;
    }

    private static long Wide(string[] parts, int index)
    {
        if (!long.TryParse(Part(parts, index), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) throw new EngineRefusedException(DipCodes.Format, "'" + Part(parts, index) + "' is not a whole number");
        return number;
    }

    private static DipChanges Perform(DipRuntime runtime, Call call)
    {
        var p = call.Arg.Split(':');
        switch (call.Do)
        {
            case "propose": return runtime.Propose(Part(p, 0), Part(p, 1), Part(p, 2), Whole(p, 3), Wide(p, 4));
            case "respond": return runtime.Respond(Id(p, 0), call.N != 0);
            case "withdraw": return runtime.Withdraw(Id(p, 0), Part(p, 1));
            case "cancel": return runtime.Cancel(Id(p, 0), Part(p, 1));
            case "war": return runtime.DeclareWar(Part(p, 0), Part(p, 1), Part(p, 2));
            case "join": return runtime.JoinWar(Id(p, 0), Part(p, 1), Part(p, 2));
            case "leave": return runtime.LeaveWar(Id(p, 0), Part(p, 1));
            case "score": return runtime.Score(Id(p, 0), Part(p, 1), Whole(p, 2), Part(p, 3));
            case "fulfil": return runtime.Fulfil(Id(p, 0), call.N != 0);
            case "mission": return runtime.StartMission(Part(p, 0), Part(p, 1), Part(p, 2));
            case "recruit": return runtime.Recruit(Part(p, 0), Part(p, 1), Whole(p, 2));
            case "release": return runtime.Release(Part(p, 0));
            case "grant": return runtime.GrantCasusBelli(Part(p, 0), Part(p, 1), Part(p, 2), Whole(p, 3));
            case "deed": return runtime.Remember(Part(p, 0), Part(p, 1), Part(p, 2), Whole(p, 3));
            case "happen": return runtime.Happen(Part(p, 0), Part(p, 1), Whole(p, 2));
            case "succession": return runtime.Succession(Part(p, 0));
            case "might": return runtime.SetMight(Part(p, 0), Wide(p, 1));
            case "leader": return runtime.SetLeaderRegard(Part(p, 0), Part(p, 1), Whole(p, 2));
        }
        throw new InvalidOperationException("unknown scripted call " + call.Do);
    }

    private static int Simulate(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        DipSet set;
        try { set = DipLoader.Load(document); }
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
        var runtime = DipRuntime.Create(set);
        var next = 0;
        var refused = 0;
        var counts = new SortedDictionary<ChangeKind, long>();
        try
        {
            for (long step = 0; step < steps; step++)
            {
                var changes = new List<DipChange>();
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
                        output.WriteLine($"{step} refused {call.Do} {call.Arg}: {refusal.Code}");
                    }
                }
                changes.AddRange(runtime.Advance(step, runtime.CycleSeasonAt, facts).Items);
                foreach (var change in changes)
                {
                    counts[change.Kind] = counts.GetValueOrDefault(change.Kind) + 1;
                    if (verbose || change.Kind is not (ChangeKind.Season or ChangeKind.Intel)) output.WriteLine(DipChangeText.Line(set, change));
                }
            }
        }
        catch (EngineRefusedException refusal)
        {
            // A fact the scenario did not give, or one of the wrong type, is the scenario's mistake: the tool says which and stops.
            return Wrong(error, "scenario problem " + refusal.Code + ": " + refusal.Message);
        }
        if (next < script.Count) output.WriteLine($"{script.Count - next} scripted calls are at or after step {steps} and were not carried out");
        output.WriteLine($"end step {runtime.Step} season {(runtime.Season < 0 ? "-" : set.Seasons[runtime.Season].Id)} treaties {runtime.Treaties.Count} wars {runtime.Wars.Count} coalitions {runtime.Coalitions.Count} proposals {runtime.Proposals.Count} " +
            $"changes {string.Join(",", counts.Select(c => EnumNames.Name(c.Key) + ":" + c.Value))} refused {refused} state {runtime.StateDigest()}");
        if (save is not null) File.WriteAllBytes(save, DipSave.Write(runtime));
        return refused == 0 && next == script.Count ? 0 : 1;
    }

    /// <summary>A scripted change of a host fact: the arg is <c>id=value</c>.</summary>
    private static void SetFact(Dictionary<string, FactValue> facts, string text)
    {
        var at = text.IndexOf('=', StringComparison.Ordinal);
        if (at < 1) throw new EngineRefusedException(DipCodes.FactInvalid, "a fact change is id=value, not '" + text + "'");
        facts[text[..at]] = ParseFact(text[(at + 1)..]);
    }
}

using System.Globalization;
using System.Text.Json;
using EngineCommon;
using RelationshipEngine;
using ScenarioEngine.Contracts;

public static partial class RelationshipCli
{
    private sealed record ScenarioActor(string Id, List<string> Traits, Dictionary<string, int> Tastes, string[] Routine);

    private sealed record ScenarioShot(long Step, string Interaction, string Actor, string Target, List<string> Witnesses, string Gift);

    private sealed record Scenario(List<ScenarioActor> Actors, Dictionary<string, FactValue> Facts, List<ScenarioShot> Script);

    private sealed class ScenarioProblem(string message) : Exception(message);

    private static string Text(JsonElement owner, string field, string where)
    {
        if (!owner.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String) throw new ScenarioProblem(where + " needs a text '" + field + "'");
        return value.GetString()!;
    }

    private static List<string> Texts(JsonElement owner, string field, string where)
    {
        if (!owner.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Array) throw new ScenarioProblem(where + " needs a list '" + field + "'");
        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
            list.Add(item.ValueKind == JsonValueKind.String ? item.GetString()! : throw new ScenarioProblem(where + "." + field + " holds texts only"));
        return list;
    }

    private static FactValue FactOf(JsonElement value, string name)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.True: return FactValue.From(true);
            case JsonValueKind.False: return FactValue.From(false);
            case JsonValueKind.Number: return FactValue.From(value.GetInt64());
            case JsonValueKind.String: return FactValue.From(value.GetString()!);
            case JsonValueKind.Undefined:
            case JsonValueKind.Object:
            case JsonValueKind.Array:
            case JsonValueKind.Null:
                break;
        }
        throw new ScenarioProblem("fact '" + name + "' is a boolean, a whole number or a text");
    }

    /// <summary>Reads a scenario: <c>actors</c> (id, traits, tastes, and a routine of one activity per step of a day), <c>facts</c> the host gives, and a <c>script</c> of interactions at steps.</summary>
    private static Scenario ReadScenario(string text, int stepsPerDay)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new ScenarioProblem("a scenario is an object");
        var actors = new List<ScenarioActor>();
        if (!root.TryGetProperty("actors", out var actorList) || actorList.ValueKind != JsonValueKind.Array) throw new ScenarioProblem("a scenario needs a list 'actors'");
        foreach (var actor in actorList.EnumerateArray())
        {
            var where = "actors[" + actors.Count + "]";
            var tastes = new Dictionary<string, int>(StringComparer.Ordinal);
            if (actor.TryGetProperty("tastes", out var tasteList) && tasteList.ValueKind == JsonValueKind.Object)
                foreach (var taste in tasteList.EnumerateObject())
                    tastes[taste.Name] = taste.Value.TryGetInt32(out var level) ? level : throw new ScenarioProblem(where + ".tastes." + taste.Name + " is a whole number");
            var routine = Texts(actor, "routine", where).ToArray();
            if (routine.Length != stepsPerDay) throw new ScenarioProblem(where + ".routine needs one activity per step of a day: " + stepsPerDay + ", not " + routine.Length);
            actors.Add(new ScenarioActor(Text(actor, "id", where), Texts(actor, "traits", where), tastes, routine));
        }
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        if (root.TryGetProperty("facts", out var factList) && factList.ValueKind == JsonValueKind.Object)
            foreach (var fact in factList.EnumerateObject())
                facts[fact.Name] = FactOf(fact.Value, fact.Name);
        var script = new List<ScenarioShot>();
        if (root.TryGetProperty("script", out var shots) && shots.ValueKind == JsonValueKind.Array)
            foreach (var shot in shots.EnumerateArray())
            {
                var where = "script[" + script.Count + "]";
                if (!shot.TryGetProperty("step", out var step) || !step.TryGetInt64(out var at)) throw new ScenarioProblem(where + " needs a whole number 'step'");
                script.Add(new ScenarioShot(at, Text(shot, "interaction", where), Text(shot, "actor", where), Text(shot, "target", where), shot.TryGetProperty("witnesses", out _) ? Texts(shot, "witnesses", where) : new List<string>(),
                    shot.TryGetProperty("gift", out var gift) && gift.ValueKind == JsonValueKind.String ? gift.GetString()! : ""));
            }
        return new Scenario(actors, facts, script.OrderBy(s => s.Step).ToList());
    }

    /// <summary>The one place a scenario is lived: for each step the routines, the scripted interactions due at it, then the step. Returns how many interactions the engine refused outright; each outcome goes to <paramref name="performed"/> or <paramref name="Report"/>, each closed step to <paramref name="advanced"/>.</summary>
    private static int Live(RelationshipRuntime runtime, Scenario scenario, long total, Action<ScenarioShot, InteractionResult> performed, Action<ScenarioShot, EngineRefusedException> Report, Action<long, RelationshipChanges> advanced, long from = 0)
    {
        var stepsPerDay = runtime.Set.StepsPerDay;
        var refused = 0;
        // A run resumed from a save starts at the step the save was taken at; the scripted interactions due before it were carried out by the run that wrote the save.
        var shots = new Queue<ScenarioShot>(scenario.Script.Where(s => from == 0 || s.Step >= from));
        for (long step = from; step < total; step++)
        {
            foreach (var actor in scenario.Actors) runtime.SetActivity(actor.Id, actor.Routine[(int)(step % stepsPerDay)]);
            while (shots.Count > 0 && shots.Peek().Step <= step)
            {
                var shot = shots.Dequeue();
                try { performed(shot, runtime.Perform(new InteractionRequest(shot.Interaction, shot.Actor, shot.Target, shot.Witnesses, shot.Gift), scenario.Facts)); }
                catch (EngineRefusedException refusal)
                {
                    refused++;
                    Report(shot, refusal);
                }
            }
            advanced(step, runtime.Advance(step + 1, scenario.Facts));
        }
        return refused;
    }

    private static int Simulate(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        string? savePath = null;
        string? resumePath = null;
        var verbose = false;
        for (var i = 4; i < args.Count; i++)
        {
            if (args[i] == "--verbose") verbose = true;
            else if (args[i] == "--save" && i + 1 < args.Count) savePath = args[++i];
            else if (args[i] == "--resume" && i + 1 < args.Count && resumePath is null) resumePath = args[++i];
            else return Wrong(error, Usage);
        }
        if (!int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var days) || days < 1 || days > 3650) return Wrong(error, "days is a whole number from 1 to 3650");
        try
        {
            var set = RelationshipLoader.Load(document);
            Scenario scenario;
            try { scenario = ReadScenario(File.ReadAllText(args[2]), set.StepsPerDay); }
            catch (ScenarioProblem problem) { return Wrong(error, "scenario problem: " + problem.Message); }
            catch (JsonException problem) { return Wrong(error, "scenario problem: not JSON: " + problem.Message); }
            RelationshipRuntime runtime;
            if (resumePath is null)
            {
                runtime = RelationshipRuntime.Create(set);
                foreach (var actor in scenario.Actors) runtime.AddActor(actor.Id, actor.Traits, actor.Tastes);
            }
            else
            {
                // Resumed from a save this tool wrote: the actors are in the save, and the run goes on from its step to the horizon.
                runtime = RelationshipSave.Open(set, File.ReadAllBytes(resumePath));
                if (runtime.Step >= (long)days * set.StepsPerDay) return Wrong(error, "the save is at step " + runtime.Step + ", which is not before the horizon of " + days + " days");
            }
            var refused = Live(runtime, scenario, (long)days * set.StepsPerDay,
                (shot, result) => output.WriteLine($"step {runtime.Step}: {shot.Actor} {shot.Interaction} {shot.Target}: {(result.Accepted ? "accepted" : "refused by the target")} (score {result.Score}, noise {result.Noise}, needs {result.Threshold})"),
                (shot, refusal) => output.WriteLine($"step {runtime.Step}: {shot.Actor} {shot.Interaction} {shot.Target}: refused {refusal.Code}: {refusal.Message}"),
                (step, changes) =>
                {
                    if (verbose) PrintChanges(output, runtime, changes);
                    if ((step + 1) % set.StepsPerDay == 0) PrintDay(output, runtime, (step + 1) / set.StepsPerDay);
                }, Math.Max(0, runtime.Step));
            PrintState(output, runtime);
            var bytes = RelationshipSave.Write(runtime);
            if (savePath is not null) File.WriteAllBytes(savePath, bytes);
            output.WriteLine("digest " + runtime.StateDigest());
            return refused == 0 ? 0 : 1;
        }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
    }

    private static void PrintChanges(TextWriter output, RelationshipRuntime runtime, RelationshipChanges changes)
    {
        var set = runtime.Set;
        foreach (var mood in changes.Moods) output.WriteLine($"  step {runtime.Step}: {mood.Actor} mood {set.Mood.States[mood.From].Id} -> {set.Mood.States[mood.To].Id} ({mood.Mood})");
        foreach (var need in changes.Needs) output.WriteLine($"  step {runtime.Step}: {need.Actor} {set.Needs[need.Need].Id} {(need.Entered ? "is critical" : "recovered")} at {need.Level}");
        foreach (var state in changes.States) output.WriteLine($"  step {runtime.Step}: {state.Actor} about {state.Target}: {set.Ladders[state.Ladder].Id} {set.Ladders[state.Ladder].Steps[state.From].Id} -> {set.Ladders[state.Ladder].Steps[state.To].Id}");
        foreach (var bond in changes.Bonds) output.WriteLine($"  step {runtime.Step}: {bond.Actor} and {bond.Target}: {set.Bonds[bond.Bond].Id} {(bond.Set ? "set" : "cleared")}");
        foreach (var memory in changes.Memories) output.WriteLine($"  step {runtime.Step}: {memory.Holder} remembers {memory.Subject}: {set.Memories[memory.Memory].Id}{(memory.Origin.Length > 0 ? " (told by " + memory.Origin + ")" : "")}");
        foreach (var evicted in changes.Evictions) output.WriteLine($"  step {runtime.Step}: {evicted.Kind} of {evicted.Actor} dropped: {evicted.What}");
        foreach (var community in changes.Communities) output.WriteLine($"  step {runtime.Step}: {community.Actor} community {community.From} -> {community.To}");
        foreach (var standing in changes.Standing) output.WriteLine($"  step {runtime.Step}: {standing.Actor} standing {standing.From} -> {standing.To}");
        foreach (var shift in changes.Balance) output.WriteLine($"  step {runtime.Step}: {shift.Holder} about {shift.Subject}: pressure {(shift.Scale > 0 ? "toward" : "away from")} liking through {shift.Via} ({shift.HolderVia}, {shift.ViaSubject})");
    }

    private static void PrintDay(TextWriter output, RelationshipRuntime runtime, long day)
    {
        var set = runtime.Set;
        var moods = string.Join(' ', runtime.Actors.Values.Select(a => a.Id + "=" + set.Mood.States[a.MoodState].Id));
        var critical = string.Join(' ', runtime.Actors.Values.SelectMany(a => set.Needs.Where(n => a.Critical[n.Index]).Select(n => a.Id + ":" + n.Id)));
        output.WriteLine($"day {day} (step {runtime.Step}): {moods}" + (critical.Length == 0 ? "" : " | critical " + critical));
    }

    private static void PrintState(TextWriter output, RelationshipRuntime runtime)
    {
        var set = runtime.Set;
        output.WriteLine($"state at step {runtime.Step}: {runtime.Actors.Count} actors, {runtime.EdgeCount} edges, {runtime.Rolls} rolls");
        foreach (var actor in runtime.Actors.Values)
        {
            output.WriteLine($"{actor.Id} mood {runtime.MoodValue(actor)} {set.Mood.States[actor.MoodState].Id} needs {string.Join(' ', set.Needs.Select(n => n.Id + "=" + runtime.NeedLevel(actor, n.Index)))} moodlets {actor.Moodlets.Count} edges {actor.Edges.Count} community {actor.Community} standing {actor.Standing}");
            foreach (var edge in actor.Edges.Values)
                output.WriteLine($"  about {edge.To}: {string.Join(' ', set.Axes.Select(a => a.Id + "=" + runtime.Opinion(actor.Id, edge.To, a.Index)))} states {string.Join(' ', set.Ladders.Select(l => l.Id + "=" + l.Steps[edge.Ladder[l.Index]].Id))}" +
                    (edge.Bonds.Count > 0 ? " bonds " + string.Join(',', edge.Bonds.Select(b => set.Bonds[b].Id)) : "") + (edge.Memories.Count > 0 ? " memories " + string.Join(',', edge.Memories.Select(m => set.Memories[m.Memory].Id)) : ""));
        }
    }
}

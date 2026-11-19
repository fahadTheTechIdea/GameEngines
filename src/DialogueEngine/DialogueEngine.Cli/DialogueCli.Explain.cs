using System.Globalization;
using System.Text.Json;
using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

public static partial class DialogueCli
{
    private static readonly StrictJson Json = new(DialogueCodes.ForDocument);

    /// <summary>
    /// Plays a session and says why each call did what it did. The session is <c>{ "seed": n, "facts": { "ext.name": value }, "calls": [ { "call": "start", "conversation": id }, { "call": "next" },
    /// { "call": "choose", "option": id }, { "call": "timeout" }, { "call": "interrupt", "conversation": id } ] }</c>; the facts are the external facts the host supplies on every call.
    /// </summary>
    private static int Explain(string document, string session, TextWriter output)
    {
        try
        {
            var set = Load(document);
            using var parsed = Json.Parse(session, "session");
            var root = Json.Object(parsed.RootElement, "session", ["seed", "facts", "calls"]);
            var seed = (ulong)Json.Long(root, "seed", "session", 0, long.MaxValue);
            var facts = ReadFacts(root.GetProperty("facts"), set);
            var runtime = new DialogueRuntime(set, seed) { Explaining = true };
            var number = 0;
            foreach (var call in Json.Array(root, "calls", "session", 0, 10_000).EnumerateArray())
            {
                var where = "session.calls[" + number++ + "]";
                var name = Json.Text(call, "call", where, 16);
                output.WriteLine("> " + Describe(call, name, where));
                try
                {
                    var step = Perform(runtime, call, name, where, facts);
                    output.Write(runtime.LastTrace!.ToText());
                    Show(output, runtime, step, facts);
                }
                catch (EngineRefusedException refusal)
                {
                    if (runtime.LastTrace is { } trace) output.Write(trace.ToText());
                    return Refused(output, refusal);
                }
            }
            return 0;
        }
        catch (EngineRefusedException refusal)
        {
            return Refused(output, refusal);
        }
    }

    private static string Describe(JsonElement call, string name, string where) =>
        name + (call.TryGetProperty("conversation", out var c) && c.ValueKind == JsonValueKind.String ? " " + c.GetString() : "") + (call.TryGetProperty("option", out var o) && o.ValueKind == JsonValueKind.String ? " " + o.GetString() : "");

    private static DialogueStep Perform(DialogueRuntime runtime, JsonElement call, string name, string where, IReadOnlyDictionary<string, FactValue> facts)
    {
        switch (name)
        {
            case "start": Json.Object(call, where, ["call", "conversation"]); return runtime.Start(Json.Id(call, "conversation", where), facts);
            case "interrupt": Json.Object(call, where, ["call", "conversation"]); return runtime.Interrupt(Json.Id(call, "conversation", where), facts);
            case "choose": Json.Object(call, where, ["call", "option"]); return runtime.Choose(Json.Id(call, "option", where), facts);
            case "next": Json.Object(call, where, ["call"]); return runtime.Next(facts);
            case "timeout": Json.Object(call, where, ["call"]); return runtime.Timeout(facts);
        }
        throw Json.Refuse(DialogueCodes.Format, where + ".call must be start, next, choose, timeout or interrupt, not '" + name + "'");
    }

    private static void Show(TextWriter output, DialogueRuntime runtime, DialogueStep step, IReadOnlyDictionary<string, FactValue> facts)
    {
        switch (step.Kind)
        {
            case StepKind.Line:
            {
                var line = step.Line!;
                output.WriteLine($"line {line.Conversation}/{line.Node} {line.Speaker}/{line.Portrait} key {line.Key}"
                    + (line.Args.Count > 0 ? " args " + string.Join(", ", line.Args.Select(a => a.Name + "=" + Show(a.Value))) : "")
                    + (line.Tags.Count > 0 ? " tags " + string.Join(",", line.Tags) : "") + (line.Voice is null ? "" : " voice " + line.Voice) + (line.Emotion is null ? "" : " emotion " + line.Emotion)
                    + (line.AutoAdvanceMs > 0 ? " auto-advance " + line.AutoAdvanceMs + "ms" : ""));
                break;
            }
            case StepKind.Choices:
            {
                var choices = step.Choices!;
                output.WriteLine($"choices {choices.Conversation}/{choices.Node}: {string.Join(", ", choices.Options.Select(o => o.Id + (o.Enabled ? "" : " (disabled)")))}"
                    + (choices.TimeoutOption is null ? "" : $"; after {choices.TimeoutMs}ms takes {choices.TimeoutOption}"));
                foreach (var why in runtime.ExplainChoice(facts)) output.WriteLine($"  {why.Id}: {why.Availability} ({why.Why})");
                break;
            }
            case StepKind.Ended:
                output.WriteLine("ended " + step.Result);
                break;
        }
        foreach (var e in step.Events) output.WriteLine("event " + Describe(e));
    }

    private static string Describe(DialogueEvent e)
    {
        switch (e)
        {
            case SignalEvent signal: return "signal " + signal.Name;
            case QuestEvent quest: return "quest " + quest.Quest + " " + QuestStatuses.Name(quest.Status) + (quest.Stage.Length > 0 ? " at " + quest.Stage : "");
            case VariableEvent variable: return "variable " + variable.Id + " = " + Show(variable.Value);
        }
        throw new InvalidOperationException("unknown dialogue event " + e.GetType().Name);
    }

    private static string Show(FactValue value)
    {
        switch (value.Kind)
        {
            case FactKind.Boolean: return value.Boolean ? "true" : "false";
            case FactKind.Integer: return value.Integer.ToString(CultureInfo.InvariantCulture);
            case FactKind.String: break;
        }
        return "\"" + value.Text + "\"";
    }

    private static IReadOnlyDictionary<string, FactValue> ReadFacts(JsonElement facts, DialogueSet set)
    {
        if (facts.ValueKind != JsonValueKind.Object) throw Json.Refuse(DialogueCodes.Format, "session.facts must be an object");
        var result = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        foreach (var property in facts.EnumerateObject())
        {
            if (!property.Name.StartsWith("ext.", StringComparison.Ordinal) || !set.Externals.TryGetValue(property.Name[4..], out var def))
                throw Json.Refuse(DialogueCodes.Reference, "session.facts names '" + property.Name + "', which is not an external fact of the document");
            var value = property.Value;
            switch (def.Kind)
            {
                case FactKind.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False: result[property.Name] = FactValue.From(value.GetBoolean()); continue;
                case FactKind.Integer when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number): result[property.Name] = FactValue.From(number); continue;
                case FactKind.String when value.ValueKind == JsonValueKind.String: result[property.Name] = FactValue.From(value.GetString()!); continue;
            }
            throw Json.Refuse(DialogueCodes.ValueType, "session.facts." + property.Name + " must be a " + def.Kind.ToString().ToLowerInvariant());
        }
        return result;
    }
}

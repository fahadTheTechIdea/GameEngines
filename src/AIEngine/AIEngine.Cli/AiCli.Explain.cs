using System.Globalization;
using System.Text.Json;
using AIEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

public static partial class AiCli
{
    private static readonly StrictJson Json = new(AiCodes.ForDocument);

    /// <summary>
    /// Runs one think of one agent, as a game would. The scenario is <c>{ "behavior": id, "tick": n, "keys": { key: value }, "percepts": [ { "kind", "id", "owner", "attrs": { name: value } } ] }</c>:
    /// the keys are the ones the host writes, each percept is told as the host would tell it, and nothing else is known to the agent.
    /// </summary>
    private static int Explain(string document, string scenario, TextWriter output)
    {
        try
        {
            var set = AiLoader.Load(document);
            using var parsed = Json.Parse(scenario, "scenario");
            var root = Json.Object(parsed.RootElement, "scenario", ["behavior", "tick", "keys", "percepts"]);
            var behavior = Json.Text(root, "behavior", "scenario");
            var tick = Json.Long(root, "tick", "scenario", 0, AiRuntime.TickLimit);
            var runtime = new AiRuntime(set, 1);
            const string agent = "subject";
            runtime.Add(agent, behavior, 0);
            Told(runtime, agent, root, set);
            runtime.Trace(agent, true);
            var report = runtime.Step(tick, set.MaxCost);
            output.Write(runtime.Explain(agent)!.ToText());
            foreach (var intent in report.Intents)
                output.WriteLine($"intent {intent.Name}({string.Join(", ", intent.Args.Select(a => a.Name + "=" + Show(a.Value)))}) from {intent.Node}");
            if (report.Intents.Count == 0) output.WriteLine("no intent raised");
            return 0;
        }
        catch (EngineRefusedException refusal)
        {
            return Refused(output, refusal);
        }
    }

    private static void Told(AiRuntime runtime, string agent, JsonElement root, AiSet set)
    {
        var keys = root.GetProperty("keys");
        if (keys.ValueKind != JsonValueKind.Object) throw Json.Refuse(AiCodes.Format, "scenario.keys must be an object");
        foreach (var key in keys.EnumerateObject())
        {
            if (!set.TryKey(key.Name, out var def)) throw Json.Refuse(AiCodes.Reference, "scenario.keys names '" + key.Name + "', which is not a key of the document");
            runtime.SetKey(agent, key.Name, Value(key.Value, def.Value.Kind, "scenario.keys." + key.Name));
        }
        var percepts = Json.Array(root, "percepts", "scenario", 0, 65_536);
        var number = 0;
        foreach (var element in percepts.EnumerateArray())
        {
            var where = "scenario.percepts[" + number++ + "]";
            Json.Object(element, where, ["kind", "id", "owner", "attrs"]);
            var kind = Json.Text(element, "kind", where);
            if (!set.TryPercept(kind, out var def)) throw Json.Refuse(AiCodes.Reference, where + " names '" + kind + "', which is not a percept kind of the document");
            var attrs = element.GetProperty("attrs");
            if (attrs.ValueKind != JsonValueKind.Object) throw Json.Refuse(AiCodes.Format, where + ".attrs must be an object");
            var told = new Dictionary<string, FactValue>(StringComparer.Ordinal);
            foreach (var attr in attrs.EnumerateObject())
            {
                var declared = def.Attrs.FirstOrDefault(a => a.Id == attr.Name);
                if (declared is null) throw Json.Refuse(AiCodes.Reference, where + ".attrs names '" + attr.Name + "', which the kind does not declare");
                told[attr.Name] = Value(attr.Value, declared.Kind, where + ".attrs." + attr.Name);
            }
            runtime.Perceive(agent, kind, Json.Text(element, "id", where), Json.Text(element, "owner", where), told, 0);
        }
    }

    private static FactValue Value(JsonElement value, FactKind kind, string where)
    {
        switch (kind)
        {
            case FactKind.Boolean:
                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return FactValue.From(value.GetBoolean());
                throw Json.Refuse(AiCodes.ValueType, where + " must be true or false");
            case FactKind.Integer:
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return FactValue.From(number);
                throw Json.Refuse(AiCodes.ValueType, where + " must be a whole number");
            case FactKind.String:
                break;
        }
        return value.ValueKind == JsonValueKind.String ? FactValue.From(value.GetString()!) : throw Json.Refuse(AiCodes.ValueType, where + " must be a string");
    }

    private static string Show(FactValue value)
    {
        switch (value.Kind)
        {
            case FactKind.Boolean: return value.Boolean ? "true" : "false";
            case FactKind.Integer: return value.Integer.ToString(CultureInfo.InvariantCulture);
            case FactKind.String: break;
        }
        return "'" + value.Text + "'";
    }
}

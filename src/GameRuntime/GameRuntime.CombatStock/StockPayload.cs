using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GameRuntime;

/// <summary>
/// The payload of the stock-flows effect: <c>{"flows":[{"kind":"consumed","squad":5,"resource":"stock.ammo.x","amount":30}, ...]}</c>.
/// Reading is strict, like every effect payload: all four fields, none unknown, whole numbers, a known kind.
/// </summary>
public static class StockPayload
{
    public static string Write(IReadOnlyList<StockLine> lines)
    {
        var builder = new StringBuilder("{\"flows\":[");
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (i > 0) builder.Append(',');
            builder.Append(CultureInfo.InvariantCulture, $"{{\"kind\":{JsonSerializer.Serialize(line.Flow.Name)},\"squad\":{line.Squad},\"resource\":{JsonSerializer.Serialize(line.Resource)},\"amount\":{line.Amount}}}");
        }
        return builder.Append("]}").ToString();
    }

    /// <summary>The lines, or null when the payload does not read.</summary>
    public static IReadOnlyList<StockLine>? Read(string payloadJson)
    {
        if (EffectPayload.Parse(payloadJson, "flows") is not { } root) return null;
        var element = root.GetProperty("flows");
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0) return null;
        var lines = new List<StockLine>(element.GetArrayLength());
        foreach (var item in element.EnumerateArray())
        {
            if (EffectPayload.Parse(item.GetRawText(), "kind", "squad", "resource", "amount") is not { } line
                || !EffectPayload.Text(line, "kind", out var kind) || !StockFlows.TryGet(kind, out var flow)
                || !EffectPayload.Int(line, "squad", out var squad) || !EffectPayload.Text(line, "resource", out var resource)
                || !EffectPayload.Int(line, "amount", out var amount)) return null;
            if (squad <= 0 || (flow.Everything ? amount != 0 : amount <= 0)) return null;
            lines.Add(new StockLine(flow, squad, resource, amount));
        }
        return lines;
    }
}

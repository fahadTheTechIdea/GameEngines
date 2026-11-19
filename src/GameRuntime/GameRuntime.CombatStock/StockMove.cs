using System.Globalization;
using System.Text;

namespace GameRuntime;

/// <summary>Stock moved from one holder (a depot) to another (a squad): whole units of one resource.</summary>
public readonly record struct StockMove(int From, int To, string Resource, int Amount);

/// <summary>
/// The payload of the supply effect: <c>{"moves":[{"from":3,"to":7,"resource":"stock.ammo.x","amount":30}, ...]}</c>. Reading is strict, like every
/// effect payload.
/// </summary>
public static class StockMovePayload
{
    public static string Write(IReadOnlyList<StockMove> moves)
    {
        var builder = new StringBuilder("{\"moves\":[");
        for (var i = 0; i < moves.Count; i++)
        {
            if (i > 0) builder.Append(',');
            builder.Append(CultureInfo.InvariantCulture,
                $"{{\"from\":{moves[i].From},\"to\":{moves[i].To},\"resource\":{System.Text.Json.JsonSerializer.Serialize(moves[i].Resource)},\"amount\":{moves[i].Amount}}}");
        }
        return builder.Append("]}").ToString();
    }

    /// <summary>The moves, or null when the payload does not read.</summary>
    public static IReadOnlyList<StockMove>? Read(string payloadJson)
    {
        if (EffectPayload.Parse(payloadJson, "moves") is not { } root) return null;
        var element = root.GetProperty("moves");
        if (element.ValueKind != System.Text.Json.JsonValueKind.Array || element.GetArrayLength() == 0) return null;
        var moves = new List<StockMove>(element.GetArrayLength());
        foreach (var item in element.EnumerateArray())
        {
            if (EffectPayload.Parse(item.GetRawText(), "from", "to", "resource", "amount") is not { } move
                || !EffectPayload.Int(move, "from", out var from) || !EffectPayload.Int(move, "to", out var to)
                || !EffectPayload.Text(move, "resource", out var resource) || !EffectPayload.Int(move, "amount", out var amount)) return null;
            if (from <= 0 || to <= 0 || from == to || amount <= 0) return null;
            moves.Add(new StockMove(from, to, resource, amount));
        }
        return moves;
    }
}

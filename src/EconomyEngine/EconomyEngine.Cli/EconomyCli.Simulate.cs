using System.Globalization;
using System.Text.Json;
using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

public static partial class EconomyCli
{
    /// <summary>One scripted call, made before the period after <see cref="Period"/> closes. A and B are the texts the kind takes, N and M its numbers.</summary>
    private enum ShotKind { Shock, Trade, Settle, Growth, Fund }

    private sealed record ScenarioShot(long Period, ShotKind Kind, string A, string B, long N, long M);

    private sealed record Scenario(Dictionary<string, FactValue> Facts, List<ScenarioShot> Script);

    private sealed class ScenarioProblem(string message) : Exception(message);

    private static string Text(JsonElement owner, string field, string where, bool optional = false)
    {
        if (owner.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString()!;
        if (optional && !owner.TryGetProperty(field, out _)) return "";
        throw new ScenarioProblem(where + " needs a text '" + field + "'");
    }

    private static long Number(JsonElement owner, string field, string where, bool optional = false)
    {
        if (owner.TryGetProperty(field, out var value) && value.TryGetInt64(out var number)) return number;
        if (optional && !owner.TryGetProperty(field, out _)) return 0;
        throw new ScenarioProblem(where + " needs a whole number '" + field + "'");
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

    /// <summary>Reads a scenario: <c>facts</c> the host gives, and a <c>script</c> of calls at periods: shock (shock, market, scale), trade (market, good, quantity), settle (schedule), growth (currency, ppm), fund (market, delta).</summary>
    private static Scenario ReadScenario(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new ScenarioProblem("a scenario is an object");
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        if (root.TryGetProperty("facts", out var factList) && factList.ValueKind == JsonValueKind.Object)
            foreach (var fact in factList.EnumerateObject()) facts[fact.Name] = FactOf(fact.Value, fact.Name);
        var script = new List<ScenarioShot>();
        if (root.TryGetProperty("script", out var shots) && shots.ValueKind == JsonValueKind.Array)
            foreach (var shot in shots.EnumerateArray())
            {
                var where = "script[" + script.Count + "]";
                var period = Number(shot, "period", where);
                var word = Text(shot, "do", where);
                if (!Kinds.TryGetValue(word, out var kind)) throw new ScenarioProblem(where + " has an unknown 'do' '" + word + "'");
                switch (kind)
                {
                    case ShotKind.Shock: script.Add(new ScenarioShot(period, kind, Text(shot, "shock", where), Text(shot, "market", where, true), Number(shot, "scale", where, true), 0)); break;
                    case ShotKind.Trade: script.Add(new ScenarioShot(period, kind, Text(shot, "market", where), Text(shot, "good", where), Number(shot, "quantity", where), 0)); break;
                    case ShotKind.Settle: script.Add(new ScenarioShot(period, kind, Text(shot, "schedule", where), "", 0, 0)); break;
                    case ShotKind.Growth: script.Add(new ScenarioShot(period, kind, Text(shot, "currency", where), "", Number(shot, "ppm", where), 0)); break;
                    case ShotKind.Fund: script.Add(new ScenarioShot(period, kind, Text(shot, "market", where), "", Number(shot, "delta", where), 0)); break;
                }
            }
        return new Scenario(facts, script.OrderBy(s => s.Period).ToList());
    }

    private static readonly Dictionary<string, ShotKind> Kinds = new(StringComparer.Ordinal)
    {
        ["shock"] = ShotKind.Shock, ["trade"] = ShotKind.Trade, ["settle"] = ShotKind.Settle, ["growth"] = ShotKind.Growth, ["fund"] = ShotKind.Fund,
    };

    private static string Do(EconomyRuntime runtime, ScenarioShot shot)
    {
        var said = "";
        switch (shot.Kind)
        {
            case ShotKind.Shock: runtime.ApplyShock(shot.A, shot.B, shot.N == 0 ? 1000 : (int)shot.N); said = "shock " + shot.A + (shot.B.Length > 0 ? " on " + shot.B : ""); break;
            case ShotKind.Trade:
                var trade = runtime.Trade(shot.A, shot.B, shot.N);
                said = $"trade {shot.A} {shot.B} {shot.N}: unit {trade.UnitPrice} value {trade.Value} tax {trade.Tax} total {trade.Total}";
                break;
            case ShotKind.Settle: runtime.Settle(shot.A); said = "settle " + shot.A; break;
            case ShotKind.Growth: runtime.SetGrowth(shot.A, shot.N); said = "growth " + shot.A + " " + shot.N; break;
            case ShotKind.Fund: runtime.Fund(shot.A, shot.N); said = "fund " + shot.A + " " + shot.N; break;
        }
        return said;
    }

    /// <summary>The one place a scenario is lived: for each period the scripted calls due at it, then the period. Returns how many calls were refused; each call's outcome goes to <paramref name="did"/> or <paramref name="Report"/>, each closed period to <paramref name="advanced"/>.</summary>
    private static int Live(EconomyRuntime runtime, Scenario scenario, long periods, Action<ScenarioShot, string> did, Action<ScenarioShot, EngineRefusedException> Report, Action<long, EconomyChanges> advanced)
    {
        var refused = 0;
        var shots = new Queue<ScenarioShot>(scenario.Script);
        for (long period = 0; period < periods; period++)
        {
            while (shots.Count > 0 && shots.Peek().Period <= period)
            {
                var shot = shots.Dequeue();
                try { did(shot, Do(runtime, shot)); }
                catch (EngineRefusedException refusal)
                {
                    refused++;
                    Report(shot, refusal);
                }
            }
            advanced(period, runtime.Advance((period + 1) * runtime.Set.PeriodSteps, scenario.Facts));
        }
        return refused;
    }

    private static int Simulate(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        string? savePath = null;
        var verbose = false;
        for (var i = 4; i < args.Count; i++)
        {
            if (args[i] == "--verbose") verbose = true;
            else if (args[i] == "--save" && i + 1 < args.Count) savePath = args[++i];
            else return Wrong(error, Usage);
        }
        if (!int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var periods) || periods < 1 || periods > 100_000) return Wrong(error, "periods is a whole number from 1 to 100000");
        try
        {
            var set = EconomyLoader.Load(document);
            Scenario scenario;
            try { scenario = ReadScenario(File.ReadAllText(args[2])); }
            catch (ScenarioProblem problem) { return Wrong(error, "scenario problem: " + problem.Message); }
            catch (JsonException problem) { return Wrong(error, "scenario problem: not JSON: " + problem.Message); }
            var runtime = EconomyRuntime.Create(set);
            var refused = Live(runtime, scenario, periods,
                (shot, said) => output.WriteLine($"period {runtime.Period}: {said}"),
                (shot, refusal) => output.WriteLine($"period {runtime.Period}: {shot.Kind.ToString().ToLowerInvariant()} {shot.A} refused {refusal.Code}: {refusal.Message}"),
                (period, changes) =>
                {
                    if (verbose) PrintChanges(output, runtime, changes);
                    if ((period + 1) % 10 == 0) PrintPeriod(output, runtime);
                });
            PrintState(output, runtime);
            var bytes = EconomySave.Write(runtime);
            if (savePath is not null) File.WriteAllBytes(savePath, bytes);
            output.WriteLine("digest " + runtime.StateDigest());
            return refused == 0 ? 0 : 1;
        }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
    }

    private static void PrintChanges(TextWriter output, EconomyRuntime runtime, EconomyChanges changes)
    {
        var set = runtime.Set;
        var at = "  period " + runtime.Period + ": ";
        foreach (var rate in changes.Rates) output.WriteLine($"{at}{set.Currencies[rate.Currency].Id} rate {rate.From} -> {rate.To}");
        foreach (var shock in changes.Shocks) output.WriteLine($"{at}shock {set.Shocks[shock.Shock].Id} {(shock.Started ? "began" : "ended")}");
        foreach (var arrival in changes.Arrivals) output.WriteLine($"{at}{arrival.Quantity} {set.Goods[arrival.Good].Id} arrived by {set.Routes[arrival.Route].Id}");
        foreach (var band in changes.Bands) output.WriteLine($"{at}{set.Markets[band.Market].Id} {set.Goods[band.Good].Id} {set.Bands[band.From].Id} -> {set.Bands[band.To].Id}");
        foreach (var shortage in changes.Shortages) output.WriteLine($"{at}{set.Markets[shortage.Market].Id} {set.Goods[shortage.Good].Id} {(shortage.Entered ? "is short" : "is supplied again")}");
        foreach (var launch in changes.Launches) output.WriteLine($"{at}{launch.Quantity} {set.Goods[launch.Good].Id} left by {set.Routes[launch.Route].Id}, due {launch.Arrives}");
        foreach (var block in changes.Blocks) output.WriteLine($"{at}route {set.Routes[block.Route].Id} could not ship {set.Goods[block.Good].Id}: too many shipments on the way");
        foreach (var settled in changes.Settlements) output.WriteLine($"{at}{set.Markets[settled.Market].Id} {settled.Kind.ToString().ToLowerInvariant()} {settled.Id}: {settled.Paid} of {settled.Due}");
        foreach (var loan in changes.Loans) output.WriteLine($"{at}loan {loan.Loan} {loan.Outcome.ToString().ToLowerInvariant()}, balance {loan.Balance}");
    }

    private static void PrintPeriod(TextWriter output, EconomyRuntime runtime)
    {
        var set = runtime.Set;
        output.WriteLine($"period {runtime.Period}: " + string.Join("; ", set.Markets.Select(m => m.Id + " " + string.Join(' ', m.Pairs.Select(p => set.Goods[p.Good].Id + "=" + runtime.Ratio(m.Index, p.Index))))));
    }

    private static void PrintState(TextWriter output, EconomyRuntime runtime)
    {
        var set = runtime.Set;
        output.WriteLine($"state at period {runtime.Period}, step {runtime.Step}");
        foreach (var currency in set.Currencies)
        {
            var state = runtime.Currencies[currency.Index];
            output.WriteLine($"currency {currency.Id} rate {state.Rate} level {state.Level} money {state.Money} world {state.World} minted {state.Minted}");
        }
        foreach (var market in set.Markets)
        {
            var state = runtime.Markets[market.Index];
            output.WriteLine($"market {market.Id} treasury {state.Treasury}{(runtime.InArrears(market.Index) ? " in arrears" : "")}");
            foreach (var pair in market.Pairs)
            {
                var p = state.Pairs[pair.Index];
                output.WriteLine($"  {set.Goods[pair.Good].Id} price {p.Price} ratio {runtime.Ratio(market.Index, pair.Index)} band {set.Bands[p.Band].Id} stock {p.Stock} supply {p.Supply} demand {p.Demand}{(p.Short ? " short" : "")}");
            }
        }
        foreach (var loan in runtime.Loans) output.WriteLine($"loan {loan.Id} {loan.Status.ToString().ToLowerInvariant()} balance {loan.Balance} remaining {loan.Remaining} missed {loan.Missed}");
        foreach (var shipment in runtime.Shipments) output.WriteLine($"shipment {shipment.Quantity} {set.Goods[shipment.Good].Id} by {set.Routes[shipment.Route].Id} due {shipment.Arrives}");
    }
}

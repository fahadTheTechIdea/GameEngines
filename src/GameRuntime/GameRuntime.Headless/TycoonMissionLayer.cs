using System.Collections.Immutable;
using System.Text.Json;
using ScenarioEngine.Contracts;
using ScenarioEngine.Core;

namespace GameRuntime.Headless;

/// <summary>
/// The tycoon mission as a ScenarioEngine game layer. It hears the sim's cash and objective events as observations, asks
/// the world for credit when the company is founded or stock is sold, and never touches the sim directly.
/// </summary>
public sealed class TycoonMissionLayer : IGameLayer
{
    public const string LayerId = "gf_tycoon";
    public const string CashChanged = "gf_tycoon.cash-changed";
    public const string ObjectiveChanged = "gf_tycoon.objective-changed";
    public const string Sell = "gf_tycoon.sell";
    public const string CreditRequest = "sim." + CreditEffect.EffectName;

    /// <summary>The sim stores money as Q32.32; the mission works in whole units.</summary>
    private const int FractionalBits = Beep.SimEngine.Fix.FractionalBits;

    public string Id => LayerId;
    public string Version => "1.0.0";

    private static JsonElement Schema(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public LayerCapabilities Capabilities { get; } = new(
        ImmutableDictionary<string, FactDefinition>.Empty
            .Add("gf_tycoon.profit", new(FactKind.Integer, -1_000_000_000, 1_000_000_000))
            .Add("gf_tycoon.stock", new(FactKind.Integer, 0, 1_000_000)),
        ImmutableDictionary<string, JsonElement>.Empty
            .Add(Sell, Schema("""{"type":"object","required":["units"],"additionalProperties":false,"properties":{"units":{"type":"integer","minimum":1,"maximum":10}}}""")),
        ImmutableDictionary<string, JsonElement>.Empty,
        ImmutableDictionary<string, JsonElement>.Empty
            .Add(CashChanged, Schema("""{"type":"object","required":["company","before","after"],"additionalProperties":false,"properties":{"company":{"type":"integer","minimum":0,"maximum":64},"before":{"type":"integer","minimum":-9000000000000000000,"maximum":9000000000000000000},"after":{"type":"integer","minimum":-9000000000000000000,"maximum":9000000000000000000}}}"""))
            .Add(ObjectiveChanged, Schema("""{"type":"object","required":["company","objective","state"],"additionalProperties":false,"properties":{"company":{"type":"integer","minimum":0,"maximum":64},"objective":{"type":"string","maxLength":128},"state":{"type":"string","maxLength":32}}}""")),
        Schema("""{"type":"object","required":["startingStock","pricePerUnit","foundingBonus"],"additionalProperties":false,"properties":{"startingStock":{"type":"integer","minimum":0,"maximum":1000},"pricePerUnit":{"type":"integer","minimum":1,"maximum":1000},"foundingBonus":{"type":"integer","minimum":1,"maximum":100000}}}"""));

    public ValidationReport Validate(JsonElement content) => ValidationReport.Valid;

    public ILayerRuntime Create(LayerStartContext context) => new Runtime(context);

    private sealed record State(bool HasBaseline, long Baseline, long Cash, long Stock, bool Founded, long Requests)
    {
        public static State Read(JsonElement data) => new(
            data.GetProperty("hasBaseline").GetBoolean(), data.GetProperty("baseline").GetInt64(), data.GetProperty("cash").GetInt64(),
            data.GetProperty("stock").GetInt64(), data.GetProperty("founded").GetBoolean(), data.GetProperty("requests").GetInt64());

        public LayerSnapshot ToSnapshot() => new(ContractJson.Element(new
        {
            hasBaseline = HasBaseline, baseline = Baseline, cash = Cash, stock = Stock, founded = Founded, requests = Requests,
        }));

        public long Profit => HasBaseline ? Cash - Baseline : 0;
    }

    private sealed class Runtime(LayerStartContext start) : ILayerRuntime
    {
        private readonly long _price = start.Scenario.LayerData.GetProperty("pricePerUnit").GetInt64();
        private readonly long _bonus = start.Scenario.LayerData.GetProperty("foundingBonus").GetInt64();

        public LayerSnapshot InitialState { get; } =
            new State(false, 0, 0, start.Scenario.LayerData.GetProperty("startingStock").GetInt64(), false, 0).ToSnapshot();

        public LayerStepResult Resolve(LayerSnapshot previous, LayerTickContext context)
        {
            var state = State.Read(previous.Data);
            var events = ImmutableArray.CreateBuilder<DomainEvent>();
            var requests = ImmutableArray.CreateBuilder<WorldRequest>();

            WorldRequest Credit(long amount)
            {
                state = state with { Requests = state.Requests + 1 };
                return new($"credit-{state.Requests}", context.World.Generation, CreditRequest, ContractJson.Element(amount));
            }

            foreach (var observation in context.World.Observations)
            {
                var payload = observation.Payload;
                if (payload.GetProperty("company").GetInt64() != 0) continue;
                switch (observation.Kind)
                {
                    case CashChanged:
                        var before = payload.GetProperty("before").GetInt64() >> FractionalBits;
                        var after = payload.GetProperty("after").GetInt64() >> FractionalBits;
                        if (!state.HasBaseline) state = state with { HasBaseline = true, Baseline = before };
                        state = state with { Cash = after };
                        events.Add(new("gf_tycoon.cash", ContractJson.Element(new { cash = state.Cash, profit = state.Profit })));
                        break;
                    case ObjectiveChanged:
                        if (payload.GetProperty("state").GetString() != "Completed" || state.Founded) break;
                        state = state with { Founded = true };
                        requests.Add(Credit(_bonus));
                        events.Add(new("gf_tycoon.founded", ContractJson.Element(new { bonus = _bonus, stock = state.Stock })));
                        break;
                    default:
                        throw new InvalidOperationException("tycoon mission received an observation it did not declare: " + observation.Kind);
                }
            }

            var outcomes = ImmutableArray.CreateBuilder<CommandOutcome>();
            foreach (var queued in context.Commands)
            {
                var command = queued.Command;
                if (command.Operation != Sell) throw new InvalidOperationException("tycoon mission received a command it did not declare: " + command.Operation);
                var units = command.Payload.GetProperty("units").GetInt64();
                if (units > state.Stock) { outcomes.Add(new(command.CommandId, false, "NO_STOCK")); continue; }
                state = state with { Stock = state.Stock - units };
                requests.Add(Credit(units * _price));
                outcomes.Add(new(command.CommandId, true, "APPLIED"));
                events.Add(new("gf_tycoon.sold", ContractJson.Element(new { units, stock = state.Stock }), command.CommandId));
            }

            return new(state.ToSnapshot(), outcomes.ToImmutable(), events.ToImmutable(), requests.ToImmutable());
        }

        public LayerEffectResult ApplyEffects(LayerSnapshot candidate, ImmutableArray<EffectEnvelope> effects, LayerTickContext context) =>
            effects.IsEmpty ? new(candidate, []) : throw new InvalidOperationException("tycoon mission declares no effects but received " + effects[0].Type);

        public ImmutableDictionary<string, FactValue> ProjectFacts(LayerSnapshot snapshot)
        {
            var state = State.Read(snapshot.Data);
            return ImmutableDictionary<string, FactValue>.Empty
                .Add("gf_tycoon.profit", FactValue.From(state.Profit))
                .Add("gf_tycoon.stock", FactValue.From(state.Stock));
        }

        public ValidationReport ValidateState(LayerSnapshot snapshot)
        {
            var state = State.Read(snapshot.Data);
            return state.Stock < 0 ? new([new("TYCOON_STATE", "Stock cannot be negative.")]) : ValidationReport.Valid;
        }
    }
}

using System.Globalization;
using Beep.SimEngine;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;
using GameRuntime.Field.Headless;

namespace GameRuntime.Tests;

/// <summary>
/// A field world with two squads raised and no commander or combat: effects are sent one at a time through the same dispatcher the game
/// uses, so a test drives exactly the path the stock-flow bridge drives, and reads the stock back as a player would.
/// </summary>
internal sealed class StockHarness
{
    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    private readonly Reporter _reporter = new();
    private readonly RuntimeSession _session;
    private readonly List<CommandResolved> _resolved = new();
    private int _request;

    public FieldWorld World { get; }
    public FieldForces Forces { get; }
    public List<(string Kind, string Payload)> Observed { get; } = new();
    public IReadOnlyList<string> Reports => _reporter.Reports;

    /// <summary>The entity ids of the two squads the harness raised, and of the two depots the content places first.</summary>
    public int SquadA { get; private set; }
    public int SquadB => SquadA + 1;
    public int DepotA => World.Depots[0];
    public int DepotB => World.Depots[1];

    public StockHarness()
    {
        var options = FieldOptions.Parse(["--pack-a", "arms-modern", "--formation-a", "arms-modern.rifle-company", "--squads-a", "1",
            "--pack-b", "arms-modern", "--formation-b", "arms-modern.rifle-company", "--squads-b", "1", "--frame-ms", "33"]);
        Forces = FieldForceSource.Load(options);
        World = FieldWorld.Create(options, Forces, null);
        _session = new RuntimeSession(new RuntimeClock(World.Game.AuthoredTicksPerSecond), _reporter);
        _session.AddComponent(new SimComponent(World.Game, null, World.Outbox), 1);
        _session.AddComponent(new SimEffectDispatcher(World.Game, World.Tickets), 2);
        _session.Subscribe<ObservationPosted>(e => Observed.Add((e.Kind, e.PayloadJson)));
        _session.Subscribe<CommandResolved>(_resolved.Add);
        _session.Init();
        var spawn = Send("characters.spawn-formation", "{\"formation\":\"arms-modern.rifle-company\",\"squads\":2,\"company\":0,\"seed\":1,\"layout\":{\"x\":10,\"y\":10,\"columns\":2,\"spacing\":2}}");
        if (!spawn.Applied) throw new InvalidOperationException("the harness could not raise its squads: " + spawn.Code);
        SquadA = Observed.Where(o => o.Kind == "sim.event.formation-spawned").Select(o => System.Text.Json.JsonDocument.Parse(o.Payload).RootElement.GetProperty("first").GetInt32()).Single();
    }

    public static long OneTick(RuntimeSession session) =>
        (RuntimeClock.MicrosecondsPerSecond + session.Clock.TicksPerSecond - 1) / session.Clock.TicksPerSecond;

    /// <summary>Sends one effect and runs the world until it is answered; returns the answer.</summary>
    public CommandResolved Send(string effect, string payload)
    {
        var id = "t" + (++_request).ToString(CultureInfo.InvariantCulture);
        var before = _resolved.Count;
        _session.Submit(new EffectRequested(id, "sim." + effect, payload, _session.Clock.Tick));
        for (var i = 0; i < 4 && !_resolved.Skip(before).Any(r => r.CommandId == "effect:" + id && r.Code != "QUEUED"); i++)
            _session.Update(OneTick(_session));
        return _resolved.Skip(before).Single(r => r.CommandId == "effect:" + id && r.Code != "QUEUED");
    }

    public CommandResolved Flows(params (string Kind, int Squad, string Resource, int Amount)[] lines) =>
        Send(StockFlowEffect.EffectName, "{\"flows\":[" + string.Join(',', lines.Select(l => string.Create(CultureInfo.InvariantCulture,
            $"{{\"kind\":\"{l.Kind}\",\"squad\":{l.Squad},\"resource\":\"{l.Resource}\",\"amount\":{l.Amount}}}"))) + "]}");

    /// <summary>What the squad holds, by resource, as its owner sees it.</summary>
    public SortedDictionary<string, long> Stock(int squad)
    {
        var facets = new List<Facet>();
        if (!World.Game.Seat(0).View.Inspect(new EntityId(squad), facets)) throw new InvalidOperationException("no such squad " + squad);
        var stock = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var facet in facets.Where(f => f.Key == Storage.FacetStock)) stock[facet.Subject!] = facet.Value.Raw >> Fix.FractionalBits;
        return stock;
    }

    /// <summary>The ammunition resource the first rifle company battery uses, and the rounds one squad of it carries.</summary>
    public (string Resource, long Rounds) Ammunition()
    {
        var group = Forces.Groups[0];
        var rounds = StockContent.Rounds(group);
        var first = rounds.First();
        return (first.Key, first.Value);
    }

    /// <summary>The hash of the entities only, taken after saving so a queued request cannot move it: a refused effect must leave it alone.</summary>
    public ulong EntitiesHash()
    {
        _ = World.Game.Save();
        return World.Game.HashBreakdown().First(p => p.Key == "entities").Value;
    }

    public IEnumerable<string> FlowKinds() =>
        Observed.Where(o => o.Kind == "sim.event.stock-changed").Select(o => System.Text.Json.JsonDocument.Parse(o.Payload).RootElement.GetProperty("kind").GetString()!);
}

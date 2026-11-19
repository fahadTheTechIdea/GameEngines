using System.Globalization;
using System.Text;
using CalendarEngine;
using DiplomacyEngine;
using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime.Diplomacy.Headless;

/// <summary>
/// A headless game of five realms on a real calendar (the Gregorian sample, 2024) whose seasons are the diplomacy's seasons, with the economy of their five markets beside them. Treaties are made and kept, a vassal pays
/// its tribute out of its treasury through the economy's fund path, a non-aggression pact is broken by a war, allies are called to arms, a peace is made, agents spy and are caught, a ruler dies, and a realm grows so strong
/// that the others balance against it. The player of the demo (the realm Cinderfell, the one the host decides for) only asks with events. The run is recorded in a journal, a checkpoint is taken part way and the rest is
/// replayed from it on a fresh session: the replay must end in the same state and raise the same events as the live run. The printed output is hashed; two runs print the same hash.
/// </summary>
public static class IslesDiplomacyGame
{
    public const int Ok = 0, Refused = 2, Diverged = 3;

    public const int TicksPerSecond = 50;
    private const string Usage = "usage: GameRuntime.Diplomacy.Headless [--days <60-3000>] [--checkpoint-day <day>] [--verbose]";

    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    /// <summary>What the game keeps that the layers read as host facts: how bad the isles' mood is (the diplomacy's), and for the calendar and the economy whether it is raining and whether the plague is about.</summary>
    public sealed class IslesHost
    {
        public long Crisis { get; set; } = 2;

        public IReadOnlyDictionary<string, FactValue> Game() => new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["game.crisis"] = FactValue.From(Crisis) };

        public IReadOnlyDictionary<string, FactValue> CalendarHost() => new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["weather.raining"] = FactValue.From(false), ["story.plague"] = FactValue.From(0L) };
    }

    /// <summary>The days the game's script puts things to the realms: (day, request kind, a, b, c, number, number2).</summary>
    public static readonly IReadOnlyList<(long Day, string Kind, string A, string B, string C, long Number, long Number2)> Script =
    [
        (2, "propose", "aurel", "brass", "alliance", 0, 0),
        (3, "propose", "dune", "cinder", "trade", 0, 0),
        (4, "propose", "brass", "dune", "vassalage", 0, 1000),
        (6, "mission", "agent-brass-1", "aurel", "spy", 0, 0),
        (8, "propose", "ember", "dune", "nap", 180, 0),
        (12, "mission", "agent-ember-1", "cinder", "sabotage", 0, 0),
        (16, "propose", "aurel", "cinder", "marriage", 0, 0),
        (22, "propose", "cinder", "ember", "embargo", 40, 0),
        (30, "mission", "agent-aurel-1", "ember", "unrest", 0, 0),
        (45, "mission", "agent-brass-2", "dune", "spy", 0, 0),
        (60, "declare", "ember", "dune", "", 0, 0),
        (64, "score", "ember", "battle", "", 0, 900),
        (66, "mission", "agent-brass-2", "aurel", "sabotage", 0, 0),
        (70, "propose", "ember", "dune", "peace", 60, 200),
        (80, "mission", "agent-ember-1", "dune", "sabotage", 0, 0),
        (90, "succeed", "ember", "", "", 0, 0),
        (95, "mission", "agent-brass-2", "dune", "sabotage", 0, 0),
        (100, "mission", "agent-aurel-1", "dune", "unrest", 0, 0),
        (110, "mission", "agent-cinder-1", "aurel", "sabotage", 0, 0),
        (118, "mission", "agent-brass-1", "cinder", "sabotage", 0, 0),
        (120, "might", "aurel", "", "", 1_400_000, 0),
        (150, "mission", "agent-dune-1", "ember", "spy", 0, 0),
    ];

    private static string Resource(string name)
    {
        using var stream = typeof(IslesDiplomacyGame).Assembly.GetManifestResourceStream(name);
        if (stream is null) throw new InvalidOperationException("the program does not carry " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        int days = 240, checkpointDay = 100;
        var verbose = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--days" when i + 1 < args.Count && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out days) && days is >= 60 and <= 3000: i++; break;
                case "--checkpoint-day" when i + 1 < args.Count && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out checkpointDay) && checkpointDay >= 1: i++; break;
                case "--verbose": verbose = true; break;
                case "--help": output.WriteLine(Usage); return Ok;
                default: error.WriteLine("unknown or incomplete option '" + args[i] + "'"); error.WriteLine(Usage); return Refused;
            }
        }
        if (checkpointDay >= days) { error.WriteLine("--checkpoint-day must be before --days"); error.WriteLine(Usage); return Refused; }
        try { return Play(days, checkpointDay, verbose, output); }
        catch (EngineRefusedException exception)
        {
            error.WriteLine(exception.Code + ": " + exception.Message);
            return Refused;
        }
    }

    private static int Play(int days, int checkpointDay, bool verbose, TextWriter output)
    {
        var calendarSet = CalendarLoader.Load(Resource("gregorian.calendar.json"));
        var economySet = EconomyLoader.Load(Resource("isles.economy.json"));
        var dipSet = DipLoader.Load(Resource("isles.diplomacy.json"));
        var chain = LocaleChain.Create("en", [LocaleTable.Load(Resource("isles.diplomacy.en.json"))], "en");
        EconomyCalendar.Check(economySet, calendarSet);
        DiplomacyCalendar.Check(dipSet, calendarSet);
        DiplomacyEconomy.Check(dipSet, economySet);
        var system = calendarSet.System;
        var ticksPerDay = calendarSet.Time.TicksPerDay;
        var start = system.InstantOf(new CalendarDate(2024, system.Months.First(m => m.Id == "january").Index, 1), 0, 0);
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(TicksPerSecond, 8), reporter);
        var calendarChannels = new CalendarChannels();
        calendarChannels.Install(session);
        var economyChannels = new EconomyChannels();
        economyChannels.Install(session);
        var diplomacyChannels = new DiplomacyChannels();
        diplomacyChannels.Install(session);
        var host = new IslesHost();
        var journal = new DiplomacyJournal();
        var calendarRuntime = CalendarRuntime.Create(calendarSet, start, 60, 1);   // one calendar hour per game tick: 24 ticks a day, the economy's own step
        var calendar = new CalendarComponent(calendarRuntime, host.CalendarHost, calendarChannels);
        var economyRuntime = EconomyRuntime.Create(economySet);
        var economy = new EconomyComponent(economyRuntime, EconomyCalendar.Clock(calendar, economySet, start), EconomyCalendar.HostFacts(calendar), economyChannels);
        var dipRuntime = DipRuntime.Create(dipSet);
        var diplomacy = new DiplomacyComponent(dipRuntime, DiplomacyCalendar.Clock(calendar, dipSet, start), DiplomacyCalendar.HostFacts(calendar, host.Game), diplomacyChannels, journal: journal);
        var bridge = new DiplomacyEconomy("diplomacy-economy", dipSet, economySet);
        var chancellor = new IslesChancellor(Script);
        session.AddComponent(calendar, 10);
        session.AddComponent(chancellor, 15);
        session.AddComponent(diplomacy, 20);
        session.AddComponent(bridge, 25);
        session.AddComponent(economy, 30);
        var transcript = new DiplomacyTranscript(session);

        var printed = new StringBuilder();
        void Say(string line) { printed.Append(line).Append('\n'); output.WriteLine(line); }
        string Day(long step) => "day " + (step + 1);
        string Realm(string id) => id.Length == 0 ? "" : DipNames.Realm(dipSet, chain, dipSet.RealmById[id].Index);
        string Type(string id) => DipNames.Type(dipSet, chain, dipSet.TypeById[id].Index).ToLowerInvariant();
        string Cb(string id) => id.Length == 0 ? "no casus belli" : DipNames.CasusBelli(dipSet, chain, dipSet.CbById[id].Index).ToLowerInvariant();
        int treaties = 0, breaches = 0, wars = 0, peaces = 0, exposed = 0, coalitions = 0, payments = 0, shocks = 0, signals = 0;
        session.Subscribe<DiplomacyRefused>(e => Say($"refused {e.RequestId} at tick {e.Tick}: {e.Code}"));
        session.Subscribe<DiplomacySeasonChanged>(e => Say($"{Day(e.Step)}: {DipNames.Season(dipSet, chain, dipSet.SeasonById[e.Season].Index)} begins"));
        session.Subscribe<DiplomacyProposalChanged>(e =>
        {
            if (e.Change is "made" && !verbose) return;
            Say($"{Day(e.Step)}: {Realm(e.From)} {(e.Change == "made" ? "offers" : "offered")} {Realm(e.To)} a {Type(e.Type)}: {e.Change}{(e.Change is "accepted" or "rejected" ? $" (score {e.Score})" : "")}");
        });
        session.Subscribe<DiplomacyTreatyChanged>(e =>
        {
            if (e.Change == "signed") treaties++;
            if (e.Change == "broken") breaches++;
            Say($"{Day(e.Step)}: the {Type(e.Type)} between {Realm(e.First)} and {Realm(e.Second)} is {e.Change}{(e.Amount > 0 ? $", tribute {e.Amount}" : "")}");
        });
        session.Subscribe<DiplomacyObligationChanged>(e =>
        {
            if (e.Change == "paid") payments++;
            if (e.Change is "due" or "paid" && !verbose) return;
            Say($"{Day(e.Step)}: {Realm(e.Owing)} owes {Realm(e.Owed)} a {e.Clause}: {e.Change}{(e.Amount > 0 ? $" ({e.Amount})" : "")}");
        });
        session.Subscribe<DiplomacyWarChanged>(e =>
        {
            if (e.Change == "declared") wars++;
            if (e.Change == "ended") peaces++;
            if (e.Change == "score" && !verbose) return;
            Say($"{Day(e.Step)}: war {e.War} ({Realm(e.Attacker)} against {Realm(e.Defender)}) {e.Change}{(e.CasusBelli.Length > 0 || e.Change == "declared" ? ", " + Cb(e.CasusBelli) : "")}{(e.Realm.Length > 0 ? ": " + Realm(e.Realm) : "")}, score {e.Score}");
        });
        session.Subscribe<DiplomacyCoalitionChanged>(e =>
        {
            if (e.Change == "formed") coalitions++;
            Say($"{Day(e.Step)}: against {Realm(e.Target)}, a coalition {e.Change}{(e.Realm.Length > 0 ? ": " + Realm(e.Realm) : "")} ({e.Members} realms)");
        });
        session.Subscribe<DiplomacyMissionChanged>(e =>
        {
            if (e.Change.Contains("exposed", StringComparison.Ordinal)) exposed++;
            if (e.Change == "started" && !verbose) return;
            Say($"{Day(e.Step)}: {Realm(e.Owner)} {(e.Change == "started" ? "sends" : "sent")} {e.Agent} on a {DipNames.Mission(dipSet, chain, dipSet.MissionById[e.Type].Index).ToLowerInvariant()} against {Realm(e.Target)}: {e.Change.Replace('_', ' ')}");
        });
        session.Subscribe<DiplomacyAgentChanged>(e => Say($"{Day(e.Step)}: {e.Agent} of {Realm(e.Owner)} is {e.Change}"));
        session.Subscribe<DiplomacyIntelChanged>(e => { if (verbose) Say($"{Day(e.Step)}: {Realm(e.Observer)} knows {Realm(e.Target)} at the level of {e.Level} (was {e.Before})"); });
        session.Subscribe<DiplomacyPoliticsChanged>(e => Say($"{Day(e.Step)}: {Realm(e.Realm)}: {e.Change.Replace('_', ' ')}, stability {e.Stability}"));
        session.Subscribe<DiplomacyGrantChanged>(e => Say($"{Day(e.Step)}: {Realm(e.Holder)} {(e.Change == "granted" ? "is granted" : e.Change == "used" ? "uses" : "loses")} {Cb(e.CasusBelli)} against {Realm(e.Target)}"));
        session.Subscribe<DiplomacyIncidentLogged>(e => { if (verbose) Say($"{Day(e.Step)}: incident {e.Kind.Replace('_', ' ')} {Realm(e.A)} {Realm(e.B)}"); });
        session.Subscribe<DiplomacyEffectIssued>(e =>
        {
            if (e.Kind == "shock") shocks++;
            else if (e.Kind == "signal") signals++;
            if (e.Kind == "shock") Say($"{Day(e.Step)}: the diplomacy asks the economy for {e.Name} on {Realm(e.Realm)}'s market at {e.Scale} per mille ({e.Origin})");
            else if (e.Kind == "signal" && verbose) Say($"{Day(e.Step)}: signal {e.Name} for {Realm(e.Realm)}{(e.Other.Length > 0 ? " and " + Realm(e.Other) : "")}");
        });
        session.Subscribe<EconomyShockChanged>(e => Say($"tick {e.Tick}: economy shock {e.Shock} on {e.Market} {(e.Started ? "began" : "ended")}"));
        session.Subscribe<EconomyRefused>(e => Say($"economy refused {e.RequestId}: {e.Code}"));
        session.Subscribe<CalendarDayChanged>(e =>
        {
            var day = e.DayNumber - system.MomentOf(start).Day.DayNumber + 1;
            if (day % 30 != 0) return;
            var rows = string.Join(" | ", dipSet.Realms.Select(r =>
            {
                var state = dipRuntime.Realms[r.Index];
                return r.Id + " stab " + state.Stability + " trust " + state.Trust / 1000 + " fear " + state.Fear / 1000 + " inf " + state.Infamy / 1000;
            }));
            Say($"day {day}: {rows} | treaties {dipRuntime.Treaties.Count} wars {dipRuntime.Wars.Count}");
        });
        session.Init();

        var tickMicroseconds = RuntimeClock.MicrosecondsPerSecond / TicksPerSecond;
        var ticks = days * (int)(ticksPerDay / 60);
        var checkpointTick = checkpointDay * (int)(ticksPerDay / 60) + 5;
        byte[]? checkpoint = null;
        var linesAtCheckpoint = 0;
        long checkpointComponentTick = 0;
        for (var tick = 1; tick <= ticks; tick++)
        {
            if (session.Update(tickMicroseconds) != 1) throw new InvalidOperationException("the session did not run tick " + tick + " (state " + session.State + "): " + string.Join("; ", reporter.Reports));
            if (tick >= checkpointTick && checkpoint is null && diplomacy.IsQuiet && diplomacyChannels.IsQuiet)
            {
                checkpoint = DiplomacyCheckpoint.Capture(diplomacy);
                linesAtCheckpoint = transcript.Lines.Count;
                checkpointComponentTick = diplomacy.Tick;
            }
        }
        if (reporter.Reports.Count > 0) throw new InvalidOperationException("the session reported: " + string.Join("; ", reporter.Reports));

        Say($"ended at tick {ticks}, day {dipRuntime.Step + 1}: {treaties} treaties signed, {breaches} broken, {wars} wars declared, {peaces} ended, {payments} tributes paid, {exposed} agents exposed, {coalitions} coalitions, {shocks} economy shocks asked, {signals} signals, {transcript.Lines.Count} events, {chancellor.Asked} requests by the chancellor");
        foreach (var realm in dipSet.Realms)
        {
            var state = dipRuntime.Realms[realm.Index];
            Say($"{Realm(realm.Id)}: stability {state.Stability} ({EnumNames.Name((StabilityBand)state.Band)}), trust {state.Trust / 1000}, fear {state.Fear / 1000}, honour {state.Honour / 1000}, infamy {state.Infamy / 1000}, generation {state.Generation}, " +
                $"{dipRuntime.Treaties.Count(t => t.First == realm.Index || t.Second == realm.Index)} treaties, treasury {economyRuntime.Markets[economySet.MarketById[realm.Market].Index].Treasury}");
        }
        var conservation = economyRuntime.Conservation();
        Say("money conserved: " + (conservation.All(c => c == 0) ? "yes" : "NO"));
        var digest = dipRuntime.StateDigest();
        Say($"diplomacy state {digest[..16]}, events {transcript.Hash[..16]}");

        // Replay everything after the checkpoint on a fresh session and compare.
        var entries = DiplomacyJournal.Parse(journal.ToText());
        var resumed = DiplomacyCheckpoint.Open(dipSet, checkpoint!);
        var replay = DiplomacyReplay.Run(resumed, entries, checkpointComponentTick, ticks, TicksPerSecond);
        var liveTail = Digest.Of(string.Join('\n', transcript.Lines.Skip(linesAtCheckpoint)));
        var same = replay.StateDigest == digest && replay.TranscriptHash == liveTail;
        Say($"replayed ticks {checkpointComponentTick + 1} to {ticks} from the checkpoint at tick {checkpointComponentTick}: state {(replay.StateDigest == digest ? "same" : "DIFFERENT")}, events {(replay.TranscriptHash == liveTail ? "same" : "DIFFERENT")} ({replay.Events} events)");
        Say("output-sha256 " + Digest.Of(printed.ToString()));
        return same ? Ok : Diverged;
    }
}

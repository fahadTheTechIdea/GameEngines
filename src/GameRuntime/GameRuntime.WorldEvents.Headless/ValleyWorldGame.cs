using System.Globalization;
using System.Text;
using CalendarEngine;
using EconomyEngine;
using EngineCommon;
using WorldEventsEngine;

namespace GameRuntime.WorldEvents.Headless;

/// <summary>
/// A headless game that is a year in three towns: Riverton, Hillfort and Seaport, on a real calendar (the Gregorian sample, 2024) whose seasons are the world's seasons, with the economy of the three towns beside them.
/// The world makes the weather, droughts, floods, storms, fires, quakes, two diseases and the cards the director draws; its shock requests reach the economy through its own shock path, and the weather and the plague it
/// made reach the markets as host facts. The player sends a hospital when a plague breaks out. The run is recorded in a journal, a checkpoint is taken part way and the rest is replayed from it on a fresh session: the
/// replay must end in the same state and raise the same events as the live run. The printed output is hashed; two runs print the same hash.
/// </summary>
public static class ValleyWorldGame
{
    public const int Ok = 0, Refused = 2, Diverged = 3;

    public const int TicksPerSecond = 50;
    private const string Usage = "usage: GameRuntime.WorldEvents.Headless [--days <30-3000>] [--storyteller <id>] [--difficulty <id>] [--checkpoint-day <day>] [--verbose]";

    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    private static string Resource(string name)
    {
        using var stream = typeof(ValleyWorldGame).Assembly.GetManifestResourceStream(name);
        if (stream is null) throw new InvalidOperationException("the program does not carry " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        int days = 365, checkpointDay = 120;
        string storyteller = "", difficulty = "";
        var verbose = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--days" when i + 1 < args.Count && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out days) && days is >= 30 and <= 3000: i++; break;
                case "--storyteller" when i + 1 < args.Count: storyteller = args[++i]; break;
                case "--difficulty" when i + 1 < args.Count: difficulty = args[++i]; break;
                case "--checkpoint-day" when i + 1 < args.Count && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out checkpointDay) && checkpointDay >= 1: i++; break;
                case "--verbose": verbose = true; break;
                case "--help": output.WriteLine(Usage); return Ok;
                default: error.WriteLine("unknown or incomplete option '" + args[i] + "'"); error.WriteLine(Usage); return Refused;
            }
        }
        if (checkpointDay >= days) { error.WriteLine("--checkpoint-day must be before --days"); error.WriteLine(Usage); return Refused; }
        try { return Play(days, checkpointDay, storyteller, difficulty, verbose, output); }
        catch (EngineRefusedException exception)
        {
            error.WriteLine(exception.Code + ": " + exception.Message);
            return Refused;
        }
    }

    private static int Play(int days, int checkpointDay, string storyteller, string difficulty, bool verbose, TextWriter output)
    {
        var calendarSet = CalendarLoader.Load(Resource("gregorian.calendar.json"));
        var economySet = EconomyLoader.Load(Resource("town.economy.json"));
        var worldSet = WorldLoader.Load(Resource("valley.world.json"));
        var chain = LocaleChain.Create("en", [LocaleTable.Load(Resource("valley.world.en.json"))], "en");
        EconomyCalendar.Check(economySet, calendarSet);
        WorldCalendar.Check(worldSet, calendarSet);
        var system = calendarSet.System;
        var ticksPerDay = calendarSet.Time.TicksPerDay;
        var start = system.InstantOf(new CalendarDate(2024, system.Months.First(m => m.Id == "january").Index, 1), 0, 0);
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(TicksPerSecond, 8), reporter);
        var calendarChannels = new CalendarChannels();
        calendarChannels.Install(session);
        var economyChannels = new EconomyChannels();
        economyChannels.Install(session);
        var worldChannels = new WorldChannels();
        worldChannels.Install(session);
        var host = new ValleyHost();
        var worldJournal = new WorldJournal();
        var worldRuntime = WorldRuntime.Create(worldSet);
        if (storyteller.Length > 0) worldRuntime.UseStoryteller(storyteller);
        if (difficulty.Length > 0) worldRuntime.UseDifficulty(difficulty);
        host.World = worldRuntime;
        var calendarRuntime = CalendarRuntime.Create(calendarSet, start, 60, 1);   // one calendar hour per game tick: 24 ticks a day, the economy's own step
        var calendar = new CalendarComponent(calendarRuntime, host.CalendarHost, calendarChannels);
        var economyRuntime = EconomyRuntime.Create(economySet);
        var economy = new EconomyComponent(economyRuntime, EconomyCalendar.Clock(calendar, economySet, start), EconomyCalendar.HostFacts(calendar), economyChannels);
        var world = new WorldComponent(worldRuntime, WorldCalendar.Clock(calendar, worldSet, start), WorldCalendar.HostFacts(calendar, host.Game), worldChannels, journal: worldJournal);
        var shocks = new WorldEconomyShocks("world-shocks", worldSet, economySet);
        var player = new ValleyPlayer(host);
        session.AddComponent(calendar, 10);
        session.AddComponent(player, 15);
        session.AddComponent(world, 20);
        session.AddComponent(shocks, 25);
        session.AddComponent(economy, 30);
        var transcript = new WorldTranscript(session);

        var printed = new StringBuilder();
        void Say(string line) { printed.Append(line).Append('\n'); output.WriteLine(line); }
        string Day(long step) => "day " + (step + 1);
        string RegionName(string id) => WorldNames.Region(worldSet, chain, worldSet.RegionById[id].Index);
        string HazardName(string id) => WorldNames.Hazard(worldSet, chain, worldSet.HazardById[id].Index);
        int hazardStarts = 0, outbreaks = 0, cards = 0, shocksAsked = 0, signals = 0, weatherChanges = 0;
        session.Subscribe<WorldRefused>(e => Say($"refused {e.RequestId} at tick {e.Tick}: {e.Code}"));
        session.Subscribe<WorldSeasonChanged>(e => Say($"{Day(e.Step)}: {WorldNames.Season(worldSet, chain, worldSet.SeasonById[e.Season].Index)} begins"));
        session.Subscribe<WorldRegimeChanged>(e => { if (verbose) Say($"{Day(e.Step)}: the {WorldNames.Regime(worldSet, chain, worldSet.Regimes.First(r => r.Id == e.Regime).Index).ToLowerInvariant()} regime sets in"); });
        session.Subscribe<WorldWeatherChanged>(e =>
        {
            weatherChanges++;
            if (verbose) Say($"{Day(e.Step)}: {RegionName(e.Region)} turns {WorldNames.Weather(worldSet, chain, worldSet.WeatherById[e.Weather].Index).ToLowerInvariant()}, {e.Temp / 100}.{Math.Abs(e.Temp % 100):00} degrees");
        });
        session.Subscribe<WorldHazardChanged>(e =>
        {
            if (e.Change == "started") hazardStarts++;
            var rung = WorldNames.Rung(worldSet, chain, worldSet.HazardById[e.Hazard].Index, worldSet.HazardById[e.Hazard].Ladder.First(r => r.Id == e.Severity).Index);
            if (e.Change is "started" or "escalated" || verbose) Say($"{Day(e.Step)}: {HazardName(e.Hazard)} ({rung.ToLowerInvariant()}) {e.Change} in {RegionName(e.Region)}, strength {e.Power}");
            else if (e.Change == "ended") Say($"{Day(e.Step)}: {HazardName(e.Hazard)} ends in {RegionName(e.Region)}");
        });
        session.Subscribe<WorldOutbreakChanged>(e =>
        {
            if (e.Change == "outbreak") outbreaks++;
            if (e.Change != "ended" || verbose) Say($"{Day(e.Step)}: {WorldNames.Disease(worldSet, chain, worldSet.DiseaseById[e.Disease].Index)} {e.Change} in {RegionName(e.Region)}: {e.Infected} ill ({e.Permille} per mille)");
        });
        session.Subscribe<WorldCardPlayed>(e =>
        {
            cards++;
            Say($"{Day(e.Step)}: {WorldNames.Card(worldSet, chain, worldSet.CardById[e.Card].Index)}{(e.Region.Length > 0 ? " in " + RegionName(e.Region) : "")} ({e.Tier}, {e.Reason}), stress {e.Stress}");
        });
        session.Subscribe<WorldDirectorChanged>(e => Say($"{Day(e.Step)}: the director goes from {e.From} to {e.To} at stress {e.Stress}"));
        session.Subscribe<WorldEffectIssued>(e =>
        {
            if (e.Kind == "shock") { shocksAsked++; Say($"{Day(e.Step)}: the world asks the economy for {e.Name}{(e.Market.Length > 0 ? " in " + e.Market : "")} at {e.Scale} per mille ({e.Origin})"); }
            else { signals++; if (verbose) Say($"{Day(e.Step)}: signal {e.Name}{(e.Region.Length > 0 ? " in " + e.Region : "")} ({e.Origin})"); }
        });
        session.Subscribe<WorldInterventionChanged>(e => Say($"{Day(e.Step)}: {WorldNames.Intervention(worldSet, chain, worldSet.InterventionById[e.Intervention].Index)} {(e.On ? "starts" : "stops")} in {RegionName(e.Region)}{(e.Automatic ? " (by itself)" : "")}"));
        session.Subscribe<WorldWeatherAnswered>(e => Say($"weather in {RegionName(e.Region)}: {e.Weather}, {e.Temp / 100}.{Math.Abs(e.Temp % 100):00} degrees, soil {e.Soil} per mille, river {e.River}"));
        session.Subscribe<EconomyShockChanged>(e => Say($"tick {e.Tick}: economy shock {e.Shock}{(e.Market.Length > 0 ? " on " + e.Market : "")} {(e.Started ? "began" : "ended")}"));
        session.Subscribe<EconomyRefused>(e => Say($"economy refused {e.RequestId}: {e.Code}"));
        session.Subscribe<CalendarDayChanged>(e =>
        {
            var day = e.DayNumber - system.MomentOf(start).Day.DayNumber + 1;
            if (day % 30 != 0) return;
            var rows = string.Join(" | ", worldSet.Regions.Select(r => r.Id + " " + worldSet.Weathers[worldRuntime.Regions[r.Index].Weather].Id + " " + worldRuntime.Regions[r.Index].Temp / 100 + "C soil " + worldRuntime.SoilPermille(r.Index) +
                " grain " + economyRuntime.Ratio(economySet.MarketById[r.Market].Index, economySet.MarketById[r.Market].Pairs.First(p => economySet.Goods[p.Good].Id == "grain").Index)));
            Say($"day {day}: {rows} | stress {worldRuntime.Stress} {Words.Name(worldRuntime.Phase)}");
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
            if (tick >= checkpointTick && checkpoint is null && world.IsQuiet && worldChannels.IsQuiet)
            {
                checkpoint = WorldCheckpoint.Capture(world);
                linesAtCheckpoint = transcript.Lines.Count;
                checkpointComponentTick = world.Tick;
            }
        }
        if (reporter.Reports.Count > 0) throw new InvalidOperationException("the session reported: " + string.Join("; ", reporter.Reports));

        Say($"ended at tick {ticks}, day {worldRuntime.Step + 1}: {hazardStarts} hazards started, {outbreaks} outbreaks, {cards} cards, {shocksAsked} economy shocks asked, {signals} signals, {weatherChanges} weather changes, {transcript.Lines.Count} events, {player.Asked} requests by the player");
        Say($"director {worldSet.Storytellers[worldRuntime.Storyteller].Id} on {worldSet.Difficulties[worldRuntime.Difficulty].Id}: {worldRuntime.Played} cards played, {worldRuntime.Suppressed} paced onsets held back, {worldRuntime.Dropped} dropped");
        foreach (var region in worldSet.Regions)
        {
            var state = worldRuntime.Regions[region.Index];
            var deaths = string.Join(", ", worldSet.Diseases.Select(d => d.Id + " " + state.Dead[d.Index] + " dead (peak " + state.Peak[d.Index] + " ill)"));
            Say($"{region.Id}: population {region.Population}, {deaths}");
        }
        var conservation = economyRuntime.Conservation();
        Say("money conserved: " + (conservation.All(c => c == 0) ? "yes" : "NO"));
        var digest = worldRuntime.StateDigest();
        Say($"world state {digest[..16]}, events {transcript.Hash[..16]}");

        // Replay everything after the checkpoint on a fresh session and compare.
        var entries = WorldJournal.Parse(worldJournal.ToText());
        var resumed = WorldCheckpoint.Open(worldSet, checkpoint!);
        var replay = WorldReplay.Run(resumed, entries, checkpointComponentTick, ticks, TicksPerSecond);
        var liveTail = Digest.Of(string.Join('\n', transcript.Lines.Skip(linesAtCheckpoint)));
        var same = replay.StateDigest == digest && replay.TranscriptHash == liveTail;
        Say($"replayed ticks {checkpointComponentTick + 1} to {ticks} from the checkpoint at tick {checkpointComponentTick}: state {(replay.StateDigest == digest ? "same" : "DIFFERENT")}, events {(replay.TranscriptHash == liveTail ? "same" : "DIFFERENT")} ({replay.Events} events)");
        Say("output-sha256 " + Digest.Of(printed.ToString()));
        return same ? Ok : Diverged;
    }
}

using System.Globalization;
using System.Text;
using CalendarEngine;
using EconomyEngine;
using EngineCommon;

namespace GameRuntime.Economy.Headless;

/// <summary>
/// A headless game that is three towns: Riverton and Hillfort trade in crowns, Seaport in florins, on a real calendar (the Gregorian sample) whose seasons move demand and supply and whose 15th is the tax decree.
/// Merchants carry goods along five routes where the prices differ, a caravan buys and sells now and then, and the story brings a drought, a central bank that prints, a devaluation, a loan and a blockade.
/// The run is recorded in a journal, a checkpoint is taken part way and the rest is replayed from it on a fresh session: the replay must end in the same state and raise the same events as the live run. The
/// printed output is hashed; two runs print the same hash.
/// </summary>
public static class TownHeadlessGame
{
    public const int Ok = 0, Refused = 2, Diverged = 3;

    public const int TicksPerSecond = 50;
    private const string Usage = "usage: GameRuntime.Economy.Headless [--days <1-3000>] [--seed <number>] [--checkpoint-day <day>] [--verbose]";

    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    /// <summary>What a production line's new limit means, in words; the limits are the names of <c>LineLimit</c> in lower case.</summary>
    private static string LimitSaying(string limit, string good)
    {
        switch (limit)
        {
            case "capacity": return "runs at capacity";
            case "input": return "is short of " + good;
            case "room": return "has no room for " + good;
            case "locked": return "is locked";
            case "held": return "is held for want of " + good;
            case "margin": return "is throttled: its outputs earn little over the cost of a run";
            case "worn": return "is held short by the wear of " + good;
        }
        throw new InvalidOperationException("unhandled production limit '" + limit + "'");
    }

    private static string Resource(string name)
    {
        using var stream = typeof(TownHeadlessGame).Assembly.GetManifestResourceStream(name);
        if (stream is null) throw new InvalidOperationException("the program does not carry " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        int days = 120, checkpointDay = 40;
        ulong seed = 5;
        var verbose = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--days" when i + 1 < args.Count && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out days) && days is >= 1 and <= 3000: i++; break;
                case "--seed" when i + 1 < args.Count && ulong.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out seed): i++; break;
                case "--checkpoint-day" when i + 1 < args.Count && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out checkpointDay) && checkpointDay >= 1: i++; break;
                case "--verbose": verbose = true; break;
                case "--help": output.WriteLine(Usage); return Ok;
                default: error.WriteLine("unknown or incomplete option '" + args[i] + "'"); error.WriteLine(Usage); return Refused;
            }
        }
        if (checkpointDay >= days) { error.WriteLine("--checkpoint-day must be before --days"); error.WriteLine(Usage); return Refused; }
        try { return Play(days, seed, checkpointDay, verbose, output); }
        catch (EngineRefusedException exception)
        {
            error.WriteLine(exception.Code + ": " + exception.Message);
            return Refused;
        }
    }

    private static int Play(int days, ulong seed, int checkpointDay, bool verbose, TextWriter output)
    {
        var calendarSet = CalendarLoader.Load(Resource("gregorian.calendar.json"));
        var economySet = EconomyLoader.Load(Resource("town.economy.json"));
        EconomyCalendar.Check(economySet, calendarSet);
        var system = calendarSet.System;
        var ticksPerDay = calendarSet.Time.TicksPerDay;
        var start = system.InstantOf(new CalendarDate(2024, system.Months.First(m => m.Id == "january").Index, 1), 0, 0);
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(TicksPerSecond, 8), reporter);
        var calendarChannels = new CalendarChannels();
        calendarChannels.Install(session);
        var economyChannels = new EconomyChannels();
        economyChannels.Install(session);
        var journal = new EconomyJournal();
        var calendarRuntime = CalendarRuntime.Create(calendarSet, start, 1, 1);   // one calendar minute per game tick
        var economyRuntime = EconomyRuntime.Create(economySet);
        // The director needs the components and the calendar needs the director's weather: the calendar is built first with a delegate that reads the director once it exists.
        TownDirector? director = null;
        var calendar = new CalendarComponent(calendarRuntime, () => director!.Weather, calendarChannels);
        var economy = new EconomyComponent(economyRuntime, EconomyCalendar.Clock(calendar, economySet, start), EconomyCalendar.HostFacts(calendar), economyChannels, journal: journal);
        director = new TownDirector(seed);
        session.AddComponent(calendar, 10);
        session.AddComponent(director, 15);
        session.AddComponent(new EconomyCalendarDates("decree-dates", economySet, [new EconomyDateRule("day:15", "decree")]), 20);
        session.AddComponent(economy, 30);
        var transcript = new EconomyTranscript(session);

        var printed = new StringBuilder();
        void Say(string line) { printed.Append(line).Append('\n'); output.WriteLine(line); }
        int trades = 0, launches = 0, arrivals = 0, bands = 0, shortages = 0, settled = 0, blocked = 0;
        long paid = 0;
        session.Subscribe<EconomyTradeExecuted>(e =>
        {
            trades++;
            Say($"tick {e.Tick}: {e.Trader} {(e.Quantity > 0 ? "bought" : "sold")} {Math.Abs(e.Quantity)} {e.Good} in {e.Market} at {e.UnitPrice} milli {e.Currency}: {(e.Quantity > 0 ? "paid" : "received")} {e.Total} (tax {e.Tax})");
        });
        session.Subscribe<EconomyRefused>(e => Say($"refused {e.RequestId} at tick {e.Tick}: {e.Code}"));
        session.Subscribe<EconomyShockChanged>(e => Say($"tick {e.Tick}: shock {e.Shock}{(e.Market.Length > 0 ? " on " + e.Market : "")} {(e.Started ? "began" : "ended")}"));
        session.Subscribe<EconomyRateChanged>(e => { if (verbose) Say($"tick {e.Tick}: {e.Currency} rate {e.From} -> {e.To}"); });
        session.Subscribe<EconomyShipmentLaunched>(e => { launches++; if (verbose) Say($"tick {e.Tick}: {e.Quantity} {e.Good} left by {e.Route}, due period {e.Arrives}"); });
        session.Subscribe<EconomyShipmentArrived>(e => { arrivals++; if (verbose) Say($"tick {e.Tick}: {e.Quantity} {e.Good} arrived by {e.Route}"); });
        session.Subscribe<EconomyBandChanged>(e => { bands++; if (verbose) Say($"tick {e.Tick}: {e.Market} {e.Good} {e.From} -> {e.To}"); });
        session.Subscribe<EconomyShortageChanged>(e => { if (e.Entered) shortages++; if (verbose) Say($"tick {e.Tick}: {e.Market} {e.Good} {(e.Entered ? "is short" : "is supplied again")}"); });
        session.Subscribe<EconomyRouteBlocked>(e => blocked++);
        session.Subscribe<EconomySettled>(e =>
        {
            settled++;
            paid += e.Paid;
            if (e.Kind is "tax" or "loan" || e.Paid < e.Due) Say($"tick {e.Tick}: {e.Market} {e.Kind} {e.Name}: {e.Paid} of {e.Due}");
        });
        session.Subscribe<EconomyLoanChanged>(e => Say($"tick {e.Tick}: loan {e.Loan} {e.Outcome}, balance {e.Balance}"));
        session.Subscribe<EconomyProductionLimited>(e => Say($"tick {e.Tick}: {e.Market} {e.Recipe} {LimitSaying(e.To, e.Good)} (was {e.From})"));
        session.Subscribe<EconomyRecipeUnlocked>(e => Say($"tick {e.Tick}: recipe {e.Recipe} was discovered"));
        session.Subscribe<CalendarDayChanged>(e =>
        {
            var rows = string.Join(" | ", economySet.Markets.Select(m => m.Id + " " + string.Join(' ', m.Pairs.Select(p => economySet.Goods[p.Good].Id[..2] + "=" + economyRuntime.Ratio(m.Index, p.Index)))));
            Say($"day {e.DayNumber - system.MomentOf(start).Day.DayNumber + 1} ({e.Weekday}): {rows} | trades {trades} shipments {launches}/{arrivals} bands {bands} short {shortages} settled {settled} blocked {blocked}");
            trades = launches = arrivals = bands = shortages = settled = blocked = 0;
        });
        session.Init();

        var tickMicroseconds = RuntimeClock.MicrosecondsPerSecond / TicksPerSecond;
        var lastTick = days * ticksPerDay;
        var checkpointTick = checkpointDay * ticksPerDay + 30;
        byte[]? checkpoint = null;
        var linesAtCheckpoint = 0;
        long checkpointComponentTick = 0;
        for (var tick = 1; tick <= lastTick; tick++)
        {
            if (session.Update(tickMicroseconds) != 1) throw new InvalidOperationException("the session did not run tick " + tick + " (state " + session.State + "): " + string.Join("; ", reporter.Reports));
            if (tick == checkpointTick)
            {
                checkpoint = EconomyCheckpoint.Capture(economy);
                linesAtCheckpoint = transcript.Lines.Count;
                checkpointComponentTick = economy.Tick;
            }
        }
        if (reporter.Reports.Count > 0) throw new InvalidOperationException("the session reported: " + string.Join("; ", reporter.Reports));

        Say($"ended at tick {lastTick}: period {economyRuntime.Period}, {economySet.Markets.Count} towns, {economySet.PairCount} prices, {economyRuntime.Shipments.Count} shipments on the way, {transcript.Lines.Count} events, {director.Requested} requests by the director");
        foreach (var currency in economySet.Currencies)
        {
            var state = economyRuntime.Currencies[currency.Index];
            Say($"currency {currency.Id}: rate {state.Rate} level {state.Level} money {state.Money} world {state.World}");
        }
        foreach (var market in economySet.Markets)
        {
            var state = economyRuntime.Markets[market.Index];
            Say($"{market.Id}: treasury {state.Treasury}{(economyRuntime.InArrears(market.Index) ? " (in arrears)" : "")}");
            foreach (var pair in market.Pairs)
            {
                var p = state.Pairs[pair.Index];
                Say($"  {economySet.Goods[pair.Good].Id}: price {p.Price} ratio {economyRuntime.Ratio(market.Index, pair.Index)} {economySet.Bands[p.Band].Id} stock {p.Stock}{(p.Short ? " short" : "")}");
            }
            for (var l = 0; l < market.Lines.Count; l++)
                Say($"  line {economySet.Recipes[market.Lines[l].Recipe].Id}: {state.Lines[l].LastRuns} runs last period ({state.Lines[l].Limit.ToString().ToLowerInvariant()}), {economyRuntime.Batches.Count(b => b.Market == market.Index && b.Recipe == market.Lines[l].Recipe)} batches on their way");
        }
        foreach (var loan in economyRuntime.Loans) Say($"loan {loan.Id}: {loan.Status.ToString().ToLowerInvariant()} balance {loan.Balance} remaining {loan.Remaining}");
        var conservation = economyRuntime.Conservation();
        Say("money conserved: " + (conservation.All(c => c == 0) ? "yes" : "NO"));
        var digest = economyRuntime.StateDigest();
        Say($"economy state {digest[..16]}, events {transcript.Hash[..16]}");

        // Replay everything after the checkpoint on a fresh session and compare.
        var entries = EconomyJournal.Parse(journal.ToText());
        var resumed = EconomyCheckpoint.Open(economySet, checkpoint!);
        var replay = EconomyReplay.Run(resumed, entries, checkpointComponentTick, lastTick, TicksPerSecond);
        var liveTail = Digest.Of(string.Join('\n', transcript.Lines.Skip(linesAtCheckpoint)));
        var same = replay.StateDigest == digest && replay.TranscriptHash == liveTail;
        Say($"replayed ticks {checkpointComponentTick + 1} to {lastTick} from the checkpoint at tick {checkpointComponentTick}: state {(replay.StateDigest == digest ? "same" : "DIFFERENT")}, events {(replay.TranscriptHash == liveTail ? "same" : "DIFFERENT")} ({replay.Events} events)");
        Say("output-sha256 " + Digest.Of(printed.ToString()));
        return same ? Ok : Diverged;
    }
}

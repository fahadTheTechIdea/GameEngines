using System.Globalization;
using System.Text;
using CalendarEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime.Calendar.Headless;

/// <summary>
/// A headless game that is only a calendar: five villagers go about a year of 28-day seasons, their schedules changing with the weekday, the weather, the season and the festivals; a storm sends one to shelter;
/// the player sleeps through a night. The run is recorded in a journal, a checkpoint is taken part way and then the rest is replayed from it on a fresh session: the replay must end in the same state and raise the
/// same events as the live run. The printed output is hashed; two runs print the same hash.
/// </summary>
public static class CalendarHeadlessGame
{
    public const int Ok = 0, Refused = 2, Diverged = 3;

    public const int TicksPerSecond = 50;
    private const string Usage = "usage: GameRuntime.Calendar.Headless [--days <1-3000>] [--seed <number>] [--checkpoint-day <day>] [--verbose]";

    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    private static string Resource(string name)
    {
        using var stream = typeof(CalendarHeadlessGame).Assembly.GetManifestResourceStream(name);
        if (stream is null) throw new InvalidOperationException("the program does not carry " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        int days = 120, checkpointDay = 50;
        ulong seed = 7;
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
        var set = CalendarLoader.Load(Resource("valley.calendar.json"));
        var system = set.System;
        var ticksPerDay = set.Time.TicksPerDay;
        var start = system.InstantOf(new CalendarDate(1, 0, 1), 6, 0);   // the first morning of spring, six o'clock
        var runtime = CalendarRuntime.Create(set, start, 1, 1);           // one calendar minute per game tick
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(TicksPerSecond, 8), reporter);
        var channels = new CalendarChannels();
        channels.Install(session);
        var journal = new CalendarJournal();
        var director = new TownDirector(seed, stormTick: 2 * ticksPerDay + 6 * 60, sleepTick: 4 * ticksPerDay + 16 * 60);
        var component = new CalendarComponent(runtime, () => director.Weather, channels, journal: journal);
        session.AddComponent(component, 10);
        session.AddComponent(director, 20);
        var transcript = new CalendarTranscript(session);

        var printed = new StringBuilder();
        void Say(string line) { printed.Append(line).Append('\n'); output.WriteLine(line); }
        string Date(long year, string month, int day) => $"year {year} {month} {day}";

        var moves = 0;
        var where = new SortedDictionary<string, string>(StringComparer.Ordinal);
        session.Subscribe<CalendarScheduleBlockChanged>(e =>
        {
            moves++;
            where[e.Actor] = e.Location;
            if (verbose || e.Tick <= 2 * ticksPerDay) Say($"tick {e.Tick}: {e.Actor} -> {e.Location} ({e.Activity}) by {e.Origin} {e.Routine}" + (e.Block.Length > 0 ? "/" + e.Block : ""));
        });
        session.Subscribe<CalendarDayChanged>(e => { Say($"day {e.DayNumber + 1}: {Date(e.Year, e.Month, e.Day)} {(e.Weekday.Length == 0 ? "-" : e.Weekday)} moves-yesterday {moves} at-midnight {string.Join(' ', where.Select(w => w.Key + "@" + w.Value))}"); moves = 0; });
        session.Subscribe<CalendarSeasonChanged>(e => Say($"season: {e.From} -> {e.To} on {Date(e.Year, e.Month, e.Day)}"));
        session.Subscribe<CalendarHolidayStarted>(e => Say($"holiday: {e.Holiday} on {Date(e.Year, e.Month, e.Day)}"));
        session.Subscribe<CalendarApplied>(e => Say($"applied {e.RequestId} at tick {e.Tick}"));
        session.Subscribe<CalendarRefused>(e => Say($"refused {e.RequestId} at tick {e.Tick}: {e.Code}"));
        session.Subscribe<CalendarWhereAnswered>(e => Say($"where {e.Actor} at tick {e.Tick}: {e.Location} ({e.Activity}) by {e.Origin} {e.Routine}"));
        session.Init();

        var tickMicroseconds = RuntimeClock.MicrosecondsPerSecond / TicksPerSecond;
        var lastTick = days * ticksPerDay;   // one tick is one calendar minute, so a day is 1,440 ticks (the skip adds to the calendar, not to the ticks)
        var checkpointTick = checkpointDay * ticksPerDay;
        byte[]? checkpoint = null;
        var linesAtCheckpoint = 0;
        for (var tick = 1; tick <= lastTick; tick++)
        {
            if (session.Update(tickMicroseconds) != 1) throw new InvalidOperationException("the session did not run tick " + tick + " (state " + session.State + "): " + string.Join("; ", reporter.Reports));
            if (tick == checkpointTick)
            {
                checkpoint = CalendarCheckpoint.Capture(component);
                linesAtCheckpoint = transcript.Lines.Count;
            }
        }
        if (reporter.Reports.Count > 0) throw new InvalidOperationException("the session reported: " + string.Join("; ", reporter.Reports));

        var finalMoment = system.MomentOf(runtime.Instant);
        var digest = CalendarSave.StateDigest(runtime);
        Say($"ended at tick {lastTick}: {Date(finalMoment.Day.Year, system.Months[finalMoment.Day.Month].Id, finalMoment.Day.Day)} {finalMoment.Hour:00}:{finalMoment.Minute:00}, {runtime.Actors.Count} villagers, {transcript.Lines.Count} events");
        Say($"calendar state {digest[..16]}, events {transcript.Hash[..16]}");

        // Replay everything after the checkpoint on a fresh session and compare.
        var entries = CalendarJournal.Parse(journal.ToText());   // the whole journal: the replay skips what happened before the checkpoint, apart from the host facts in force at it
        var resumed = CalendarCheckpoint.Open(set, checkpoint!);
        var replay = CalendarReplay.Run(resumed, entries, lastTick, TicksPerSecond);
        var liveTail = Digest.Of(string.Join('\n', transcript.Lines.Skip(linesAtCheckpoint)));
        var same = replay.StateDigest == digest && replay.TranscriptHash == liveTail;
        Say($"replayed ticks {checkpointTick + 1} to {lastTick} from the checkpoint at tick {checkpointTick}: state {(replay.StateDigest == digest ? "same" : "DIFFERENT")}, events {(replay.TranscriptHash == liveTail ? "same" : "DIFFERENT")} ({replay.Events} events)");
        Say("output-sha256 " + Digest.Of(printed.ToString()));
        return same ? Ok : Diverged;
    }
}

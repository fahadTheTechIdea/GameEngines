using System.Globalization;
using System.Text;
using CalendarEngine;
using EngineCommon;
using RelationshipEngine;

namespace GameRuntime.Relationship.Headless;

/// <summary>
/// A headless game that is a village: six people on a real calendar (the Gregorian sample) go through their days, their schedules (eating, sleeping, working, the Sunday festival) feeding their needs, their moods
/// rising and falling, and when they meet they chat, give gifts, insult, steal, share meals and flirt, as the relationship engine says they want to and may; witnesses remember, and news travels by gossip. The run
/// is recorded in a journal, a checkpoint is taken part way and the rest is replayed from it on a fresh session: the replay must end in the same state and raise the same events as the live run. The printed
/// output is hashed; two runs print the same hash.
/// </summary>
public static class VillageHeadlessGame
{
    public const int Ok = 0, Refused = 2, Diverged = 3;

    public const int TicksPerSecond = 50;
    private const string Usage = "usage: GameRuntime.Relationship.Headless [--days <1-3000>] [--seed <number>] [--checkpoint-day <day>] [--verbose]";

    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    private static string Resource(string name)
    {
        using var stream = typeof(VillageHeadlessGame).Assembly.GetManifestResourceStream(name);
        if (stream is null) throw new InvalidOperationException("the program does not carry " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        int days = 60, checkpointDay = 20;
        ulong seed = 11;
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
        var relationshipSet = RelationshipLoader.Load(Resource("village.relationship.json"));
        RelationshipCalendar.Check(relationshipSet, calendarSet);
        var system = calendarSet.System;
        var ticksPerDay = calendarSet.Time.TicksPerDay;
        var start = system.InstantOf(new CalendarDate(2024, system.Months.First(m => m.Id == "january").Index, 1), 0, 0);
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(TicksPerSecond, 8), reporter);
        var calendarChannels = new CalendarChannels();
        calendarChannels.Install(session);
        var relationshipChannels = new RelationshipChannels();
        relationshipChannels.Install(session);
        var journal = new RelationshipJournal();
        var calendarRuntime = CalendarRuntime.Create(calendarSet, start, 1, 1);   // one calendar minute per game tick
        var relationshipRuntime = RelationshipRuntime.Create(relationshipSet);
        // The director needs both components and the components need the director's weather: the calendar is built first with a delegate that reads the director once it exists.
        VillageDirector? director = null;
        var calendar = new CalendarComponent(calendarRuntime, () => director!.Weather, calendarChannels);
        var relationships = new RelationshipComponent(relationshipRuntime, RelationshipCalendar.Clock(calendar, relationshipSet, start), RelationshipCalendar.HostFacts(calendar), relationshipChannels, journal: journal);
        director = new VillageDirector(seed, relationships, calendar);
        session.AddComponent(calendar, 10);
        session.AddComponent(director, 15);
        session.AddComponent(new RelationshipCalendarActivities("activities", relationships), 20);
        session.AddComponent(relationships, 30);
        var transcript = new RelationshipTranscript(session);

        var printed = new StringBuilder();
        void Say(string line) { printed.Append(line).Append('\n'); output.WriteLine(line); }
        var set = relationshipSet;
        int accepted = 0, refused = 0, moodChanges = 0, criticals = 0, gossip = 0, evictions = 0;
        session.Subscribe<RelationshipInteractionPerformed>(e =>
        {
            if (e.Accepted) accepted++; else refused++;
            if (verbose) Say($"tick {e.Tick}: {e.Actor} {e.Interaction} {e.Target}: {(e.Accepted ? "accepted" : "refused")} (score {e.Score}, noise {e.Noise})");
        });
        session.Subscribe<RelationshipRefused>(e => Say($"refused {e.RequestId} at tick {e.Tick}: {e.Code}"));
        session.Subscribe<RelationshipMoodChanged>(e => { moodChanges++; if (verbose) Say($"tick {e.Tick}: {e.Actor} mood {e.From} -> {e.To} ({e.Mood})"); });
        session.Subscribe<RelationshipNeedCritical>(e => { if (e.Entered) criticals++; if (verbose) Say($"tick {e.Tick}: {e.Actor} {e.Need} {(e.Entered ? "critical" : "recovered")} at {e.Level}"); });
        session.Subscribe<RelationshipStateChanged>(e => Say($"tick {e.Tick}: {e.Actor} about {e.Target}: {e.Ladder} {e.From} -> {e.To}"));
        session.Subscribe<RelationshipBondChanged>(e => Say($"tick {e.Tick}: {e.Actor} and {e.Target}: {e.Bond} {(e.Set ? "set" : "cleared")}"));
        session.Subscribe<RelationshipMemoryAdded>(e =>
        {
            if (e.Origin.Length == 0) return;
            gossip++;
            Say($"tick {e.Tick}: {e.Holder} heard from {e.Origin} about {e.Subject}: {e.Memory}");
        });
        session.Subscribe<RelationshipEvicted>(e => { evictions++; Say($"tick {e.Tick}: {e.Kind} of {e.Actor} dropped: {e.What}"); });
        session.Subscribe<RelationshipCommunityChanged>(e => Say($"tick {e.Tick}: {e.Actor} joined community {e.To} (from {e.From})"));
        session.Subscribe<RelationshipStandingChanged>(e => Say($"tick {e.Tick}: {e.Actor} standing {e.From} -> {e.To}"));
        session.Subscribe<RelationshipBalanceShifted>(e => Say($"tick {e.Tick}: {e.Holder} about {e.Subject}: pressed {(e.Scale > 0 ? "toward liking" : "away from liking")} through {e.Via} ({e.HolderVia}, {e.ViaSubject})"));
        session.Subscribe<CalendarDayChanged>(e =>
        {
            var moods = string.Join(' ', relationshipRuntime.Actors.Values.Select(a => a.Id + "=" + set.Mood.States[a.MoodState].Id));
            Say($"day {e.DayNumber - system.MomentOf(start).Day.DayNumber + 1} ({e.Weekday}): {moods} | interactions {accepted}/{refused} mood-changes {moodChanges} critical {criticals} gossip {gossip}");
            accepted = refused = moodChanges = criticals = gossip = 0;
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
                checkpoint = RelationshipCheckpoint.Capture(relationships);
                linesAtCheckpoint = transcript.Lines.Count;
                checkpointComponentTick = relationships.Tick;
            }
        }
        if (reporter.Reports.Count > 0) throw new InvalidOperationException("the session reported: " + string.Join("; ", reporter.Reports));

        Say($"ended at tick {lastTick}: step {relationshipRuntime.Step}, {relationshipRuntime.Actors.Count} villagers, {relationshipRuntime.EdgeCount} opinions, {relationshipRuntime.Rolls} rolls, {transcript.Lines.Count} events, {director.Requested} interactions asked for");
        foreach (var actor in relationshipRuntime.Actors.Values)
        {
            Say($"{actor.Id}: mood {relationshipRuntime.MoodValue(actor)} {set.Mood.States[actor.MoodState].Id}, needs {string.Join(' ', set.Needs.Select(n => n.Id + "=" + relationshipRuntime.NeedLevel(actor, n.Index)))}, community {actor.Community} of {relationshipRuntime.Actors.Values.Count(a => a.Community == actor.Community)}, standing {actor.Standing}");
            foreach (var edge in actor.Edges.Values.OrderByDescending(e => Math.Abs(relationshipRuntime.Opinion(actor.Id, e.To, 0))).ThenBy(e => e.To, StringComparer.Ordinal).Take(3))
                Say($"  about {edge.To}: {string.Join(' ', set.Axes.Select(a => a.Id + "=" + relationshipRuntime.Opinion(actor.Id, edge.To, a.Index)))} {string.Join(' ', set.Ladders.Select(l => l.Id + "=" + l.Steps[edge.Ladder[l.Index]].Id))}{(edge.Bonds.Count > 0 ? " bonds " + string.Join(',', edge.Bonds.Select(b => set.Bonds[b].Id)) : "")}");
        }
        var digest = relationshipRuntime.StateDigest();
        Say($"relationship state {digest[..16]}, events {transcript.Hash[..16]}");

        // Replay everything after the checkpoint on a fresh session and compare.
        var entries = RelationshipJournal.Parse(journal.ToText());
        var resumed = RelationshipCheckpoint.Open(relationshipSet, checkpoint!);
        var replay = RelationshipReplay.Run(resumed, entries, checkpointComponentTick, lastTick, TicksPerSecond);
        var liveTail = Digest.Of(string.Join('\n', transcript.Lines.Skip(linesAtCheckpoint)));
        var same = replay.StateDigest == digest && replay.TranscriptHash == liveTail;
        Say($"replayed ticks {checkpointComponentTick + 1} to {lastTick} from the checkpoint at tick {checkpointComponentTick}: state {(replay.StateDigest == digest ? "same" : "DIFFERENT")}, events {(replay.TranscriptHash == liveTail ? "same" : "DIFFERENT")} ({replay.Events} events)");
        Say("output-sha256 " + Digest.Of(printed.ToString()));
        return same ? Ok : Diverged;
    }
}

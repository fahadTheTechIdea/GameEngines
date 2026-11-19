using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using CombatEngine;
using GameContent.Catalog;
using Microsoft.Extensions.Logging;
using ScenarioEngine.Contracts;
using ScenarioEngine.Diagnostics;

namespace GameRuntime.Field.Headless;

/// <summary>
/// The headless host of the field game: the engine's main loop around a RuntimeSession that holds the SimEngine world, the combat rules and
/// the commander. The host only reads how much time went by, hands it to the session and prints what the session published. After the
/// hashed output it prints wall-clock timings, which are not part of the hash because they change from run to run.
/// </summary>
public static class FieldHeadlessGame
{
    public const int Ok = 0, Faulted = 1, Refused = 2, CheckpointRefused = 3, JournalRefused = 4;

    private static readonly JournalCodec Codec = new();

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, IEngineReporter? reporter = null)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder
            .AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace).SetMinimumLevel(LogLevel.Warning));
        using var diagnostics = new ApplicationDiagnostics(loggerFactory.CreateLogger("GameRuntime.Field.Headless"), AuditPathFrom(args), new ConsoleNotificationSink());
        IEngineReporter report = reporter ?? diagnostics;

        // The outermost boundary of the host: every failure kind is reported through the one reporter and becomes an exit code.
        try
        {
            var options = FieldOptions.Parse(args);
            if (options.Help) { output.WriteLine(FieldOptions.Usage); return Ok; }
            var forces = FieldForceSource.Load(options);
            var runtimeReporter = new EngineReporterAdapter(report);
            return options.ReplayPath is null ? Play(options, forces, runtimeReporter, output) : Replay(options, forces, runtimeReporter, output);
        }
        catch (FieldUsageException exception)
        {
            Report(report, "FIELD_USAGE", exception);
            error.WriteLine(exception.Message);
            error.WriteLine(FieldOptions.Usage);
            return Refused;
        }
        catch (CatalogRefusedException exception) { return Fail(report, error, exception.Code, exception, Refused); }
        catch (CombatAdaptRefusedException exception) { return Fail(report, error, exception.Code, exception, Refused); }
        catch (CharacterAdaptRefusedException exception) { return Fail(report, error, exception.Code, exception, Refused); }
        catch (CombatRefusedException exception) when (exception.Code.Contains("CHECKPOINT", StringComparison.Ordinal)) { return Fail(report, error, exception.Code, exception, CheckpointRefused); }
        catch (CombatRefusedException exception) { return Fail(report, error, exception.Code, exception, Refused); }
        catch (Beep.SimEngine.Kernel.CorruptStateException exception) { return Fail(report, error, "FIELD_CHECKPOINT_WORLD", exception, CheckpointRefused); }
        catch (JournalRefusedException exception) { return Fail(report, error, "FIELD_" + exception.Code, exception, JournalRefused); }
        catch (IOException exception) { return Fail(report, error, "FIELD_FILE", exception, Refused); }
        catch (UnauthorizedAccessException exception) { return Fail(report, error, "FIELD_FILE", exception, Refused); }
        catch (Beep.SimEngine.Content.ContentException exception) { return Fail(report, error, "FIELD_CONTENT", exception, Refused); }
        catch (Exception exception)
        {
            // Not one of the refusals above: a component broke while the session was being built or initialised.
            return Fail(report, error, "FIELD_FAULT", exception, Faulted);
        }
    }

    private static int Play(FieldOptions options, FieldForces forces, IRuntimeReporter reporter, TextWriter output)
    {
        var resume = options.LoadPath is null ? null : FieldCheckpoint.Open(File.ReadAllBytes(options.LoadPath));
        var created = Stopwatch.StartNew();
        var world = FieldWorld.Create(options, forces, resume?.Sim);
        var combat = resume is null ? null : CombatCheckpoint.Open(forces.Groups, resume.Combat);
        var journal = options.JournalPath is null ? null : new SessionJournal();
        var session = FieldSession.Create(options, forces, world, reporter, combat, resume?.Commander, journal);
        created.Stop();
        var host = session.Runtime;

        var log = new OutputLog(output);
        var mode = options.RealTime ? "real-time" : $"fixed-frame frame-us={options.FrameMicroseconds}";
        log.Line($"game field {forces.Source} seed={options.Seed} tick-rate={session.Runtime.Clock.TicksPerSecond} mode={mode}");
        if (resume is not null) log.Line($"resumed tick={resume.Tick}");
        string? ending = null;
        Print(host, log, id => ending = id);
        host.Init();

        var startTick = host.Clock.Tick;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var collections = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
        var running = Stopwatch.StartNew();
        // The loop calls this before every Update, so the gap since the previous call is how long that frame took; the first call has no frame behind it.
        var frameWatch = Stopwatch.StartNew();
        var frames = new List<(long Tick, double Milliseconds)>();
        var firstFrame = true;
        EngineMainLoop.Run(host, options.TotalMicroseconds, options.RealTime ? null : options.FrameMicroseconds, _ =>
        {
            var elapsed = frameWatch.Elapsed.TotalMilliseconds;
            frameWatch.Restart();
            if (!firstFrame) frames.Add((host.Clock.Tick, elapsed));
            firstFrame = false;
        });
        running.Stop();
        var ticks = host.Clock.Tick - startTick;
        var allocatedPerTick = ticks == 0 ? 0 : (GC.GetAllocatedBytesForCurrentThread() - allocated) / ticks;
        var gc = (GC.CollectionCount(0) - collections.Item1, GC.CollectionCount(1) - collections.Item2, GC.CollectionCount(2) - collections.Item3);

        var timings = new List<string>();
        if (host.State != RuntimeState.Faulted && options.SavePath is not null) Save(options.SavePath, session, log, timings);
        if (host.State != RuntimeState.Faulted && journal is not null)
        {
            var bytes = journal.Serialize();
            File.WriteAllBytes(options.JournalPath!, bytes);
            log.Line($"journal ticks={journal.Hashes.Count} sha256={Convert.ToHexString(SHA256.HashData(bytes))}");
        }

        var state = session.Commander.State;
        log.Line($"final a={Head(session, forces.SideA)}/{state.StrengthA * forces.SquadsA} b={Head(session, forces.SideB)}/{state.StrengthB * forces.SquadsB} rounds={state.Round}");
        if (host.State != RuntimeState.Faulted && state.Spawned)
        {
            var ledger = FieldLedger.Read(session, forces);
            log.Line($"ledger a={ledger.PeopleA}/{ledger.RoundsA} b={ledger.PeopleB}/{ledger.RoundsB} depot-a={ledger.DepotPeopleA}/{ledger.DepotRoundsA} depot-b={ledger.DepotPeopleB}/{ledger.DepotRoundsB} reconciled={ledger.Reconciled}");
        }
        log.Line($"result state={host.State} ending={ending ?? "none"} tick={host.Clock.Tick} {session.Combat.Fingerprint()}");
        log.Finish();

        var hashWatch = Stopwatch.StartNew();
        var hash = session.World.Game.Hash();
        hashWatch.Stop();
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"timing build={created.Elapsed.TotalMilliseconds:F1}ms run={running.Elapsed.TotalMilliseconds:F1}ms ticks={ticks} per-tick={(ticks == 0 ? 0 : running.Elapsed.TotalMilliseconds / ticks):F3}ms slowest-frames={string.Join(",", frames.OrderByDescending(f => f.Milliseconds).Take(3).Select(f => FormattableString.Invariant($"t{f.Tick}:{f.Milliseconds:F1}ms")))} allocated-per-tick={allocatedPerTick}B gc={gc.Item1}/{gc.Item2}/{gc.Item3} world-hash={hash:X16} hash={hashWatch.Elapsed.TotalMilliseconds:F2}ms entities={session.World.Game.Seat(0).View.EntityCount}"));
        foreach (var line in timings) output.WriteLine(line);
        return host.State == RuntimeState.Faulted ? Faulted : Ok;
    }

    private static int Head(FieldSession session, IReadOnlyList<string> side) =>
        side.Sum(g => session.Commander.State.SquadOf.TryGetValue(g, out var squad) && session.Commander.State.Headcount.TryGetValue(squad, out var left) ? left : 0);

    private static int Replay(FieldOptions options, FieldForces forces, IRuntimeReporter reporter, TextWriter output)
    {
        var recorded = SessionJournal.Deserialize(File.ReadAllBytes(options.ReplayPath!));
        var log = new OutputLog(output);
        log.Line($"replay seed={options.Seed} ticks={recorded.Hashes.Count}");
        var watch = Stopwatch.StartNew();
        var verdict = JournalReplay.Verify(recorded, Codec, () =>
        {
            var fresh = new SessionJournal();
            var session = FieldSession.Create(options, forces, FieldWorld.Create(options, forces, null), reporter, null, null, fresh);
            return (session.Runtime, fresh);
        });
        watch.Stop();
        log.Line($"replay matches={verdict.Matches} code={verdict.Code} diverged-at={(verdict.DivergedAtTick is { } tick ? tick.ToString(CultureInfo.InvariantCulture) : "none")}");
        log.Finish();
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"timing replay={watch.Elapsed.TotalMilliseconds:F1}ms"));
        return verdict.Matches ? Ok : JournalRefused;
    }

    /// <summary>A checkpoint is only consistent when nothing is in flight, so the run lets every open request be answered first.</summary>
    private static void Save(string path, FieldSession session, OutputLog log, List<string> timings)
    {
        const int maxSettleTicks = 240;
        var oneTick = (RuntimeClock.MicrosecondsPerSecond + session.Runtime.Clock.TicksPerSecond - 1) / session.Runtime.Clock.TicksPerSecond;
        for (var settled = 0; !session.IsSettled && session.Runtime.State == RuntimeState.Running; settled++)
        {
            if (settled == maxSettleTicks) throw new CombatRefusedException(FieldCheckpoint.NotQuietCode, "requests stayed open after " + maxSettleTicks + " ticks");
            session.Runtime.Update(oneTick);
        }
        if (!session.IsSettled) throw new CombatRefusedException(FieldCheckpoint.NotQuietCode, "the session ended with a request still open");
        var watch = Stopwatch.StartNew();
        var bytes = FieldCheckpoint.Capture(session);
        watch.Stop();
        File.WriteAllBytes(path, bytes);
        log.Line($"checkpoint tick={session.Runtime.Clock.Tick} bytes={bytes.Length} sha256={Convert.ToHexString(SHA256.HashData(bytes))}");
        timings.Add(string.Create(CultureInfo.InvariantCulture, $"timing save={watch.Elapsed.TotalMilliseconds:F1}ms"));
    }

    /// <summary>Everything the session publishes becomes a line. Tick-stamped events carry their own tick, so the output does not depend on frame size.</summary>
    private static void Print(IGameRuntime host, OutputLog log, Action<string> ended)
    {
        host.Subscribe<ForcesRestored>(e => log.Line($"forces {e.Groups.Split(';').Length} groups"));
        host.Subscribe<FieldProgress>(e => log.Line($"t{e.Tick} {e.Kind} {e.Detail}"));
        host.Subscribe<SessionEnded>(e => { ended(e.EndingId); log.Line($"t{e.Tick} ended {e.EndingId}"); });
        host.Subscribe<SessionFaulted>(e => log.Line($"t{e.Tick} fault {e.Component}: {e.Message}"));
    }

    private static string AuditPathFrom(IReadOnlyList<string> args)
    {
        for (var i = 0; i + 1 < args.Count; i++)
            if (args[i] == "--audit") return args[i + 1];
        return new FieldOptions().AuditPath;
    }

    private static int Fail(IEngineReporter reporter, TextWriter error, string code, Exception exception, int exitCode)
    {
        Report(reporter, code, exception);
        error.WriteLine(code + ": " + exception.Message);
        return exitCode;
    }

    private static void Report(IEngineReporter reporter, string code, Exception exception) =>
        reporter.Report(new EngineNotice(code, exception.Message, Severity.Error, null, exception));
}

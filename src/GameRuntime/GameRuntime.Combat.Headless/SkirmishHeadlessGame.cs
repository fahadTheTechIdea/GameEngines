using System.Security.Cryptography;
using CombatEngine;
using GameContent.Catalog;
using Microsoft.Extensions.Logging;
using ScenarioEngine.Contracts;
using ScenarioEngine.Diagnostics;

namespace GameRuntime.Combat.Headless;

/// <summary>
/// The headless host of the skirmish game: the engine's main loop around a RuntimeSession. The session owns the clock; the loop only
/// reads how much time went by and calls <c>runtime.Update(elapsed)</c> (through <see cref="EngineMainLoop"/>), and the session runs
/// every component's Update once per clock tick. Between Updates the loop only writes the input state (from the script) and prints
/// the events the session published; nothing in the game changes outside Update.
/// </summary>
public static class SkirmishHeadlessGame
{
    public const int Ok = 0, Faulted = 1, Refused = 2, CheckpointRefused = 3, JournalRefused = 4;

    private static readonly JournalCodec Codec = InputEdgeJournal.CreateCodec();

    /// <param name="reporter">Where failures are reported; null reports through the application diagnostics (log, notification) built here. A test passes its own to see every report.</param>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, IReadOnlyList<IGameComponent>? extra = null, IEngineReporter? reporter = null)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder
            .AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace).SetMinimumLevel(LogLevel.Warning));
        using var diagnostics = new ApplicationDiagnostics(loggerFactory.CreateLogger("GameRuntime.Combat.Headless"),
            AuditPathFrom(args), new ConsoleNotificationSink());

        IEngineReporter report = reporter ?? diagnostics;

        // The outermost boundary of the host: every failure kind is reported through the one reporter and becomes an exit code.
        try
        {
            var options = SkirmishOptions.Parse(args);
            if (options.Help) { output.WriteLine(SkirmishOptions.Usage); return Ok; }
            var forces = SkirmishForceSource.Load(options);
            var runtimeReporter = new EngineReporterAdapter(report);
            return options.ReplayPath is null
                ? Play(options, forces, runtimeReporter, output, extra)
                : Replay(options, forces, runtimeReporter, output, extra);
        }
        catch (SkirmishUsageException exception)
        {
            Report(report, "COMBAT_USAGE", exception);
            error.WriteLine(exception.Message);
            error.WriteLine(SkirmishOptions.Usage);
            return Refused;
        }
        catch (CatalogRefusedException exception) { return Fail(report, error, exception.Code, exception, Refused); }
        catch (CombatAdaptRefusedException exception) { return Fail(report, error, exception.Code, exception, Refused); }
        catch (CombatRefusedException exception) when (exception.Code.StartsWith("COMBAT_CHECKPOINT", StringComparison.Ordinal)) { return Fail(report, error, exception.Code, exception, CheckpointRefused); }
        catch (CombatRefusedException exception) { return Fail(report, error, exception.Code, exception, Refused); }
        catch (JournalRefusedException exception) { return Fail(report, error, "COMBAT_" + exception.Code, exception, JournalRefused); }
        catch (IOException exception) { return Fail(report, error, "COMBAT_FILE", exception, Refused); }
        catch (UnauthorizedAccessException exception) { return Fail(report, error, "COMBAT_FILE", exception, Refused); }
        catch (Exception exception)
        {
            // Not one of the refusals above: a component broke while the session was being built or initialised.
            // The session already reported it when it stopped; this reports it as the run's outcome.
            return Fail(report, error, "COMBAT_FAULT", exception, Faulted);
        }
    }

    private static int Play(SkirmishOptions options, SkirmishForces forces, IRuntimeReporter reporter, TextWriter output, IReadOnlyList<IGameComponent>? extra)
    {
        var script = options.ScriptPath is null ? SkirmishScript.Empty : SkirmishScript.Parse(File.ReadAllLines(options.ScriptPath));
        var resume = options.LoadPath is null ? null : CombatCheckpoint.Open(forces.Groups, File.ReadAllBytes(options.LoadPath));
        var journal = options.JournalPath is null ? null : new SessionJournal();
        var bot = SkirmishBots.Create(options.Player, options.Plan, options.Seed);
        var session = SkirmishSession.Create(forces, options.Seed, reporter, resume, journal, extra, bot, journal is null ? null : Codec);
        var host = session.Runtime;

        var log = new OutputLog(output);
        var mode = options.RealTime ? "real-time" : $"fixed-frame frame-us={options.FrameMicroseconds}";
        log.Line($"game skirmish {forces.Source} seed={options.Seed} tick-rate={session.Clock.TicksPerSecond} mode={mode} player={options.Player}");
        if (resume is not null) log.Line($"resumed tick={resume.Tick}");
        string? ending = null;
        Print(host, log, id => ending = id);
        host.Init();

        EngineMainLoop.Run(host, options.TotalMicroseconds, options.RealTime ? null : options.FrameMicroseconds, hostMicroseconds =>
        {
            foreach (var line in script.Due(hostMicroseconds))
            {
                session.Input.Press(line.Order.Action, line.Order.Key);
                session.Input.Release(line.Order.Action);
                log.Line($"host input {line.Order.Action} {line.Order.Key}");
            }
        });

        if (host.State != RuntimeState.Faulted && options.SavePath is not null) Save(options.SavePath, session, log);
        if (host.State != RuntimeState.Faulted && journal is not null)
        {
            var bytes = journal.Serialize();
            File.WriteAllBytes(options.JournalPath!, bytes);
            log.Line($"journal inputs={journal.Inputs.Count} ticks={journal.Hashes.Count} sha256={Convert.ToHexString(SHA256.HashData(bytes))}");
        }

        foreach (var group in session.Combat.Groups.Values)
            log.Line($"final {group.Id} side={group.Side} members={group.Members}/{group.InitialMembers} health={group.Health} standing={group.Standing} morale={group.Morale} rounds={group.Batteries.Sum(b => b.Rounds)}");
        log.Line($"result state={host.State} ending={ending ?? "none"} {session.Fingerprint()}");
        log.Finish();
        return host.State == RuntimeState.Faulted ? Faulted : Ok;
    }

    private static int Replay(SkirmishOptions options, SkirmishForces forces, IRuntimeReporter reporter, TextWriter output, IReadOnlyList<IGameComponent>? extra)
    {
        var recorded = SessionJournal.Deserialize(File.ReadAllBytes(options.ReplayPath!));
        var log = new OutputLog(output);
        log.Line($"replay seed={options.Seed} inputs={recorded.Inputs.Count} ticks={recorded.Hashes.Count}");
        var verdict = JournalReplay.Verify(recorded, Codec, () =>
        {
            var fresh = new SessionJournal();
            return (SkirmishSession.Create(forces, options.Seed, reporter, null, fresh, extra).Runtime, fresh);
        });
        log.Line($"replay matches={verdict.Matches} code={verdict.Code} diverged-at={(verdict.DivergedAtTick is { } tick ? tick.ToString() : "none")}");
        log.Finish();
        return verdict.Matches ? Ok : JournalRefused;
    }

    /// <summary>A checkpoint is only consistent when nothing is in flight, so the run lets the commander's open order land first.</summary>
    private static void Save(string path, SkirmishSession session, OutputLog log)
    {
        const int maxSettleTicks = 120;
        var oneTick = (RuntimeClock.MicrosecondsPerSecond + session.Clock.TicksPerSecond - 1) / session.Clock.TicksPerSecond;
        for (var settled = 0; !session.IsSettled && session.Runtime.State == RuntimeState.Running; settled++)
        {
            if (settled == maxSettleTicks) throw new CombatRefusedException(CombatCheckpoint.NotQuietCode, "the commander or input stayed busy after " + maxSettleTicks + " ticks");
            session.Runtime.Update(oneTick);
        }
        if (!session.IsSettled) throw new CombatRefusedException(CombatCheckpoint.NotQuietCode, "the session ended with a request or input still open");
        var bytes = CombatCheckpoint.Capture(session.Clock.Tick, session.Combat);
        File.WriteAllBytes(path, bytes);
        log.Line($"checkpoint tick={session.Clock.Tick} bytes={bytes.Length} sha256={Convert.ToHexString(SHA256.HashData(bytes))}");
    }

    /// <summary>Every event the session publishes becomes a line. Tick-stamped events carry their own tick, so the output does not depend on frame size.</summary>
    private static void Print(IGameRuntime host, OutputLog log, Action<string> ended)
    {
        host.Subscribe<ForcesRestored>(e => log.Line($"forces {e.Groups}"));
        host.Subscribe<Engagement>(e => log.Line($"t{e.Tick} engagement {e.RequestId} {e.Attacker}>{e.Defender} fired={e.Fired} hits={e.Hits} damage={e.DamageToDefender}/{e.DamageToAttacker} health={e.AttackerHealth}/{e.DefenderHealth}{(e.Codes.Length == 0 ? "" : " codes=" + e.Codes)}"));
        host.Subscribe<Casualty>(e => log.Line($"t{e.Tick} casualty {e.Group} lost={e.Lost} left={e.Left}"));
        host.Subscribe<AmmoSpent>(e => log.Line($"t{e.Tick} ammo {e.Group} {e.Battery} spent={e.Rounds} left={e.Left}"));
        host.Subscribe<Standing>(e => log.Line($"t{e.Tick} standing {e.Group} {e.State} morale={e.Morale}"));
        host.Subscribe<Moved>(e => log.Line($"t{e.Tick} moved {e.Group} {e.FromMetres}m -> {e.ToMetres}m over {e.Terrain}"));
        host.Subscribe<Resupplied>(e => log.Line($"t{e.Tick} resupplied {e.Group} {e.Battery} accepted={e.Accepted} left={e.Left}"));
        host.Subscribe<EngagementRefused>(e => log.Line($"t{e.Tick} refused {e.RequestId} {e.Code}"));
        host.Subscribe<SkirmishPlayerDecided>(e => log.Line($"t{e.Tick} commander {e.Kind} {e.Detail}"));
        host.Subscribe<SessionEnded>(e => { ended(e.EndingId); log.Line($"t{e.Tick} ended {e.EndingId}"); });
        host.Subscribe<SessionFaulted>(e => log.Line($"t{e.Tick} fault {e.Component}: {e.Message}"));
    }

    private static string AuditPathFrom(IReadOnlyList<string> args)
    {
        for (var i = 0; i + 1 < args.Count; i++)
            if (args[i] == "--audit") return args[i + 1];
        return new SkirmishOptions().AuditPath;
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

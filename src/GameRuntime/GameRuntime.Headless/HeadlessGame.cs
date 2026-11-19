using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ScenarioEngine.Contracts;
using ScenarioEngine.Diagnostics;

namespace GameRuntime.Headless;

/// <summary>
/// The headless host: the engine's main loop around a RuntimeSession. The session owns the clock; each pass of the loop
/// reads how much time went by and calls <c>runtime.Update(elapsed)</c>, and the session runs every component's Update once
/// per clock tick. By default the loop reads the real clock (Stopwatch); with <c>--frame-ms</c> it feeds fixed frames of
/// virtual time, which makes a run reproducible. Between Updates the loop only writes the <see cref="InputState"/> (from
/// the script) and prints the events the session published; nothing in the game changes outside Update.
/// </summary>
public static class HeadlessGame
{
    public const int Ok = 0, Faulted = 1, Refused = 2, CheckpointRefused = 3, JournalRefused = 4;

    private static readonly JournalCodec Codec = InputEdgeJournal.CreateCodec();

    /// <param name="extra">Further components of the host; the session updates them after the standard ones.</param>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output, TextWriter error, IReadOnlyList<IGameComponent>? extra = null)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder
            .AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace).SetMinimumLevel(LogLevel.Warning));
        using var diagnostics = new ApplicationDiagnostics(loggerFactory.CreateLogger("GameRuntime.Headless"),
            AuditPathFrom(args), new ConsoleNotificationSink());

        // The outermost boundary of the host: every failure kind is reported through the one reporter and becomes an exit code.
        try
        {
            var options = HeadlessOptions.Parse(args);
            if (options.Help) { output.WriteLine(HeadlessOptions.Usage); return Ok; }
            var environment = await HeadlessEnvironment.LoadAsync(diagnostics);
            return options.ReplayPath is null
                ? Play(options, environment, output, extra)
                : Replay(options, environment, output, extra);
        }
        catch (UsageException exception)
        {
            Report(diagnostics, "HEADLESS_USAGE", exception);
            error.WriteLine(exception.Message);
            error.WriteLine(HeadlessOptions.Usage);
            return Refused;
        }
        catch (ContentRefusedException exception) { return Fail(diagnostics, error, "HEADLESS_CONTENT", exception, Refused); }
        catch (CheckpointRefusedException exception) { return Fail(diagnostics, error, "HEADLESS_" + exception.Code, exception, CheckpointRefused); }
        catch (JournalRefusedException exception) { return Fail(diagnostics, error, "HEADLESS_" + exception.Code, exception, JournalRefused); }
        catch (IOException exception) { return Fail(diagnostics, error, "HEADLESS_FILE", exception, Refused); }
        catch (UnauthorizedAccessException exception) { return Fail(diagnostics, error, "HEADLESS_FILE", exception, Refused); }
        catch (Exception exception)
        {
            // Not one of the refusals above: a component or engine broke while the session was being built or initialised.
            // The session already reported it when it stopped; this reports it as the run's outcome.
            return Fail(diagnostics, error, "HEADLESS_FAULT", exception, Faulted);
        }
    }

    private static int Play(HeadlessOptions options, HeadlessEnvironment environment, TextWriter output, IReadOnlyList<IGameComponent>? extra)
    {
        var script = options.ScriptPath is null ? InputScript.Empty : InputScript.Parse(File.ReadAllLines(options.ScriptPath));
        var checkpoints = new CompositeCheckpointService(new ScenarioEngine.Persistence.ScenarioSaveCodec());
        var resume = options.LoadPath is null ? null : checkpoints.Open(File.ReadAllBytes(options.LoadPath), environment.Scenario.Identity);
        var journal = options.JournalPath is null ? null : new SessionJournal();
        var bot = TycoonBots.Create(options.Player, options.Seed, environment.GoalProfit);
        var session = HeadlessSession.Create(environment, options.Seed, resume, journal, extra, bot, journal is null ? null : Codec);
        var host = session.Runtime;

        var log = new OutputLog(output);
        var mode = options.RealTime ? "real-time" : $"fixed-frame frame-us={options.FrameMicroseconds}";
        log.Line($"game tycoon scenario={TycoonWorld.Scenario} seed={options.Seed} tick-rate={session.Clock.TicksPerSecond} mode={mode} player={options.Player}");
        if (resume is not null) log.Line($"resumed tick={resume.Tick}");
        Print(host, session, log);
        host.Init();

        MainLoop(options, script, session, log);

        if (host.State != RuntimeState.Faulted && options.SavePath is not null) Save(options.SavePath, checkpoints, session, log);
        if (host.State != RuntimeState.Faulted && journal is not null)
        {
            var bytes = journal.Serialize();
            File.WriteAllBytes(options.JournalPath!, bytes);
            log.Line($"journal inputs={journal.Inputs.Count} ticks={journal.Hashes.Count} sha256={Convert.ToHexString(SHA256.HashData(bytes))}");
        }

        var ending = session.Mission.Snapshot.EndingId ?? "none";
        log.Line($"result state={host.State} ending={ending} {session.Fingerprint()}");
        log.Finish();
        return host.State == RuntimeState.Faulted ? Faulted : Ok;
    }

    /// <summary>Each pass: write the input that is due, hand the session the time that went by, repeat until the game or the time is over.</summary>
    private static void MainLoop(HeadlessOptions options, InputScript script, HeadlessSession session, OutputLog log)
    {
        EngineMainLoop.Run(session.Runtime, options.TotalMicroseconds, options.RealTime ? null : options.FrameMicroseconds,
            hostMicroseconds => { foreach (var line in script.Due(hostMicroseconds)) Apply(line, session, log); });
    }

    private static int Replay(HeadlessOptions options, HeadlessEnvironment environment, TextWriter output, IReadOnlyList<IGameComponent>? extra)
    {
        var recorded = SessionJournal.Deserialize(File.ReadAllBytes(options.ReplayPath!));
        var log = new OutputLog(output);
        log.Line($"replay seed={options.Seed} inputs={recorded.Inputs.Count} ticks={recorded.Hashes.Count}");
        var verdict = JournalReplay.Verify(recorded, Codec, () =>
        {
            var fresh = new SessionJournal();
            return (HeadlessSession.Create(environment, options.Seed, null, fresh, extra).Runtime, fresh);
        });
        log.Line($"replay matches={verdict.Matches} code={verdict.Code} diverged-at={(verdict.DivergedAtTick is { } tick ? tick.ToString() : "none")}");
        log.Finish();
        return verdict.Matches ? Ok : JournalRefused;
    }

    /// <summary>The script is a human at the keyboard: it writes the input state, or works the host's own pause. It changes no game state.</summary>
    private static void Apply(ScriptLine line, HeadlessSession session, OutputLog log)
    {
        switch (line.Action)
        {
            case ScriptAction.Sell:
                session.Input.Press(PlayerActions.Sell, line.Units.ToString());
                session.Input.Release(PlayerActions.Sell);
                log.Line($"host input sell {line.Units}");
                break;
            case ScriptAction.AutoSell:
                if (line.Units == 0) session.Input.Release(PlayerActions.AutoSell);
                else session.Input.Press(PlayerActions.AutoSell, line.Units.ToString());
                log.Line(line.Units == 0 ? "host input autosell off" : $"host input autosell {line.Units}");
                break;
            case ScriptAction.Speed:
                session.Input.Press(PlayerActions.Speed, $"{line.SpeedNumerator}/{line.SpeedDenominator}");
                session.Input.Release(PlayerActions.Speed);
                log.Line($"host input speed {line.SpeedNumerator}/{line.SpeedDenominator}");
                break;
            case ScriptAction.Pause:
                session.Clock.Pause();
                log.Line("host pause");
                break;
            case ScriptAction.Resume:
                session.Clock.Resume();
                log.Line("host resume");
                break;
        }
    }

    /// <summary>A checkpoint is only consistent when no director ticket is open and the input state is quiet, so the run lets in-flight effects land first.</summary>
    private static void Save(string path, CompositeCheckpointService checkpoints, HeadlessSession session, OutputLog log)
    {
        const int maxSettleTicks = 4;
        var oneTick = (RuntimeClock.MicrosecondsPerSecond + session.Clock.TicksPerSecond - 1) / session.Clock.TicksPerSecond;
        bool Settled() => session.Tickets.OpenCount == 0 && session.Input.IsQuiet;
        for (var settled = 0; !Settled() && session.Runtime.State == RuntimeState.Running; settled++)
        {
            if (settled == maxSettleTicks) throw new CheckpointRefusedException("CHECKPOINT_NOT_QUIESCENT", "director tickets or input stayed open after " + maxSettleTicks + " ticks");
            session.Runtime.Update(oneTick);
        }
        if (!Settled()) throw new CheckpointRefusedException("CHECKPOINT_NOT_QUIESCENT", "the session ended with director tickets or input open");
        var bytes = checkpoints.Capture(session.Clock.Tick, session.Game, session.Mission);
        File.WriteAllBytes(path, bytes);
        log.Line($"checkpoint tick={session.Clock.Tick} bytes={bytes.Length} sha256={Convert.ToHexString(SHA256.HashData(bytes))}");
    }

    /// <summary>Every event the session publishes becomes a line. Tick-stamped events carry their own tick, so the output does not depend on frame size.</summary>
    private static void Print(IGameRuntime host, HeadlessSession session, OutputLog log)
    {
        host.Subscribe<ObservationPosted>(e => { if (e.Kind.StartsWith("sim.event.", StringComparison.Ordinal)) log.Line($"t{e.Tick} {e.Kind} {e.PayloadJson}"); });
        host.Subscribe<MissionEvent>(e => log.Line($"t{e.Tick} mission {e.Kind} {e.PayloadJson}"));
        host.Subscribe<EffectRequested>(e => log.Line($"t{e.Tick} effect {e.Kind} {e.PayloadJson}"));
        host.Subscribe<CommandResolved>(e => log.Line($"command {e.CommandId} {e.Engine} applied={e.Applied} {e.Code}"));
        host.Subscribe<PlayerDecided>(e => log.Line($"t{e.Tick} player {e.Kind} {e.Detail}"));
        host.Subscribe<ClockRequested>(e =>
        {
            // The player asked for a speed; the host owns pacing, so it applies the request between Updates.
            session.Clock.SetSpeed(e.Numerator, e.Denominator);
            log.Line($"host speed {e.Numerator}/{e.Denominator}");
        });
        host.Subscribe<SessionEnded>(e => log.Line($"t{e.Tick} ended {e.EndingId}"));
        host.Subscribe<SessionFaulted>(e => log.Line($"t{e.Tick} fault {e.Component}: {e.Message}"));
    }

    private static string AuditPathFrom(IReadOnlyList<string> args)
    {
        for (var i = 0; i + 1 < args.Count; i++)
            if (args[i] == "--audit") return args[i + 1];
        return new HeadlessOptions().AuditPath;
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

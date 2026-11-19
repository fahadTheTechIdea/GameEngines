using System.Security.Cryptography;
using System.Text.Json;
using AlchemyEngine;
using Microsoft.Extensions.Logging;
using ScenarioEngine.Contracts;
using ScenarioEngine.Diagnostics;

namespace GameRuntime.AlchemySim.Headless;

/// <summary>
/// The headless host of the alchemist workshop: the engine's main loop around a RuntimeSession. The session owns the clock; the loop
/// only reads how much time went by and calls <c>runtime.Update(elapsed)</c> (through <see cref="EngineMainLoop"/>), and the session runs
/// every component's Update once per clock tick. Between Updates the loop only writes the input state (from the script) and prints the
/// events the session published; nothing in the game changes outside Update.
/// </summary>
public static class WorkshopHeadlessGame
{
    public const int Ok = 0, Faulted = 1, Refused = 2, CheckpointRefused = 3, JournalRefused = 4;

    private static readonly JournalCodec Codec = InputEdgeJournal.CreateCodec();

    /// <param name="extra">Further components of the host; the session updates them after the standard ones.</param>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, IReadOnlyList<IGameComponent>? extra = null)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder
            .AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace).SetMinimumLevel(LogLevel.Warning));
        using var diagnostics = new ApplicationDiagnostics(loggerFactory.CreateLogger("GameRuntime.AlchemySim.Headless"),
            AuditPathFrom(args), new ConsoleNotificationSink());

        // The outermost boundary of the host: every failure kind is reported through the one reporter and becomes an exit code.
        try
        {
            var options = WorkshopOptions.Parse(args);
            if (options.Help) { output.WriteLine(WorkshopOptions.Usage); return Ok; }
            var reporter = new EngineReporterAdapter(diagnostics);
            return options.ReplayPath is null ? Play(options, reporter, output, extra) : Replay(options, reporter, output, extra);
        }
        catch (WorkshopUsageException exception)
        {
            Report(diagnostics, "WORKSHOP_USAGE", exception);
            error.WriteLine(exception.Message);
            error.WriteLine(WorkshopOptions.Usage);
            return Refused;
        }
        catch (AlchemyRefusedException exception) when (IsCheckpointCode(exception.Code)) { return Fail(diagnostics, error, exception.Code, exception, CheckpointRefused); }
        catch (AlchemyRefusedException exception) { return Fail(diagnostics, error, exception.Code, exception, Refused); }
        catch (JournalRefusedException exception) { return Fail(diagnostics, error, "WORKSHOP_" + exception.Code, exception, JournalRefused); }
        catch (IOException exception) { return Fail(diagnostics, error, "WORKSHOP_FILE", exception, Refused); }
        catch (UnauthorizedAccessException exception) { return Fail(diagnostics, error, "WORKSHOP_FILE", exception, Refused); }
        catch (Exception exception)
        {
            // Not one of the refusals above: a component or the engine broke while the session was being built or initialised.
            // The session already reported it when it stopped; this reports it as the run's outcome.
            return Fail(diagnostics, error, "WORKSHOP_FAULT", exception, Faulted);
        }
    }

    private static bool IsCheckpointCode(string code) =>
        code.StartsWith("ALCHEMY_CHECKPOINT", StringComparison.Ordinal) || code.StartsWith("ALCHEMY_BOOK_", StringComparison.Ordinal);

    private static int Play(WorkshopOptions options, IRuntimeReporter reporter, TextWriter output, IReadOnlyList<IGameComponent>? extra)
    {
        var script = options.ScriptPath is null ? WorkshopScript.Empty : WorkshopScript.Parse(File.ReadAllLines(options.ScriptPath));
        var resume = options.LoadPath is null ? null : WorkshopCheckpoint.Open(File.ReadAllBytes(options.LoadPath));
        var journal = options.JournalPath is null ? null : new SessionJournal();
        var policy = options.Player == "alchemist" ? new AlchemistPolicy() : null;
        var session = WorkshopSession.Create(options.Seed, reporter, resume, journal, extra, policy, journal is null ? null : Codec);
        var host = session.Runtime;

        var log = new OutputLog(output);
        var mode = options.RealTime ? "real-time" : $"fixed-frame frame-us={options.FrameMicroseconds}";
        log.Line($"game workshop recipes={session.View.Recipes.Count} excluded={session.View.Excluded.Count} content={session.View.Alchemy.Digest[..12]} seed={options.Seed} tick-rate={session.Clock.TicksPerSecond} mode={mode} player={options.Player}");
        foreach (var excluded in session.View.Excluded) log.Line($"excluded {excluded.TransformationId} {excluded.Code}");
        if (resume is not null) log.Line($"resumed tick={resume.Tick}");
        string? ending = null;
        Print(host, log, id => ending = id);
        host.Init();

        EngineMainLoop.Run(host, options.TotalMicroseconds, options.RealTime ? null : options.FrameMicroseconds, hostMicroseconds =>
        {
            foreach (var line in script.Due(hostMicroseconds))
            {
                session.Input.Press(line.Action, line.Argument);
                session.Input.Release(line.Action);
                log.Line($"host input {line.Action} {line.Argument}");
            }
        });

        if (host.State != RuntimeState.Faulted && options.SavePath is not null) Save(options.SavePath, session, log);
        if (host.State != RuntimeState.Faulted && journal is not null)
        {
            var bytes = journal.Serialize();
            File.WriteAllBytes(options.JournalPath!, bytes);
            log.Line($"journal inputs={journal.Inputs.Count} ticks={journal.Hashes.Count} sha256={Convert.ToHexString(SHA256.HashData(bytes))}");
        }

        var book = session.Alchemy.Book;
        log.Line($"result state={host.State} ending={ending ?? "none"} {session.Fingerprint()} elements={book.Elements.Count}/{session.View.Alchemy.Elements.Count} recipes={book.Recipes.Count}/{session.View.Recipes.Count} attempts={book.Attempts} ordered={session.Player.View.Ordered.Count} drift={session.Drift().Count}");
        log.Finish();
        return host.State == RuntimeState.Faulted ? Faulted : Ok;
    }

    private static int Replay(WorkshopOptions options, IRuntimeReporter reporter, TextWriter output, IReadOnlyList<IGameComponent>? extra)
    {
        var recorded = SessionJournal.Deserialize(File.ReadAllBytes(options.ReplayPath!));
        var log = new OutputLog(output);
        log.Line($"replay seed={options.Seed} inputs={recorded.Inputs.Count} ticks={recorded.Hashes.Count}");
        var verdict = JournalReplay.Verify(recorded, Codec, () =>
        {
            var fresh = new SessionJournal();
            return (WorkshopSession.Create(options.Seed, reporter, null, fresh, extra).Runtime, fresh);
        });
        log.Line($"replay matches={verdict.Matches} code={verdict.Code} diverged-at={(verdict.DivergedAtTick is { } tick ? tick.ToString() : "none")}");
        log.Finish();
        return verdict.Matches ? Ok : JournalRefused;
    }

    /// <summary>A checkpoint is only consistent when nothing is in flight, so the run lets the player's open questions and the sim's open commands land first.</summary>
    private static void Save(string path, WorkshopSession session, OutputLog log)
    {
        const int maxSettleTicks = 240;
        var oneTick = (RuntimeClock.MicrosecondsPerSecond + session.Clock.TicksPerSecond - 1) / session.Clock.TicksPerSecond;
        for (var settled = 0; !session.IsSettled && session.Runtime.State == RuntimeState.Running; settled++)
        {
            if (settled == maxSettleTicks) throw new AlchemyRefusedException(AlchemyCheckpoint.NotQuietCode, "the player or the sim stayed busy after " + maxSettleTicks + " ticks");
            session.Runtime.Update(oneTick);
        }
        if (!session.IsSettled) throw new AlchemyRefusedException(AlchemyCheckpoint.NotQuietCode, "the session ended with a request, order or input still open");
        var bytes = WorkshopCheckpoint.Capture(session);
        File.WriteAllBytes(path, bytes);
        log.Line($"checkpoint tick={session.Clock.Tick} bytes={bytes.Length} sha256={Convert.ToHexString(SHA256.HashData(bytes))}");
    }

    /// <summary>Every event the session publishes becomes a line. Tick-stamped events carry their own tick, so the output does not depend on frame size.</summary>
    private static void Print(IGameRuntime host, OutputLog log, Action<string> ended)
    {
        host.Subscribe<Combined>(e => log.Line($"t{e.Tick} combined {e.Items}{(e.Environment.Length == 0 ? "" : " in " + e.Environment)} -> {e.Outcome}{(e.RecipeId.Length == 0 ? "" : " via " + e.RecipeId)}{(e.Produced.Length == 0 ? "" : " makes " + e.Produced)}{(e.Kept.Length == 0 ? "" : " keeps " + e.Kept)}"));
        host.Subscribe<CombineRefused>(e => log.Line($"t{e.Tick} refused {e.RequestId} {e.Code}"));
        host.Subscribe<DiscoveryMade>(e => log.Line($"t{e.Tick} discovered {e.Kind} {e.Id} ({e.RequestId})"));
        host.Subscribe<TransformationOrderResolved>(e => log.Line($"t{e.Tick} order {e.RequestId} accepted={e.Accepted} {e.Code}"));
        host.Subscribe<CommandResolved>(e => log.Line($"command {e.CommandId} {e.Engine} applied={e.Applied} {e.Code}"));
        host.Subscribe<ObservationPosted>(e => { if (e.Kind.StartsWith("sim.event.", StringComparison.Ordinal)) log.Line($"t{e.Tick} {e.Kind} {e.PayloadJson}"); });
        host.Subscribe<WorkshopPlayerDecided>(e => log.Line($"t{e.Tick} player {e.Kind} {e.Detail}"));
        host.Subscribe<SessionEnded>(e => { ended(e.EndingId); log.Line($"t{e.Tick} ended {e.EndingId}"); });
        host.Subscribe<SessionFaulted>(e => log.Line($"t{e.Tick} fault {e.Component}: {e.Message}"));
    }

    private static string AuditPathFrom(IReadOnlyList<string> args)
    {
        for (var i = 0; i + 1 < args.Count; i++)
            if (args[i] == "--audit") return args[i + 1];
        return new WorkshopOptions().AuditPath;
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

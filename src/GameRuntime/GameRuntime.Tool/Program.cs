using System.Collections.Immutable;
using Counter.GameLayer;
using GameRuntime;
using Microsoft.Extensions.Logging;
using ScenarioEngine.Content;
using ScenarioEngine.Contracts;
using ScenarioEngine.Core;
using ScenarioEngine.Diagnostics;

// Headless host: drives the reference GameRuntime with the Counter mission and a source component that posts one
// observation per tick. The mission is advanced only by events; the runtime owns the clock.
var packPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "counter.json");
var output = Path.GetFullPath(Path.Combine("artifacts", "runtime-demo"));
Directory.CreateDirectory(output);
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
using var diagnostics = new ApplicationDiagnostics(loggerFactory.CreateLogger("GameRuntime"),
    Path.Combine(output, "audit.jsonl"), new ConsoleNotificationSink());

var registry = new GameLayerRegistry([new CounterLayer()]);
var validator = new JsonSchemaContractValidator();
var catalog = new ScenarioContentService(registry, validator, diagnostics);
var load = await catalog.LoadAsync(new JsonFilePackSource(packPath), CancellationToken.None);
if (!load.IsReady)
{
    foreach (var d in load.Report.Diagnostics) Console.Error.WriteLine($"{d.Code}: {d.Message}");
    return 2;
}

// Runs the same session with a given frame size; returns the ordered event log.
async Task<List<string>> RunAsync(long frameMicroseconds, long totalMicroseconds)
{
    var log = new List<string>();
    var sessions = new ScenarioSessionFactory(registry, validator, diagnostics);
    var simulation = sessions.Create(load.Scenarios[0], new ScenarioStartOptions("runtime-demo", 42, ImmutableHashSet.Create("player")), new WorldFrame(0, 0, []));
    var runtime = new RuntimeSession(new RuntimeClock(ticksPerSecond: 20), new DiagnosticsReporter(diagnostics));
    runtime.AddComponent(new DeltaSource(), 100);
    runtime.AddComponent(new MissionComponent(simulation), 200);
    runtime.Subscribe<MissionEvent>(e => log.Add($"t{e.Tick} {e.Kind} {e.PayloadJson}"));
    runtime.Subscribe<SessionEnded>(e => log.Add($"t{e.Tick} ended {e.EndingId}"));
    runtime.Init();
    for (long elapsed = 0; elapsed < totalMicroseconds; elapsed += frameMicroseconds) runtime.Update(frameMicroseconds);
    log.Add($"state={runtime.State} tick={runtime.Clock.Tick}");
    await Task.CompletedTask;
    return log;
}

var a = await RunAsync(50_000, 500_000);   // ten ticks as ten 50 ms frames
var b = await RunAsync(500_000, 500_000);  // the same time as one 500 ms frame, capped at 8 ticks per update
foreach (var line in a) Console.WriteLine(line);
Console.WriteLine(a.SequenceEqual(b) ? "frame-split: identical" : "frame-split: DIFFERENT (expected only if the catch-up cap applied)");

// --- SimEngine + ScenarioEngine in one session, one clock -------------------------------------------------------
// The sim (quickstart game) is advanced only by the runtime clock; its tick counter drives the mission through a
// change probe owned by the Sim adapter. The sim's own clock and Update are never used.
async Task<(List<string> Log, ulong Hash, long SimTick)> RunSimAsync(long frameMicroseconds, long totalMicroseconds)
{
    var log = new List<string>();
    var tickets = new DirectorTickets();
    var definition = new Beep.SimEngine.Kernel.GameDefinition("quick-game", new Beep.SimEngine.Kernel.GameModule[] {
        new Beep.SimEngine.Standard.ContentAccessModule(), new Beep.SimEngine.Standard.WorldSetupModule(), new QuickGame.Engine.QuickModule(),
        new DirectorBridgeModule(tickets, new IDirectorEffect[] { new NotifyEffect() }) });
    var files = new Beep.SimEngine.Hosting.DiskFileSource(Path.Combine(AppContext.BaseDirectory, "SimContent"));
    var simFactory = new Beep.SimEngine.Hosting.GameFactory(definition, Beep.SimEngine.Hosting.ContentSet.Load(files, definition.ContentTypes));
    var game = simFactory.NewGame(new Beep.SimEngine.Hosting.NewGameRequest { Scenario = "quick", Seed = 42 });
    var sessions = new ScenarioSessionFactory(registry, validator, diagnostics);
    var mission = sessions.Create(load.Scenarios[0], new ScenarioStartOptions("runtime-sim", 42, ImmutableHashSet.Create("player")), new WorldFrame(0, 0, []));
    var runtime = new RuntimeSession(new RuntimeClock(game.AuthoredTicksPerSecond), new DiagnosticsReporter(diagnostics));
    var tickProbe = new SimProbe("test_counter.adjustment", g => g.Tick, (before, now) => "{\"delta\":" + (now - before) + "}");
    runtime.AddComponent(new SimComponent(game, [tickProbe]), 100);
    runtime.AddComponent(new MissionComponent(mission, new HashSet<string> { "test_counter.adjustment" }), 200);
    runtime.AddComponent(new EffectSource(atTick: 2), 250);
    runtime.AddComponent(new SimEffectDispatcher(game, tickets), 300);
    runtime.Subscribe<CommandResolved>(e => log.Add($"command {e.CommandId} {e.Engine} applied={e.Applied} {e.Code}"));
    runtime.Subscribe<ObservationPosted>(e => { if (e.Kind == "sim.notification") log.Add($"t{e.Tick} {e.Kind} {e.PayloadJson}"); });
    runtime.Subscribe<MissionEvent>(e => log.Add($"t{e.Tick} {e.Kind}"));
    runtime.Subscribe<SessionEnded>(e => log.Add($"t{e.Tick} ended {e.EndingId}"));
    runtime.Init();
    for (long elapsed = 0; elapsed < totalMicroseconds; elapsed += frameMicroseconds) runtime.Update(frameMicroseconds);
    // A forged director command (no ticket) must be refused by the sim itself.
    var forged = game.Seat(0).Submit(new DirectorEffectCommand { Key = "forged", Effect = "notify", Payload = "\"x\"" });
    game.Update(game.Tick + 1);
    var answers = new List<Beep.SimEngine.CommandResult>(); long cursor = 0; game.Seat(0).ReadResults(ref cursor, answers);
    var forgedAnswer = answers.Last(a => a.Sequence == forged);
    log.Add($"forged command accepted={forgedAnswer.Accepted} refusal={forgedAnswer.Refusal.Id}");
    log.Add($"state={runtime.State} runtimeTick={runtime.Clock.Tick} simTick={game.Tick} missionTick={mission.Snapshot.Tick}");
    await Task.CompletedTask;
    return (log, game.Hash(), game.Tick);
}

var simA = await RunSimAsync(50_000, 500_000);
var simB = await RunSimAsync(250_000, 500_000);
foreach (var line in simA.Log) Console.WriteLine("sim+mission: " + line);
Console.WriteLine(simA.Log.SequenceEqual(simB.Log) && simA.Hash == simB.Hash
    ? $"sim frame-split: identical log, sim hash {simA.Hash:X16}"
    : "sim frame-split: DIFFERENT");


// --- composite checkpoint: save at tick 2, continue, restore elsewhere, continue; both must end identical -------------
(Beep.SimEngine.Hosting.GameFactory Factory, DirectorTickets Tickets) MakeSimFactory()
{
    var tickets = new DirectorTickets();
    var definition = new Beep.SimEngine.Kernel.GameDefinition("quick-game", new Beep.SimEngine.Kernel.GameModule[] {
        new Beep.SimEngine.Standard.ContentAccessModule(), new Beep.SimEngine.Standard.WorldSetupModule(), new QuickGame.Engine.QuickModule(),
        new DirectorBridgeModule(tickets, new IDirectorEffect[] { new NotifyEffect() }) });
    var files = new Beep.SimEngine.Hosting.DiskFileSource(Path.Combine(AppContext.BaseDirectory, "SimContent"));
    return (new Beep.SimEngine.Hosting.GameFactory(definition, Beep.SimEngine.Hosting.ContentSet.Load(files, definition.ContentTypes)), tickets);
}
RuntimeSession Assemble(Beep.SimEngine.Hosting.SimGame game, ScenarioEngine.Contracts.IScenarioSimulation mission, DirectorTickets tickets, long startTick)
{
    var runtime = new RuntimeSession(new RuntimeClock(game.AuthoredTicksPerSecond, startTick: startTick), new EngineReporterAdapter(diagnostics));
    var probe = new SimProbe("test_counter.adjustment", g => g.Tick, (before, now) => "{\"delta\":" + (now - before) + "}");
    runtime.AddComponent(new SimComponent(game, [probe]), 100);
    runtime.AddComponent(new MissionComponent(mission, new HashSet<string> { "test_counter.adjustment" }), 200);
    runtime.AddComponent(new SimEffectDispatcher(game, tickets), 300);
    return runtime;
}
string MissionHash(ScenarioEngine.Contracts.IScenarioSimulation m) => ScenarioEngine.Core.ScenarioEngine.StateHash(m.Snapshot)[..16];
string Fingerprint(Beep.SimEngine.Hosting.SimGame g, ScenarioEngine.Contracts.IScenarioSimulation m) =>
    $"tick={g.Tick} simHash={g.Hash():X16} missionHash={MissionHash(m)} value={m.Snapshot.Layer.Data.GetProperty("value")} phase={m.Snapshot.Phase}";

var checkpoints = new CompositeCheckpointService(new ScenarioEngine.Persistence.ScenarioSaveCodec());
var (factoryA, ticketsA) = MakeSimFactory();
var gameA = factoryA.NewGame(new Beep.SimEngine.Hosting.NewGameRequest { Scenario = "quick", Seed = 42 });
var missionA = new ScenarioSessionFactory(registry, validator, diagnostics).Create(load.Scenarios[0], new ScenarioStartOptions("ckpt", 42, ImmutableHashSet.Create("player")), new WorldFrame(0, 0, []));
var runA = Assemble(gameA, missionA, ticketsA, 0);
runA.Init();
runA.Update(70_000); // two ticks at 30 Hz
var saved = checkpoints.Capture(runA.Clock.Tick, gameA, missionA);
runA.Update(40_000); // one more tick: the mission reaches its ending
Console.WriteLine("checkpoint: original   " + Fingerprint(gameA, missionA));

var parts = checkpoints.Open(saved, missionA.Snapshot.Identity);
var (factoryB, ticketsB) = MakeSimFactory();
var gameB = factoryB.Load(parts.Sim);
var missionB = new ScenarioSessionFactory(registry, validator, diagnostics).Restore(load.Scenarios[0], parts.Mission, new WorldFrame(parts.Mission.WorldGeneration, parts.Tick, []));
var runB = Assemble(gameB, missionB, ticketsB, parts.Tick);
runB.Init();
runB.Update(40_000);
Console.WriteLine("checkpoint: restored   " + Fingerprint(gameB, missionB));
Console.WriteLine(Fingerprint(gameA, missionA) == Fingerprint(gameB, missionB) ? "checkpoint: restore + continue == uninterrupted run" : "checkpoint: DIVERGED");

var tampered = (byte[])saved.Clone(); tampered[tampered.Length / 2] ^= 1;
foreach (var (name, bytes, identity) in new[] {
    ("tampered bytes", tampered, missionA.Snapshot.Identity),
    ("wrong mission", saved, missionA.Snapshot.Identity with { ContentDigest = "other" }) })
{
    try { checkpoints.Open(bytes, identity); Console.WriteLine($"checkpoint: {name} ACCEPTED (defect)"); }
    catch (CheckpointRefusedException refusal) { Console.WriteLine($"checkpoint: {name} refused {refusal.Code}"); }
}

// --- journal and replay: record inputs + per-tick hashes, replay on fresh engines, then tamper --------------------
var journalCodec = new JournalCodec().Register<MissionCommandRequested>("mission-command", e =>
    new MissionCommandRequested(e.GetProperty("CommandId").GetString()!, e.GetProperty("ControllerId").GetString()!,
        e.GetProperty("Operation").GetString()!, e.GetProperty("PayloadJson").GetString()!));
(IGameRuntime Runtime, SessionJournal Journal, Beep.SimEngine.Hosting.SimGame Game, ScenarioEngine.Contracts.IScenarioSimulation Mission) BuildJournaled(bool wrap, SessionJournal? existing = null)
{
    var (factory, tickets) = MakeSimFactory();
    var game = factory.NewGame(new Beep.SimEngine.Hosting.NewGameRequest { Scenario = "quick", Seed = 42 });
    var mission = new ScenarioSessionFactory(registry, validator, diagnostics).Create(load.Scenarios[0], new ScenarioStartOptions("journal", 42, ImmutableHashSet.Create("player")), new WorldFrame(0, 0, []));
    var journal = existing ?? new SessionJournal();
    var session = Assemble(game, mission, tickets, 0);
    session.AddComponent(new JournalComponent(journal, () => Fingerprint(game, mission)), 900);
    IGameRuntime runtime = wrap ? new JournalingRuntime(session, journal, journalCodec) : session;
    return (runtime, journal, game, mission);
}
var recordedRun = BuildJournaled(wrap: true);
recordedRun.Runtime.Init();
long micros = 0;
for (long t = 1; t <= 3; t++)
{
    if (t == 2) recordedRun.Runtime.Submit(new MissionCommandRequested("c1", "player", "test_counter.increment", "{\"amount\":1}"));
    var next = (t * 1_000_000 + 29) / 30;
    recordedRun.Runtime.Update(next - micros); micros = next;
}
var journalBytes = recordedRun.Journal.Serialize();
Console.WriteLine($"journal: {recordedRun.Journal.Inputs.Count} input(s), {recordedRun.Journal.Hashes.Count} tick hash(es), {journalBytes.Length} bytes");
var reloaded = SessionJournal.Deserialize(journalBytes);
var verdict = JournalReplay.Verify(reloaded, journalCodec, () => { var r = BuildJournaled(wrap: false, new SessionJournal()); return (r.Runtime, r.Journal); });
Console.WriteLine($"replay: matches={verdict.Matches} {verdict.Code}");
var altered = SessionJournal.Deserialize(journalBytes);
altered.Inputs.Clear();
var divergent = JournalReplay.Verify(altered, journalCodec, () => { var r = BuildJournaled(wrap: false, new SessionJournal()); return (r.Runtime, r.Journal); });
Console.WriteLine($"replay without the recorded input: matches={divergent.Matches} first divergence tick={divergent.DivergedAtTick} {divergent.Code}");
var damaged = (byte[])journalBytes.Clone(); damaged[damaged.Length / 2] ^= 1;
try { SessionJournal.Deserialize(damaged); Console.WriteLine("journal: damaged bytes ACCEPTED (defect)"); }
catch (JournalRefusedException refusal) { Console.WriteLine("journal: damaged bytes refused " + refusal.Code); }

// --- tycoon game: a mission effect credits cash; the engine's cash-changed event reaches the mission side -----------
{
    var tickets = new DirectorTickets();
    var outbox = new SimEventOutbox();
    var tycoonFactory = GameRuntime.Headless.TycoonWorld.Factory(
        GameRuntime.Headless.TycoonWorld.Definition(tickets, outbox, [new GameRuntime.Headless.CreditEffect()]));
    var tycoonGame = tycoonFactory.NewGame(new Beep.SimEngine.Hosting.NewGameRequest { Scenario = "accounts-only", Seed = 7 });
    var tycoonLog = new List<string>();
    var tycoon = new RuntimeSession(new RuntimeClock(tycoonGame.AuthoredTicksPerSecond), new EngineReporterAdapter(diagnostics));
    tycoon.AddComponent(new SimComponent(tycoonGame, null, outbox), 100);
    tycoon.AddComponent(new EffectSourceFor("sim.credit", "\"250\"", atTick: 2), 250);
    tycoon.AddComponent(new SimEffectDispatcher(tycoonGame, tickets), 300);
    tycoon.Subscribe<ObservationPosted>(e => { if (e.Kind.StartsWith("sim.event.")) tycoonLog.Add($"t{e.Tick} {e.Kind} {e.PayloadJson}"); });
    tycoon.Subscribe<CommandResolved>(e => tycoonLog.Add($"command {e.CommandId} applied={e.Applied} {e.Code}"));
    tycoon.Init();
    tycoon.Update(4 * 1_000_000L / tycoonGame.AuthoredTicksPerSecond + 10);
    foreach (var line in tycoonLog) Console.WriteLine("tycoon: " + line);
    Console.WriteLine($"tycoon: state={tycoon.State} tick={tycoon.Clock.Tick}");
}

// --- objective state: the engine announces an objective becoming active and completing ---------------------------
{
    var tickets = new DirectorTickets();
    var outbox = new SimEventOutbox();
    var definition = new Beep.SimEngine.Kernel.GameDefinition("objective-demo", new Beep.SimEngine.Kernel.GameModule[] {
        new Beep.SimEngine.Standard.ContentAccessModule(), new Beep.SimEngine.Standard.WorldSetupModule(), new QuickGame.Engine.QuickModule(),
        new Beep.SimEngine.Standard.ObjectiveModule(), new DirectorBridgeModule(tickets, new IDirectorEffect[] { new NotifyEffect() }, outbox) });
    var files = new Beep.SimEngine.Hosting.DiskFileSource(Path.Combine(AppContext.BaseDirectory, "ObjectiveContent"));
    var factory = new Beep.SimEngine.Hosting.GameFactory(definition, Beep.SimEngine.Hosting.ContentSet.Load(files, definition.ContentTypes));
    var game = factory.NewGame(new Beep.SimEngine.Hosting.NewGameRequest { Scenario = "quick", Seed = 1 });
    var session = new RuntimeSession(new RuntimeClock(game.AuthoredTicksPerSecond), new EngineReporterAdapter(diagnostics));
    session.AddComponent(new SimComponent(game, null, outbox), 100);
    session.Subscribe<ObservationPosted>(e => { if (e.Kind.StartsWith("sim.event.")) Console.WriteLine($"objective: t{e.Tick} {e.Kind} {e.PayloadJson}"); });
    session.Init();
    session.Update(4 * 1_000_000L / game.AuthoredTicksPerSecond + 10);
    Console.WriteLine($"objective: state={session.State} tick={session.Clock.Tick}");
}
// --- failure paths -----------------------------------------------------------------------------------------------
var failures = new List<string>();
var faulty = new RuntimeSession(new RuntimeClock(20), new RecordingReporter(failures));
faulty.AddComponent(new Thrower(), 1);
faulty.Init();
var faultSeen = false;
faulty.Subscribe<SessionFaulted>(_ => faultSeen = true);
faulty.Update(50_000);
Console.WriteLine($"fault: state={faulty.State} reported={failures.Count} eventDelivered={faultSeen}");
try { faulty.Init(); } catch (InvalidOperationException e) { Console.WriteLine("second Init refused: " + e.Message); }
var paused = new RuntimeSession(new RuntimeClock(20), new RecordingReporter(failures));
paused.Init();
((RuntimeClock)paused.Clock).Pause();
Console.WriteLine($"paused update ran {paused.Update(1_000_000)} ticks; capped update ran ");
((RuntimeClock)paused.Clock).Resume();
Console.WriteLine($"resumed 1s at 20 Hz ran {paused.Update(1_000_000)} ticks (cap 8), pending {paused.Clock.PendingTicks}, slowly={paused.Clock.IsRunningSlowly}");
return 0;

internal sealed class DeltaSource : IGameComponent
{
    public string Id => "delta-source";
    private IEventPort? _events;
    public void Init(in ComponentContext context) { _events = context.Events; }
    public void Update(in UpdateContext context) =>
        _events!.Emit(new ObservationPosted("test_counter.adjustment", "{\"delta\":1}", context.Tick));
}

internal sealed class DiagnosticsReporter(IEngineReporter reporter) : IRuntimeReporter
{
    public void Report(string code, string message, Exception? exception) =>
        reporter.Report(new EngineNotice(code, message, Severity.Error, null, exception));
}

internal sealed class Thrower : IGameComponent
{
    public string Id => "thrower";
    public void Init(in ComponentContext context) { }
    public void Update(in UpdateContext context) => throw new InvalidOperationException("deliberate failure");
}

internal sealed class RecordingReporter(List<string> sink) : IRuntimeReporter
{
    public void Report(string code, string message, Exception? exception) => sink.Add(code + ": " + message);
}

internal sealed class NotifyEffect : IDirectorEffect
{
    public string Name => "notify";
    public Beep.SimEngine.Code Check(Beep.SimEngine.Kernel.World world, string payloadJson) => Beep.SimEngine.Code.None;
    public void Apply(Beep.SimEngine.Kernel.World world, string payloadJson) =>
        world.Notify(new Beep.SimEngine.Code("gf.director.notice"), Beep.SimEngine.PlayerId.None, subject: payloadJson);
}

/// <summary>Stands in for a mission that issues a request, so the mission-to-world path can be shown with the Counter pack.</summary>
internal sealed class EffectSource(long atTick) : IGameComponent
{
    public string Id => "effect-source";
    private IEventPort? _events;
    public void Init(in ComponentContext context) { _events = context.Events; }
    public void Update(in UpdateContext context)
    {
        if (context.Tick == atTick) _events!.Emit(new EffectRequested("r1", "sim.notify", "\"hello from the mission\"", context.Tick));
    }
}

internal sealed class EffectSourceFor(string kind, string payload, long atTick) : IGameComponent
{
    private IEventPort? _events;
    public string Id => "effect-source-" + kind;
    public void Init(in ComponentContext context) { _events = context.Events; }
    public void Update(in UpdateContext context)
    {
        if (context.Tick == atTick) _events!.Emit(new EffectRequested("c1", kind, payload, context.Tick));
    }
}

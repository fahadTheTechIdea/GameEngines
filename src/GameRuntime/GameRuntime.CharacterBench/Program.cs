using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Text.Json;
using Beep.SimEngine;
using Beep.SimEngine.Content;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Maps;
using Beep.SimEngine.Standard;
using GameRuntime;

// A self-contained measurement of the current SimEngine with many character-like entities (a walker: tile presence and a mover; a
// soldier: those plus a worker and a work policy), created through the engine's own creation path (PrepareCreation then CommitCreation,
// inside a director command so it runs in the world's tick). No BenchmarkDotNet: allocations come from GC.GetAllocatedBytesForCurrentThread,
// collections from GC.CollectionCount and pauses from GC.GetTotalPauseDuration, all around the measured region.
return Bench.Run(args);

internal static class Bench
{
    public static int Run(string[] args)
    {
        if (args.Length == 4 && args[0] == "--march") { MarchOnly(args[1], int.Parse(args[2], CultureInfo.InvariantCulture), int.Parse(args[3], CultureInfo.InvariantCulture)); return 0; }
        if (args.Length == 4 && args[0] == "--alloc") { AllocProfile(args[1], int.Parse(args[2], CultureInfo.InvariantCulture), int.Parse(args[3], CultureInfo.InvariantCulture)); return 0; }
        var counts = new[] { 100, 1000, 5000, 10000 };
        var variants = new[] { "bench.walker", "bench.soldier" };
        var ticks = 60;
        var repeats = 3;
        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--n": counts = args[i + 1].Split(',').Select(int.Parse).ToArray(); break;
                case "--variant": variants = args[i + 1].Split(','); break;
                case "--ticks": ticks = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                case "--repeats": repeats = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                default: Console.Error.WriteLine("unknown argument " + args[i]); return 2;
            }
        }
        Console.WriteLine($"runtime={Environment.Version} serverGC={GCSettings.IsServerGC} latency={GCSettings.LatencyMode} cpus={Environment.ProcessorCount} ticks={ticks} repeats={repeats}");
        Console.WriteLine("variant,n,phase,ms,ms_per_tick,alloc_bytes,alloc_bytes_per_tick,gen0,gen1,gen2,pause_ms,extra");
        foreach (var variant in variants)
            foreach (var n in counts)
                for (var repeat = 0; repeat < repeats; repeat++)
                    Scenario(variant, n, ticks, repeat);
        return 0;
    }

    private sealed record Sample(double Ms, long Alloc, int Gen0, int Gen1, int Gen2, double PauseMs);

    private static Sample Measure(Action action)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        var pause = GC.GetTotalPauseDuration();
        var watch = Stopwatch.StartNew();
        action();
        watch.Stop();
        return new Sample(watch.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - bytes, GC.CollectionCount(0) - g0, GC.CollectionCount(1) - g1, GC.CollectionCount(2) - g2,
            (GC.GetTotalPauseDuration() - pause).TotalMilliseconds);
    }

    private static void Print(string variant, int n, string phase, Sample s, int ticks, string extra = "")
    {
        var perTick = ticks > 0 ? s.Ms / ticks : 0;
        var allocPerTick = ticks > 0 ? s.Alloc / ticks : 0;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{variant},{n},{phase},{s.Ms:F2},{perTick:F3},{s.Alloc},{allocPerTick},{s.Gen0},{s.Gen1},{s.Gen2},{s.PauseMs:F2},{extra}"));
    }

    private sealed class Driver(SimGame game, DirectorTickets tickets)
    {
        private int _key;
        public void Command(string effect, string payload)
        {
            var key = "k" + _key++;
            tickets.Issue(key, effect, payload);
            game.Seat(0).Submit(new DirectorEffectCommand { Key = key, Effect = effect, Payload = payload });
        }

        public void Tick(int count) { for (var i = 0; i < count; i++) game.Update(game.Tick + 1); }
        public SimGame Game => game;
    }

    private static void Scenario(string variant, int n, int ticks, int repeat)
    {
        var tickets = new DirectorTickets();
        var spawn = new SpawnEffect();
        var definition = new GameDefinition("bench", new GameModule[] {
            new ContentAccessModule(), new WorldSetupModule(), new InventoryModule(), new MotionModule(), new TileNavigationModule(), new WorkModule(), new CharacterModule(), new BenchModule(),
            new DirectorBridgeModule(tickets, new IDirectorEffect[] { spawn, new DespawnEffect(), new MoveEffect(), new BenchCasualties() }.Concat(CharacterEffects.All()).ToArray(), null) });
        var files = new DiskFileSource(System.IO.Path.Combine(AppContext.BaseDirectory, "Content"));
        var content = ContentSet.Load(files, definition.ContentTypes);
        var factory = new GameFactory(definition, content);
        SimGame game = null!;
        var start = Measure(() => { game = factory.NewGame(new NewGameRequest { Scenario = "bench", Seed = 7 }); game.Initialize(); });
        if (repeat == 0) Print(variant, n, "startup", start, 0);
        var driver = new Driver(game, tickets);
        driver.Tick(2);

        var before = GC.GetTotalMemory(true);
        var create = Measure(() => { Spawn(driver, variant, n); driver.Tick(1); });
        var after = GC.GetTotalMemory(true);
        var live = game.Seat(0).View.EntityCount;
        var expected = variant == "squad" ? n / 100 : n;
        if (live != expected) throw new InvalidOperationException($"expected {expected} entities but the world has {live}");
        Print(variant, n, "create", create, 1, $"retained_bytes_per_entity={(after - before) / (double)expected:F0},retained_bytes_per_character={(after - before) / (double)n:F0},entities={live}");

        Print(variant, n, "tick-idle", Measure(() => driver.Tick(ticks)), ticks);

        driver.Command("move", "{}");
        driver.Tick(1);
        Print(variant, n, "tick-moving", Measure(() => driver.Tick(ticks)), ticks);

        var k = Math.Max(1, n / 20);
        Print(variant, n, "churn", Measure(() =>
        {
            for (var i = 0; i < ticks; i++)
            {
                if (variant == "squad") driver.Command("bench-casualties", "{}"); else driver.Command("despawn", "{\"count\":" + k.ToString(CultureInfo.InvariantCulture) + "}");
                if (variant != "squad") Spawn(driver, variant, k);
                driver.Tick(1);
            }
        }), ticks, $"per_tick_replaced={k}");
        if (game.Seat(0).View.EntityCount != expected) throw new InvalidOperationException("churn changed the population");

        ulong hash = 0;
        Print(variant, n, "hash", Measure(() => hash = game.Hash()), 0, $"hash={hash:X16}");
        byte[] save = Array.Empty<byte>();
        Print(variant, n, "save", Measure(() => save = game.Save()), 0, $"bytes={save.Length}");
        SimGame loaded = null!;
        Print(variant, n, "load", Measure(() => loaded = factory.Load(save)), 0);
        if (loaded.Hash() != hash) throw new InvalidOperationException("loaded world hash differs");
    }

    /// <summary>Which types the engine allocates during idle ticks, from the runtime's sampled allocation-tick events (about one per 100 KB).</summary>
    private static void AllocProfile(string variant, int n, int ticks)
    {
        var tickets = new DirectorTickets();
        var definition = new GameDefinition("bench", new GameModule[] {
            new ContentAccessModule(), new WorldSetupModule(), new InventoryModule(), new MotionModule(), new TileNavigationModule(), new WorkModule(), new CharacterModule(), new BenchModule(),
            new DirectorBridgeModule(tickets, new IDirectorEffect[] { new SpawnEffect(), new DespawnEffect(), new MoveEffect(), new BenchCasualties() }.Concat(CharacterEffects.All()).ToArray(), null) });
        var files = new DiskFileSource(System.IO.Path.Combine(AppContext.BaseDirectory, "Content"));
        var factory = new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes));
        var game = factory.NewGame(new NewGameRequest { Scenario = "bench", Seed = 7 });
        game.Initialize();
        var driver = new Driver(game, tickets);
        driver.Tick(2);
        Spawn(driver, variant, n);
        driver.Tick(1);
        driver.Tick(5);   // warm up
        using var listener = new AllocListener();
        driver.Tick(ticks);
        GC.Collect();
        Console.WriteLine($"allocation sample for {variant} n={n} ticks={ticks}: type, sampled bytes, share");
        var total = listener.Bytes.Values.Sum();
        foreach (var (type, bytes) in listener.Bytes.OrderByDescending(p => p.Value).Take(25))
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{type},{bytes},{100.0 * bytes / Math.Max(1, total):F1}%"));
    }

    private sealed class AllocListener : System.Diagnostics.Tracing.EventListener
    {
        public Dictionary<string, long> Bytes { get; } = new();
        protected override void OnEventSourceCreated(System.Diagnostics.Tracing.EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime") EnableEvents(source, System.Diagnostics.Tracing.EventLevel.Verbose, (System.Diagnostics.Tracing.EventKeywords)0x1);
        }
        protected override void OnEventWritten(System.Diagnostics.Tracing.EventWrittenEventArgs e)
        {
            if (e.EventName == null || !e.EventName.StartsWith("GCAllocationTick", StringComparison.Ordinal) || e.PayloadNames == null || e.Payload == null) return;
            var type = e.Payload[e.PayloadNames.IndexOf("TypeName")] as string ?? "?";
            var amount = Convert.ToInt64(e.Payload[e.PayloadNames.IndexOf("AllocationAmount64")], CultureInfo.InvariantCulture);
            lock (Bytes) Bytes[type] = Bytes.GetValueOrDefault(type) + amount;
        }
    }

    /// <summary>Spawns <paramref name="count"/> characters the way the variant means it: raw entities, characters from the factory, or squads of one hundred.</summary>
    private static void Spawn(Driver driver, string variant, int count)
    {
        if (variant == "squad")
            driver.Command("characters.spawn-formation", "{\"formation\":\"bench.company\",\"squads\":" + (count / 100).ToString(CultureInfo.InvariantCulture) + ",\"company\":0,\"seed\":1,\"layout\":{\"x\":0,\"y\":0,\"columns\":10,\"spacing\":1}}");
        else if (variant == "factory-individual")
            driver.Command("characters.spawn-individuals", "{\"archetype\":\"bench.person\",\"count\":" + count.ToString(CultureInfo.InvariantCulture) + ",\"company\":0,\"seed\":1,\"layout\":{\"x\":0,\"y\":0,\"columns\":100,\"spacing\":1}}");
        else
            driver.Command("spawn", Payload(variant, count));
    }

    /// <summary>One casualty in every squad: the squad analogue of replacing part of a population each tick.</summary>
    private sealed class BenchCasualties : IDirectorEffect
    {
        public string Name => "bench-casualties";
        public Code Check(World world, string payloadJson) => Code.None;
        public void Apply(World world, string payloadJson)
        {
            var factory = world.Services.Get<ICharacterFactory>();
            foreach (var entity in world.Entities.All.ToList())
                if (entity.Get<ISquad>() is { Aggregate: > 0 }) factory.Casualties(entity.Id, 1);
        }
    }

    /// <summary>Only the marching phase, repeated for a long run, so an external sampling profiler sees little else.</summary>
    private static void MarchOnly(string variant, int n, int ticks)
    {
        var tickets = new DirectorTickets();
        var definition = new GameDefinition("bench", new GameModule[] {
            new ContentAccessModule(), new WorldSetupModule(), new InventoryModule(), new MotionModule(), new TileNavigationModule(), new WorkModule(), new CharacterModule(), new BenchModule(),
            new DirectorBridgeModule(tickets, new IDirectorEffect[] { new SpawnEffect(), new DespawnEffect(), new MoveEffect(), new BenchCasualties() }.Concat(CharacterEffects.All()).ToArray(), null) });
        var files = new DiskFileSource(System.IO.Path.Combine(AppContext.BaseDirectory, "Content"));
        var factory = new GameFactory(definition, ContentSet.Load(files, definition.ContentTypes));
        var game = factory.NewGame(new NewGameRequest { Scenario = "bench", Seed = 7 });
        game.Initialize();
        var driver = new Driver(game, tickets);
        driver.Tick(2);
        Spawn(driver, variant, n);
        driver.Tick(1);
        driver.Command("move", "{}");
        driver.Tick(1);
        var sample = Measure(() => driver.Tick(ticks));
        Print(variant, n, "march-only", sample, ticks);
    }

    private static string Payload(string variant, int count) => "{\"def\":\"" + variant + "\",\"count\":" + count.ToString(CultureInfo.InvariantCulture) + "}";

    private sealed class BenchModule : GameModule
    {
        public override string Id => "bench";
        public override ModuleVersion Version => new(1, 0, 0);
        public override IReadOnlyList<ModuleRequirement> Requires => new[] {
            new ModuleRequirement(ContentAccessModule.ModuleId, new ModuleVersion(1, 0, 0)),
            new ModuleRequirement(WorldSetupModule.ModuleId, new ModuleVersion(1, 0, 0)) };
    }

    /// <summary>Creates entities the way the engine does elsewhere: admission with PrepareCreation, then CommitCreation.</summary>
    private sealed class SpawnEffect : IDirectorEffect
    {
        private int _next;
        public string Name => "spawn";
        public Code Check(World world, string payloadJson) => Code.None;
        public void Apply(World world, string payloadJson)
        {
            using var document = JsonDocument.Parse(payloadJson);
            var def = world.Content.Find<EntityDef>(document.RootElement.GetProperty("def").GetString()!) ?? throw new InvalidOperationException("no such def");
            var count = document.RootElement.GetProperty("count").GetInt32();
            for (var i = 0; i < count; i++)
            {
                var slot = _next++;
                var tile = new TileCoord(slot % 128, slot / 128 % 128);
                var placement = new TilePlacement("lab.tiles", tile, FixVec2.Centre(tile));
                var refusal = world.PrepareCreation(def, new PlayerId(0), EntityId.None, placement, out var creation);
                if (!refusal.IsNone) throw new InvalidOperationException("creation refused: " + refusal.Id);
                world.CommitCreation(creation);
            }
        }
    }

    private sealed class DespawnEffect : IDirectorEffect
    {
        public string Name => "despawn";
        public Code Check(World world, string payloadJson) => Code.None;
        public void Apply(World world, string payloadJson)
        {
            using var document = JsonDocument.Parse(payloadJson);
            var count = document.RootElement.GetProperty("count").GetInt32();
            var victims = world.Entities.All.Take(count).ToList();
            foreach (var victim in victims) world.Despawn(victim);
        }
    }

    private sealed class MoveEffect : IDirectorEffect
    {
        public string Name => "move";
        public Code Check(World world, string payloadJson) => Code.None;
        public void Apply(World world, string payloadJson)
        {
            var i = 0;
            foreach (var entity in world.Entities.All)
            {
                i++;
                entity.Get<IMobility>()?.GoTo(new TileRect(i * 37 % 128, i * 53 % 128, 1, 1));
            }
        }
    }
}

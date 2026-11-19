using EngineCommon;
using GameRuntime;
using Xunit;

namespace EngineCommon.Tests;

/// <summary>The one channel registry, driven through two test-only protocols so that nothing depends on a particular engine.</summary>
public sealed class ChannelRegistryTests
{
    internal readonly record struct PingRequested(string RequestId, string Channel);
    internal readonly record struct PingRefused(string RequestId, long Tick, string Code, string Channel);
    internal readonly record struct PongRequested(string RequestId, string Channel);
    internal readonly record struct PongRefused(string RequestId, long Tick, string Code, string Channel);

    private sealed class Faults : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    private sealed class PingChannels : ChannelRegistry
    {
        public PingChannels(int rank = 0) : base(new ChannelProtocol(
            rank, "ping-channels", "PING_UNKNOWN", "PING_DUPLICATE",
            session =>
            {
                session.DeclareEvent<PingRequested>("ping.requested", 1);
                session.DeclareEvent<PingRefused>("ping.refused", 1);
            },
            (events, handler) => events.Connect<PingRequested>(r => handler(r.Channel, r.RequestId)),
            (events, requestId, tick, channel) => events.Emit(new PingRefused(requestId, tick, "PING_UNKNOWN", channel)))) { }
    }

    private sealed class PongChannels : ChannelRegistry
    {
        public PongChannels(int rank = 1) : base(new ChannelProtocol(
            rank, "pong-channels", "PONG_UNKNOWN", "PONG_DUPLICATE",
            session =>
            {
                session.DeclareEvent<PongRequested>("pong.requested", 1);
                session.DeclareEvent<PongRefused>("pong.refused", 1);
            },
            (events, handler) => events.Connect<PongRequested>(r => handler(r.Channel, r.RequestId)),
            (events, requestId, tick, channel) => events.Emit(new PongRefused(requestId, tick, "PONG_UNKNOWN", channel)))) { }
    }

    private sealed class Server(ChannelRegistry channels, string id, string channel) : IGameComponent
    {
        public string Id => id;
        public void Init(in ComponentContext context) => channels.Serve(channel, id);
        public void Update(in UpdateContext context) { }
    }

    private static RuntimeSession NewSession() => new(new RuntimeClock(50, 8), new Faults());

    private static void Tick(RuntimeSession session) => session.Update(1_000_000 / 50);

    [Fact]
    public void ARequestOnAChannelNobodyServesIsRefusedInTheSameTickWithTheEnginesOwnEvent()
    {
        var session = NewSession();
        var ping = new PingChannels();
        ping.Install(session);
        session.AddComponent(new Server(ping, "server", "a"), 10);
        var refusals = new List<PingRefused>();
        session.Subscribe<PingRefused>(refusals.Add);
        session.Init();
        session.Submit(new PingRequested("r1", "a"));
        session.Submit(new PingRequested("r2", "b"));
        Tick(session);
        var refused = Assert.Single(refusals);
        Assert.Equal(new PingRefused("r2", 1, "PING_UNKNOWN", "b"), refused);
        Assert.Equal(1, ping.Refused);
        Assert.True(ping.IsQuiet);
        Assert.Equal("channels=a refused=1 pending=0", ping.Fingerprint());
    }

    [Fact]
    public void TwoRegistriesOnOneSessionEachRefuseOnlyTheirOwnEnginesRequests()
    {
        var session = NewSession();
        var ping = new PingChannels();
        var pong = new PongChannels();
        ping.Install(session);
        pong.Install(session);
        var pings = new List<PingRefused>();
        var pongs = new List<PongRefused>();
        session.Subscribe<PingRefused>(pings.Add);
        session.Subscribe<PongRefused>(pongs.Add);
        session.Init();
        session.Submit(new PingRequested("p", ""));
        session.Submit(new PongRequested("q", ""));
        Tick(session);
        Assert.Equal(["p"], pings.Select(e => e.RequestId));
        Assert.Equal(["q"], pongs.Select(e => e.RequestId));
    }

    [Fact]
    public void ARequestRaisedByAnOrdinaryComponentDuringTheTickIsRefusedInThatSameTick()
    {
        var session = NewSession();
        var ping = new PingChannels(0);
        var pong = new PongChannels(1);
        pong.Install(session);
        ping.Install(session);
        session.AddComponent(new Raiser(), int.MaxValue - 100);
        var refusals = new List<PingRefused>();
        session.Subscribe<PingRefused>(refusals.Add);
        session.Init();
        Tick(session);
        Assert.Equal([new PingRefused("raised", 1, "PING_UNKNOWN", "elsewhere")], refusals);
    }

    private sealed class Raiser : IGameComponent
    {
        private IEventPort? _events;
        public string Id => "raiser";
        public void Init(in ComponentContext context) => _events = context.Events;
        public void Update(in UpdateContext context) => _events!.Emit(new PingRequested("raised", "elsewhere"));
    }

    [Fact]
    public void TwoRegistriesOfOneRankCannotShareASession()
    {
        var session = NewSession();
        new PingChannels(3).Install(session);
        var refusal = Assert.Throws<InvalidOperationException>(() => new PongChannels(3).Install(session));
        Assert.Contains("share order", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARegistryRankBelowZeroIsRefusedWhenTheRegistryIsBuilt() =>
        Assert.Throws<ArgumentException>(() => new PingChannels(-1));

    [Fact]
    public void ASecondComponentOnAServedChannelIsRefusedAtInitWithTheEnginesDuplicateCode()
    {
        var session = NewSession();
        var ping = new PingChannels();
        ping.Install(session);
        session.AddComponent(new Server(ping, "one", "same"), 10);
        session.AddComponent(new Server(ping, "two", "same"), 11);
        var refusal = Assert.Throws<EngineRefusedException>(session.Init);
        Assert.Equal("PING_DUPLICATE", refusal.Code);
        Assert.Contains("'one'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("'two'", refusal.Message, StringComparison.Ordinal);
    }

    private sealed class CapturedPort : IEventPort
    {
        public void Emit<T>(in T evt) where T : struct { }
        public void Connect<T>(Action<T> handler) where T : struct { }
    }

    [Fact]
    public void ARegistryServesOneSessionAndKnowsWhichOne()
    {
        var session = NewSession();
        var ping = new PingChannels();
        ping.Install(session);
        session.Init();
        var elsewhere = new CapturedPort();
        Assert.False(ping.RunsOn(elsewhere));
        Assert.Throws<InvalidOperationException>(() => ping.Init(new ComponentContext(elsewhere)));
    }

    [Fact]
    public void ARegistryThatWasNeverInitialisedRunsOnNoSessionAndCannotUpdate()
    {
        var ping = new PingChannels();
        Assert.False(ping.RunsOn(new CapturedPort()));
        Assert.Throws<InvalidOperationException>(() => ping.Update(new UpdateContext(1, 20_000, 0, 20_000)));
    }

    [Fact]
    public void ARefusalWaitingForTheRegistryMakesItNotQuietAndTheServedChannelsAreSorted()
    {
        var session = NewSession();
        var ping = new PingChannels();
        ping.Install(session);
        session.AddComponent(new Server(ping, "zeta-server", "zeta"), 10);
        session.AddComponent(new Server(ping, "alpha-server", "alpha"), 11);
        var quiet = new List<bool>();
        session.AddComponent(new Probe(ping, quiet), 20);
        session.Init();
        session.Submit(new PingRequested("r", "missing"));
        Tick(session);
        Assert.Equal([false], quiet);   // the probe ran after delivery and before the registry answered
        Assert.True(ping.IsQuiet);
        Assert.Equal(["alpha", "zeta"], ping.Served.Keys);
        Assert.Equal(["alpha-server", "zeta-server"], ping.Served.Values);
    }

    private sealed class Probe(ChannelRegistry registry, List<bool> quiet) : IGameComponent
    {
        public string Id => "probe";
        public void Init(in ComponentContext context) { }
        public void Update(in UpdateContext context) => quiet.Add(registry.IsQuiet);
    }

    [Fact]
    public void RefusalsAreWrittenInTheOrderTheRequestsArrived()
    {
        var session = NewSession();
        var ping = new PingChannels();
        ping.Install(session);
        var ids = new List<string>();
        session.Subscribe<PingRefused>(e => ids.Add(e.RequestId));
        session.Init();
        foreach (var id in new[] { "c", "a", "b" }) session.Submit(new PingRequested(id, "none"));
        Tick(session);
        Assert.Equal(["c", "a", "b"], ids);
        Assert.Equal(3, ping.Refused);
    }
}

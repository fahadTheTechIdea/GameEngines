using Xunit;

namespace GameRuntime.Tests;

public sealed class RuntimeTests
{
    private sealed class Recorder(string id, List<string> log, Action<UpdateContext, IEventPort>? onUpdate = null) : IGameComponent
    {
        private IEventPort? _events;
        public string Id => id;
        public void Init(in ComponentContext context)
        {
            _events = context.Events;
            context.Events.Connect<ObservationPosted>(e => log.Add($"{id} heard {e.Kind}@{e.Tick}"));
        }
        public void Update(in UpdateContext context)
        {
            log.Add($"{id} t{context.Tick}");
            onUpdate?.Invoke(context, _events!);
        }
    }

    private sealed class Reporter(List<string> sink) : IRuntimeReporter
    {
        public void Report(string code, string message, Exception? exception) => sink.Add(code);
    }

    private static RuntimeSession Session(int rate, List<string> failures) => new(new RuntimeClock(rate), new Reporter(failures));

    [Fact]
    public void ComponentsUpdateInOrderAndEventsArriveAfterTheEmitterReturns()
    {
        var log = new List<string>();
        var session = Session(10, []);
        session.AddComponent(new Recorder("b", log), 200);
        session.AddComponent(new Recorder("a", log, (c, e) => e.Emit(new ObservationPosted("x", "{}", c.Tick))), 100);
        session.Init();
        session.Update(100_000);
        Assert.Equal("a t1", log[0]);
        Assert.True(log.IndexOf("a heard x@1") > log.IndexOf("a t1"));
        Assert.True(log.IndexOf("b t1") > log.IndexOf("a heard x@1"));
    }

    [Fact]
    public void SameElapsedTimeInDifferentFramesGivesTheSameTicks()
    {
        var one = Session(30, []); one.Init();
        var many = Session(30, []); many.Init();
        one.Update(200_000);
        for (var i = 0; i < 20; i++) many.Update(10_000);
        Assert.Equal(one.Clock.Tick, many.Clock.Tick);
    }

    [Fact]
    public void PauseFreezesAndCapKeepsBacklog()
    {
        var session = Session(20, []);
        session.Init();
        ((RuntimeClock)session.Clock).Pause();
        Assert.Equal(0, session.Update(1_000_000));
        ((RuntimeClock)session.Clock).Resume();
        Assert.Equal(8, session.Update(1_000_000));
        Assert.True(session.Clock.IsRunningSlowly);
        Assert.Equal(12, session.Clock.PendingTicks);
    }

    [Fact]
    public void HandlerExceptionFaultsOnceAndStopsTheSession()
    {
        var failures = new List<string>();
        var session = Session(10, failures);
        session.AddComponent(new Recorder("a", [], (_, _) => throw new InvalidOperationException("boom")), 1);
        session.Init();
        session.Update(100_000);
        Assert.Equal(RuntimeState.Faulted, session.State);
        Assert.Single(failures);
        Assert.Equal(0, session.Update(100_000));
    }

    [Fact]
    public void InitSucceedsOnceAndUpdateBeforeInitIsRefused()
    {
        var session = Session(10, []);
        Assert.Throws<InvalidOperationException>(() => session.Update(1));
        session.Init();
        Assert.Throws<InvalidOperationException>(() => session.Init());
    }

    [Fact]
    public void JournalDetectsTamperingAndRoundTrips()
    {
        var journal = new SessionJournal();
        journal.Inputs.Add(new SessionJournal.Input(2, "t", "{}"));
        journal.Hashes.Add(new SessionJournal.Hash(1, "abc"));
        var bytes = journal.Serialize();
        Assert.Equal("abc", SessionJournal.Deserialize(bytes).Hashes[0].Value);
        var damaged = (byte[])bytes.Clone();
        damaged[damaged.Length / 2] ^= 1;
        Assert.Throws<JournalRefusedException>(() => SessionJournal.Deserialize(damaged));
    }
}

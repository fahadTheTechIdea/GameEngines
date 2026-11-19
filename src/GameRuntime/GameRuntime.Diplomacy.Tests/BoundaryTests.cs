using CalendarEngine;
using DiplomacyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace GameRuntime.Diplomacy.Tests;

/// <summary>Written from the survivors of the first round of mutation checks: the counts a standing answer gives, the facts a replay starts with, and the first step of a calendar clock.</summary>
public sealed class BoundaryTests
{
    private static DiplomacyStandingAnswered Standing(DiplomacyHost host, string realm)
    {
        var before = host.Of<DiplomacyStandingAnswered>().Count();
        host.Ask("standing", realm);
        host.Tick();
        var answers = host.Of<DiplomacyStandingAnswered>().ToList();
        Assert.Equal(before + 1, answers.Count);
        return answers[^1];
    }

    [Fact]
    public void AStandingAnswerCountsTheTreatiesOfBothPartiesAndTheWarsOfBothSides()
    {
        var host = new DiplomacyHost();
        host.Tick(2);
        host.Runtime.Propose("aurel", "dune", "nap", 120, 0);
        host.Tick(40);
        var treaty = Assert.Single(host.Runtime.Treaties);
        Assert.Equal((host.Runtime.Set.RealmById["aurel"].Index, host.Runtime.Set.RealmById["dune"].Index), (treaty.First, treaty.Second));
        host.Runtime.DeclareWar("brass", "ember", "");
        host.Tick(2);
        var war = Assert.Single(host.Runtime.Wars);
        Assert.NotEqual(0UL, war.AttackerSide);
        Assert.NotEqual(0UL, war.DefenderSide);
        Assert.Equal((1, 0), (Standing(host, "aurel").Treaties, Standing(host, "aurel").Wars));      // the first party of the treaty
        Assert.Equal((1, 0), (Standing(host, "dune").Treaties, Standing(host, "dune").Wars));        // the second party
        Assert.Equal((0, 0), (Standing(host, "cinder").Treaties, Standing(host, "cinder").Wars));
        Assert.Equal((0, 1), (Standing(host, "brass").Treaties, Standing(host, "brass").Wars));      // the attacker
        Assert.Equal((0, 1), (Standing(host, "ember").Treaties, Standing(host, "ember").Wars));      // the defender
    }

    [Fact]
    public void AReplayStartsWithTheFactsTheJournalHeldAtTheTickItStartsFrom()
    {
        var set = Samples.Calm();
        var host = new DiplomacyHost(set, journal: true);
        host.Tick(1);
        Assert.True(host.Component.IsQuiet && host.Channels.IsQuiet);
        var checkpoint = DiplomacyCheckpoint.Capture(host.Component);
        var checkpointTick = host.Component.Tick;
        var linesAtCheckpoint = host.Transcript.Lines.Count;
        host.Tick(40);                                            // the facts never change again: the only entry that holds them is the one at the checkpoint's own tick
        var entries = DiplomacyJournal.Parse(host.Journal.ToText());
        Assert.Single(entries, e => e.Facts is not null);
        Assert.Equal(checkpointTick, entries.Single(e => e.Facts is not null).Tick);
        var replay = DiplomacyReplay.Run(DiplomacyCheckpoint.Open(set, checkpoint), entries, checkpointTick, host.Component.Tick, DiplomacyHost.TicksPerSecond);
        Assert.Equal(host.Runtime.StateDigest(), replay.StateDigest);
        Assert.Equal(host.Transcript.Lines.Count - linesAtCheckpoint, replay.Events);
    }

    [Fact]
    public void TheCalendarClockBeginsAtStepZeroOnTheOriginAndIsBeforeItJustBeforeThat()
    {
        var gregorian = CalendarLoader.Load(Samples.Read("gregorian.calendar.json"));
        var set = DipLoader.Load(Samples.Read("isles.diplomacy.json"));
        long Step(long origin)
        {
            var start = gregorian.System.InstantOf(new CalendarDate(2024, gregorian.System.Months.First(m => m.Id == "march").Index, 1), 0, 0);
            var session = new RuntimeSession(new RuntimeClock(50, 8), new Reporter());
            var channels = new CalendarChannels();
            channels.Install(session);
            var calendar = new CalendarComponent(CalendarRuntime.Create(gregorian, start, 60, 1), () => new Dictionary<string, FactValue>(StringComparer.Ordinal), channels);
            return DiplomacyCalendar.Clock(calendar, set, origin).StepAt(0);
        }
        var instant = gregorian.System.InstantOf(new CalendarDate(2024, gregorian.System.Months.First(m => m.Id == "march").Index, 1), 0, 0);
        Assert.Equal(0, Step(instant));
        Assert.Equal(-1, Step(instant + 1));
    }
}

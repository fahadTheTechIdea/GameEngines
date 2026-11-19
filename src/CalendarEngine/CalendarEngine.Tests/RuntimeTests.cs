using System.Text.Json.Nodes;
using EngineCommon;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>The calendar state: the clock fed by game ticks (exact rational scale, pause, backlog, end of time, skips), the actors, their interrupts and what has been announced.</summary>
public sealed class RuntimeTests
{
    private static CalendarSet Set => Fixture.Gregorian;

    private static CalendarRuntime Runtime(string month = "january", int day = 1, int hour = 0, long scaleNumerator = 1, long scaleDenominator = 1, long year = 2024) =>
        CalendarRuntime.Create(Set, Fixture.At(Set, year, month, day, hour), scaleNumerator, scaleDenominator);

    [Fact]
    public void EachGameTickMovesTheInstantByTheScale()
    {
        var runtime = Runtime();
        var start = runtime.Instant;
        var step = runtime.Step(1);
        Assert.Equal(start + 1, runtime.Instant);
        Assert.Equal((start, start + 1, 0L, false, false), (step!.From, step.To, step.DaysCrossed, step.Clamped, step.DayPartChanged));
        runtime.Step(11);
        Assert.Equal(start + 11, runtime.Instant);
        Assert.Equal(11, runtime.LastTick);
    }

    [Fact]
    public void TheSameTickAgainMovesNothing()
    {
        var runtime = Runtime();
        runtime.Step(5);
        Assert.Null(runtime.Step(5));
        Assert.Equal(Fixture.At(Set, 2024, "january", 1) + 5, runtime.Instant);
    }

    [Fact]
    public void AFractionalScaleLosesNothingHoweverTheTicksAreSplit()
    {
        const long total = 1000;
        var once = Runtime(scaleNumerator: 7, scaleDenominator: 3);
        once.Step(total);
        Assert.Equal(Fixture.At(Set, 2024, "january", 1) + total * 7 / 3, once.Instant);
        Assert.Equal(total * 7 % 3, once.ScaleRemainder);
        for (var seed = 0; seed < 40; seed++)
        {
            var split = Runtime(scaleNumerator: 7, scaleDenominator: 3);
            long tick = 0;
            while (tick < total)
            {
                tick = Math.Min(total, tick + 1 + (long)DeterministicRoll.Below((ulong)seed, "split", tick, 37));
                split.Step(tick);
            }
            Assert.Equal((once.Instant, once.ScaleRemainder, once.Backlog), (split.Instant, split.ScaleRemainder, split.Backlog));
            Assert.Equal(CalendarSave.StateDigest(once), CalendarSave.StateDigest(split));
        }
    }

    [Fact]
    public void ASlowScaleAdvancesOnlyWhenAWholeTickIsEarned()
    {
        var runtime = Runtime(scaleNumerator: 1, scaleDenominator: 60);
        var start = runtime.Instant;
        for (var tick = 1; tick < 60; tick++) Assert.Null(runtime.Step(tick));
        Assert.Equal(start, runtime.Instant);
        Assert.NotNull(runtime.Step(60));
        Assert.Equal(start + 1, runtime.Instant);
        Assert.Equal(0, runtime.ScaleRemainder);
    }

    [Fact]
    public void ChangingTheScaleDropsTheFractionEarnedAtTheOldSpeed()
    {
        var runtime = Runtime(scaleNumerator: 1, scaleDenominator: 2);
        runtime.Step(1);
        Assert.Equal(1, runtime.ScaleRemainder);
        runtime.SetScale(3, 1);
        Assert.Equal(0, runtime.ScaleRemainder);
        runtime.Step(2);
        Assert.Equal(Fixture.At(Set, 2024, "january", 1) + 3, runtime.Instant);
    }

    [Fact]
    public void AScaleOutsideTheLimitsIsRefusedAndChangesNothing()
    {
        var runtime = Runtime();
        var digest = CalendarSave.StateDigest(runtime);
        var most = CalendarRuntime.MaxScaleDays * Set.Time.TicksPerDay;
        Assert.Equal(CalendarCodes.Scale, Fixture.CodeOf(() => runtime.SetScale(0, 1)));
        Assert.Equal(CalendarCodes.Scale, Fixture.CodeOf(() => runtime.SetScale(1, 0)));
        Assert.Equal(CalendarCodes.Scale, Fixture.CodeOf(() => runtime.SetScale(1, CalendarRuntime.MaxScaleDenominator + 1)));
        Assert.Equal(CalendarCodes.Scale, Fixture.CodeOf(() => runtime.SetScale(most + 1, 1)));
        Assert.Equal(CalendarCodes.Scale, Fixture.CodeOf(() => runtime.SetScale(-5, 1)));
        runtime.SetScale(most, 1);
        Assert.Equal(digest.Length, CalendarSave.StateDigest(runtime).Length);
        Assert.Equal(CalendarCodes.Scale, Fixture.CodeOf(() => CalendarRuntime.Create(Set, 0, 0, 1)));
    }

    [Fact]
    public void WhilePausedNoTimeIsEarnedAndNoneIsOwedAfterwards()
    {
        var runtime = Runtime();
        var start = runtime.Instant;
        runtime.Step(10);
        runtime.SetPaused(true);
        Assert.Null(runtime.Step(1000));
        Assert.Equal(start + 10, runtime.Instant);
        Assert.Equal(1000, runtime.LastTick);
        runtime.SetPaused(false);
        runtime.Step(1001);
        Assert.Equal(start + 11, runtime.Instant);
    }

    [Fact]
    public void TicksGoForwardAndNotMoreThanTheLimitAtOnce()
    {
        var runtime = Runtime();
        runtime.Step(10);
        Assert.Equal(CalendarCodes.TickOrder, Fixture.CodeOf(() => runtime.Step(9)));
        Assert.Equal(CalendarCodes.TickOrder, Fixture.CodeOf(() => runtime.Step(10 + CalendarRuntime.MaxElapsedTicks + 1)));
        Assert.Equal(10, runtime.LastTick);
        Assert.Equal(CalendarCodes.TickOrder, Fixture.CodeOf(() => CalendarRuntime.Create(Set, 0, 1, 1, -1)));
    }

    [Fact]
    public void WhatIsOwedBeyondOneStepIsPaidInTheNextSteps()
    {
        var most = CalendarRuntime.MaxScaleDays * Set.Time.TicksPerDay;
        var runtime = Runtime(scaleNumerator: most, scaleDenominator: 1);
        var start = runtime.Instant;
        var perStep = CalendarRuntime.MaxAdvanceDays * Set.Time.TicksPerDay;
        // Ten game ticks earn 3,660 days; a step applies at most 400 days.
        var first = runtime.Step(10)!;
        Assert.Equal(perStep, first.To - first.From);
        Assert.Equal(10 * most - perStep, runtime.Backlog);
        var steps = 1;
        while (runtime.Backlog > 0)
        {
            var step = runtime.Step(10)!;
            Assert.InRange(step.To - step.From, 1, perStep);
            steps++;
        }
        Assert.Equal(start + 10 * most, runtime.Instant);
        Assert.Equal(10, steps);
        Assert.Null(runtime.Step(10));
    }

    [Fact]
    public void TimeStopsAtTheLastSupportedInstantAndSaysSo()
    {
        var runtime = CalendarRuntime.Create(Set, Set.System.MaxInstant - 100, 1000, 1);
        var step = runtime.Step(1)!;
        Assert.True(step.Clamped);
        Assert.Equal(Set.System.MaxInstant, runtime.Instant);
        Assert.Equal(0, runtime.Backlog);
        Assert.Null(runtime.Step(2));
        Assert.Null(runtime.Step(3));
        Assert.Equal(Set.System.MaxInstant, runtime.Instant);
    }

    [Fact]
    public void TimeEarnedExactlyUpToTheLastInstantIsNotReportedAsDropped()
    {
        var runtime = CalendarRuntime.Create(Set, Set.System.MaxInstant - 100, 1, 1);
        var step = runtime.Step(100)!;
        Assert.False(step.Clamped);
        Assert.Equal(Set.System.MaxInstant, runtime.Instant);
        Assert.True(runtime.Step(101) is null);
    }

    [Fact]
    public void ASkipGoesForwardToAnInstantWithinAYearAndIgnoresPause()
    {
        var runtime = Runtime();
        runtime.SetPaused(true);
        var target = Fixture.At(Set, 2024, "january", 2, 6);
        var step = runtime.SkipTo(target);
        Assert.Equal(target, runtime.Instant);
        Assert.Equal(1, step.DaysCrossed);
        Assert.Equal(CalendarCodes.Skip, Fixture.CodeOf(() => runtime.SkipTo(target)));
        Assert.Equal(CalendarCodes.Skip, Fixture.CodeOf(() => runtime.SkipTo(target - 1)));
        Assert.Equal(CalendarCodes.Skip, Fixture.CodeOf(() => runtime.SkipTo(target + (CalendarRuntime.MaxSkipDays + 1) * Set.Time.TicksPerDay)));
        Assert.Equal(CalendarCodes.InstantRange, Fixture.CodeOf(() => runtime.SkipTo(Set.System.MaxInstant + 1)));
        runtime.SkipTo(target + CalendarRuntime.MaxSkipDays * Set.Time.TicksPerDay);
    }

    [Fact]
    public void AStepReportsTheSeasonsAndHolidaysItCrossedInOrder()
    {
        var runtime = Runtime("february", 28, 23);
        var step = runtime.SkipTo(Fixture.At(Set, 2024, "march", 1, 0));
        Assert.Equal(2, step.DaysCrossed);
        Assert.True(step.DayPartChanged);
        var march1 = Set.System.DayNumber(new CalendarDate(2024, 2, 1));
        Assert.Equal([new SeasonChange(march1, 3, 0)], step.Seasons);
        Assert.Equal([new HolidayStart(march1 - 1, Set.Holidays.First(h => h.Id == "leap-day").Index)], step.Holidays);
        Assert.Equal(0, step.HolidaysOmitted);

        var year = Runtime("january", 1, 0, year: 2024);
        var all = year.SkipTo(Fixture.At(Set, 2025, "january", 1, 0) - 1);
        Assert.Equal(["spring", "summer", "autumn", "winter"], all.Seasons.Select(s => Set.Seasons[s.To].Id).ToArray());
        Assert.Equal(["christmas", "harvest-week", "labor-day", "leap-day", "thanksgiving"], all.Holidays.Select(h => Set.Holidays[h.Holiday].Id).Order().ToArray());
    }

    [Fact]
    public void AStepInsideOnePartOfTheDayChangesNoPartAndOneAcrossTheBoundaryDoes()
    {
        var runtime = Runtime("march", 6, 7);
        Assert.False(runtime.SkipTo(Fixture.At(Set, 2024, "march", 6, 7, 59)).DayPartChanged);
        Assert.True(runtime.SkipTo(Fixture.At(Set, 2024, "march", 6, 8, 0)).DayPartChanged);
        Assert.False(runtime.SkipTo(Fixture.At(Set, 2024, "march", 6, 8, 1)).DayPartChanged);
        // A whole day later is the same part but a new day: the pair changed.
        Assert.True(runtime.SkipTo(Fixture.At(Set, 2024, "march", 7, 8, 1)).DayPartChanged);
    }

    [Fact]
    public void ALongJumpListsOnlyTheFirstHolidaysAndCountsTheRest()
    {
        var text = Fixture.MutateGregorian(root =>
        {
            root["holidays"] = new JsonArray(new JsonObject
            {
                ["id"] = "daily", ["key"] = "cal.holiday.new-year", ["days"] = 1, ["tags"] = new JsonArray(),
                ["rule"] = JsonNode.Parse("{ \"freq\": \"daily\", \"start\": { \"year\": 2024, \"month\": \"january\", \"day\": 1 } }"),
            });
            root["schedules"] = new JsonArray();
        });
        var set = CalendarLoader.Load(text);
        var runtime = CalendarRuntime.Create(set, Fixture.At(set, 2024, "january", 1), 1, 1);
        var step = runtime.SkipTo(Fixture.At(set, 2024, "january", 1) + 200 * 1440);
        Assert.Equal(CalendarRuntime.MaxListed, step.Holidays.Count);
        Assert.Equal(200 - CalendarRuntime.MaxListed, step.HolidaysOmitted);
        Assert.Equal(Enumerable.Range(1, CalendarRuntime.MaxListed).Select(i => Fixture.At(set, 2024, "january", 1) / 1440 + i).ToArray(), step.Holidays.Select(h => h.Day).ToArray());
    }

    // ---- actors ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ActorsAreAddedOnceAndRemovedByTheirId()
    {
        var runtime = Runtime();
        runtime.AddActor("anna", "shopkeeper");
        Assert.Equal(CalendarCodes.DuplicateActor, Fixture.CodeOf(() => runtime.AddActor("anna", "guard")));
        Assert.Equal(CalendarCodes.UnknownSchedule, Fixture.CodeOf(() => runtime.AddActor("ben", "nobody")));
        Assert.Equal(CalendarCodes.Range, Fixture.CodeOf(() => runtime.AddActor("", "guard")));
        Assert.Equal(CalendarCodes.Range, Fixture.CodeOf(() => runtime.AddActor(new string('a', 129), "guard")));
        runtime.RemoveActor("anna");
        Assert.Equal(CalendarCodes.UnknownActor, Fixture.CodeOf(() => runtime.RemoveActor("anna")));
        Assert.Equal(CalendarCodes.UnknownActor, Fixture.CodeOf(() => runtime.Interrupt("anna", "alarm", 5)));
        Assert.Equal(CalendarCodes.UnknownActor, Fixture.CodeOf(() => runtime.ClearInterrupt("anna")));
        Assert.Equal(CalendarCodes.UnknownActor, Fixture.CodeOf(() => runtime.WhereIs(runtime.Context(Fixture.Dry), "anna")));
    }

    [Fact]
    public void ACalendarFollowsAtMostTheLimitOfActors()
    {
        var runtime = Runtime();
        for (var i = 0; i < CalendarRuntime.MaxActors; i++) runtime.AddActor("a" + i, "guard");
        Assert.Equal(CalendarCodes.Capacity, Fixture.CodeOf(() => runtime.AddActor("one-too-many", "guard")));
    }

    [Fact]
    public void RefreshAnnouncesEveryoneOnceInIdOrderAndThenOnlyWhoseBlockChanged()
    {
        var runtime = Runtime("march", 6, 6, year: 2024);   // wednesday 06:00
        runtime.AddActor("zed", "guard");
        runtime.AddActor("anna", "shopkeeper");
        runtime.AddActor("mia", "shopkeeper");
        var first = runtime.Refresh(runtime.Context(Fixture.Dry), 100);
        Assert.Equal(["anna", "mia", "zed"], first.Select(c => c.Actor).ToArray());
        Assert.Equal(["home/sleep", "home/sleep", "home/sleep"], first.Select(c => c.Answer.Location + "/" + c.Answer.Activity).ToArray());
        Assert.Empty(runtime.Refresh(runtime.Context(Fixture.Dry), 100));
        runtime.SkipTo(Fixture.At(Set, 2024, "march", 6, 7, 0));
        var second = runtime.Refresh(runtime.Context(Fixture.Dry), 100);
        Assert.Equal(["anna", "mia"], second.Select(c => c.Actor).ToArray());   // the guard sleeps until 14:00; the shopkeepers have breakfast
        Assert.All(second, c => Assert.Equal("breakfast", c.Answer.Block));
    }

    [Fact]
    public void ARefreshLimitLeavesTheRestForTheNextCallAndLosesNobody()
    {
        var runtime = Runtime();
        for (var i = 0; i < 25; i++) runtime.AddActor("a" + i.ToString("00"), "guard");
        var heard = new List<string>();
        for (var call = 0; call < 5; call++)
        {
            var changes = runtime.Refresh(runtime.Context(Fixture.Dry), 10);
            Assert.True(changes.Count <= 10);
            heard.AddRange(changes.Select(c => c.Actor));
        }
        Assert.Equal(Enumerable.Range(0, 25).Select(i => "a" + i.ToString("00")).ToArray(), heard.ToArray());
        Assert.Equal(CalendarCodes.Range, Fixture.CodeOf(() => runtime.Refresh(runtime.Context(Fixture.Dry), 0)));
    }

    [Fact]
    public void AnInterruptChangesTheAnswerUntilItRunsOutOrIsCleared()
    {
        var runtime = Runtime("march", 6, 10);
        runtime.AddActor("anna", "shopkeeper");
        runtime.Refresh(runtime.Context(Fixture.Dry), 10);
        runtime.Interrupt("anna", "alarm", 30);
        var during = runtime.Refresh(runtime.Context(Fixture.Dry), 10);
        Assert.Equal("guardhouse/defend/interrupt", during.Single().Answer.Location + "/" + during.Single().Answer.Activity + "/" + during.Single().Answer.Source.ToString().ToLowerInvariant());
        runtime.SkipTo(runtime.Instant + 29);
        Assert.Empty(runtime.Refresh(runtime.Context(Fixture.Dry), 10));
        runtime.SkipTo(runtime.Instant + 1);   // the interrupt ends exactly now
        var after = runtime.Refresh(runtime.Context(Fixture.Dry), 10);
        Assert.Equal("shop/work", after.Single().Answer.Location + "/" + after.Single().Answer.Activity);
        Assert.Null(runtime.Actors["anna"].Interrupt);

        runtime.Interrupt("anna", "alarm", 100);
        runtime.ClearInterrupt("anna");
        Assert.Equal("shop", runtime.WhereIs(runtime.Context(Fixture.Dry), "anna").Location);
    }

    [Fact]
    public void AnInterruptOfLowerPriorityDoesNotReplaceOneInForceButAHigherOneDoes()
    {
        var runtime = Runtime("march", 6, 10);
        runtime.AddActor("anna", "shopkeeper");
        runtime.Interrupt("anna", "invitation", 60);
        runtime.Interrupt("anna", "alarm", 10);
        Assert.Equal("alarm", runtime.Actors["anna"].Interrupt);
        Assert.Equal(CalendarCodes.Outranked, Fixture.CodeOf(() => runtime.Interrupt("anna", "invitation", 60)));
        Assert.Equal("alarm", runtime.Actors["anna"].Interrupt);
        runtime.Interrupt("anna", "alarm", 500);   // the same priority replaces, and extends
        Assert.Equal(runtime.Instant + 500, runtime.Actors["anna"].InterruptUntil);
        Assert.Equal(CalendarCodes.UnknownInterrupt, Fixture.CodeOf(() => runtime.Interrupt("anna", "nope", 5)));
        Assert.Equal(CalendarCodes.Range, Fixture.CodeOf(() => runtime.Interrupt("anna", "alarm", 0)));
        Assert.Equal(CalendarCodes.Range, Fixture.CodeOf(() => runtime.Interrupt("anna", "alarm", CalendarRuntime.MaxSkipDays * Set.Time.TicksPerDay + 1)));
    }

    [Fact]
    public void AFactTheScheduleNeedsIsAskedForWhenAnActorIsRefreshed()
    {
        var runtime = Runtime("january", 10, 10);   // winter: the rain routine reads weather.raining
        runtime.AddActor("anna", "shopkeeper");
        Assert.Equal(CalendarCodes.FactMissing, Fixture.CodeOf(() => runtime.Refresh(runtime.Context(), 10)));
        Assert.Equal("anna", runtime.Refresh(runtime.Context(Fixture.Rain), 10).Single().Actor);
        Assert.Equal("home", runtime.WhereIs(runtime.Context(Fixture.Rain), "anna").Location);
    }
}

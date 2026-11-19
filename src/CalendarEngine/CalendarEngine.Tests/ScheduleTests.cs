using ScenarioEngine.Contracts;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>Where somebody is at a time: routines by weekday, season, holiday and fact, the priority between them, gaps that fall through to a lower routine, and interrupts.</summary>
public sealed class ScheduleTests
{
    private static CalendarSet Set => Fixture.Gregorian;

    private static ScheduleAnswer Where(string schedule, long year, string month, int day, int hour, int minute = 0, IReadOnlyDictionary<string, FactValue>? facts = null, string? interrupt = null) =>
        CalendarContext.At(Set, Fixture.At(Set, year, month, day, hour, minute), facts ?? Fixture.Dry).WhereIs(schedule, interrupt);

    private static string Short(ScheduleAnswer a) => $"{a.Location}/{a.Activity} {(a.Source == AnswerSource.Interrupt ? "interrupt" : "routine")} {a.Routine}" + (a.Block.Length > 0 ? "/" + a.Block : "");

    [Fact]
    public void AnOrdinaryWeekdayFollowsTheBaseRoutine()
    {
        Assert.Equal("home/sleep routine base/sleep-night", Short(Where("shopkeeper", 2024, "march", 6, 3)));
        Assert.Equal("shop/work routine base/open-morning", Short(Where("shopkeeper", 2024, "march", 6, 10)));
        Assert.Equal("tavern/eat routine base/lunch", Short(Where("shopkeeper", 2024, "march", 6, 12)));
        Assert.Equal("home/sleep routine base/sleep-late", Short(Where("shopkeeper", 2024, "march", 6, 23, 59)));
    }

    [Fact]
    public void ABlockCoversItsStartAndNotItsEnd()
    {
        Assert.Equal("breakfast", Where("shopkeeper", 2024, "march", 6, 7, 0).Block);
        Assert.Equal("sleep-night", Where("shopkeeper", 2024, "march", 6, 6, 59).Block);
        var answer = Where("shopkeeper", 2024, "march", 6, 7, 30);
        Assert.Equal((420, 480), (answer.From, answer.To));
    }

    [Fact]
    public void ARoutineThatCoversOnlyPartOfTheDayLetsTheRestFallThroughToTheNextOne()
    {
        // Saturday: the market runs 9:00 to 15:00 and the base routine has the rest.
        Assert.Equal("saturday", Set.System.Weekdays[Set.System.DayOf(Set.System.DayNumber(new CalendarDate(2024, 2, 9))).Weekday].Id);
        Assert.Equal("square/work routine saturday-market/market", Short(Where("shopkeeper", 2024, "march", 9, 10)));
        Assert.Equal("home/eat routine base/breakfast", Short(Where("shopkeeper", 2024, "march", 9, 7, 30)));
        Assert.Equal("shop/work routine base/open-afternoon", Short(Where("shopkeeper", 2024, "march", 9, 16)));
    }

    [Fact]
    public void ASundayRoutineHasMassAndRestAndTheEveningIsTheBaseRoutines()
    {
        Assert.Equal("church/worship routine sunday/mass", Short(Where("shopkeeper", 2024, "march", 10, 9)));
        Assert.Equal("home/idle routine sunday/rest", Short(Where("shopkeeper", 2024, "march", 10, 13)));
        Assert.Equal("tavern/idle routine base/evening", Short(Where("shopkeeper", 2024, "march", 10, 19)));
    }

    [Fact]
    public void AHolidayOutranksTheWeekdayItFallsOn()
    {
        // 2022-12-25 was a Sunday: the holiday routine (priority 20) beats the sunday routine (10) and the base routine takes the hours the holiday leaves open.
        Assert.Equal("square/celebrate routine holiday/feast", Short(Where("shopkeeper", 2022, "december", 25, 9)));
        Assert.Equal("home/sleep routine base/sleep-late", Short(Where("shopkeeper", 2022, "december", 25, 22, 30)));
        Assert.Equal("home/eat routine base/breakfast", Short(Where("shopkeeper", 2022, "december", 25, 7, 30)));
    }

    [Fact]
    public void AMultiDayHolidayAppliesOnEachOfItsDaysAndNotAfter()
    {
        Assert.Equal("fair", Where("shopkeeper", 2024, "october", 1, 12).Block);
        Assert.Equal("fair", Where("shopkeeper", 2024, "october", 3, 12).Block);
        Assert.Equal("open-morning", Where("shopkeeper", 2024, "october", 4, 9).Block);
        Assert.Equal("breakfast", Where("shopkeeper", 2024, "october", 2, 7, 30).Block);
    }

    [Fact]
    public void AConditionOnAHostFactDecidesAWinterRoutine()
    {
        Assert.Equal("home/idle routine winter-rain/stay-in", Short(Where("shopkeeper", 2024, "january", 10, 10, facts: Fixture.Rain)));
        Assert.Equal("shop/work routine base/open-morning", Short(Where("shopkeeper", 2024, "january", 10, 10, facts: Fixture.Dry)));
        // The rain rule outranks the Sunday rule (15 over 10), but a holiday outranks the rain (20 over 15).
        Assert.Equal("stay-in", Where("shopkeeper", 2024, "january", 14, 10, facts: Fixture.Rain).Block);
        Assert.Equal("feast", Where("shopkeeper", 2024, "december", 25, 10, facts: Fixture.Rain).Block);
    }

    [Fact]
    public void AFactIsOnlyNeededWhereTheRoutineCouldApply()
    {
        // Spring: the rain routine is for winter, so its fact is never read and need not be given.
        Assert.Equal("open-morning", CalendarContext.At(Set, Fixture.At(Set, 2024, "april", 10, 10)).WhereIs("shopkeeper").Block);
        var winter = CalendarContext.At(Set, Fixture.At(Set, 2024, "january", 10, 10));
        Assert.Equal(CalendarCodes.FactMissing, Fixture.CodeOf(() => winter.WhereIs("shopkeeper")));
    }

    [Fact]
    public void FactsTheDocumentDoesNotDeclareOrOfTheWrongKindOrRangeAreRefused()
    {
        var instant = Fixture.At(Set, 2024, "april", 10, 10);
        Assert.Equal(CalendarCodes.FactInvalid, Fixture.CodeOf(() => CalendarContext.At(Set, instant, Fixture.Facts(("weather.snowing", FactValue.From(true))))));
        Assert.Equal(CalendarCodes.FactInvalid, Fixture.CodeOf(() => CalendarContext.At(Set, instant, Fixture.Facts(("weather.raining", FactValue.From(1))))));
        Assert.Equal(CalendarCodes.FactInvalid, Fixture.CodeOf(() => CalendarContext.At(Set, instant, Fixture.Facts(("story.plague", FactValue.From(4))))));
        Assert.Equal(CalendarCodes.FactInvalid, Fixture.CodeOf(() => CalendarContext.At(Set, instant, Fixture.Facts(("cal.season", FactValue.From("winter"))))));
        _ = CalendarContext.At(Set, instant, Fixture.Facts(("story.plague", FactValue.From(3))));
    }

    [Fact]
    public void AnInterruptWinsOverARoutineOfEqualOrLowerPriorityAndLosesToAHigherOne()
    {
        Assert.Equal("guardhouse/defend interrupt alarm", Short(Where("shopkeeper", 2024, "march", 6, 10, interrupt: "alarm")));
        Assert.Equal("guardhouse/defend interrupt alarm", Short(Where("shopkeeper", 2024, "december", 25, 10, interrupt: "alarm")));   // 900 over the holiday's 20
        Assert.Equal("square/celebrate interrupt invitation", Short(Where("shopkeeper", 2024, "march", 6, 10, interrupt: "invitation")));   // 12 over base 0
        Assert.Equal("square/celebrate interrupt invitation", Short(Where("shopkeeper", 2024, "march", 10, 9, interrupt: "invitation")));   // 12 over sunday 10
        Assert.Equal("feast", Where("shopkeeper", 2024, "december", 25, 10, interrupt: "invitation").Block);   // 12 under the holiday 20
        Assert.Equal(AnswerSource.Routine, Where("shopkeeper", 2024, "december", 25, 10, interrupt: "invitation").Source);
    }

    [Fact]
    public void AnInterruptLosesToTheRoutinePriorityOfTheBlockThatAnswersNotOfTheHighestApplicableRoutine()
    {
        // On a Sunday at 19:00 the sunday routine (10) has no block, so the base routine (0) answers and the invitation (12) wins over it, as it would on any evening.
        Assert.Equal(AnswerSource.Interrupt, Where("shopkeeper", 2024, "march", 10, 19, interrupt: "invitation").Source);
        // At 9:00 the sunday routine answers with priority 10: the invitation is still higher. On a rainy winter Sunday the rain routine (15) answers and the invitation loses.
        Assert.Equal(AnswerSource.Routine, Where("shopkeeper", 2024, "january", 14, 10, facts: Fixture.Rain, interrupt: "invitation").Source);
    }

    [Fact]
    public void UnknownSchedulesAndInterruptsAreRefused()
    {
        var context = CalendarContext.At(Set, Fixture.At(Set, 2024, "april", 10, 10));
        Assert.Equal(CalendarCodes.UnknownSchedule, Fixture.CodeOf(() => context.WhereIs("nobody")));
        Assert.Equal(CalendarCodes.UnknownInterrupt, Fixture.CodeOf(() => context.WhereIs("guard", "nope")));
    }

    [Fact]
    public void EveryScheduleAnswersEveryMinuteOfEveryKindOfDayAndTheAnswerContainsTheMinute()
    {
        var checkedAnswers = 0;
        for (var i = 0; i < 6000; i++)
        {
            var year = 1970 + (long)(EngineCommon.DeterministicRoll.Next(3, "year", i) % 60);
            var instant = Fixture.At(Set, year, "january", 1) + (long)(EngineCommon.DeterministicRoll.Next(3, "tick", i) % (ulong)(365 * 1440));
            foreach (var rain in new[] { Fixture.Dry, Fixture.Rain })
            {
                var context = CalendarContext.At(Set, instant, rain);
                foreach (var schedule in Set.Schedules)
                {
                    var answer = context.WhereIs(schedule.Id);
                    Assert.InRange(context.Moment.MinuteOfDay, answer.From, answer.To - 1);
                    checkedAnswers++;
                }
            }
        }
        Assert.Equal(6000 * 2 * 2, checkedAnswers);
    }

    [Fact]
    public void TheAnswerDoesNotDependOnWhatWasAskedBeforeOrInWhichOrder()
    {
        var instants = Enumerable.Range(0, 300).Select(i => Fixture.At(Set, 2024, "january", 1) + i * 977L).ToList();
        string Run(IEnumerable<long> order) => string.Join(";", order.OrderBy(x => x).Select(t => CalendarContext.At(Set, t, Fixture.Rain).WhereIs("shopkeeper").Key));
        var forward = instants.Select(t => (t, CalendarContext.At(Set, t, Fixture.Rain).WhereIs("shopkeeper").Key)).ToList();
        var backward = Enumerable.Reverse(instants).Select(t => (t, CalendarContext.At(Set, t, Fixture.Rain).WhereIs("shopkeeper").Key)).OrderBy(p => p.t).ToList();
        Assert.Equal(forward, backward);
        Assert.Equal(Run(instants), Run(Enumerable.Reverse(instants)));
    }


    [Fact]
    public void ExplainSaysWhyEachRoutineAppliedOrNotAndWhichBlockAnswered()
    {
        var instant = Fixture.At(Set, 2022, "december", 25, 9);
        var text = CalendarExplain.WhereIs(Set, instant, "shopkeeper", "invitation", Fixture.Dry);
        var expected = new[]
        {
            "instant " + instant + " is 2022-december-25 09:00, weekday sunday, season winter, day part morning, holidays christmas",
            "schedule shopkeeper",
            "  routine holiday (priority 20): applies, block feast covers 08:00 to 22:00",
            "  routine harvest (priority 18): does not apply, no holiday of harvest-week is on",
            "  routine winter-rain (priority 15): does not apply, condition is false: weather.raining eq true [false] = false",
            "  routine sunday (priority 10): applies, block mass covers 08:00 to 12:00",
            "  routine saturday-market (priority 5): does not apply, weekday is sunday, not one of saturday",
            "  routine base (priority 0): applies, block open-morning covers 08:00 to 12:00",
            "  interrupt invitation (priority 12): loses to routine holiday (priority 20)",
            "result: location square, activity celebrate, from routine holiday block feast",
        };
        Assert.Equal(expected, text.Split('\n'));
    }

    [Fact]
    public void ExplainNamesARoutineThatAppliesButHasNoBlockAtThatTime()
    {
        var text = CalendarExplain.WhereIs(Set, Fixture.At(Set, 2024, "march", 9, 7, 30), "shopkeeper", null, Fixture.Dry);
        Assert.Contains("  routine saturday-market (priority 5): applies, but has no block at this time, so the next routine is asked", text);
        Assert.EndsWith("result: location home, activity eat, from routine base block breakfast", text);
    }
}

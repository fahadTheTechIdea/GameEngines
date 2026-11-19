using System.Text.Json.Nodes;
using Xunit;

namespace TutorialEngine.Tests;

/// <summary>Contextual one-shot tips: shown while their context holds, for a duration, a limited number of times a cooldown apart, never after the player acknowledged them, at most so many at once.</summary>
public sealed class TipTests
{
    private static TutorialRuntime Ready() => Fixture.Ready();

    private static bool Visible(TutorialRuntime runtime, int tip = 0) => runtime.Tips[tip].Visible;

    [Fact]
    public void ATipShowsWhenItsContextHoldsAndLastsItsDurationExactly()
    {
        var runtime = Ready();
        runtime.Advance(100, Fixture.Host(flag: false));
        Assert.False(Visible(runtime));
        var changes = runtime.Advance(101, Fixture.Host(flag: true));
        Assert.Equal([new TipChanged(0, TipEnd.Shown, 1)], changes.Tips);
        Assert.Equal(101, runtime.Tips[0].ShownAt);
        Assert.Equal(111, runtime.Tips[0].Until);
        runtime.Advance(110, Fixture.Host(flag: true));
        Assert.True(Visible(runtime));                            // one step left
        var ended = runtime.Advance(111, Fixture.Host(flag: true));
        Assert.Equal([new TipChanged(0, TipEnd.Expired, 1)], ended.Tips);
        Assert.False(Visible(runtime));
        Assert.Equal(111, runtime.Tips[0].EndedAt);
    }

    [Fact]
    public void ATipComesBackOnlyAfterItsCooldownAndOnlyUpToItsNumberOfShows()
    {
        var runtime = Ready();
        runtime.Advance(0, Fixture.Host(flag: true));             // shows 1: 0 to 10
        runtime.Advance(10, Fixture.Host(flag: true));            // ended at 10, cooldown 20
        runtime.Advance(29, Fixture.Host(flag: true));
        Assert.False(Visible(runtime));
        var changes = runtime.Advance(30, Fixture.Host(flag: true));
        Assert.Equal([new TipChanged(0, TipEnd.Shown, 2)], changes.Tips);
        runtime.Advance(40, Fixture.Host(flag: true));            // ended at 40
        runtime.Advance(1000, Fixture.Host(flag: true));
        Assert.False(Visible(runtime));                           // maxShows is 2
        Assert.Equal(2, runtime.Tips[0].Shows);
    }

    [Fact]
    public void ATipWhoseContextEndsIsWithdrawnAtOnceAndThenWaitsOutItsCooldown()
    {
        var runtime = Ready();
        runtime.Advance(5, Fixture.Host(flag: true));
        var changes = runtime.Advance(7, Fixture.Host(flag: false));
        Assert.Equal([new TipChanged(0, TipEnd.Withdrawn, 1)], changes.Tips);
        Assert.Equal(7, runtime.Tips[0].EndedAt);
        runtime.Advance(26, Fixture.Host(flag: true));
        Assert.False(Visible(runtime));
        runtime.Advance(27, Fixture.Host(flag: true));
        Assert.True(Visible(runtime));
    }

    [Fact]
    public void AcknowledgingATipEndsItForGoodEvenWithShowsLeft()
    {
        var runtime = Ready();
        runtime.Advance(5, Fixture.Host(flag: true));
        var changes = runtime.DismissTip("t1");
        Assert.Equal([new TipChanged(0, TipEnd.Dismissed, 1)], changes.Tips);
        Assert.True(runtime.Tips[0].Acknowledged);
        Assert.True(changes.Persist);
        runtime.Advance(500, Fixture.Host(flag: true));
        Assert.False(Visible(runtime));
        Assert.Equal(1, runtime.Tips[0].Shows);
    }

    [Fact]
    public void ADismissIsRefusedForATipThatDoesNotShowAndOneThatDoesNotExist()
    {
        var runtime = Ready();
        Assert.Equal(TutorialCodes.TipState, Fixture.Refusal(() => runtime.DismissTip("t1")));
        Assert.Equal(TutorialCodes.UnknownTip, Fixture.Refusal(() => runtime.DismissTip("nope")));
        Assert.False(runtime.Tips[0].Acknowledged);
    }

    [Fact]
    public void NoTipShowsBeforeTheFirstRunJourneyIsDoneAndTheyShowOnceItIs()
    {
        var runtime = TutorialRuntime.Create(Fixture.Mini());
        runtime.Advance(5, Fixture.Host(flag: true));
        Assert.False(Visible(runtime));
        runtime.SelectLanguage("en");
        runtime.Advance(6, Fixture.Host(flag: true));
        Assert.False(Visible(runtime));
        while (runtime.Journey.State == JourneyState.Story) runtime.StoryNext();
        runtime.ChooseEntry("join");
        runtime.Advance(7, Fixture.Host(flag: true));
        Assert.True(Visible(runtime));
    }

    [Fact]
    public void ReplayingTheStoryTakesAShowingTipAwayAndTipsWaitUntilItIsDoneAgain()
    {
        var runtime = Ready();
        runtime.Advance(5, Fixture.Host(flag: true));
        var changes = runtime.ReplayStory();
        Assert.Equal([new TipChanged(0, TipEnd.Withdrawn, 1)], changes.Tips);
        runtime.Advance(500, Fixture.Host(flag: true));
        Assert.False(Visible(runtime));
        runtime.StorySkip();
        runtime.Advance(501, Fixture.Host(flag: true));
        Assert.True(Visible(runtime));
    }

    [Fact]
    public void TurningTipsOffWithdrawsAtOnceAndTurningThemOnLetsThemShowAgain()
    {
        var runtime = Ready();
        runtime.Advance(5, Fixture.Host(flag: true));
        var changes = runtime.SetTipsEnabled(false);
        Assert.Equal([new SettingChanged("tips", 0)], changes.Settings);
        Assert.Equal([new TipChanged(0, TipEnd.Withdrawn, 1)], changes.Tips);
        runtime.Advance(100, Fixture.Host(flag: true));
        Assert.False(Visible(runtime));
        Assert.False(runtime.SetTipsEnabled(false).Any);          // no change, no event
        runtime.SetTipsEnabled(true);
        runtime.Advance(101, Fixture.Host(flag: true));
        Assert.True(Visible(runtime));
    }

    [Fact]
    public void ResettingTipsForgetsWhatTheyShowedAndWhatThePlayerAcknowledged()
    {
        var runtime = Ready();
        runtime.Advance(5, Fixture.Host(flag: true));
        runtime.DismissTip("t1");
        runtime.Advance(6, Fixture.Host(flag: true));
        Assert.False(Visible(runtime));
        runtime.ResetTips();
        Assert.False(runtime.Tips[0].Acknowledged);
        Assert.Equal(0, runtime.Tips[0].Shows);
        Assert.Equal(-1, runtime.LastTipShown);
        runtime.Advance(7, Fixture.Host(flag: true));
        Assert.True(Visible(runtime));
    }

    [Fact]
    public void EnlargedTextKeepsATipLongerSoItCanStillBeRead()
    {
        var runtime = Ready();
        runtime.SetTextScale(2000);
        runtime.Advance(5, Fixture.Host(flag: true));
        Assert.Equal(25, runtime.Tips[0].Until);                  // 10 steps at twice the size
        var normal = Ready();
        normal.Advance(5, Fixture.Host(flag: true));
        Assert.Equal(15, normal.Tips[0].Until);
        Assert.Equal(1, TutorialPresentation.TipDuration(1, 1000));
        Assert.Equal(2, TutorialPresentation.TipDuration(1, 1001));   // rounded up
        Assert.Equal(3, TutorialPresentation.TipDuration(1, 3000));
    }

    private static TutorialSet Crowd(int tips, int visible, int gap, Action<JsonObject>? more = null) => Fixture.Mini(r =>
    {
        r["limits"]!["maxVisibleTips"] = visible;
        r["limits"]!["tipGap"] = gap;
        var template = Tiny.Tip(r, "t1");
        var all = r["tips"]!.AsArray();
        all.Clear();
        for (var i = 0; i < tips; i++)
        {
            var tip = Tiny.Copy(template).AsObject();
            tip["id"] = "t" + i; tip["priority"] = i % 3; tip["when"] = Tiny.Cond("flag", "eq", true);
            all.Add(tip);
        }
        more?.Invoke(r);
    });

    [Fact]
    public void AtMostTheLimitOfTipsShowAtOnceHighestPriorityFirstAndDocumentOrderAmongEquals()
    {
        var runtime = Fixture.Ready(Crowd(6, 2, 0));       // priorities 0,1,2,0,1,2
        runtime.Advance(0, Fixture.Host(flag: true));
        Assert.Equal([2, 5], Enumerable.Range(0, 6).Where(i => Visible(runtime, i)).ToArray());
        runtime.Advance(10, Fixture.Host(flag: true));                          // both ended; the next two by priority are 1 and 4
        Assert.Equal([1, 4], Enumerable.Range(0, 6).Where(i => Visible(runtime, i)).ToArray());
    }

    [Fact]
    public void FiftyTipsCanShowAtTheSameTimeWhenTheDocumentAllowsIt()
    {
        var runtime = Fixture.Ready(Crowd(50, 50, 0));
        var changes = runtime.Advance(0, Fixture.Host(flag: true));
        Assert.Equal(50, changes.Tips.Count(t => t.Change == TipEnd.Shown));
        Assert.Equal(50, runtime.VisibleTips().Count);
        var ended = runtime.Advance(10, Fixture.Host(flag: true));
        Assert.Equal(50, ended.Tips.Count(t => t.Change == TipEnd.Expired));
    }

    [Fact]
    public void TwoTipsBeginAtLeastTheTipGapApart()
    {
        var runtime = Fixture.Ready(Crowd(3, 3, 4));
        runtime.Advance(0, Fixture.Host(flag: true));
        Assert.Equal(1, Enumerable.Range(0, 3).Count(i => Visible(runtime, i)));
        runtime.Advance(3, Fixture.Host(flag: true));
        Assert.Equal(1, Enumerable.Range(0, 3).Count(i => Visible(runtime, i)));
        runtime.Advance(4, Fixture.Host(flag: true));
        Assert.Equal(2, Enumerable.Range(0, 3).Count(i => Visible(runtime, i)));
        Assert.Equal(4, runtime.LastTipShown);
    }

    [Fact]
    public void ATipFollowingAStepWaitsUntilThatStepIsSatisfied()
    {
        var set = Fixture.Mini(r => Tiny.Tip(r, "t1")["afterStep"] = "s1");
        var runtime = Fixture.Ready(set);
        runtime.Advance(0, Fixture.Host(flag: true));
        Assert.False(Visible(runtime));
        runtime.MoveTo(1); runtime.Observe("e1");
        runtime.Evaluate(Fixture.Host(flag: true));
        Assert.True(Visible(runtime));
    }

    [Fact]
    public void ATipForASkillThePlayerHasIsNeverShownAndIsWithdrawnWhenTheSkillIsGained()
    {
        var set = Fixture.Mini(r => Tiny.Tip(r, "t1")["skill"] = "sk");
        var runtime = Fixture.Ready(set);
        runtime.Advance(0, Fixture.Host(flag: true));
        Assert.True(Visible(runtime));
        runtime.MoveTo(1); runtime.Observe("e3");                    // not evidence
        runtime.Evaluate(Fixture.Host(flag: true));
        Assert.True(Visible(runtime));
        runtime.MoveTo(2); runtime.Observe("e1", 3);                 // the skill is mastered
        var changes = runtime.Evaluate(Fixture.Host(flag: true));
        Assert.Equal([new TipChanged(0, TipEnd.Withdrawn, 1)], changes.Tips);
        runtime.Advance(500, Fixture.Host(flag: true));
        Assert.False(Visible(runtime));

        var already = Fixture.Ready(set);
        already.Observe("e1", 3);
        already.Advance(0, Fixture.Host(flag: true));
        Assert.False(Visible(already));
    }

    [Fact]
    public void TipsOfTheSampleShowByTheirContextAndTheCooldownsOfTheDocument()
    {
        var runtime = Fixture.Ready(Fixture.Valley);
        runtime.Advance(0, Fixture.ValleyHost(paused: true));
        Assert.False(runtime.Tips[0].Visible);                    // "paused" follows the menu step
        runtime.MoveTo(1); runtime.Observe("moved"); runtime.Evaluate(Fixture.ValleyHost());
        runtime.MoveTo(2); runtime.Observe("camera_moved"); runtime.Evaluate(Fixture.ValleyHost());
        runtime.MoveTo(3); runtime.Observe("menu_opened"); runtime.Evaluate(Fixture.ValleyHost(paused: true));
        Assert.Contains(runtime.VisibleTips(), t => t.Tip == "paused");
    }
}

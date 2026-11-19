using Xunit;

namespace TutorialEngine.Tests;

/// <summary>The first-run journey: language, then the story, then how to enter; saved at once; skipped by a returning player; replayable; the language changeable from preferences.</summary>
public sealed class JourneyTests
{
    private static TutorialRuntime New() => TutorialRuntime.Create(Fixture.Mini());

    [Fact]
    public void ANewPlayerStartsAtTheLanguage()
    {
        var runtime = New();
        Assert.Equal(JourneyState.Language, runtime.Journey.State);
        Assert.Equal("", runtime.Journey.Language);
    }

    [Fact]
    public void NothingAfterTheLanguageCanBeDoneBeforeIt()
    {
        var runtime = New();
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.StoryNext()));
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.StoryBack()));
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.StorySkip()));
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.ChooseEntry("host")));
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.ReplayStory()));
        Assert.Equal(JourneyState.Language, runtime.Journey.State);
    }

    [Fact]
    public void NoAccountHostOrJoinChoiceIsPossibleBeforeTheStoryIsOver()
    {
        var runtime = New();
        runtime.SelectLanguage("fr");
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.ChooseEntry("sign-in")));
        runtime.StoryNext();
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.ChooseEntry("host")));
        runtime.StoryNext();
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.ChooseEntry("join")));
        runtime.StoryNext();
        Assert.Equal(JourneyState.Entry, runtime.Journey.State);
        runtime.ChooseEntry("join");
        Assert.Equal(JourneyState.Done, runtime.Journey.State);
        Assert.Equal("join", runtime.Journey.Entry);
    }

    [Fact]
    public void TheLanguageIsPersistedTheMomentItIsChosenAndBeginsTheStory()
    {
        var runtime = New();
        var changes = runtime.SelectLanguage("fr");
        Assert.Equal([new JourneyChanged(JourneyState.Language, JourneyState.Story, 0, "fr")], changes.Journeys);
        Assert.True(changes.Persist);
        var reopened = TutorialSave.Open(runtime.Set, TutorialSave.Write(runtime));        // killed right after: the language and the place in the story are kept
        Assert.Equal("fr", reopened.Journey.Language);
        Assert.Equal(JourneyState.Story, reopened.Journey.State);
    }

    [Fact]
    public void ALanguageThePlayerCannotChooseIsRefused()
    {
        var runtime = New();
        Assert.Equal(TutorialCodes.UnknownLanguage, Fixture.Refusal(() => runtime.SelectLanguage("de")));
        Assert.Equal(TutorialCodes.UnknownLanguage, Fixture.Refusal(() => runtime.SelectLanguage("")));
        Assert.Equal(JourneyState.Language, runtime.Journey.State);
    }

    [Fact]
    public void TheStoryMovesOnePageAtATimeWithNextAndBack()
    {
        var runtime = New();
        runtime.SelectLanguage("en");
        Assert.Equal(0, runtime.Journey.Page);
        runtime.StoryNext(); Assert.Equal(1, runtime.Journey.Page);
        runtime.StoryNext(); Assert.Equal(2, runtime.Journey.Page);
        runtime.StoryBack(); Assert.Equal(1, runtime.Journey.Page);
        runtime.StoryBack(); Assert.Equal(0, runtime.Journey.Page);
        Assert.Equal(JourneyState.Story, runtime.Journey.State);
    }

    [Fact]
    public void BackFromTheFirstPageGoesToTheLanguageAndBackFromTheEntryGoesToTheLastPage()
    {
        var runtime = New();
        runtime.SelectLanguage("en");
        var back = runtime.StoryBack();
        Assert.Equal([new JourneyChanged(JourneyState.Story, JourneyState.Language, 0, "en")], back.Journeys);
        runtime.SelectLanguage("fr");
        Assert.Equal("fr", runtime.Journey.Language);
        Assert.Equal(JourneyState.Story, runtime.Journey.State);
        runtime.StoryNext(); runtime.StoryNext(); runtime.StoryNext();
        Assert.Equal(JourneyState.Entry, runtime.Journey.State);
        runtime.StoryBack();
        Assert.Equal((JourneyState.Story, 2), (runtime.Journey.State, runtime.Journey.Page));
    }

    [Fact]
    public void SkippingTheStoryGoesToTheEntryAndIsRemembered()
    {
        var runtime = New();
        runtime.SelectLanguage("en");
        runtime.StoryNext();
        runtime.StorySkip();
        Assert.Equal(JourneyState.Entry, runtime.Journey.State);
        Assert.True(runtime.Journey.StorySkipped);
        runtime.ChooseEntry("sign-in");
        Assert.True(runtime.Journey.StorySkipped);
        runtime.ReplayStory();
        Assert.False(runtime.Journey.StorySkipped);
    }

    [Fact]
    public void AnEntryTheDocumentDoesNotOfferIsRefused()
    {
        var runtime = New();
        runtime.SelectLanguage("en");
        runtime.StorySkip();
        Assert.Equal(TutorialCodes.UnknownEntry, Fixture.Refusal(() => runtime.ChooseEntry("register")));
        Assert.Equal(TutorialCodes.UnknownEntry, Fixture.Refusal(() => runtime.ChooseEntry("")));
        Assert.Equal(JourneyState.Entry, runtime.Journey.State);
    }

    [Fact]
    public void ADoneJourneyCannotMoveTheStoryOrChooseAgain()
    {
        var runtime = Fixture.Ready();
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.StoryNext()));
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.StoryBack()));
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.StorySkip()));
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.ChooseEntry("host")));
    }

    [Fact]
    public void AReturningPlayerIsLoadedDoneAndSeesNeitherLanguageNorStoryAgain()
    {
        var first = Fixture.Ready();
        var returning = TutorialSave.Open(first.Set, TutorialSave.Write(first));
        Assert.Equal(JourneyState.Done, returning.Journey.State);
        Assert.Equal("en", returning.Journey.Language);
        Assert.Equal("host", returning.Journey.Entry);
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => returning.StoryNext()));
    }

    [Fact]
    public void TheStoryCanBeReplayedFromPreferencesAndEndsWhereThePlayerWasNotAtTheEntry()
    {
        var runtime = Fixture.Ready();
        var started = runtime.ReplayStory();
        Assert.Equal([new JourneyChanged(JourneyState.Done, JourneyState.Story, 0, "en")], started.Journeys);
        Assert.True(runtime.Journey.Replaying);
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.StoryBack()));      // a replay has no language to go back to
        Assert.Equal(TutorialCodes.JourneyOrder, Fixture.Refusal(() => runtime.ChooseEntry("join")));
        runtime.StoryNext(); runtime.StoryNext();
        var end = runtime.StoryNext();
        Assert.Equal([new JourneyChanged(JourneyState.Story, JourneyState.Done, 2, "en")], end.Journeys);
        Assert.False(runtime.Journey.Replaying);
        Assert.Equal("host", runtime.Journey.Entry);
    }

    [Fact]
    public void AReplayCanBeSkippedToo()
    {
        var runtime = Fixture.Ready();
        runtime.ReplayStory();
        runtime.StorySkip();
        Assert.Equal(JourneyState.Done, runtime.Journey.State);
        Assert.False(runtime.Journey.Replaying);
    }

    [Fact]
    public void TheLanguageCanBeChangedFromPreferencesWithoutMovingTheJourney()
    {
        var runtime = Fixture.Ready();
        var changes = runtime.SelectLanguage("fr");
        Assert.Equal([new JourneyChanged(JourneyState.Done, JourneyState.Done, 2, "fr")], changes.Journeys);
        Assert.Equal(JourneyState.Done, runtime.Journey.State);
        Assert.Equal("fr", runtime.Journey.Language);
        var mid = Fixture.Ready();
        mid.ReplayStory();
        mid.StoryNext();
        mid.SelectLanguage("fr");
        Assert.Equal((JourneyState.Story, 1), (mid.Journey.State, mid.Journey.Page));
    }

    [Fact]
    public void ADeepLinkIntentWaitsThroughTheJourneyAndCanBeCleared()
    {
        var runtime = New();
        runtime.SetIntent("join:abc-123/room.4");
        runtime.SelectLanguage("en");
        runtime.StorySkip();
        runtime.ChooseEntry("join");
        Assert.Equal("join:abc-123/room.4", runtime.Journey.Intent);
        var reopened = TutorialSave.Open(runtime.Set, TutorialSave.Write(runtime));
        Assert.Equal("join:abc-123/room.4", reopened.Journey.Intent);
        reopened.SetIntent("");
        Assert.Equal("", reopened.Journey.Intent);
    }

    [Theory]
    [InlineData("Join")]
    [InlineData("a b")]
    [InlineData("a;b")]
    [InlineData("é")]
    public void AnIntentOfOtherCharactersIsRefused(string intent) => Assert.Equal(TutorialCodes.Intent, Fixture.Refusal(() => New().SetIntent(intent)));

    [Fact]
    public void AnIntentOfTheLongestLengthIsKeptAndOneLongerIsRefused()
    {
        var runtime = New();
        runtime.SetIntent(new string('a', TutorialRuntime.MaxIntentLength));
        Assert.Equal(TutorialCodes.Intent, Fixture.Refusal(() => runtime.SetIntent(new string('a', TutorialRuntime.MaxIntentLength + 1))));
    }

    [Fact]
    public void EveryJourneyChangeIsSomethingToSaveAtOnce()
    {
        var runtime = New();
        Assert.True(runtime.SelectLanguage("en").Persist);
        Assert.True(runtime.StoryNext().Persist);
        Assert.True(runtime.StoryBack().Persist);
        Assert.True(runtime.StorySkip().Persist);
        Assert.True(runtime.ChooseEntry("host").Persist);
        Assert.True(runtime.ReplayStory().Persist);
        Assert.True(runtime.SetIntent("x").Persist);
    }

    [Fact]
    public void TheJourneyFactsReadTheState()
    {
        var runtime = New();
        Assert.Equal("language", runtime.ReadFacts()["journey.state"].Text);
        runtime.SelectLanguage("fr");
        var facts = runtime.ReadFacts("ext.");
        Assert.Equal("story", facts["ext.journey.state"].Text);
        Assert.Equal("fr", facts["ext.journey.language"].Text);
    }
}

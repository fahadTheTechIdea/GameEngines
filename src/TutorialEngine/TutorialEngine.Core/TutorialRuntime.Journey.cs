using EngineCommon;

namespace TutorialEngine;

public sealed partial class TutorialRuntime
{
    // The first-run journey: language first, then the story (one page at a time, with Back, Next and Skip), then how to enter (sign in, register, host, join), then done. Nothing about accounts, hosting or
    // joining can be chosen before the language and the story. A returning player is loaded already done. Every step is a change the host saves at once. The story can be replayed and the language changed
    // at any time from preferences; a deep-link intent given at any point waits until the end.

    private static EngineRefusedException OutOfOrder(string message) => new(TutorialCodes.JourneyOrder, message);

    private TutorialChanges Moved(JourneyState from)
    {
        var changes = new TutorialChanges();
        changes.Journeys.Add(new JourneyChanged(from, Journey.State, Journey.Page, Journey.Language));
        return changes;
    }

    /// <summary>
    /// Chooses the language. While the journey is at the language, this is the first step and the story begins; at any other time it is a change of language from preferences and the journey stays where it is.
    /// </summary>
    public TutorialChanges SelectLanguage(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (!Set.Journey.Languages.Contains(tag, StringComparer.Ordinal)) throw new EngineRefusedException(TutorialCodes.UnknownLanguage, "the journey offers no language '" + tag + "'");
        var from = Journey.State;
        Journey.Language = tag;
        if (from == JourneyState.Language) Journey.State = JourneyState.Story;       // the page is already the first: nothing reaches the language but the start and Back from the first page
        return Moved(from);
    }

    /// <summary>The next page of the story; after the last page the story is done and the player chooses how to enter (or, in a replay, the journey is done again).</summary>
    public TutorialChanges StoryNext()
    {
        RequireStory("next");
        var from = Journey.State;
        if (Journey.Page < Set.Journey.Pages.Count - 1) Journey.Page++;
        else FinishStory();
        return Moved(from);
    }

    /// <summary>The page before; from the choice of entry the player goes back to the last page; before the first page of a first run the player goes back to the language. A replay has no language step to go back to.</summary>
    public TutorialChanges StoryBack()
    {
        var from = Journey.State;
        if (from == JourneyState.Entry)
        {
            Journey.State = JourneyState.Story;
            Journey.Page = Set.Journey.Pages.Count - 1;
            Journey.StorySkipped = false;
            return Moved(from);
        }
        RequireStory("back");
        if (Journey.Page > 0) Journey.Page--;
        else if (Journey.Replaying) throw OutOfOrder("a replay of the story has nothing before its first page");
        else Journey.State = JourneyState.Language;
        return Moved(from);
    }

    /// <summary>The player skips the story: the same as reaching its end, and remembered as skipped.</summary>
    public TutorialChanges StorySkip()
    {
        RequireStory("skip");
        var from = Journey.State;
        FinishStory();
        Journey.StorySkipped = true;
        return Moved(from);
    }

    private void RequireStory(string what)
    {
        if (Journey.State != JourneyState.Story) throw OutOfOrder("the story can only be moved (" + what + ") while it is showing, the journey is at " + TutorialFacts.JourneyName(Journey.State));
    }

    private void FinishStory()
    {
        Journey.StorySkipped = false;
        Journey.State = Journey.Replaying ? JourneyState.Done : JourneyState.Entry;
        Journey.Replaying = false;
    }

    /// <summary>The player chooses how to enter: sign in, register, host, join or guest, whichever the document offers. Only after the story.</summary>
    public TutorialChanges ChooseEntry(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (Journey.State != JourneyState.Entry) throw OutOfOrder("an entry can only be chosen after the language and the story, the journey is at " + TutorialFacts.JourneyName(Journey.State));
        if (!Set.Journey.Entries.Contains(entry, StringComparer.Ordinal)) throw new EngineRefusedException(TutorialCodes.UnknownEntry, "the journey offers no entry '" + entry + "'");
        var from = Journey.State;
        Journey.Entry = entry;
        Journey.State = JourneyState.Done;
        return Moved(from);
    }

    /// <summary>Shows the story again from its first page (from preferences). Only a player whose journey is done; the language and the way they entered stay.</summary>
    public TutorialChanges ReplayStory()
    {
        if (Journey.State != JourneyState.Done) throw OutOfOrder("only a finished journey can replay the story, it is at " + TutorialFacts.JourneyName(Journey.State));
        var from = Journey.State;
        Journey.State = JourneyState.Story;
        Journey.Page = 0;
        Journey.Replaying = true;
        Journey.StorySkipped = false;
        var changes = Moved(from);
        WithdrawVisibleTips(changes);
        return changes;
    }

    /// <summary>The place the player meant to go (a deep link, a room to join) is kept until the journey is done, then the host takes it. Empty clears it.</summary>
    public TutorialChanges SetIntent(string intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!IntentText.IsMatch(intent)) throw new EngineRefusedException(TutorialCodes.Intent, "an intent uses a-z, 0-9, dot, underscore, colon, slash and hyphen, at most " + MaxIntentLength + " characters");
        var from = Journey.State;
        Journey.Intent = intent;
        return Moved(from);
    }
}

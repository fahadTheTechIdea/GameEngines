using System.Globalization;
using System.Text;
using DialogueEngine;
using EngineCommon;
using TutorialEngine;

namespace GameRuntime.Tutorial.Headless;

/// <summary>
/// A headless game that teaches itself: a newcomer to a valley is taken through the first-run journey (language, story, entry) and then through a tutorial of twelve steps, with contextual tips, hints that
/// escalate while she stalls, a step skipped, a conversation with the mayor that is the step "talk", the tutorial finished, replayed and skipped. A second player on his own channel knows the game, and what he
/// already does is never taught. The tutorial's words are rendered from the shared locale tables. A checkpoint is taken part way and the rest is replayed from it on a fresh session: the replay must end in the
/// same state and raise the same events as the live run. The printed output is hashed; two runs print the same hash.
/// </summary>
public static class ValleyHeadlessGame
{
    public const int Ok = 0, Refused = 2, Diverged = 3;

    public const int TicksPerSecond = 50;
    private const string Usage = "usage: GameRuntime.Tutorial.Headless [--ticks <1000-100000>] [--checkpoint-tick <tick>] [--verbose]";

    private sealed class Reporter : IRuntimeReporter
    {
        public List<string> Reports { get; } = new();
        public void Report(string code, string message, Exception? exception) => Reports.Add(code + ": " + message);
    }

    private static string Resource(string name)
    {
        using var stream = typeof(ValleyHeadlessGame).Assembly.GetManifestResourceStream(name);
        if (stream is null) throw new InvalidOperationException("the program does not carry " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        int ticks = 5000, checkpointTick = 2000;
        var verbose = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--ticks" when i + 1 < args.Count && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out ticks) && ticks is >= 1000 and <= 100_000: i++; break;
                case "--checkpoint-tick" when i + 1 < args.Count && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out checkpointTick) && checkpointTick >= 1: i++; break;
                case "--verbose": verbose = true; break;
                case "--help": output.WriteLine(Usage); return Ok;
                default: error.WriteLine("unknown or incomplete option '" + args[i] + "'"); error.WriteLine(Usage); return Refused;
            }
        }
        if (checkpointTick >= ticks) { error.WriteLine("--checkpoint-tick must be before --ticks"); error.WriteLine(Usage); return Refused; }
        try { return Play(ticks, checkpointTick, verbose, output); }
        catch (EngineRefusedException exception)
        {
            error.WriteLine(exception.Code + ": " + exception.Message);
            return Refused;
        }
    }

    private static int Play(int ticks, int checkpointTick, bool verbose, TextWriter output)
    {
        var set = TutorialLoader.Load(Resource("valley.tutorial.json"));
        var chain = LocaleChain.Create("en", [LocaleTable.Load(Resource("valley.en.json"))], "en");
        var dialogueChain = LocaleChain.Create("en", [LocaleTable.Load(MayorTalk.Messages)], "en");
        var mayor = new Mayor();
        var dialogueSet = DialogueLoader.Load(MayorTalk.Document, mayor);
        var reporter = new Reporter();
        var session = new RuntimeSession(new RuntimeClock(TicksPerSecond, 8), reporter);
        var tutorialChannels = new TutorialChannels();
        tutorialChannels.Install(session);
        var dialogueChannels = new DialogueChannels();
        dialogueChannels.Install(session);
        var game = new ValleyGame();
        var input = new InputState(() => session.Clock.Tick + 1);
        var journal = new TutorialJournal();
        var primary = new TutorialComponent(TutorialRuntime.Create(set), new TickTutorialClock(), game.Facts, tutorialChannels, "tutorial", journal: journal);
        var expert = new TutorialComponent(TutorialRuntime.Create(set), new TickTutorialClock(), game.Facts, tutorialChannels, "tutorial-expert", "expert");
        var facts = new TutorialDialogueFacts(primary);
        TutorialDialogueFacts.Check(dialogueSet, set);
        var dialogue = new DialogueComponent(new DialogueRuntime(dialogueSet, 1), mayor, facts.Facts, dialogueChannels);
        var speaker = new TutorialDialogueSpeaker("speaker", [new TutorialSpokenStep("talk", "mayor-intro", "dialogue_finished")], set, dialogueSet);
        session.AddComponent(new TutorialInputComponent(input, set), 5);
        session.AddComponent(primary, 30);
        session.AddComponent(expert, 31);
        session.AddComponent(dialogue, 35);
        session.AddComponent(speaker, 40);
        var transcript = new TutorialTranscript(session);
        var player = new ValleyPlayer(session, input, game);
        var printed = new StringBuilder();
        void Say(string line) { printed.Append(line).Append('\n'); output.WriteLine(line); }
        string Who(string channel) => channel.Length == 0 ? "" : "[" + channel + "] ";
        var saveNeeded = false;
        var saves = 0;
        session.Subscribe<TutorialRefused>(e => Say($"tick {e.Tick}: {Who(e.Channel)}refused {e.RequestId}: {e.Code}"));
        session.Subscribe<TutorialJourneyChanged>(e => Say($"tick {e.Tick}: {Who(e.Channel)}journey {e.From} -> {e.To}, page {e.Page + 1}, language {(e.Language.Length == 0 ? "-" : e.Language)}"));
        session.Subscribe<TutorialSettingChanged>(e => Say($"tick {e.Tick}: {Who(e.Channel)}setting {e.Name} = {e.Value}"));
        session.Subscribe<TutorialPhaseChanged>(e => Say($"tick {e.Tick}: {Who(e.Channel)}tutorial {e.From} -> {e.To}"));
        session.Subscribe<TutorialSkillMastered>(e => Say($"tick {e.Tick}: {Who(e.Channel)}skill mastered: {TutorialNames.Skill(set, chain, set.SkillById[e.Skill].Index)}"));
        session.Subscribe<TutorialStepChanged>(e =>
        {
            var step = set.StepById[e.Step];
            Say($"tick {e.Tick}: {Who(e.Channel)}step \"{TutorialNames.Step(set, chain, step.Index)}\" {e.From} -> {e.To}" + (e.To == "active" ? ": " + TutorialNames.StepBody(set, chain, step.Index) : ""));
        });
        session.Subscribe<TutorialHintChanged>(e =>
        {
            if (e.Level == 0) { if (verbose) Say($"tick {e.Tick}: {Who(e.Channel)}hint for {e.Step} taken away"); return; }
            Say($"tick {e.Tick}: {Who(e.Channel)}hint {e.Level} ({e.Kind}) for {e.Step}: \"{TutorialNames.Hint(set, chain, set.StepById[e.Step].Index, e.Level)}\" target {(e.Target.Length == 0 ? "-" : e.Target)}, motion {e.Motion}");
        });
        session.Subscribe<TutorialTipChanged>(e => Say($"tick {e.Tick}: {Who(e.Channel)}tip {e.Change}: \"{TutorialNames.Tip(set, chain, set.TipById[e.Tip].Index)}\"" + (e.Change == "shown" ? $" until step {e.Until}, motion {e.Motion}" : "")));
        session.Subscribe<TutorialSaveNeeded>(e => { if (e.Channel.Length == 0) saveNeeded = true; });
        session.Subscribe<DialogueLineShown>(e =>
        {
            Say($"tick {e.Tick}: mayor says: \"{dialogueChain.Render(e.Key, Array.Empty<MessageArg>()).Text}\"");
            player.Later(e.Tick + 30, () => session.Submit(new DialogueAdvanceRequested("read-" + e.Tick, "")));
        });
        session.Subscribe<DialogueEnded>(e => Say($"tick {e.Tick}: the conversation {e.Conversation} ended ({e.Result})"));
        session.Init();

        var tickMicroseconds = RuntimeClock.MicrosecondsPerSecond / TicksPerSecond;
        byte[]? checkpoint = null;
        var linesAtCheckpoint = 0;
        long checkpointComponentTick = 0;
        byte[]? firstSave = null;
        for (var tick = 1; tick <= ticks; tick++)
        {
            player.Act(tick);
            if (session.Update(tickMicroseconds) != 1) throw new InvalidOperationException("the session did not run tick " + tick + " (state " + session.State + "): " + string.Join("; ", reporter.Reports));
            if (saveNeeded && primary.IsQuiet && tutorialChannels.IsQuiet)
            {
                // The player's progress is saved the moment it changes, not at exit.
                saveNeeded = false;
                saves++;
                var bytes = TutorialCheckpoint.Capture(primary);
                firstSave ??= bytes;
                if (verbose) Say($"tick {tick}: saved (state {primary.Runtime.StateDigest()[..16]}, {bytes.Length} bytes)");
            }
            if (checkpoint is null && tick >= checkpointTick && primary.IsQuiet && tutorialChannels.IsQuiet)
            {
                checkpoint = TutorialCheckpoint.Capture(primary);
                linesAtCheckpoint = transcript.Lines.Count;
                checkpointComponentTick = primary.Tick;
            }
        }
        if (reporter.Reports.Count > 0) throw new InvalidOperationException("the session reported: " + string.Join("; ", reporter.Reports));

        var runtime = primary.Runtime;
        Say($"ended at tick {ticks}: {runtime.Satisfied()} of {set.Steps.Count} steps satisfied ({runtime.ProgressPermille()} permille), phase {TutorialFacts.PhaseName(runtime.Phase)}, journey {TutorialFacts.JourneyName(runtime.Journey.State)} in {runtime.Journey.Language}, entered by {runtime.Journey.Entry}, intent {runtime.Journey.Intent}");
        Say($"settings: reduced motion {(runtime.ReducedMotion ? "on" : "off")}, text scale {runtime.TextScale}, tips {(runtime.TipsEnabled ? "on" : "off")}; tips shown: {string.Join(", ", set.Tips.Where(t => runtime.Tips[t.Index].Shows > 0).Select(t => t.Id + " x" + runtime.Tips[t.Index].Shows))}");
        Say($"expert: {string.Join(", ", set.Steps.Select(s => s.Id + "=" + TutorialFacts.StatusName(expert.Runtime.Steps[s.Index].Status)))}");
        Say($"saves: {saves} taken because the progress changed; conversations: {speaker.Started} started, {speaker.Finished} finished; events {transcript.Lines.Count}");
        var digest = runtime.StateDigest();
        Say($"tutorial state {digest[..16]}, expert state {expert.Runtime.StateDigest()[..16]}, events {transcript.Hash[..16]}");

        // A player who closes the game right after choosing a language comes back to the story, in that language: the first save holds it.
        var returning = TutorialCheckpoint.Open(set, firstSave!);
        Say($"first save reopened: journey {TutorialFacts.JourneyName(returning.Journey.State)}, language {returning.Journey.Language}, page {returning.Journey.Page + 1}");

        // Replay everything after the checkpoint on a fresh session and compare (the newcomer's channel only; the expert has his own journal in a game that keeps one).
        var entries = TutorialJournal.Parse(journal.ToText());
        var resumed = TutorialCheckpoint.Open(set, checkpoint!);
        var replay = TutorialReplay.Run(resumed, entries, checkpointComponentTick, ticks, TicksPerSecond);
        var liveTail = Digest.Of(string.Join('\n', transcript.Lines.Skip(linesAtCheckpoint).Where(l => !l.Contains("Source=tutorial-expert", StringComparison.Ordinal) && !l.Contains("Source=tutorial-channels", StringComparison.Ordinal))));
        var same = replay.StateDigest == digest && replay.TranscriptHash == liveTail;
        Say($"replayed ticks {checkpointComponentTick + 1} to {ticks} from the checkpoint at tick {checkpointComponentTick}: state {(replay.StateDigest == digest ? "same" : "DIFFERENT")}, events {(replay.TranscriptHash == liveTail ? "same" : "DIFFERENT")} ({replay.Events} events)");
        Say("output-sha256 " + Digest.Of(printed.ToString()));
        return same ? Ok : Diverged;
    }
}

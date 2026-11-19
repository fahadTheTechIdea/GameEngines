using System.Text;
using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>
/// The random walks of <see cref="FuzzTests"/> over documents that also have subflows (calls and returns), variants of every mode, two quests with stage rules, external facts the host supplies fresh
/// on every call (so conditions, stage rules and content warnings change as the walk goes), content warnings, history, and conversations that queue and preempt. For every seed: a walk with random
/// saves and reloads ends in the state, transcript and save bytes of the same calls played straight; a refused call changes nothing; the invariants of the stack, the queue and the history hold after every call; and two
/// runs hash alike. The random numbers are test data only.
/// </summary>
public sealed class RichFuzzTests
{
    private const int Seeds = 200;

    /// <summary>One call and the facts the host supplies with it. <paramref name="Mask"/> says which: 1 the content-warning policy, 2 the rest, 3 both, 0 none, so a call can be refused part-way for a fact that is missing.</summary>
    private sealed record Call(string Kind, string Argument, bool Flag, long Level, bool WarnA, bool WarnB, int Mask);

    private sealed class Counts
    {
        public long Calls, Refusals, Saves, Ended, Preempted, Queued, Resumed, Started, Skipped, Quest, Signals, Replaced, Reloads, Histories, Frames;
    }

    private sealed class Walk
    {
        public List<Call> Calls { get; } = new();
        public List<string> Transcript { get; } = new();
        public DialogueRuntime Runtime { get; set; } = null!;
        public Counts Counts { get; } = new();
    }

    private static IReadOnlyDictionary<string, FactValue> FactsOf(Call call)
    {
        var facts = new Dictionary<string, FactValue>();
        if ((call.Mask & 2) != 0)
        {
            facts["ext.flag"] = FactValue.From(call.Flag);
            facts["ext.level"] = FactValue.From(call.Level);
        }
        if ((call.Mask & 1) != 0)
        {
            facts["ext.cw-a"] = FactValue.From(call.WarnA);
            facts["ext.cw-b"] = FactValue.From(call.WarnB);
        }
        return facts;
    }

    private static string Show(DialogueStep step)
    {
        var text = new StringBuilder(step.Kind.ToString());
        foreach (var e in step.Events) text.Append(" | ").Append(e);
        switch (step.Kind)
        {
            case StepKind.Line: text.Append(" line ").Append(step.Line!.Conversation).Append('/').Append(step.Line.Node).Append(' ').Append(step.Line.Key).Append(step.Line.Seen ? " seen" : "").Append(step.Line.Replaced ? " replaced" : ""); break;
            case StepKind.Choices: text.Append(" choices ").Append(step.Choices!.Node).Append(' ').Append(string.Join(",", step.Choices.Options.Select(o => o.Id + (o.Enabled ? "+" : "-") + (o.Replaced ? "r" : "")))); break;
            case StepKind.Ended: text.Append(" ended ").Append(step.Result).Append(' ').Append(step.EndedConversation); break;
            case StepKind.Queued: text.Append(" queued ").Append(step.Result); break;
        }
        return text.ToString();
    }

    private static string Flat(IEnumerable<HistoryEntry> entries) => string.Join(";", entries.Select(e => string.Join("|", e.Turn, e.Tick, e.Kind, e.Conversation, e.Node, e.Key, e.Chosen, e.Seen, e.Skipped, e.Replaced, string.Join(",", e.Args.Select(a => a.Name + "=" + a.Value)))));

    /// <summary>Applies a call and returns what it showed; a query call shows what it found.</summary>
    private static string Apply(DialogueRuntime runtime, Call call, long tick)
    {
        var facts = FactsOf(call);
        runtime.Tick = tick;
        switch (call.Kind)
        {
            case "start": return Show(runtime.Start(call.Argument, facts));
            case "next": return Show(runtime.Next(facts));
            case "choose": return Show(runtime.Choose(call.Argument, facts));
            case "timeout": return Show(runtime.Timeout(facts));
            case "interrupt": return Show(runtime.Interrupt(call.Argument, facts));
            case "skip": return Show(runtime.SkipSeen(facts));
            case "quest": return string.Join(" | ", runtime.StartQuest(call.Argument, facts));
            case "quests": return string.Join(" | ", runtime.UpdateQuests(facts));
            case "history": return Flat(runtime.History(int.Parse(call.Argument), call.Flag));
            case "explain": return string.Join(";", runtime.ExplainChoice(facts).Select(e => e.Id + ":" + e.Availability + ":" + e.Why));
        }
        throw new InvalidOperationException("unknown call " + call.Kind);
    }

    private static Call Choose(DialogueRuntime runtime, Random random)
    {
        var pick = random.Next(100);
        var supply = pick < 82 ? 3 : pick < 88 ? 0 : pick < 94 ? 1 : 2;
        if (runtime.State.Queue.Count > 0 && random.Next(4) == 0) supply = random.Next(3);   // calls that end a conversation with others waiting are often refused for a missing fact
        Call Make(string kind, string argument = "", bool flag = false) => new(kind, argument, flag || random.Next(2) == 0, random.Next(0, 6), random.Next(3) == 0, random.Next(3) == 0, supply);
        var roll = random.Next(100);
        if (roll < 6) return Make(new[] { "choose", "next", "timeout", "interrupt", "start" }[random.Next(5)], new[] { "o0", "a", "ghost", "talk" }[random.Next(4)]);
        if (roll < 12) return Make("start", RichGraph.Starters[random.Next(RichGraph.Starters.Length)]);
        if (roll < 16) return Make("interrupt", RichGraph.Interrupters[random.Next(RichGraph.Interrupters.Length)]);
        if (roll < 20) return Make("skip");
        if (roll < 23) return Make("quest", RichGraph.Quests[random.Next(RichGraph.Quests.Length)]);
        if (roll < 26) return Make("quests");
        if (roll < 29) return Make("history", random.Next(0, 15).ToString(), random.Next(2) == 0);
        if (roll < 31 && runtime.State.WaitingFor == Waiting.Choice) return Make("explain");
        switch (runtime.State.WaitingFor)
        {
            case Waiting.Nothing: return runtime.State.Conversation is null ? Make("start", random.Next(5) == 0 ? RichGraph.Starters[random.Next(3)] : "talk") : Make("next");
            case Waiting.Line: return Make("next");
            case Waiting.Choice:
            {
                var node = (ChoiceNode)runtime.Set.Conversations[runtime.State.Conversation!].Nodes[runtime.State.Node!];
                return Make("choose", node.Options[random.Next(node.Options.Count)].Id);
            }
        }
        throw new InvalidOperationException("unknown waiting state");
    }

    /// <summary>The rules of the state that hold after every call whether it was accepted or refused.</summary>
    private static void CheckInvariants(DialogueRuntime runtime, int seed, int step)
    {
        var state = runtime.State;
        Assert.True(state.History.Count <= runtime.Set.HistoryLimit, $"seed {seed} step {step}: the history outgrew its limit");
        Assert.True(state.History.Count <= state.Recorded, $"seed {seed} step {step}: more entries than were ever recorded");
        Assert.True(state.Queue.Count <= DialogueRuntime.QueueLimit, $"seed {seed} step {step}: the queue outgrew its limit");
        Assert.True(state.Frames.Count <= DialogueRuntime.CallDepthLimit, $"seed {seed} step {step}: the stack outgrew its limit");
        Assert.True(state.Queue.Count == 0 || state.Conversation is not null, $"seed {seed} step {step}: conversations wait in the queue but none is open");
        Assert.Equal(state.Conversation is null, runtime.RootConversation is null);
        for (var i = 1; i < state.History.Count; i++) Assert.True(state.History[i].Turn > state.History[i - 1].Turn, $"seed {seed} step {step}: history turns do not rise");
    }

    private static void Count(Counts counts, string shown)
    {
        counts.Calls++;
        if (shown.StartsWith("Ended", StringComparison.Ordinal)) counts.Ended++;
        counts.Preempted += Occurrences(shown, "Preempted");
        counts.Queued += Occurrences(shown, "Queued");
        counts.Resumed += Occurrences(shown, "Resumed");
        counts.Started += Occurrences(shown, "Started");
        counts.Skipped += Occurrences(shown, "SkippedEvent");
        counts.Quest += Occurrences(shown, "QuestEvent");
        counts.Signals += Occurrences(shown, "SignalEvent");
        counts.Replaced += Occurrences(shown, " replaced");
    }

    private static int Occurrences(string text, string what)
    {
        var count = 0;
        for (var at = text.IndexOf(what, StringComparison.Ordinal); at >= 0; at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    /// <summary>Plays one seed with random saves and reloads, or, given a script, exactly those calls straight with none (the control).</summary>
    private static Walk Play(DialogueSet set, int seed, IReadOnlyList<Call>? script = null)
    {
        var random = new Random(seed);
        var walk = new Walk { Runtime = new DialogueRuntime(set, (ulong)seed * 104729UL + 3) };
        var current = walk.Runtime;
        for (var step = 0; step < 300 && (script is null || step < script.Count); step++)
        {
            var call = script is null ? Choose(current, random) : script[step];
            walk.Calls.Add(call);
            var before = DialogueSave.StateDigest(current.State);
            try
            {
                var shown = Apply(current, call, step);
                walk.Transcript.Add(call.Kind + " " + call.Argument + " => " + shown);
                Count(walk.Counts, shown);
                if (call.Kind == "history") walk.Counts.Histories++;
            }
            catch (EngineRefusedException refusal)
            {
                walk.Counts.Calls++;
                walk.Counts.Refusals++;
                walk.Transcript.Add(call.Kind + " " + call.Argument + " => refused " + refusal.Code);
                Assert.True(before == DialogueSave.StateDigest(current.State), $"seed {seed} step {step}: a refused {call.Kind} changed the state");
            }
            CheckInvariants(current, seed, step);
            walk.Counts.Frames = Math.Max(walk.Counts.Frames, current.State.Frames.Count);
            if (script is null && random.Next(5) == 0)
            {
                var bytes = DialogueSave.Write(current);
                var opened = DialogueSave.Open(set, bytes);
                Assert.True(DialogueSave.StateDigest(current.State) == DialogueSave.StateDigest(opened.State), $"seed {seed} step {step}: a save opened to another state");
                Assert.True(bytes.AsSpan().SequenceEqual(DialogueSave.Write(opened)), $"seed {seed} step {step}: a save did not write the same bytes again");
                current = opened;
                walk.Counts.Saves++;
            }
            if (script is null && random.Next(15) == 0)
            {
                var digest = DialogueSave.StateDigest(current.State);
                current.Reload(set);
                walk.Counts.Reloads++;
                Assert.True(digest == DialogueSave.StateDigest(current.State), $"seed {seed} step {step}: reloading the document onto itself changed the state");
            }
        }
        walk.Runtime = current;
        return walk;
    }

    private static string Hash(Walk walk) => Digest.Of(string.Join("\n", walk.Transcript) + "\n" + DialogueSave.StateDigest(walk.Runtime.State));

    private static DialogueSet Load(int seed) => DialogueLoader.Load(new RichGraph(seed).Build(10 + seed % 31).Text, Fixture.Catalog);

    [Fact]
    public void RandomWalksOverRichDocumentsEndWhereTheSameCallsPlayedStraightEndAndEveryRunHashesTheSame()
    {
        var total = new Counts();
        for (var seed = 0; seed < Seeds; seed++)
        {
            var set = Load(seed);
            var walk = Play(set, seed);
            var control = Play(set, seed, walk.Calls);
            Assert.True(walk.Transcript.SequenceEqual(control.Transcript), $"seed {seed}: the walk with saves showed another transcript than the same calls played straight");
            Assert.True(DialogueSave.StateDigest(walk.Runtime.State) == DialogueSave.StateDigest(control.Runtime.State), $"seed {seed}: the walk with saves ended in another state");
            Assert.True(DialogueSave.Write(walk.Runtime).AsSpan().SequenceEqual(DialogueSave.Write(control.Runtime)), $"seed {seed}: the final saves differ");
            var again = Play(Load(seed), seed);
            Assert.True(Hash(walk) == Hash(again), $"seed {seed}: two runs of the same seed hashed differently");
            foreach (var field in typeof(Counts).GetFields())
            {
                if (field.Name == "Frames") field.SetValue(total, Math.Max((long)field.GetValue(total)!, (long)field.GetValue(walk.Counts)!));
                else field.SetValue(total, (long)field.GetValue(total)! + (long)field.GetValue(walk.Counts)!);
            }
        }
        // The walks reached every feature this test is about, many times.
        Assert.True(total.Calls > 59_000, "calls " + total.Calls);
        Assert.True(total.Refusals > 5_000, "refusals " + total.Refusals);
        Assert.True(total.Saves > 8_000, "saves " + total.Saves);
        Assert.True(total.Ended > 3_000, "ended " + total.Ended);
        Assert.True(total.Preempted > 600, "preempted " + total.Preempted);
        Assert.True(total.Queued > 1_000, "queued " + total.Queued);
        Assert.True(total.Resumed > 600, "resumed " + total.Resumed);
        Assert.True(total.Started > 500, "started " + total.Started);
        Assert.True(total.Skipped > 400, "skipped " + total.Skipped);
        Assert.True(total.Quest > 800, "quest " + total.Quest);
        Assert.True(total.Signals > 5_000, "signals " + total.Signals);
        Assert.True(total.Replaced > 700, "replaced " + total.Replaced);
        Assert.True(total.Reloads > 2_500, "reloads " + total.Reloads);
        Assert.True(total.Histories > 1_200, "histories " + total.Histories);
        Assert.True(total.Frames >= 4, "deepest stack " + total.Frames);
    }

    [Fact]
    public void ASaveTakenAtAnyCallOfARichWalkResumesToTheStateOfTheWalkThatNeverSaved()
    {
        for (var seed = 5000; seed < 5010; seed++)
        {
            var set = Load(seed);
            var straight = Play(set, seed);
            var finalDigest = DialogueSave.StateDigest(straight.Runtime.State);
            for (var cut = 0; cut <= straight.Calls.Count; cut += 7)
            {
                var head = new DialogueRuntime(set, (ulong)seed * 104729UL + 3);
                for (var i = 0; i < cut; i++) _ = Try(head, straight.Calls[i], i);
                var resumed = DialogueSave.Open(set, DialogueSave.Write(head));
                for (var i = cut; i < straight.Calls.Count; i++) _ = Try(resumed, straight.Calls[i], i);
                Assert.True(finalDigest == DialogueSave.StateDigest(resumed.State), $"seed {seed}: resuming after call {cut} ended in another state");
            }
        }
    }

    private static string? Try(DialogueRuntime runtime, Call call, long tick)
    {
        var before = DialogueSave.StateDigest(runtime.State);
        try
        {
            Apply(runtime, call, tick);
            return null;
        }
        catch (EngineRefusedException refusal)
        {
            Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
            return refusal.Code;
        }
    }
}

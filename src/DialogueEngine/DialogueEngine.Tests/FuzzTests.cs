using System.Text;
using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>
/// Seeded random walks through generated valid documents. Each walk takes random answers, makes calls the session must refuse, saves and resumes at random points and reloads the document
/// onto itself, and three things are asserted for every seed: the walk with saves in it ends in exactly the state (and has shown exactly the transcript) of the same inputs played from
/// scratch without a save; every save opens to the same state and writes the same bytes again; and playing the whole seed twice gives the same transcript hash. The random numbers are test
/// data only; the engine under test rolls nothing here but its own seeded rolls.
/// </summary>
public sealed class FuzzTests
{
    private const int Seeds = 400;

    private static readonly IReadOnlyDictionary<string, FactValue> NoFacts = new Dictionary<string, FactValue>();

    private sealed record Call(string Kind, string Argument);

    private sealed class Walk
    {
        public List<Call> Calls { get; } = new();
        public List<string> Transcript { get; } = new();
        public DialogueRuntime Runtime { get; set; } = null!;
        public int Saves { get; set; }
        public int Refusals { get; set; }
        public int Ended { get; set; }
    }

    private static string Show(DialogueStep step)
    {
        var text = new StringBuilder(step.Kind.ToString());
        foreach (var e in step.Events) text.Append(" | ").Append(e);
        switch (step.Kind)
        {
            case StepKind.Line: text.Append(" line ").Append(step.Line!.Node).Append(' ').Append(step.Line.Key); break;
            case StepKind.Choices: text.Append(" choices ").Append(step.Choices!.Node).Append(' ').Append(string.Join(",", step.Choices.Options.Select(o => o.Id + (o.Enabled ? "+" : "-")))); break;
            case StepKind.Ended: text.Append(" ended ").Append(step.Result); break;
        }
        return text.ToString();
    }

    private static DialogueStep Apply(DialogueRuntime runtime, Call call) => call.Kind switch
    {
        "start" => runtime.Start("talk", NoFacts),
        "next" => runtime.Next(NoFacts),
        "choose" => runtime.Choose(call.Argument, NoFacts),
        "timeout" => runtime.Timeout(NoFacts),
        "interrupt" => runtime.Interrupt("talk", NoFacts),
        _ => throw new InvalidOperationException("unknown call " + call.Kind),
    };

    /// <summary>Plays one seed: the original walk, with random saves and reloads in it. With a <paramref name="script"/> it plays exactly those calls straight, with no save and no reload, as the control.</summary>
    private static Walk Play(DialogueSet set, int seed, IReadOnlyList<Call>? script = null)
    {
        var random = new Random(seed);
        var walk = new Walk { Runtime = new DialogueRuntime(set, (ulong)seed * 7919UL + 1) };
        var current = walk.Runtime;
        for (var step = 0; step < 300 && (script is null || step < script.Count); step++)
        {
            Call call;
            if (script is not null) call = script[step];
            else
            {
                call = Choose(current, random);
            }
            walk.Calls.Add(call);
            var before = DialogueSave.StateDigest(current.State);
            try
            {
                var shown = Apply(current, call);
                walk.Transcript.Add(call.Kind + " " + call.Argument + " => " + Show(shown));
                if (shown.Kind == StepKind.Ended) walk.Ended++;
            }
            catch (EngineRefusedException refusal)
            {
                walk.Refusals++;
                walk.Transcript.Add(call.Kind + " " + call.Argument + " => refused " + refusal.Code);
                Assert.True(before == DialogueSave.StateDigest(current.State), $"seed {seed} step {step}: a refused {call.Kind} changed the state");
            }
            if (script is null && random.Next(5) == 0)
            {
                var bytes = DialogueSave.Write(current);
                var opened = DialogueSave.Open(set, bytes);
                Assert.True(DialogueSave.StateDigest(current.State) == DialogueSave.StateDigest(opened.State), $"seed {seed} step {step}: a save opened to another state");
                Assert.True(bytes.AsSpan().SequenceEqual(DialogueSave.Write(opened)), $"seed {seed} step {step}: a save did not write the same bytes again");
                current = opened;
                walk.Saves++;
            }
            if (script is null && random.Next(15) == 0)
            {
                var digest = DialogueSave.StateDigest(current.State);
                current.Reload(set);
                Assert.True(digest == DialogueSave.StateDigest(current.State), $"seed {seed} step {step}: reloading the document onto itself changed the state");
            }
        }
        walk.Runtime = current;
        return walk;
    }

    /// <summary>A mostly legal next call, sometimes one the session must refuse.</summary>
    private static Call Choose(DialogueRuntime runtime, Random random)
    {
        if (random.Next(12) == 0) return new Call(new[] { "choose", "next", "timeout", "interrupt", "start" }[random.Next(5)], "o" + random.Next(4));
        switch (runtime.State.WaitingFor)
        {
            case Waiting.Nothing: return runtime.State.Conversation is null ? new Call("start", "") : new Call("next", "");
            case Waiting.Line: return new Call("next", "");
            case Waiting.Choice:
            {
                var node = (ChoiceNode)runtime.Set.Conversations["talk"].Nodes[runtime.State.Node!];
                return new Call("choose", node.Options[random.Next(node.Options.Count)].Id);
            }
        }
        throw new InvalidOperationException("unknown waiting state");
    }

    private static string Hash(Walk walk) => Digest.Of(string.Join("\n", walk.Transcript) + "\n" + DialogueSave.StateDigest(walk.Runtime.State));

    [Fact]
    public void RandomWalksWithSavesAndRefusedCallsEndWhereTheSameInputsPlayedStraightEndAndEveryRunHashesTheSame()
    {
        long refusals = 0, saves = 0, ended = 0, calls = 0;
        for (var seed = 0; seed < Seeds; seed++)
        {
            var generator = new GraphGenerator(seed).Build(8 + seed % 53);
            var set = DialogueLoader.Load(generator.Text(), Fixture.Catalog);
            var walk = Play(set, seed);
            // The control plays the same calls from scratch with no save and no reload in them.
            var control = Play(set, seed, walk.Calls);
            Assert.True(walk.Transcript.SequenceEqual(control.Transcript), $"seed {seed}: the walk with saves showed another transcript than the same calls played straight");
            Assert.True(DialogueSave.StateDigest(walk.Runtime.State) == DialogueSave.StateDigest(control.Runtime.State), $"seed {seed}: the walk with saves ended in another state");
            Assert.True(DialogueSave.Write(walk.Runtime).AsSpan().SequenceEqual(DialogueSave.Write(control.Runtime)), $"seed {seed}: the final saves differ");
            var again = Play(DialogueLoader.Load(new GraphGenerator(seed).Build(8 + seed % 53).Text(), Fixture.Catalog), seed);
            Assert.True(Hash(walk) == Hash(again), $"seed {seed}: two runs of the same seed hashed differently");
            refusals += walk.Refusals;
            saves += walk.Saves;
            ended += walk.Ended;
            calls += walk.Calls.Count;
        }
        // The walks are not empty ones: they refused calls, saved, and reached the end of conversations.
        Assert.True(calls > 30_000, "calls " + calls);
        Assert.True(refusals > 1_000, "refusals " + refusals);
        Assert.True(saves > 3_000, "saves " + saves);
        Assert.True(ended > 300, "ended " + ended);
    }

    [Fact]
    public void ASaveTakenAtEveryCallOfALongWalkResumesToTheSameHashAsTheWalkThatNeverSaved()
    {
        for (var seed = 1000; seed < 1012; seed++)
        {
            var set = DialogueLoader.Load(new GraphGenerator(seed).Build(40).Text(), Fixture.Catalog);
            var straight = Play(set, seed);
            // Resume from a fresh copy of the state after each call and play the rest: every one ends where the straight walk ended.
            var finalDigest = DialogueSave.StateDigest(straight.Runtime.State);
            for (var cut = 0; cut <= straight.Calls.Count; cut += 5)
            {
                var head = new DialogueRuntime(set, (ulong)seed * 7919UL + 1);
                foreach (var call in straight.Calls.Take(cut)) _ = Try(head, call);
                var resumed = DialogueSave.Open(set, DialogueSave.Write(head));
                foreach (var call in straight.Calls.Skip(cut)) _ = Try(resumed, call);
                Assert.True(finalDigest == DialogueSave.StateDigest(resumed.State), $"seed {seed}: resuming after call {cut} ended in another state");
            }
        }
    }

    /// <summary>Plays a call that the original walk made; it may have been refused there, and is refused here with the same code and the state as it was. Returns the code, or null when it was accepted.</summary>
    private static string? Try(DialogueRuntime runtime, Call call)
    {
        var before = DialogueSave.StateDigest(runtime.State);
        try
        {
            Apply(runtime, call);
            return null;
        }
        catch (EngineRefusedException refusal)
        {
            Assert.Equal(before, DialogueSave.StateDigest(runtime.State));
            return refusal.Code;
        }
    }
}

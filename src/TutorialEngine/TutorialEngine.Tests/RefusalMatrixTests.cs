using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using EngineCommon;
using ScenarioEngine.Contracts;
using Xunit;

namespace TutorialEngine.Tests;

/// <summary>Every refusal code is reached by at least one input, and the set of codes the matrix reaches is exactly the constants of <see cref="TutorialCodes"/>.</summary>
public sealed class RefusalMatrixTests
{
    private static JsonNode Deep(int levels)
    {
        JsonNode node = Tiny.Cond("flag", "eq", true);
        for (var i = 0; i < levels; i++) node = new JsonObject { ["not"] = node };
        return node;
    }

    /// <summary>Documents that are wrong in one place: the code each is refused with, a name for the failure message, and the document.</summary>
    private static IEnumerable<(string Code, string Name, Func<string> Text)> Documents()
    {
        string Doc(Action<JsonObject> edit) { var r = Tiny.Root(); edit(r); return r.ToJsonString(); }
        yield return (TutorialCodes.Format, "format 2", () => Doc(r => r["format"] = 2));
        yield return (TutorialCodes.Format, "not JSON", () => "{ nope");
        yield return (TutorialCodes.Format, "steps is not an array", () => Doc(r => r["steps"] = 5));
        yield return (TutorialCodes.Format, "motion unknown", () => Doc(r => Tiny.Step(r, "s1")["motion"] = "wobble"));
        yield return (TutorialCodes.Format, "condition operator", () => Doc(r => Tiny.Step(r, "s1")["complete"]!["op"] = "like"));
        yield return (TutorialCodes.Format, "hint kind unknown", () => Doc(r => Tiny.Step(r, "s1")["hints"]![0]!["kind"] = "shout"));
        yield return (TutorialCodes.Format, "host fact type", () => Doc(r => Tiny.Array(r, "facts")[0]!["type"] = "float"));
        yield return (TutorialCodes.Format, "steps entry without an id", () => Doc(r => Tiny.Array(r, "steps").Add(5)));
        yield return (TutorialCodes.Format, "language is a number", () => Doc(r => r["journey"]!["languages"]![0] = 1));
        yield return (TutorialCodes.Format, "requires holds a number", () => Doc(r => Tiny.Step(r, "s2")["requires"] = new JsonArray(1)));
        yield return (TutorialCodes.Format, "entry is a number", () => Doc(r => r["journey"]!["entries"]![0] = 1));
        yield return (TutorialCodes.Format, "id with a capital", () => Doc(r => r["id"] = "Tiny"));
        yield return (TutorialCodes.UnknownField, "extra top-level field", () => Doc(r => r["extra"] = 1));
        yield return (TutorialCodes.UnknownField, "extra step field", () => Doc(r => Tiny.Step(r, "s1")["colour"] = "red"));
        yield return (TutorialCodes.UnknownField, "condition with an unknown shape", () => Doc(r => Tiny.Step(r, "s1")["complete"] = new JsonObject { ["fact"] = "flag" }));
        yield return (TutorialCodes.MissingField, "no journey", () => Doc(r => r.Remove("journey")));
        yield return (TutorialCodes.MissingField, "step without complete", () => Doc(r => Tiny.Step(r, "s1").Remove("complete")));
        yield return (TutorialCodes.MissingField, "int host fact without range", () => Doc(r => Tiny.Array(r, "facts").Add(new JsonObject { ["id"] = "x", ["type"] = "int" })));
        yield return (TutorialCodes.Range, "no steps in a day", () => Doc(r => r["time"]!["stepsPerDay"] = 0));
        yield return (TutorialCodes.Range, "too many active steps", () => Doc(r => r["limits"]!["maxActiveSteps"] = 9));
        yield return (TutorialCodes.Range, "tip duration 0", () => Doc(r => Tiny.Tip(r, "t1")["duration"] = 0));
        yield return (TutorialCodes.Range, "tip shows 0", () => Doc(r => Tiny.Tip(r, "t1")["maxShows"] = 0));
        yield return (TutorialCodes.Range, "hint idle 0", () => Doc(r => Tiny.Step(r, "s1")["hints"]![0]!["idle"] = 0));
        yield return (TutorialCodes.Range, "no steps", () => Doc(r => r["steps"] = new JsonArray()));
        yield return (TutorialCodes.Range, "skill mastery 0", () => Doc(r => Tiny.Array(r, "skills")[0]!["mastered"] = 0));
        yield return (TutorialCodes.Range, "evidence worth 0", () => Doc(r => Tiny.Array(r, "skills")[0]!["evidence"]![0]!["points"] = 0));
        yield return (TutorialCodes.Range, "no pages", () => Doc(r => r["journey"]!["pages"] = new JsonArray()));
        yield return (TutorialCodes.Range, "no languages", () => Doc(r => r["journey"]!["languages"] = new JsonArray()));
        yield return (TutorialCodes.Range, "more entries than kinds", () => Doc(r => r["journey"]!["entries"] = new JsonArray("sign-in", "register", "host", "join", "guest", "x")));
        yield return (TutorialCodes.Range, "condition nested too deep", () => Doc(r => Tiny.Step(r, "s1")["complete"] = Deep(8)));
        yield return (TutorialCodes.Range, "text key too long", () => Doc(r => Tiny.Step(r, "s1")["target"] = new string('a', 129)));
        yield return (TutorialCodes.Range, "document too long", () => new string(' ', TutorialLoader.MaxCharacters + 1));
        yield return (TutorialCodes.Range, "tip priority beyond", () => Doc(r => Tiny.Tip(r, "t1")["priority"] = 1001));
        yield return (TutorialCodes.Duplicate, "step twice", () => Doc(r => Tiny.Array(r, "steps").Add(Tiny.Copy(Tiny.Step(r, "s1")))));
        yield return (TutorialCodes.Duplicate, "tip twice", () => Doc(r => Tiny.Array(r, "tips").Add(Tiny.Copy(Tiny.Tip(r, "t1")))));
        yield return (TutorialCodes.Duplicate, "event twice", () => Doc(r => Tiny.Array(r, "events").Add(Tiny.Copy(Tiny.Array(r, "events")[0]!))));
        yield return (TutorialCodes.Duplicate, "skill twice", () => Doc(r => Tiny.Array(r, "skills").Add(Tiny.Copy(Tiny.Array(r, "skills")[0]!))));
        yield return (TutorialCodes.Duplicate, "host fact twice", () => Doc(r => Tiny.Array(r, "facts").Add(Tiny.Copy(Tiny.Array(r, "facts")[0]!))));
        yield return (TutorialCodes.Duplicate, "prerequisite twice", () => Doc(r => Tiny.Step(r, "s2")["requires"] = new JsonArray("s1", "s1")));
        yield return (TutorialCodes.Duplicate, "entry twice", () => Doc(r => r["journey"]!["entries"] = new JsonArray("host", "host")));
        yield return (TutorialCodes.Duplicate, "language twice", () => Doc(r => r["journey"]!["languages"] = new JsonArray("en", "en")));
        yield return (TutorialCodes.Duplicate, "page twice", () => Doc(r => Tiny.Array(r["journey"]!.AsObject(), "pages").Add(Tiny.Copy(Tiny.Array(r["journey"]!.AsObject(), "pages")[0]!))));
        yield return (TutorialCodes.Duplicate, "input action twice", () => Doc(r => Tiny.Array(r, "inputs").Add(Tiny.Copy(Tiny.Array(r, "inputs")[0]!))));
        yield return (TutorialCodes.Reference, "prerequisite unknown", () => Doc(r => Tiny.Step(r, "s2")["requires"] = new JsonArray("nope")));
        yield return (TutorialCodes.Reference, "step skill unknown", () => Doc(r => Tiny.Step(r, "s1")["skill"] = "nope"));
        yield return (TutorialCodes.Reference, "condition reads an unknown fact", () => Doc(r => Tiny.Step(r, "s3")["complete"]!["fact"] = "nerve"));
        yield return (TutorialCodes.Reference, "condition reads an unknown event", () => Doc(r => Tiny.Step(r, "s3")["complete"]!["fact"] = "event.zzz.count"));
        yield return (TutorialCodes.Reference, "tip after an unknown step", () => Doc(r => Tiny.Tip(r, "t1")["afterStep"] = "nope"));
        yield return (TutorialCodes.Reference, "input for an unknown event", () => Doc(r => Tiny.Array(r, "inputs")[0]!["event"] = "nope"));
        yield return (TutorialCodes.Reference, "evidence for an unknown event", () => Doc(r => Tiny.Array(r, "skills")[0]!["evidence"]![0]!["event"] = "nope"));
        yield return (TutorialCodes.Reserved, "host fact in the event namespace", () => Doc(r => Tiny.Array(r, "facts").Add(new JsonObject { ["id"] = "event.x", ["type"] = "bool" })));
        yield return (TutorialCodes.Reserved, "host fact in the tutorial namespace", () => Doc(r => Tiny.Array(r, "facts").Add(new JsonObject { ["id"] = "tutorial.x", ["type"] = "bool" })));
        yield return (TutorialCodes.Reserved, "input named like a control", () => Doc(r => Tiny.Array(r, "inputs")[0]!["action"] = "tutorial.skip"));
        yield return (TutorialCodes.Reserved, "input named like a journey control", () => Doc(r => Tiny.Array(r, "inputs")[0]!["action"] = "journey.next"));
        yield return (TutorialCodes.Prereq, "prerequisite declared later", () => Doc(r => Tiny.Step(r, "s1")["requires"] = new JsonArray("s2")));
        yield return (TutorialCodes.Prereq, "prerequisite is the step itself", () => Doc(r => Tiny.Step(r, "s2")["requiresAny"] = new JsonArray("s2")));
        yield return (TutorialCodes.Hint, "hints do not wait longer", () => Doc(r => Tiny.Step(r, "s1")["hints"]![1]!["idle"] = 10));
        yield return (TutorialCodes.Hint, "a hint gentler than the one before", () => Doc(r => Tiny.Step(r, "s1")["hints"]![2]!["kind"] = "nudge"));
        yield return (TutorialCodes.Scope, "a tip reads since the step began", () => Doc(r => Tiny.Tip(r, "t1")["when"] = Tiny.Cond("event.e1.since_step", "gte", 1)));
        yield return (TutorialCodes.Scope, "a branch reads since the step began", () => Doc(r => Tiny.Step(r, "s2")["when"] = Tiny.Cond("event.e1.since_step", "gte", 1)));
        yield return (TutorialCodes.Journey, "language tag with a capital", () => Doc(r => r["journey"]!["languages"] = new JsonArray("EN")));
        yield return (TutorialCodes.Journey, "default language not offered", () => Doc(r => r["journey"]!["defaultLanguage"] = "de"));
        yield return (TutorialCodes.Journey, "entry of a kind the engine lacks", () => Doc(r => r["journey"]!["entries"] = new JsonArray("teleport")));
        yield return (TutorialCodes.Skill, "evidence repeats an event", () => Doc(r => Tiny.Array(r, "skills")[0]!["evidence"]!.AsArray().Add(Tiny.Copy(Tiny.Array(r, "skills")[0]!["evidence"]![0]!))));
    }

    /// <summary>Calls that are wrong: the code, a name, and the call. Each is made on a fresh runtime of the small document.</summary>
    private static IEnumerable<(string Code, string Name, Action<TutorialRuntime> Call)> Calls()
    {
        yield return (TutorialCodes.UnknownEvent, "observe an unknown event", r => r.Observe("zzz"));
        yield return (TutorialCodes.Count, "observe zero times", r => r.Observe("e1", 0));
        yield return (TutorialCodes.UnknownStep, "skip an unknown step", r => r.SkipStep("zzz"));
        yield return (TutorialCodes.UnknownTip, "dismiss an unknown tip", r => r.DismissTip("zzz"));
        yield return (TutorialCodes.UnknownLanguage, "a language the journey lacks", r => r.SelectLanguage("zz"));
        yield return (TutorialCodes.UnknownEntry, "an entry the journey lacks", r => { var fresh = TutorialRuntime.Create(r.Set); fresh.SelectLanguage("en"); fresh.StorySkip(); fresh.ChooseEntry("zzz"); });
        yield return (TutorialCodes.NotSkippable, "skip a step that may not be skipped", r => { r.Advance(0, Fixture.Host()); r.MoveTo(1); r.Observe("e1"); r.Evaluate(Fixture.Host()); r.SkipStep("s2"); });
        yield return (TutorialCodes.StepState, "skip a step not being taught", r => r.SkipStep("s3"));
        yield return (TutorialCodes.TipState, "dismiss a tip that does not show", r => r.DismissTip("t1"));
        yield return (TutorialCodes.Phase, "start twice", r => r.Start());
        yield return (TutorialCodes.JourneyOrder, "an entry before the story", r => TutorialRuntime.Create(r.Set).ChooseEntry("host"));
        yield return (TutorialCodes.FactMissing, "a host fact not given", r => r.Evaluate(new Dictionary<string, FactValue>()));
        yield return (TutorialCodes.FactInvalid, "a host fact of the wrong type", r => r.Evaluate(new Dictionary<string, FactValue> { ["flag"] = FactValue.From(3), ["level"] = FactValue.From(1) }));
        yield return (TutorialCodes.StepOrder, "time going back", r => { r.MoveTo(5); r.MoveTo(4); });
        yield return (TutorialCodes.Setting, "a text scale out of range", r => r.SetTextScale(10));
        yield return (TutorialCodes.Intent, "an intent of other characters", r => r.SetIntent("A!"));
    }

    private static readonly HashSet<string> Reached = new();

    [Fact]
    public void EveryDocumentRefusalIsReachedByItsCase()
    {
        foreach (var (code, name, text) in Documents())
        {
            var refusal = Assert.Throws<EngineRefusedException>(() => TutorialLoader.Load(text()));
            Assert.True(code == refusal.Code, name + " was refused with " + refusal.Code + ": " + refusal.Message + " (expected " + code + ")");
            lock (Reached) Reached.Add(code);
        }
    }

    [Fact]
    public void EveryCallRefusalIsReachedByItsCaseAndLeavesTheStateAsItWas()
    {
        foreach (var (code, name, call) in Calls())
        {
            var runtime = Fixture.Ready();
            var before = runtime.StateDigest();
            var refusal = Assert.Throws<EngineRefusedException>(() => call(runtime));
            Assert.True(code == refusal.Code, name + " was refused with " + refusal.Code + ": " + refusal.Message + " (expected " + code + ")");
            lock (Reached) Reached.Add(code);
            if (name is "skip a step that may not be skipped" or "an entry before the story" or "time going back") continue;    // these cases prepare state first
            Assert.True(before == runtime.StateDigest(), name + " changed the state");
        }
    }

    [Fact]
    public void EverySaveRefusalIsReachedByItsCase()
    {
        var runtime = Tamper.Played(out var set);
        var bytes = TutorialSave.Write(runtime);
        void Expect(string code, Action open) { Assert.Equal(code, Fixture.Refusal(open)); lock (Reached) Reached.Add(code); }
        Expect(TutorialCodes.SaveFormat, () => TutorialSave.Open(set, Encoding.UTF8.GetBytes("{}")));
        Expect(TutorialCodes.SaveDocument, () => TutorialSave.Open(Fixture.Valley, bytes));
        Expect(TutorialCodes.SaveDigest, () => TutorialSave.Open(set, Tamper.Stale(bytes, i => i.Skills[0] = 0)));
        Expect(TutorialCodes.SaveState, () => TutorialSave.Open(set, Tamper.Forge(bytes, i => i.Step = -3)));
    }

    [Fact]
    public void TheCodesTheMatrixReachesAreExactlyTheConstantsOfTheCodesClass()
    {
        EveryDocumentRefusalIsReachedByItsCase();
        EveryCallRefusalIsReachedByItsCaseAndLeavesTheStateAsItWas();
        EverySaveRefusalIsReachedByItsCase();
        var declared = typeof(TutorialCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        Assert.True(declared.Count >= 30);
        Assert.Empty(declared.Except(Reached));
        Assert.Empty(Reached.Except(declared));
        Assert.All(declared, code => Assert.StartsWith("TUT_", code));
        Assert.Equal(declared.Count, declared.Distinct().Count());
    }
}

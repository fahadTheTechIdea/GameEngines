using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>A signal can carry arguments (fixed text, whole numbers and true/false, or a variable read when the signal is raised), and a line can say the next one continues it (glue).</summary>
public sealed class SignalArgsTests
{
    private static string Document(string effects, string lineExtra = "") =>
        $$"""
        {
         "format": 3, "id": "demo.signals",
         "variables": [ { "id": "gold", "type": "int", "initial": 5, "min": 0, "max": 99 }, { "id": "mood", "type": "string", "initial": "calm" }, { "id": "flag", "type": "bool", "initial": true } ],
         "externalFacts": [], "speakers": [ { "id": "cmdr", "portraits": ["calm"] } ], "quests": [],
         "conversations": [ { "id": "talk", "start": "fx", "nodes": [
           { "id": "fx", "kind": "effects", "effects": [ {{effects}} ], "next": "l1" },
           { "id": "l1", "kind": "line", "speaker": "cmdr", "portrait": "calm", "line": "k.l1", {{lineExtra}} "next": "bye" },
           { "id": "bye", "kind": "end", "result": "done" } ] } ]
        }
        """;

    private static DialogueStep Play(string effects) => new DialogueRuntime(DialogueLoader.Load(Document(effects), Fixture.Catalog), 1).Start("talk", FeatureFixture.NoFacts);

    private static string Refusal(string effects) => Fixture.Refusal(() => DialogueLoader.Load(Document(effects), Fixture.Catalog));

    [Fact]
    public void ASignalWithNoArgumentsIsAsBeforeAndHasNone()
    {
        var signal = Play("""{ "signal": "ping" }""").Events.OfType<SignalEvent>().Single();
        Assert.Equal("ping", signal.Name);
        Assert.Empty(signal.Args);
        Assert.Equal("SignalEvent { Name = ping }", signal.ToString());
    }

    [Fact]
    public void ArgumentsAreTextWholeNumbersTrueFalseAndVariablesInTheOrderWritten()
    {
        var signal = Play("""{ "signal": "give", "args": [ { "text": "sword" }, { "int": -3 }, { "bool": false }, { "var": "gold" }, { "var": "mood" }, { "var": "flag" } ] }""").Events.OfType<SignalEvent>().Single();
        Assert.Equal([FactValue.From("sword"), FactValue.From(-3L), FactValue.From(false), FactValue.From(5L), FactValue.From("calm"), FactValue.From(true)], signal.Args);
        Assert.Equal("SignalEvent { Name = give, Args = [sword, -3, false, 5, calm, true] }", signal.ToString());
    }

    [Fact]
    public void AVariableArgumentIsReadWhenTheSignalIsRaisedNotWhenTheDocumentWasLoaded()
    {
        var events = Play("""{ "add": "gold", "by": 4 }, { "signal": "pay", "args": [ { "var": "gold" } ] }, { "set": "gold", "value": 1 }""").Events;
        Assert.Equal(FactValue.From(9L), events.OfType<SignalEvent>().Single().Args.Single());
    }

    [Theory]
    [InlineData("""{ "signal": "x", "args": [] }""", DialogueCodes.Range)]
    [InlineData("""{ "signal": "x", "args": [ {"int":1},{"int":2},{"int":3},{"int":4},{"int":5},{"int":6},{"int":7},{"int":8},{"int":9} ] }""", DialogueCodes.Range)]
    [InlineData("""{ "signal": "x", "args": [ { "float": 1 } ] }""", DialogueCodes.UnknownField)]
    [InlineData("""{ "signal": "x", "args": [ { "int": 1, "text": "a" } ] }""", DialogueCodes.Format)]
    [InlineData("""{ "signal": "x", "args": [ {} ] }""", DialogueCodes.Format)]
    [InlineData("""{ "signal": "x", "args": [ 5 ] }""", DialogueCodes.Format)]
    [InlineData("""{ "signal": "x", "args": [ { "int": 1.5 } ] }""", DialogueCodes.ValueType)]
    [InlineData("""{ "signal": "x", "args": [ { "int": 1000000001 } ] }""", DialogueCodes.ValueType)]
    [InlineData("""{ "signal": "x", "args": [ { "text": 5 } ] }""", DialogueCodes.ValueType)]
    [InlineData("""{ "signal": "x", "args": [ { "bool": "yes" } ] }""", DialogueCodes.ValueType)]
    [InlineData("""{ "signal": "x", "args": [ { "var": "nope" } ] }""", DialogueCodes.Reference)]
    [InlineData("""{ "signal": "x", "args": "all" }""", DialogueCodes.Format)]
    [InlineData("""{ "signal": "x", "extra": 1 }""", DialogueCodes.UnknownField)]
    public void AMistakeInASignalsArgumentsIsRefusedWithItsOwnCode(string effect, string code)
    {
        Assert.Equal(code, Refusal(effect));
    }

    [Fact]
    public void ALongTextArgumentIsRefused()
    {
        Assert.Equal(DialogueCodes.ValueType, Refusal("{ \"signal\": \"x\", \"args\": [ { \"text\": \"" + new string('a', 257) + "\" } ] }"));
    }

    // ---- glue ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ALineWithGlueSaysSoAndOneWithoutDoesNot()
    {
        Assert.True(new DialogueRuntime(DialogueLoader.Load(Document("""{ "signal": "x" }""", "\"glue\": true,"), Fixture.Catalog), 1).Start("talk", FeatureFixture.NoFacts).Line!.Glue);
        Assert.False(Play("""{ "signal": "x" }""").Line!.Glue);
        Assert.False(new DialogueRuntime(DialogueLoader.Load(Document("""{ "signal": "x" }""", "\"glue\": false,"), Fixture.Catalog), 1).Start("talk", FeatureFixture.NoFacts).Line!.Glue);
    }

    [Fact]
    public void GlueMustBeTrueOrFalse()
    {
        Assert.Equal(DialogueCodes.Format, Fixture.Refusal(() => DialogueLoader.Load(Document("""{ "signal": "x" }""", "\"glue\": \"yes\","), Fixture.Catalog)));
    }
}

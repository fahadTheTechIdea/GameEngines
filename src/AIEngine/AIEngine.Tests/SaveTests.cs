using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AIEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace AIEngine.Tests;

/// <summary>A9: a save is digest-checked and bound to its document, and a forged digest still cannot bring in a state the document could not have made.</summary>
public sealed class SaveTests
{
    private static readonly JsonSerializerOptions Strict = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    /// <summary>A little of everything: scouts mid-errand and mid-cooldown with rolls drawn, a commander with units and a belief, a worker with a utility choice.</summary>
    private static (AiRuntime Runtime, Action<AiRuntime, int> Host) Busy(ulong seed = 11)
    {
        var runtime = new AiRuntime(Fixture.Load(), seed);
        for (var i = 0; i < 4; i++) runtime.Add("s" + i, "scout", 0);
        runtime.Add("cmd", "commander", 0);
        runtime.Add("w", "worker", 0);
        foreach (var own in new[] { "a1", "a2" }) Fixture.Tell(runtime, "cmd", own, hostile: false);
        Fixture.Tell(runtime, "cmd", "b1", hostile: true);
        runtime.Perceive("s0", "stock", "depot-a", "depot-a", new Dictionary<string, FactValue> { ["held"] = FactValue.From(40L) }, 0);
        void Host(AiRuntime runtime, int tick)
        {
            if (tick == 4) foreach (var id in new[] { "s0", "s1", "s2", "s3", "cmd" }) runtime.SetKey(id, "ready", FactValue.From(true));
            if (tick == 9) foreach (var id in new[] { "s0", "s1", "s2", "s3" }) runtime.SetKey(id, "ready", FactValue.From(false));
            if (tick == 6) Fixture.Tell(runtime, "cmd", "b1", hostile: true, health: 60, tick: 6);   // told again later, so a percept has a tick that is not 0
            if (tick % 11 == 0) runtime.SetKey("w", "hunger", FactValue.From((long)(tick * 7 % 100)));
        }
        return (runtime, Host);
    }

    private static List<string> Run(AiRuntime runtime, Action<AiRuntime, int> host, int from, int to)
    {
        var lines = new List<string>();
        for (var tick = from; tick <= to; tick++)
        {
            host(runtime, tick);
            lines.Add(Fixture.Lines(runtime.Step(tick, 100_000)));
        }
        return lines;
    }

    private static JsonNode Agent(JsonObject image, string id) => image["Agents"]!.AsArray().Single(a => a!["Id"]!.GetValue<string>() == id)!;

    private static string Open(byte[] bytes) => Fixture.Refusal(() => AiSave.Open(Fixture.Load(), bytes));

    private static byte[] Edit(byte[] bytes, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(bytes)!.AsObject();
        change(node);
        return Encoding.UTF8.GetBytes(node.ToJsonString());
    }

    [Fact]
    public void ASessionSavedMidRunContinuesExactlyLikeOneThatWasNeverSaved()
    {
        var (original, host) = Busy();
        var first = Run(original, host, 1, 25);
        Assert.Contains(first, l => l.Contains("rest", StringComparison.Ordinal));
        var bytes = AiSave.Write(original);
        var loaded = AiSave.Open(Fixture.Load(), bytes);
        Assert.Equal(AiSave.StateDigest(original), AiSave.StateDigest(loaded));
        Assert.Equal(original.LastTick, loaded.LastTick);
        // What each agent knows, with when it was told, came back as it was.
        string Knowledge(AiRuntime r) => string.Join(";", r.Agents.SelectMany(a => new[] { "unit", "stock" }.SelectMany(k => r.Known(a, k).Select(p => $"{a}/{k}/{p.Id}/{p.Owner}/{p.Seen}/" + string.Join(",", p.Attrs.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "=" + Fixture.Show(x.Value)))))));
        Assert.Equal(Knowledge(original), Knowledge(loaded));
        Assert.Contains("/stock/depot-a/", Knowledge(loaded));
        var one = Run(original, host, 26, 120);
        var two = Run(loaded, host, 26, 120);
        Assert.Equal(one, two);
        Assert.Equal(AiSave.StateDigest(original), AiSave.StateDigest(loaded));
        Assert.Contains(one, l => l.Contains("heads", StringComparison.Ordinal) || l.Contains("tails", StringComparison.Ordinal));   // rolls were drawn after the load
    }

    [Fact]
    public void AnEmptySetAndTheSameStateAlwaysWriteTheSameBytes()
    {
        var empty = new AiRuntime(Fixture.Load(), 3);
        Assert.Empty(AiSave.Open(Fixture.Load(), AiSave.Write(empty)).Agents);
        var (a, hostA) = Busy();
        var (b, hostB) = Busy();
        Run(a, hostA, 1, 30);
        Run(b, hostB, 1, 30);
        Assert.Equal(AiSave.Write(a), AiSave.Write(b));
    }

    [Fact]
    public void EachKindOfDamageToASaveIsRefusedWithItsOwnCode()
    {
        var (runtime, host) = Busy();
        Run(runtime, host, 1, 20);
        var bytes = AiSave.Write(runtime);
        Assert.Equal(AiCodes.SaveFormat, Open(bytes[..(bytes.Length / 2)]));
        Assert.Equal(AiCodes.SaveFormat, Open(Encoding.UTF8.GetBytes("[]")));
        Assert.Equal(AiCodes.SaveFormat, Open(Edit(bytes, n => n["Format"] = 2)));
        Assert.Equal(AiCodes.SaveFormat, Open(Edit(bytes, n => n["Extra"] = 1)));
        Assert.Equal(AiCodes.SaveFormat, Open(Edit(bytes, n => n["Seed"] = "many")));
        Assert.Equal(AiCodes.SaveFormat, Open(Edit(bytes, n => n["Agents"]!.AsArray().Add(JsonNode.Parse(n["Agents"]![0]!.ToJsonString())))));          // an agent twice
        Assert.Equal(AiCodes.SaveFormat, Open(Edit(bytes, n => { var agents = n["Agents"]!.AsArray(); var first = agents[0]!.DeepClone(); agents.RemoveAt(0); agents.Add(first); })));   // out of id order
        Assert.Equal(AiCodes.SaveDocument, Open(Edit(bytes, n => n["Document"] = new string('A', 64))));
        Assert.Equal(AiCodes.SaveDocument, Open(Edit(bytes, n => n["Id"] = "another")));
        Assert.Equal(AiCodes.SaveDigest, Open(Edit(bytes, n => n["Seed"] = "12")));
        Assert.Equal(AiCodes.SaveDigest, Open(Edit(bytes, n => n["LastTick"] = 999)));
        Assert.Equal(AiCodes.SaveDigest, Open(Edit(bytes, n => n["Agents"]![0]!["Due"] = 777)));
        Assert.Equal(AiCodes.SaveDigest, Open(Edit(bytes, n => n["Agents"]![0]!["Roll"] = 9)));
        Assert.Equal(AiCodes.SaveDigest, Open(Edit(bytes, n => n["Agents"]![0]!["Behavior"] = "worker")));
        Assert.Equal(AiCodes.SaveDigest, Open(Edit(bytes, n => n["Agents"]![1]!["Keys"]![0]!["Value"]!["Number"] = 1)));
        Assert.Equal(AiCodes.SaveDigest, Open(Edit(bytes, n => n["Agents"]![1]!["Slots"]![0] = 3)));
        Assert.Equal(AiCodes.SaveDigest, Open(Edit(bytes, n => n["Agents"]![1]!["Knowledge"]![0]!["Seen"] = 4)));
        Assert.Equal(AiCodes.SaveDigest, Open(Edit(bytes, n => Agent(n, "s0")["Knowledge"]![0]!["Owner"] = "depot-z")));                       // who owns a thing
        Assert.Equal(AiCodes.SaveDigest, Open(Edit(bytes, n => Agent(n, "s0")["Keys"]!.AsArray().Single(k => k!["Id"]!.GetValue<string>() == "mode")!["Value"]!["Text"] = "other")));   // the text of a key
        Assert.Equal(AiCodes.SaveDigest, Open(Edit(bytes, n => Agent(n, "cmd")["Knowledge"]!.AsArray().Single(k => k!["Id"]!.GetValue<string>() == "b1")!["Values"]![2]!["Number"] = 61)));   // an attribute of a thing
        Assert.Equal(AiCodes.SaveDigest, Open(Edit(bytes, n => n["Digest"] = new string('0', 64))));
    }

    /// <summary>The save with one thing in its state changed and the digest recomputed to match, as a forger who has read this code would.</summary>
    private static string Forged(byte[] bytes, Action<AiImage> change)
    {
        var image = JsonSerializer.Deserialize<AiImage>(bytes, Strict)!;
        change(image);
        image.Digest = EngineCommon.Digest.Of(AiImage.Canonical(image));
        var forged = JsonSerializer.SerializeToUtf8Bytes(image);
        return Fixture.Refusal(() => AiSave.Open(Fixture.Load(), forged));
    }

    [Fact]
    public void AForgedDigestDoesNotBringInAStateTheDocumentCouldNotHaveMade()
    {
        var (runtime, host) = Busy();
        Run(runtime, host, 1, 20);
        var bytes = AiSave.Write(runtime);
        var cmd = (AiImage i) => i.Agents.Single(a => a.Id == "cmd");
        var s0 = (AiImage i) => i.Agents.Single(a => a.Id == "s0");
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Keys.Single(k => k.Id == "distance").Value.Number = 100_001));      // above its range
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Keys.Single(k => k.Id == "ready").Value.Number = 2));               // not a boolean
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Keys.Single(k => k.Id == "mode").Value.Text = new string('x', 257)));   // too long
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Keys.Single(k => k.Id == "mode").Value.Kind = "Integer"));          // wrong kind
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Keys.RemoveAt(0)));                                                  // a key missing
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => { var k = cmd(i).Keys; (k[0], k[1]) = (k[1], k[0]); }));                    // keys out of place
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Behavior = "nothing"));
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Slots.Add(0)));                                                      // a slot too many
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => s0(i).Slots[1] = 99));                                                      // a child of the resuming sequence that does not exist
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => s0(i).Slots[0] = -1));
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Roll = -3));
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Due = -1));
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => i.LastTick = -1));
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Knowledge[0].Kind = "ghost"));
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Knowledge[0].Owner = "someone"));                                    // a kind with no owners
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Knowledge[0].Values.RemoveAt(0)));
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Knowledge.Single(k => k.Id == "b1").Values[2].Number = 5000));      // health above its range
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => cmd(i).Knowledge.Single(k => k.Id == "b1").Seen = -1));
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i => { for (var n = 0; n < 17; n++) cmd(i).Knowledge.Add(new ImagePercept { Kind = "unit", Id = "z" + n, Owner = "", Seen = 0, Values = cmd(i).Knowledge[0].Values.Select(v => new ImageValue { Kind = v.Kind, Number = v.Number, Text = v.Text }).ToList() }); }));   // past the capacity
        Assert.Equal(AiCodes.SaveState, Forged(bytes, i =>
        {
            // five stocks held by one owner, where one owner may hold four (and the kind holds eight, so it is the owner capacity that is passed)
            var knowledge = s0(i).Knowledge;
            var model = knowledge.First(k => k.Kind == "stock");
            knowledge.RemoveAll(k => k.Kind == "stock");
            for (var n = 0; n < 5; n++) knowledge.Add(new ImagePercept { Kind = "stock", Id = "z" + n, Owner = "depot-a", Seen = 0, Values = model.Values.Select(v => new ImageValue { Kind = v.Kind, Number = v.Number, Text = v.Text }).ToList() });
        }));
        Assert.Equal(AiCodes.SaveFormat, Forged(bytes, i => cmd(i).Knowledge.Add(new ImagePercept { Kind = cmd(i).Knowledge[0].Kind, Id = cmd(i).Knowledge[0].Id, Owner = "", Seen = 0, Values = cmd(i).Knowledge[0].Values })));   // a thing twice
    }

    [Fact]
    public void ASaveOpenedAgainstAnEditedDocumentIsRefused()
    {
        var (runtime, host) = Busy();
        Run(runtime, host, 1, 5);
        var edited = Fixture.Load(Fixture.Document.Replace("\"momentum\": 150", "\"momentum\": 151", StringComparison.Ordinal));
        Assert.Equal(AiCodes.SaveDocument, Fixture.Refusal(() => AiSave.Open(edited, AiSave.Write(runtime))));
    }
}
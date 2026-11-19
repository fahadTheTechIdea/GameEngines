using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ScenarioEngine.Contracts;
using Xunit;

namespace WorldEventsEngine.Tests;

/// <summary>
/// With every hazard's excitation off and every zone's spill 0, format 2 is the format 1 engine: the same changes at the same steps and the same end state. The three hashes below were recorded from the pass-38 engine
/// (format 1) before it was changed, over the same documents with the two new fields added; they are not re-recorded when the engine changes. (The one deliberate difference the valley needs: its quake cooldown, which the
/// format 2 sample lowers so that aftershocks can follow, is put back to 90, and its plague seed rate to 600.)
/// </summary>
public sealed class FormatTwoEquivalenceTests
{
    private static string Hash(WorldSet set, long to, IReadOnlyDictionary<string, FactValue> host)
    {
        var runtime = WorldRuntime.Create(set);
        var text = new StringBuilder();
        for (long step = 0; step <= to; step++)
            foreach (var c in runtime.Advance(step, runtime.CycleSeasonAt, host).Items)
                text.Append($"{c.Kind}|{c.Step}|{c.Region}|{c.Index}|{c.Sub}|{c.Sub2}|{c.Amount}|{c.Amount2}|{c.Name}|{c.Source}\n");
        text.Append(string.Join(';', runtime.Regions.Select(x => $"{x.Weather},{x.Anomaly},{x.Temp},{x.Precip},{x.River},{string.Join(',', x.Infectious)}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    [Fact]
    public void TheSmallDocumentWithNothingNewSwitchedOnLivesAsTheFormatOneEngineDid()
    {
        Assert.Equal("F169BD47A129908FEE4818B3893457A7B80C478E24F24AD6E3EC764AF4D29869", Hash(Fixture.Mini(), 800, Fixture.Host()));
        Assert.Equal("5354F27138F31DE18BEEBE18C7E74D37463938E5EEEDF01867652D6ED7AA0277", Hash(Fixture.Mini(Tiny.Certain), 400, Fixture.Host()));
    }

    [Fact]
    public void TheValleyWithNothingNewSwitchedOnLivesAsTheFormatOneEngineDid()
    {
        var root = JsonNode.Parse(Fixture.ValleyText)!.AsObject();
        foreach (var zone in root["zones"]!.AsArray()) zone!["spill"] = 0;
        foreach (var disease in root["diseases"]!.AsArray())
            if (disease!["id"]!.GetValue<string>() == "plague") disease["seedPpm"] = 600;        // the format 2 sample seeds the plague more often, see docs section 11.4
        foreach (var hazard in root["hazards"]!.AsArray())
        {
            hazard!["excite"] = new JsonObject { ["jumpPpm"] = 0, ["decayPermille"] = 1000, ["capPpm"] = 0 };
            if (hazard["id"]!.GetValue<string>() == "quake") hazard["cooldown"] = 90;
        }
        Assert.Equal("0234C81AB941FFF33EB72AB7F9F2C8B1FD716182B83AE1A8160DDF2EDFA52739", Hash(WorldLoader.Load(root.ToJsonString()), 730, Fixture.ValleyHost()));
    }

    [Fact]
    public void TheValleyAsShippedLivesDifferentlyBecauseItsZonesSpillAndItsQuakesExcite()
    {
        Assert.NotEqual("0234C81AB941FFF33EB72AB7F9F2C8B1FD716182B83AE1A8160DDF2EDFA52739", Hash(Fixture.Valley, 730, Fixture.ValleyHost()));
        Assert.Equal(Hash(Fixture.Valley, 200, Fixture.ValleyHost()), Hash(WorldLoader.Load(Fixture.ValleyText), 200, Fixture.ValleyHost()));
    }
}

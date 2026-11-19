using System.Text.Json;
using System.Text.Json.Serialization;

namespace RelationshipEngine.Tests;

/// <summary>Builds saves that are wrong in one place: with the digest left stale, or recomputed so only the state rules can catch the forgery.</summary>
internal static class Tamper
{
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    /// <summary>A small village with edges, memories, moodlets, a bond and a cooldown, after two days.</summary>
    public static RelationshipRuntime Played()
    {
        var runtime = Fixture.Runtime();
        runtime.AddActor("alice", ["kind"], new Dictionary<string, int> { ["flower"] = 2 });
        runtime.AddActor("bob", ["grumpy", "glutton"]);
        runtime.AddActor("cara", []);
        runtime.AddActor("dan", ["stoic"]);
        runtime.SetActivity("alice", "eat");
        runtime.Advance(24, Fixture.Spring);
        runtime.Perform(new InteractionRequest("give-gift", "bob", "alice", ["cara"], "flowers"), Fixture.Spring);
        runtime.Perform(new InteractionRequest("insult", "alice", "bob", [], ""), Fixture.Spring);
        runtime.SetBond("alice", "dan", "spouse", true);
        runtime.AdjustAxis("cara", "dan", "attraction", 300);
        runtime.Advance(30, Fixture.Spring);
        return runtime;
    }

    public static RelationshipImage Image(byte[] save) => JsonSerializer.Deserialize<RelationshipImage>(save, Options)!;

    /// <summary>The save with the edit applied to its state and the digest recomputed, so only the rules about the state can refuse it.</summary>
    public static byte[] Forge(byte[] save, Action<RelationshipImage> edit)
    {
        var image = Image(save);
        edit(image);
        image.Digest = EngineCommon.Digest.Of(RelationshipImage.Canonical(image));
        return JsonSerializer.SerializeToUtf8Bytes(image, Options);
    }

    /// <summary>The save with the edit applied and the old digest left, so only the digest can refuse it.</summary>
    public static byte[] Stale(byte[] save, Action<RelationshipImage> edit)
    {
        var image = Image(save);
        edit(image);
        return JsonSerializer.SerializeToUtf8Bytes(image, Options);
    }
}

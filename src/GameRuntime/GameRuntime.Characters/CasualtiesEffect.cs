using Beep.SimEngine;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>The game layer reports members lost from a squad (for example from a combat result). Payload: <c>{"squad":id,"count":n}</c>.</summary>
public sealed class CasualtiesEffect : IDirectorEffect
{
    public const string EffectName = "characters.casualties";
    public string Name => EffectName;

    private static (EntityId Squad, int Count)? Read(string payloadJson)
    {
        var root = EffectPayload.Parse(payloadJson, "squad", "count");
        if (root is not { } r || !EffectPayload.Int(r, "squad", out var squad) || !EffectPayload.Int(r, "count", out var count)) return null;
        return (new EntityId(squad), count);
    }

    public Code Check(World world, string payloadJson) =>
        Read(payloadJson) is { } read ? world.Services.Get<ICharacterFactory>().CheckCasualties(read.Squad, read.Count) : Codes.InvalidArgument;

    public void Apply(World world, string payloadJson)
    {
        var read = Read(payloadJson) ?? throw new InvalidOperationException("the payload was checked but does not read: " + payloadJson);
        world.Services.Get<ICharacterFactory>().Casualties(read.Squad, read.Count);
    }
}

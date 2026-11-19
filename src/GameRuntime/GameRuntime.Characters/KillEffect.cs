using Beep.SimEngine;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>An individually simulated member of a squad dies. Payload: <c>{"character":id}</c>.</summary>
public sealed class KillEffect : IDirectorEffect
{
    public const string EffectName = "characters.kill";
    public string Name => EffectName;

    private static EntityId? Read(string payloadJson)
    {
        var root = EffectPayload.Parse(payloadJson, "character");
        return root is { } r && EffectPayload.Int(r, "character", out var id) ? new EntityId(id) : null;
    }

    public Code Check(World world, string payloadJson) =>
        Read(payloadJson) is { } id ? world.Services.Get<ICharacterFactory>().CheckKill(id) : Codes.InvalidArgument;

    public void Apply(World world, string payloadJson) =>
        world.Services.Get<ICharacterFactory>().Kill(Read(payloadJson) ?? throw new InvalidOperationException("the payload was checked but does not read: " + payloadJson));
}

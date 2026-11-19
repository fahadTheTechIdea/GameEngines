using Beep.SimEngine;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>The game layer needs one member of a squad as an individual. Payload: <c>{"squad":id,"slot":n}</c>.</summary>
public sealed class MaterializeEffect : IDirectorEffect
{
    public const string EffectName = "characters.materialize";
    public string Name => EffectName;

    private static (EntityId Squad, int Slot)? Read(string payloadJson)
    {
        var root = EffectPayload.Parse(payloadJson, "squad", "slot");
        if (root is not { } r || !EffectPayload.Int(r, "squad", out var squad) || !EffectPayload.Int(r, "slot", out var slot)) return null;
        return (new EntityId(squad), slot);
    }

    public Code Check(World world, string payloadJson) =>
        Read(payloadJson) is { } read ? world.Services.Get<ICharacterFactory>().CheckMaterialize(read.Squad, read.Slot) : Codes.InvalidArgument;

    public void Apply(World world, string payloadJson)
    {
        var read = Read(payloadJson) ?? throw new InvalidOperationException("the payload was checked but does not read: " + payloadJson);
        world.Services.Get<ICharacterFactory>().Materialize(read.Squad, read.Slot);
    }
}

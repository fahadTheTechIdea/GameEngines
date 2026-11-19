using Beep.SimEngine;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>
/// The game layer asks for individually simulated characters. Payload:
/// <c>{"archetype":"id","count":n,"company":0,"seed":123,"layout":null|{"x":..,"y":..,"columns":..,"spacing":..}}</c>.
/// </summary>
public sealed class SpawnIndividualsEffect : IDirectorEffect
{
    public const string EffectName = "characters.spawn-individuals";
    public string Name => EffectName;

    private static SpawnIndividualsRequest? Read(string payloadJson)
    {
        var root = EffectPayload.Parse(payloadJson, "archetype", "count", "company", "seed", "layout");
        if (root is not { } r) return null;
        if (!EffectPayload.Text(r, "archetype", out var archetype) || !EffectPayload.Int(r, "count", out var count)
            || !EffectPayload.Int(r, "company", out var company) || !EffectPayload.ULong(r, "seed", out var seed) || !LayoutPayload.Layout(r, out var layout))
            return null;
        return new SpawnIndividualsRequest(archetype, count, new PlayerId(company), seed, layout);
    }

    public Code Check(World world, string payloadJson) =>
        Read(payloadJson) is { } request ? world.Services.Get<ICharacterFactory>().CheckSpawnIndividuals(request) : Codes.InvalidArgument;

    public void Apply(World world, string payloadJson) =>
        world.Services.Get<ICharacterFactory>().SpawnIndividuals(Read(payloadJson) ?? throw new InvalidOperationException("the payload was checked but does not read: " + payloadJson));
}

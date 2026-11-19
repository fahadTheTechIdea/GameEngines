using Beep.SimEngine;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>
/// The game layer asks for squads of a formation. Payload: <c>{"formation":"id","squads":n,"company":0,"seed":123,"layout":null|{"x":..,"y":..,"columns":..,"spacing":..}}</c>.
/// Like every director effect it runs inside the world's next tick, never at the moment of the request; the answer is the world's events.
/// </summary>
public sealed class SpawnFormationEffect : IDirectorEffect
{
    public const string EffectName = "characters.spawn-formation";
    public string Name => EffectName;

    private static SpawnFormationRequest? Read(string payloadJson)
    {
        var root = EffectPayload.Parse(payloadJson, "formation", "squads", "company", "seed", "layout");
        if (root is not { } r) return null;
        if (!EffectPayload.Text(r, "formation", out var formation) || !EffectPayload.Int(r, "squads", out var squads)
            || !EffectPayload.Int(r, "company", out var company) || !EffectPayload.ULong(r, "seed", out var seed) || !LayoutPayload.Layout(r, out var layout))
            return null;
        return new SpawnFormationRequest(formation, squads, new PlayerId(company), seed, layout);
    }

    public Code Check(World world, string payloadJson) =>
        Read(payloadJson) is { } request ? world.Services.Get<ICharacterFactory>().CheckSpawnFormation(request) : Codes.InvalidArgument;

    public void Apply(World world, string payloadJson) =>
        world.Services.Get<ICharacterFactory>().SpawnFormation(Read(payloadJson) ?? throw new InvalidOperationException("the payload was checked but does not read: " + payloadJson));
}

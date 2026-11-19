using DialogueEngine;
using EngineCommon;
using ScenarioEngine.Contracts;
using WorldEventsEngine;

namespace GameRuntime;

/// <summary>
/// What a conversation reads of the world, as external facts: the season, the stress, and for one region its weather, its water, how bad each hazard is in it, how many are ill and each modifier, under the
/// <c>ext.</c> prefix a dialogue document uses (a farmer who says "the river is rising" when <c>ext.world.riverton.river</c> is high). The dialogue component is given <see cref="Facts"/> as its fact source; a dialogue
/// document declares the ones it reads as external facts with the same ids (without the prefix), and <see cref="Check"/> verifies the declarations against the world document when the game is assembled.
/// </summary>
public sealed class WorldDialogueFacts
{
    public const string Mismatch = "WE_DIALOGUE_MISMATCH";

    private readonly WorldComponent _component;

    public WorldDialogueFacts(WorldComponent component, string region, string prefix = "ext.")
    {
        ArgumentNullException.ThrowIfNull(component);
        _component = component;
        Region = region;
        Prefix = prefix;
    }

    public string Region { get; private set; }
    public string Prefix { get; }

    /// <summary>Points the facts at another region, for the next conversation (the player walked into another town).</summary>
    public void At(string region) => Region = region;

    /// <summary>The facts of the world and of the region now. It must be a region (or empty for the world's only), or the call is refused with <c>WE_UNKNOWN_REGION</c>.</summary>
    public IReadOnlyDictionary<string, FactValue> Facts() => _component.Facts(Prefix, Region);

    /// <summary>
    /// Refuses a dialogue document that declares an external fact in the world namespace (<c>world.</c>) that the world document does not offer, or offers with another type, or with a range the declaration does not cover.
    /// </summary>
    public static void Check(DialogueSet dialogue, WorldSet world)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(world);
        var offered = WorldFacts.Offered(world);
        foreach (var external in dialogue.Externals.Values.Where(e => e.Id.StartsWith("world.", StringComparison.Ordinal)))
        {
            if (!offered.TryGetValue(external.Id, out var given)) throw new EngineRefusedException(Mismatch, "the dialogue document reads '" + external.Id + "', which the world document does not offer");
            if (given.Kind != external.Kind) throw new EngineRefusedException(Mismatch, "'" + external.Id + "' is " + given.Kind + " in the world document and " + external.Kind + " in the dialogue document");
            if (given.Kind == FactKind.Integer && (given.Minimum < external.Minimum || given.Maximum > external.Maximum))
                throw new EngineRefusedException(Mismatch, "'" + external.Id + "' ranges " + given.Minimum + " to " + given.Maximum + " in the world document, outside the " + external.Minimum + " to " + external.Maximum + " the dialogue document declares");
        }
    }
}

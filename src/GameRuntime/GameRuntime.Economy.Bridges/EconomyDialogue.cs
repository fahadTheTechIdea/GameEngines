using DialogueEngine;
using EconomyEngine;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// What a conversation reads of a market, as external facts: its treasury, staff, arrears, and for each good its price, the price over its reference, its stock, its band and whether it is short, with the rate and
/// price level of each currency, under the <c>ext.</c> prefix a dialogue document uses. The dialogue component is given <see cref="Facts"/> as its fact source (a shopkeeper who says "grain is dear this week"
/// reads <c>ext.market.band.grain</c>); a dialogue document declares the ones it reads as external facts with the same ids, and <see cref="Check"/> verifies the declarations against the economy document when the
/// game is assembled.
/// </summary>
public sealed class EconomyDialogueFacts
{
    public const string Mismatch = "ECON_DIALOGUE_MISMATCH";

    private readonly EconomyComponent _component;

    public EconomyDialogueFacts(EconomyComponent component, string market, string prefix = "ext.")
    {
        ArgumentNullException.ThrowIfNull(component);
        _component = component;
        Market = market;
        Prefix = prefix;
    }

    public string Market { get; private set; }
    public string Prefix { get; }

    /// <summary>Points the facts at another market, for the next conversation (the player walked into another shop).</summary>
    public void At(string market) => Market = market;

    /// <summary>The facts of the market now. It must be a market, or the call is refused with <c>ECON_UNKNOWN_MARKET</c>.</summary>
    public IReadOnlyDictionary<string, FactValue> Facts() => EconomyFacts.Read(_component.Runtime, Market, Prefix);

    /// <summary>
    /// Refuses a dialogue document that declares an external fact in the economy namespace (<c>market.</c>, <c>currency.</c>) that the economy document does not offer, or offers with another type, or with a range
    /// the declaration does not cover.
    /// </summary>
    public static void Check(DialogueSet dialogue, EconomySet economy)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(economy);
        foreach (var external in dialogue.Externals.Values.Where(e => EconomyFacts.IsReserved(e.Id)))
        {
            if (!economy.Facts.TryGetValue(external.Id, out var offered)) throw new EngineRefusedException(Mismatch, "the dialogue document reads '" + external.Id + "', which the economy document does not offer");
            if (offered.Kind != external.Kind) throw new EngineRefusedException(Mismatch, "'" + external.Id + "' is " + offered.Kind + " in the economy document and " + external.Kind + " in the dialogue document");
            if (offered.Kind == FactKind.Integer && (offered.Minimum < external.Minimum || offered.Maximum > external.Maximum))
                throw new EngineRefusedException(Mismatch, "'" + external.Id + "' ranges " + offered.Minimum + " to " + offered.Maximum + " in the economy document, outside the " + external.Minimum + " to " + external.Maximum + " the dialogue document declares");
        }
    }
}

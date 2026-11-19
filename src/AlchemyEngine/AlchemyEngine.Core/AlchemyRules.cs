using System.Security.Cryptography;
using System.Text;

namespace AlchemyEngine;

/// <summary>
/// The pure rules of one alchemy game: a function from a combination and a context to a result. It holds only the validated content;
/// it has no clock, no counters and no randomness except the seed and attempt the caller passes in.
/// </summary>
public sealed partial class AlchemyRules(AlchemyContent content)
{
    public const int MaxItems = 8;

    public AlchemyContent Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    public CombineResult Combine(IReadOnlyList<string> items, CombineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var match = Match(items, context.Environment);
        if (match.Recipe is null || match.Result.Outcome != CombineOutcome.Reacted) return match.Result;
        var recipe = match.Recipe;
        if (recipe.ChanceNumerator < recipe.ChanceDenominator && !Roll(recipe, match.Result, context))
            return match.Result with { Outcome = CombineOutcome.Fizzled, Code = AlchemyCodes.Fizzled, Produced = Array.Empty<string>(), Kept = Array.Empty<string>() };
        return match.Result;
    }

    /// <summary>The caller's seed and attempt decide the chance, so the same attempt always rolls the same way.</summary>
    private static bool Roll(RecipeDef recipe, CombineResult request, CombineContext context)
    {
        var text = "alchemy-roll|" + context.Seed + "|" + context.Attempt + "|" + recipe.Id + "|" + string.Join('+', request.Items) + "|" + string.Join(',', request.Environment);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var value = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(hash);
        return value % (ulong)recipe.ChanceDenominator < (ulong)recipe.ChanceNumerator;
    }
}

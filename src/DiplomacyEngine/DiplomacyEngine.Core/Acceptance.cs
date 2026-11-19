using System.Globalization;
using System.Text;
using EngineCommon;

namespace DiplomacyEngine;

/// <summary>One line of the reasoning behind an acceptance: the input, the raw number, the weight the treaty type gives it (per mille) and what it added to the score.</summary>
public sealed record AcceptanceLine(string Name, long Raw, long Weight, long Contribution);

/// <summary>
/// What an acceptance came to: the score, the chance of saying yes (0 to 1,000: certain below the reject line, certain at or above the accept line, in proportion between) and every line of how the score was made.
/// </summary>
public sealed record Verdict(long Score, int Chance, IReadOnlyList<AcceptanceLine> Lines);

/// <summary>
/// Everything an acceptance reads, already measured: one raw number for each <see cref="FactorSource"/> (minus 1,000 to 1,000), the bias the realm's traits add, and the document's conditional modifiers that hold, each with
/// its value. The evaluator reads nothing else, so it is a pure function of these and the treaty type.
/// </summary>
public sealed record AcceptanceInputs(IReadOnlyList<long> Raw, int TraitBias, IReadOnlyList<(string Id, int Value)> Modifiers);

/// <summary>
/// The acceptance score of a proposal, after the weighted-sum AI of the strategy games that came before (Paradox's acceptance lists a base and a sum of factors, each a measured quantity times a weight; Stellaris adds
/// trust requirements): <c>score = bias + trait bias + sum of raw x weight / 1000 + the modifiers that hold</c>. At or above the accept line it is yes, below the reject line no, in between a chance in proportion, rolled by
/// the runtime from the proposal's own seeded stream. The evaluator is pure and explains itself line by line.
/// </summary>
public static class Acceptance
{
    public static Verdict Evaluate(TreatyTypeDef type, AcceptanceRules rules, AcceptanceInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(inputs);
        var lines = new List<AcceptanceLine>();
        long score = type.Bias;
        lines.Add(new AcceptanceLine("bias", 1000, type.Bias, type.Bias));
        if (inputs.TraitBias != 0)
        {
            score += inputs.TraitBias;
            lines.Add(new AcceptanceLine("traits", 1000, inputs.TraitBias, inputs.TraitBias));
        }
        foreach (var source in Enum.GetValues<FactorSource>())
        {
            var weight = type.Weights[(int)source];
            if (weight == 0) continue;
            var raw = inputs.Raw[(int)source];
            var contribution = raw * weight / 1000;
            score += contribution;
            lines.Add(new AcceptanceLine(EnumNames.Name(source), raw, weight, contribution));
        }
        foreach (var (id, value) in inputs.Modifiers)
        {
            score += value;
            lines.Add(new AcceptanceLine("modifier " + id, 1000, value, value));
        }
        return new Verdict(score, ChanceOf(score, rules), lines);
    }

    public static int ChanceOf(long score, AcceptanceRules rules)
    {
        if (score >= rules.AcceptAt) return 1000;
        if (score < rules.RejectAt) return 0;
        return (int)((score - rules.RejectAt) * 1000 / (rules.AcceptAt - rules.RejectAt));
    }

    public static string Describe(Verdict verdict, AcceptanceRules rules)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(rules);
        var text = new StringBuilder();
        foreach (var line in verdict.Lines)
            text.Append("  ").Append(line.Name).Append(": ").Append(line.Raw.ToString(CultureInfo.InvariantCulture)).Append(" x ").Append(line.Weight.ToString(CultureInfo.InvariantCulture)).Append(" / 1000 = ")
                .Append(line.Contribution.ToString(CultureInfo.InvariantCulture)).Append('\n');
        text.Append("  score ").Append(verdict.Score.ToString(CultureInfo.InvariantCulture)).Append(" (accepts at ").Append(rules.AcceptAt.ToString(CultureInfo.InvariantCulture)).Append(", refuses below ")
            .Append(rules.RejectAt.ToString(CultureInfo.InvariantCulture)).Append("): chance ").Append(verdict.Chance.ToString(CultureInfo.InvariantCulture)).Append(" per mille");
        return text.ToString();
    }
}

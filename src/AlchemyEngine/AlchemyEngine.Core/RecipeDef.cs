namespace AlchemyEngine;

/// <summary>
/// A reaction. <see cref="Inputs"/> are consumed; <see cref="Catalysts"/> must be present and are not consumed;
/// <see cref="Environment"/> tags must all be offered by the caller (for example "heat"). <see cref="Outputs"/> are what appears.
/// When <see cref="Ordered"/> the inputs must be supplied in exactly this sequence; otherwise any order matches.
/// The chance is the exact fraction <see cref="ChanceNumerator"/>/<see cref="ChanceDenominator"/>, decided only from the caller's seed and attempt.
/// </summary>
public sealed record RecipeDef(
    string Id,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Catalysts,
    IReadOnlyList<string> Environment,
    IReadOnlyList<string> Outputs,
    bool Ordered,
    int ChanceNumerator,
    int ChanceDenominator);

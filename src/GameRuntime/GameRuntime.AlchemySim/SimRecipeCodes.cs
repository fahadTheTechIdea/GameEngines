namespace GameRuntime;

/// <summary>
/// Why a SimEngine transformation is not part of the alchemy view. Each code names one thing alchemy cannot represent; nothing is
/// dropped without one of these.
/// </summary>
public static class SimRecipeCodes
{
    /// <summary>The transformation is continuous (rates, not a batch of inputs to outputs), so it is not a combination.</summary>
    public const string Continuous = "ALCHEMYSIM_CONTINUOUS";
    /// <summary>The transformation makes an entity, not resources; alchemy elements are resources.</summary>
    public const string EntityOutput = "ALCHEMYSIM_ENTITY_OUTPUT";
    /// <summary>The transformation consumes nothing, so there is nothing to combine.</summary>
    public const string NoInputs = "ALCHEMYSIM_NO_INPUTS";
    /// <summary>A required tool is also an input resource; alchemy would need one resource to be both consumed and kept.</summary>
    public const string ToolIsInput = "ALCHEMYSIM_TOOL_IS_INPUT";
    /// <summary>No technology unlocks the transformation, so discovery could never gate it.</summary>
    public const string NotGated = "ALCHEMYSIM_NOT_GATED";
    /// <summary>The unlocking technology has prerequisites, unlocks other things, or another technology also unlocks the transformation.</summary>
    public const string GateAmbiguous = "ALCHEMYSIM_GATE_AMBIGUOUS";

    public const string UnknownProcessor = "ALCHEMYSIM_UNKNOWN_PROCESSOR";
}

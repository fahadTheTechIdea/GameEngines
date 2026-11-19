namespace GameRuntime;

/// <summary>A transformation left out of the alchemy view, with the code that says why (<see cref="SimRecipeCodes"/>).</summary>
public sealed record SimRecipeExclusion(string TransformationId, string Code, string Detail);

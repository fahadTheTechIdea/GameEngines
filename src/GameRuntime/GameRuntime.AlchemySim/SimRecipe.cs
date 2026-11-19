namespace GameRuntime;

/// <summary>
/// One transformation as the alchemy view sees it. <see cref="Id"/> is both the transformation id and the alchemy recipe id.
/// <see cref="TechnologyId"/> is the technology whose knowledge unlocks ordering it in the sim, taken from the engine's own unlock content.
/// </summary>
public sealed record SimRecipe(string Id, string TechnologyId, SimRecipeFacts Facts, IReadOnlyList<SimProcessor> Processors);

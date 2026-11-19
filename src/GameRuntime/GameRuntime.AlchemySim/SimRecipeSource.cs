using Beep.SimEngine.Content;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>
/// Reads the recipes a SimEngine game already defines and produces the alchemy view of them. The engine's content is the only owner of
/// the recipe fact: this reads the resolved content (resources, batch transformations, the technologies that unlock them, the entities
/// that can run them) and copies nothing into alchemy beyond which resources go in and which come out. Quantities, ports, durations,
/// costs and quality rules are exposed as the engine's own read-only <see cref="SimRecipeFacts"/>. A transformation alchemy cannot
/// represent is excluded with a <see cref="SimRecipeCodes"/> code; it is never half-bridged.
/// </summary>
public static partial class SimRecipeSource
{
    public static SimRecipeView Read(ResolvedContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var excluded = new List<SimRecipeExclusion>();
        var gates = GatesByTransformation(content);
        var processors = ProcessorsByTransformation(content);

        var recipes = new List<SimRecipe>();
        foreach (var transformation in content.All<ITransformationDefinition>())
        {
            var reason = Unrepresentable(content, transformation, gates, out var technology);
            if (reason is not null)
            {
                excluded.Add(new SimRecipeExclusion(transformation.Id, reason.Value.Code, reason.Value.Detail));
                continue;
            }
            recipes.Add(new SimRecipe(transformation.Id, technology!, FactsOf(transformation),
                processors.TryGetValue(transformation.Id, out var runners) ? runners : Array.Empty<SimProcessor>()));
        }
        return new SimRecipeView(AlchemyContentOf(content, recipes), recipes, excluded);
    }

    private readonly record struct Reason(string Code, string Detail);

    private static Reason? Unrepresentable(ResolvedContent content, ITransformationDefinition transformation,
        Dictionary<string, List<ITechnologyDefinition>> gates, out string? technology)
    {
        technology = null;
        if (transformation.Mode == TransformationMode.Continuous)
            return new Reason(SimRecipeCodes.Continuous, "runs at rates instead of turning a batch of inputs into outputs");
        if (transformation.EntityOutput != null)
            return new Reason(SimRecipeCodes.EntityOutput, "makes the entity '" + transformation.EntityOutput.Id + "', not resources");
        if (transformation.Inputs.IsEmpty)
            return new Reason(SimRecipeCodes.NoInputs, "consumes no resource");
        foreach (var tool in transformation.Tools.Lines)
            foreach (var input in transformation.Inputs.Lines)
                if (tool.Resource == input.Resource)
                    return new Reason(SimRecipeCodes.ToolIsInput, "'" + content.ResourceName(tool.Resource) + "' is both consumed and held as a tool");
        if (!gates.TryGetValue(transformation.Id, out var unlockers) || unlockers.Count == 0)
            return new Reason(SimRecipeCodes.NotGated, "no technology unlocks it, so discovery could not gate ordering it");
        if (unlockers.Count > 1)
            return new Reason(SimRecipeCodes.GateAmbiguous, "several technologies unlock it");
        var gate = unlockers[0];
        if (gate.Dependencies.Count != 0)
            return new Reason(SimRecipeCodes.GateAmbiguous, "its technology '" + gate.Id + "' has prerequisites");
        if (gate.Unlocks.Count != 1)
            return new Reason(SimRecipeCodes.GateAmbiguous, "its technology '" + gate.Id + "' also unlocks other things");
        technology = gate.Id;
        return null;
    }

    /// <summary>The technologies whose unlock content names each transformation, taken from the engine's own technology content.</summary>
    private static Dictionary<string, List<ITechnologyDefinition>> GatesByTransformation(ResolvedContent content)
    {
        var gates = new Dictionary<string, List<ITechnologyDefinition>>(StringComparer.Ordinal);
        foreach (var technology in content.All<ITechnologyDefinition>())
            foreach (var unlock in technology.Unlocks)
            {
                if (unlock is not TransformationTechnologyUnlockDef unlocksTransformation) continue;
                var id = unlocksTransformation.Target.Id;
                if (!gates.TryGetValue(id, out var list)) gates.Add(id, list = new List<ITechnologyDefinition>());
                list.Add(technology);
            }
        return gates;
    }

    /// <summary>The entities whose transformer component lists each transformation, with the operation that queues it.</summary>
    private static Dictionary<string, List<SimProcessor>> ProcessorsByTransformation(ResolvedContent content)
    {
        var processors = new Dictionary<string, List<SimProcessor>>(StringComparer.Ordinal);
        foreach (var entity in content.All<EntityDef>())
            foreach (var component in entity.Components)
            {
                if (component is not TransformerDef transformer) continue;
                foreach (var transformation in transformer.Transformations)
                {
                    if (!processors.TryGetValue(transformation.Id, out var list)) processors.Add(transformation.Id, list = new List<SimProcessor>());
                    list.Add(new SimProcessor(entity.Id, transformer.CapabilityId.Id));
                }
            }
        return processors;
    }

    private static SimRecipeFacts FactsOf(ITransformationDefinition transformation) =>
        new(transformation.Id, transformation.Category, transformation.Inputs, transformation.Tools, transformation.ToolConsumption,
            transformation.Outputs, transformation.Duration, transformation.Cash, transformation.InputPorts, transformation.ToolPorts,
            transformation.OutputPorts, transformation.OutputQualities.Count, transformation.OperatingInputs.Count);
}

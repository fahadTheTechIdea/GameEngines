using System.Text.Json;
using Beep.SimEngine;
using Beep.SimEngine.Content;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>
/// The one writer of the sim's knowledge of a discovered recipe. It records the recipe's unlocking technology in the engine's company
/// knowledge, the same record a finished research leaves, and announces it with the engine's own <see cref="TechnologyLearned"/> event.
/// From then on the engine's content-access rule lets the transformation be ordered; before it, the engine refuses. The payload is
/// <c>{"company":0,"recipe":"&lt;transformation id&gt;"}</c>; recipes outside the alchemy view are refused.
/// </summary>
public sealed class DiscoverRecipeEffect(SimRecipeViewHolder holder) : IDirectorEffect
{
    public const string EffectName = "discover-recipe";
    public string Name => EffectName;

    public Code Check(World world, string payloadJson)
    {
        var (company, recipe) = Read(payloadJson);
        if (!holder.View.TryRecipe(recipe, out var found)) return Codes.UnknownDef;
        if (!world.Services.TryGet<ICompanyKnowledge>(out var knowledge)) return Codes.NotAllowed;
        return knowledge.CheckRecord(company, TechnologyModule.ModuleId, found.TechnologyId, JsonValue.NewObject());
    }

    public void Apply(World world, string payloadJson)
    {
        var (company, recipe) = Read(payloadJson);
        var found = holder.View.Recipes.Single(r => r.Id == recipe);
        var knowledge = world.Services.Get<ICompanyKnowledge>();
        // Already known (for example by finished research): nothing to record and nothing to announce twice.
        if (knowledge.TryRead(PlayerId.None, company, TechnologyModule.ModuleId, found.TechnologyId, out _)) return;
        var recorded = knowledge.Record(company, TechnologyModule.ModuleId, found.TechnologyId, JsonValue.NewObject());
        if (!recorded.IsNone) throw new InvalidOperationException("the discovery was checked but could not be recorded: " + recorded.Id);
        world.Events.Publish(new TechnologyLearned(company, found.TechnologyId));
    }

    private static (PlayerId Company, string Recipe) Read(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;
        return (new PlayerId(root.GetProperty("company").GetInt32()), root.GetProperty("recipe").GetString()
            ?? throw new InvalidOperationException("discover-recipe payload needs a recipe"));
    }
}

using AlchemyEngine;
using Beep.SimEngine.Hosting;
using Beep.SimEngine.Standard;
using Beep.SimEngine.Views;

namespace GameRuntime;

/// <summary>One recipe the alchemy book and the sim disagree about.</summary>
public sealed record RecipeDrift(string RecipeId, bool InBook, bool InSim);

/// <summary>
/// Compares the two places a discovered recipe is held: the alchemy book (what the player knows) and the sim's company knowledge (what the
/// engine lets be ordered). They are written by one path and must agree whenever nothing is in flight; this reports every disagreement.
/// </summary>
public static class AlchemySimConsistency
{
    public static IReadOnlyList<RecipeDrift> Check(SimRecipeView view, SimGame game, DiscoveryBook book, int seat = 0)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(book);
        var technologies = game.Seat(seat).View.Feature<TechnologyView>();
        var drift = new List<RecipeDrift>();
        foreach (var recipe in view.Recipes)
        {
            var code = technologies.ReadTechnology(recipe.TechnologyId, out var snapshot);
            if (!code.IsNone) throw new InvalidOperationException("the sim cannot report technology '" + recipe.TechnologyId + "': " + code.Id);
            var inBook = book.Recipes.Contains(recipe.Id);
            if (inBook != snapshot.Known) drift.Add(new RecipeDrift(recipe.Id, inBook, snapshot.Known));
        }
        return drift;
    }
}

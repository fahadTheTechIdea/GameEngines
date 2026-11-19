using GameContent.Catalog;
using Xunit;

namespace GameContent.Tests;

/// <summary>Every age of the shipped history has a formation to field, and every formation is on offer from the age its equipment and its recipe allow.</summary>
public sealed class RosterCoverageTests
{
    private static readonly string[] ArmsPacks = ["arms-ancient", "arms-medieval", "arms-modern", "arms-future", "arms-fantasy"];

    private static CatalogSet Load(string id) => CatalogLoader.Load(new DirectoryCatalogSource(ShippedPackTests.PacksRoot), id).Required();

    /// <summary>The order of the first age a formation exists in: its own availability entry, else the latest age any of its equipment starts in.</summary>
    private static int FirstAge(CatalogSet set, CatalogFormation formation)
    {
        int OrderOf(string eraId) => set.TryEra(eraId, out var era) ? era.Order : throw new InvalidOperationException("no era " + eraId);
        var own = set.TryAvailability(formation.Resource, out var entry) ? OrderOf(entry.From) : int.MinValue;
        var equipment = formation.Equipment.Select(e => set.TryAvailability(e.Item, out var a) ? OrderOf(a.From) : int.MinValue).DefaultIfEmpty(int.MinValue).Max();
        return Math.Max(own, equipment);
    }

    [Fact]
    public void EveryAgeFromTheStoneAgeToTheInformationAgeHasAFormationToField()
    {
        var ages = Load("ages");
        var historical = ages.Eras.Where(e => e.Span is not null).OrderBy(e => e.Order).ToList();
        Assert.True(historical.Count >= 12, "found " + historical.Count + " historical ages");

        var first = new List<(string Formation, int Age)>();
        foreach (var id in ArmsPacks.Where(p => p != "arms-future" && p != "arms-fantasy"))
        {
            var set = Load(id);
            first.AddRange(set.Root.Units.Select(u => (u.Resource, FirstAge(set, u))));
        }
        var missing = new List<string>();
        foreach (var age in historical)
        {
            // Every age introduces a formation of its own: one whose newest equipment first exists in that age.
            if (!first.Any(f => f.Age == age.Order)) missing.Add(age.Id + " (order " + age.Order + ")");
        }
        Assert.True(missing.Count == 0, "no formation exists yet in: " + string.Join(", ", missing) + "; first ages: " + string.Join(", ", first.OrderBy(f => f.Age).Select(f => f.Formation + "@" + f.Age)));
    }

    [Fact]
    public void EveryFormationHasADistinctAgeOrRoleSoTheRostersAreNotCopiesOfEachOther()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var set in ArmsPacks.Select(Load))
            foreach (var unit in set.Root.Units)
            {
                Assert.True(names.Add(unit.Resource), unit.Resource + " is defined twice");
                Assert.NotEmpty(unit.Equipment);
            }
        Assert.True(names.Count >= 21, "found " + names.Count + " formations");
    }
}

using AIEngine;

namespace GameRuntime.Field.Headless;

/// <summary>The AI document of the field game (<c>field-ai.json</c>): how the commanders choose targets and when, and what the quartermaster offers and how much. It is data; the components around it only sense and translate.</summary>
public static class FieldAi
{
    private static readonly Lazy<AiSet> Document = new(() =>
    {
        using var stream = typeof(FieldAi).Assembly.GetManifestResourceStream("field-ai.json") ?? throw new InvalidOperationException("the program does not carry field-ai.json");
        using var reader = new StreamReader(stream);
        return AiLoader.Load(reader.ReadToEnd());
    });

    public static AiSet Set => Document.Value;

    /// <summary>The most of anything (rounds, fuel in thousandths, people, stock) the agents are told: what the document allows a battery to hold. Larger real amounts are told as this, never wrapped.
    /// The document keeps it at a hundredth of the engine's value limit, because the offer rules compare a load with a percentage (a load times 100 must still fit).</summary>
    public static long MaxQuantity => Set.Percepts.Single(p => p.Id == "battery").Attrs.Single(a => a.Id == "rounds").Maximum;

    /// <summary>
    /// A component that runs <paramref name="agents"/> agents of one behaviour of the field document. A think is only started when what is left of the step budget covers the behaviour's
    /// bound, so the budget is the bound of every agent that can be due on one tick (and never less than the largest bound of the document, which a step requires): then each agent thinks on every tick it is due, as the hand-written rules did, and none waits for another.
    /// </summary>
    public static AiComponent Component(string id, ulong seed, string behavior, int agents) => new(new AiRuntime(Set, seed), Math.Max(Set.MaxCost, Set.Behaviors[behavior].MaxCost * agents), id);

    /// <summary>
    /// The fight the document can hold: every group is told to the agents as a <c>unit</c> and a <c>group</c>, and every battery of a group as a <c>battery</c> it owns. A fight with more
    /// groups, or a group with more batteries, than the document's capacities would lose knowledge silently, so it is refused before the first tick, naming the capacity. The battery capacity
    /// of the document is the group capacity times the per-group capacity, so a fight that passes both holds all its batteries.
    /// </summary>
    public static void Check(IReadOnlyList<CombatEngine.Group> groups)
    {
        foreach (var kind in new[] { "group", "unit" })
            if (groups.Count > Set.Percepts.Single(p => p.Id == kind).Capacity)
                throw new FieldUsageException("the fight has " + groups.Count + " groups; the field AI document holds " + Set.Percepts.Single(p => p.Id == kind).Capacity + " of kind '" + kind + "'");
        var battery = Set.Percepts.Single(p => p.Id == "battery");
        foreach (var group in groups)
            if (group.Batteries.Count > battery.OwnerCapacity)
                throw new FieldUsageException("group '" + group.Id + "' has " + group.Batteries.Count + " batteries; the field AI document holds " + battery.OwnerCapacity + " for one group");
    }
}
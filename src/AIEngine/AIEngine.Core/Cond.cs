using EngineCommon;
using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>A condition of the shared ScenarioEngine grammar with the facts it needs resolved to places, evaluated by the shared evaluator.</summary>
internal sealed class Cond(Condition condition, IReadOnlyList<FactRef> facts)
{
    public bool Test(ThinkContext c)
    {
        var scratch = c.Scratch;
        scratch.Clear();
        foreach (var fact in facts) scratch[fact.Id] = c.Fact(fact);
        return Conditions.Evaluate(condition, scratch);
    }
}
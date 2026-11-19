using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>Adds a whole number to a number variable. The result stays inside the range the variable declares (it saturates there).</summary>
public sealed class AddEffect(VariableDef variable, long by) : Effect
{
    public VariableDef Variable { get; } = variable;
    public long By { get; } = by;

    internal override void Apply(StepContext context)
    {
        var current = context.State.VariableValues[Variable.Id].Integer;
        var next = Math.Clamp(checked(current + By), Variable.Minimum, Variable.Maximum);
        context.SetVariable(Variable, FactValue.From(next));
    }
}
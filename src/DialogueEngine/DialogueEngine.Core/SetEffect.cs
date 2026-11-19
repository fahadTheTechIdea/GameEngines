using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>Sets a variable to a value of its own type (and, for text with a list, one of the allowed values).</summary>
public sealed class SetEffect(VariableDef variable, FactValue value) : Effect
{
    public VariableDef Variable { get; } = variable;
    public FactValue Value { get; } = value;

    internal override void Apply(StepContext context) => context.SetVariable(Variable, Value);
}
using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>One argument of a signal: a fixed value, or a variable of the document read at the moment the signal is raised.</summary>
public sealed record SignalArg(FactValue? Value, string? Variable);

/// <summary>Tells the host something happened that only it can act on (start an objective, give an item), with the arguments the author gave. The dialogue does not change the world.</summary>
public sealed class SignalEffect(string name, IReadOnlyList<SignalArg>? args = null) : Effect
{
    public string Name { get; } = name;
    public IReadOnlyList<SignalArg> Args { get; } = args ?? Array.Empty<SignalArg>();

    internal override void Apply(StepContext context)
    {
        if (Args.Count == 0) { context.Events.Add(new SignalEvent(Name)); return; }
        var values = new List<FactValue>(Args.Count);
        foreach (var arg in Args) values.Add(arg.Variable is null ? arg.Value! : context.State.VariableValues[arg.Variable]);
        context.Events.Add(new SignalEvent(Name, values));
    }
}

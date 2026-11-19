using ScenarioEngine.Contracts;

namespace AIEngine;

public sealed record IntentArg(string Name, FactValue Value);

/// <summary>What an agent asks the host to do, with the arguments it worked out. <see cref="Node"/> is the act node that raised it.</summary>
public sealed record Intent(long Tick, string Agent, string Node, string Name, IReadOnlyList<IntentArg> Args)
{
    public FactValue Arg(string name) => Args.First(a => a.Name == name).Value;
}
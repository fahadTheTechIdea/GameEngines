using System.Text;

namespace AIEngine;

/// <summary>One node a think ran: how deep in the tree, which node, what it reported and, where it decided something, what.</summary>
public sealed record TraceStep(int Depth, string Node, string Kind, Status Status, string Detail);

/// <summary>
/// Why an agent did what it did: the nodes its last think ran, in the order it ran them, each with the status it reported and the figures it decided on (the score of every
/// utility option, the value a <c>let</c> wrote, how many things a query found, the intent an <c>act</c> raised). It holds at most <see cref="MaxSteps"/> steps and
/// <see cref="MaxDetail"/> characters of each detail, so a think that ran more says so with <see cref="Truncated"/>. The same state gives the same trace.
/// </summary>
public sealed class ThinkTrace(string agent, string behavior, long tick, long spent, IReadOnlyList<TraceStep> steps, bool truncated)
{
    public const int MaxSteps = 4096;
    public const int MaxDetail = 256;

    public string Agent { get; } = agent;
    public string Behavior { get; } = behavior;
    public long Tick { get; } = tick;

    /// <summary>The work units the think cost.</summary>
    public long Spent { get; } = spent;

    public IReadOnlyList<TraceStep> Steps { get; } = steps;

    /// <summary>True when the think ran more nodes than <see cref="MaxSteps"/>; the steps are the first that many.</summary>
    public bool Truncated { get; } = truncated;

    /// <summary>The trace as indented text, one node to a line.</summary>
    public string ToText()
    {
        var text = new StringBuilder();
        text.Append("agent ").Append(Agent).Append(" behaviour ").Append(Behavior).Append(" tick ").Append(Tick).Append(" spent ").Append(Spent).Append(" steps ").Append(Steps.Count).Append('\n');
        foreach (var step in Steps)
        {
            text.Append(' ', step.Depth * 2).Append(step.Node).Append(" [").Append(step.Kind).Append("] ").Append(step.Status.ToString().ToLowerInvariant());
            if (step.Detail.Length > 0) text.Append(" - ").Append(step.Detail);
            text.Append('\n');
        }
        if (Truncated) text.Append("... the think ran more nodes than the trace holds (").Append(MaxSteps).Append(")\n");
        return text.ToString();
    }
}
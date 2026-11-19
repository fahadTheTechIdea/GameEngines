namespace AIEngine;

public sealed class RandomOption(long weight, Node child)
{
    public long Weight { get; } = weight;
    public Node Child { get; } = child;
}
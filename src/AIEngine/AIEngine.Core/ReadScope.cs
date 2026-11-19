namespace AIEngine;

/// <summary>The percept loops the node being read is inside, innermost last. <see cref="HasIndex"/> is true inside a for-each body, where the item has a place in the loop.</summary>
internal sealed class ReadScope
{
    internal readonly record struct Frame(PerceptKindDef Kind, bool HasIndex, int Limit);

    private readonly List<Frame> _frames = new();

    public bool Empty => _frames.Count == 0;
    public Frame Innermost => _frames[^1];
    public int Depth => _frames.Count;

    public void Push(PerceptKindDef kind, bool hasIndex, int limit) => _frames.Add(new Frame(kind, hasIndex, limit));
    public void Pop() => _frames.RemoveAt(_frames.Count - 1);
}
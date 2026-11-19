namespace AIEngine;

/// <summary>Collects the steps of one think while it runs. Reused from think to think; bounded by <see cref="ThinkTrace.MaxSteps"/>.</summary>
internal sealed class TraceRecorder
{
    private readonly List<TraceStep> _steps = new();
    private int _depth;
    private readonly Stack<int> _places = new();
    private bool _truncated;

    public void Start()
    {
        _steps.Clear();
        _depth = 0;
        _places.Clear();
        _truncated = false;
    }

    /// <summary>Records that a node began; returns its place, or -1 when the trace is full.</summary>
    public int Enter(Node node)
    {
        var place = -1;
        if (_steps.Count < ThinkTrace.MaxSteps)
        {
            place = _steps.Count;
            _steps.Add(new TraceStep(_depth, node.Id, node.Kind, Status.Running, ""));
        }
        else _truncated = true;
        _depth++;
        _places.Push(place);
        return place;
    }

    public void Leave(int place, Status status)
    {
        _depth--;
        if (place >= 0) _steps[place] = _steps[place] with { Status = status };
        _places.Pop();
    }

    /// <summary>What the node being run decided, said once its decision is made. Ignored when the node is not in the trace.</summary>
    public void Note(string detail)
    {
        var place = _places.Peek();
        if (place < 0) return;
        _steps[place] = _steps[place] with { Detail = detail.Length <= ThinkTrace.MaxDetail ? detail : detail[..(ThinkTrace.MaxDetail - 3)] + "..." };
    }

    public ThinkTrace Finish(string agent, string behavior, long tick, long spent) => new(agent, behavior, tick, spent, _steps.ToArray(), _truncated);
}
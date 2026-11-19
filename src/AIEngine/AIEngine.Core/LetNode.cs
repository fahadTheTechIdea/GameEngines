namespace AIEngine;

/// <summary>Writes the value of an expression to one of the agent own keys. The document proved the value fits the key.</summary>
public sealed class LetNode : Node
{
    private readonly KeyDef _key;
    private readonly Expr _value;

    internal LetNode(string id, KeyDef key, Expr value) : base(id, -1, 1, false)
    {
        _key = key;
        _value = value;
    }

    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1);
        c.Agent.Keys[_key.Index] = _value.Eval(c);
        if (c.Trace is not null) c.Trace.Note(_key.Id + " = " + TraceText.Describe(c.Agent.Keys[_key.Index].ToFact(_key.Value.Kind)));
        return Status.Success;
    }
}
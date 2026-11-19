using ScenarioEngine.Contracts;

namespace AIEngine;

/// <summary>
/// Raises an intent. Without <c>until</c> it succeeds at once. With it, the node reports Running after raising the intent (once) and succeeds the first think
/// its <c>until</c> holds, fails when <c>failWhen</c> holds or when <c>timeout</c> ticks have passed, and raises the intent again only after it has ended.
/// </summary>
public sealed class ActNode : Node
{
    private readonly IntentDef _intent;
    private readonly Expr[] _args;
    private readonly Cond? _until;
    private readonly Cond? _failWhen;
    private readonly long _timeout;

    internal ActNode(string id, int slot, IntentDef intent, Expr[] args, Cond? until, Cond? failWhen, long timeout) : base(id, slot, 1, until is not null)
    {
        _intent = intent;
        _args = args;
        _until = until;
        _failWhen = failWhen;
        _timeout = timeout;
    }


    private void Raise(ThinkContext c)
    {
        var args = new IntentArg[_args.Length];
        for (var i = 0; i < args.Length; i++) args[i] = new IntentArg(_intent.Args[i].Id, _args[i].Eval(c).ToFact(_intent.Args[i].Kind));
        c.Intents.Add(new Intent(c.Tick, c.Agent.Id, Id, _intent.Id, args));
        if (c.Trace is not null) c.Trace.Note("raises " + _intent.Id + "(" + string.Join(", ", args.Select(a => a.Name + "=" + TraceText.Describe(a.Value))) + ")");
    }

    internal override Status Execute(ThinkContext c)
    {
        c.Charge(1);
        if (_until is null)
        {
            Raise(c);
            return Status.Success;
        }
        var slots = c.Agent.Slots;
        if (slots[Slot] == 0)
        {
            Raise(c);
            slots[Slot] = c.Tick + 1;
            c.Alive[Slot] = true;
            return Status.Running;
        }
        if (_failWhen is not null && _failWhen.Test(c))
        {
            slots[Slot] = 0;
            return Status.Failure;
        }
        if (_until.Test(c))
        {
            slots[Slot] = 0;
            return Status.Success;
        }
        if (_timeout > 0 && c.Tick - (slots[Slot] - 1) >= _timeout)
        {
            slots[Slot] = 0;
            return Status.Failure;
        }
        c.Alive[Slot] = true;
        return Status.Running;
    }
}
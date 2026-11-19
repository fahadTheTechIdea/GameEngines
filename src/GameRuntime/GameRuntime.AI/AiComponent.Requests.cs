using AIEngine;
using EngineCommon;

namespace GameRuntime;

public sealed partial class AiComponent
{
    private void Apply(IEventPort events, long tick, in Pending request)
    {
        try
        {
            switch (request.Kind)
            {
                case RequestKind.Add: _runtime.Add(request.Agent, request.A, tick); break;
                case RequestKind.Remove: _runtime.Remove(request.Agent); break;
                case RequestKind.Percept: Perceive(tick, request); break;
                case RequestKind.Lose: _runtime.Lose(request.Agent, request.A, request.B); break;
                case RequestKind.Key: SetKey(request); break;
            }
        }
        catch (EngineRefusedException refused)
        {
            // A refusal is a normal answer to a bad request: it is published with its code and the agents have not changed.
            events.Emit(new AiRefused(Id, request.RequestId, tick, refused.Code));
        }
    }

    private void Perceive(long tick, in Pending request)
    {
        if (!_runtime.Set.TryPercept(request.A, out var kind)) throw new EngineRefusedException(AiCodes.Percept, "there is no percept kind '" + request.A + "'");
        _runtime.Perceive(request.Agent, request.A, request.B, request.C, AiValues.Decode(kind, request.D, AiCodes.Percept), tick);
    }

    private void SetKey(in Pending request)
    {
        if (!_runtime.Set.TryKey(request.A, out var key)) throw new EngineRefusedException(AiCodes.Reference, "there is no key '" + request.A + "'");
        _runtime.SetKey(request.Agent, request.A, AiValues.Decode(key.Value, request.B, AiCodes.ValueType));
    }
}
namespace EngineCommon;

/// <summary>A request, document or save the engine refuses whole. The code says why; nothing was changed or repaired.</summary>
public sealed class EngineRefusedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
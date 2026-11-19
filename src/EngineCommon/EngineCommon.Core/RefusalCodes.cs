namespace EngineCommon;

/// <summary>The codes an engine uses when a document does not read: each engine states its own so a refusal names the engine.</summary>
public sealed record RefusalCodes(string Format, string UnknownField, string MissingField, string Range, string Duplicate, string Reference);
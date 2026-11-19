using ScenarioEngine.Contracts;

namespace AIEngine;

public enum FactSource { Key, ItemAttr, ItemId, ItemOwner, ItemIndex, Tick }

/// <summary>Where a fact a condition names comes from, worked out when the document loads so a think never parses a fact name.</summary>
internal readonly record struct FactRef(string Id, FactSource Source, int Index, FactKind Kind);
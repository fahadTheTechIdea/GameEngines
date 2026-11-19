namespace SpatialEngine;

/// <summary>
/// A kind of ground the map names. <see cref="Cost"/> is the cost of entering a cell of it (1 to 100, ordinary ground 10), 0 when it cannot be entered; it is
/// the only movement number the map carries. What the ground does to a shot or a march is a combat rule keyed by the same id, and lives in the combat rules.
/// </summary>
public sealed record TerrainDef(int Index, string Id, int Cost, bool BlocksSight, char Symbol);
using EngineCommon;

namespace WorldEventsEngine;

/// <summary>Reads and checks a world-events document. Anything the engine cannot run is refused here, whole, with a code; a loaded set never fails to run for a reason that could have been seen in the file.</summary>
public static class WorldLoader
{
    /// <summary>Format 2 adds self-exciting hazards (<c>excite</c> on every hazard) and neighbour-tilted weather (<c>spill</c> on every zone). Format 1 is refused with <see cref="WorldCodes.FormatSuperseded"/>; it is never migrated.</summary>
    public const int Format = 2;

    /// <summary>The longest document read at all, in characters.</summary>
    public const int MaxCharacters = 8 * 1024 * 1024;

    /// <param name="json">The exact text of the document; its SHA-256 becomes the digest of the set.</param>
    public static WorldSet Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaxCharacters) throw new EngineRefusedException(WorldCodes.Range, "a world-events document is at most " + MaxCharacters + " characters");
        return new WorldReader(json).Read();
    }
}

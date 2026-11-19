using EngineCommon;

namespace DiplomacyEngine;

/// <summary>Reads and checks a diplomacy document. Anything the engine cannot run is refused here, whole, with a code; a loaded set never fails to run for a reason that could have been seen in the file.</summary>
public static class DipLoader
{
    public const int Format = 1;

    /// <summary>The longest document read at all, in characters.</summary>
    public const int MaxCharacters = 8 * 1024 * 1024;

    public static DipSet Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaxCharacters) throw new EngineRefusedException(DipCodes.Range, "a diplomacy document is at most " + MaxCharacters + " characters");
        return new DipReader(json).Read();
    }
}

using EngineCommon;

namespace EconomyEngine;

/// <summary>Reads and checks an economy document. Anything the engine cannot run is refused here, whole, with a code; a loaded set never fails to run for a reason that could have been seen in the file.</summary>
public static class EconomyLoader
{
    public const int Format = 6;

    /// <summary>The longest document read at all, in characters.</summary>
    public const int MaxCharacters = 64 * 1024 * 1024;

    /// <param name="json">The exact text of the document; its SHA-256 becomes the digest of the set.</param>
    public static EconomySet Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaxCharacters) throw new EngineRefusedException(EconomyCodes.Range, "an economy document is at most " + MaxCharacters + " characters");
        return new EconomyReader(json).Read();
    }
}

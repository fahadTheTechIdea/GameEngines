using EngineCommon;

namespace TutorialEngine;

/// <summary>Reads and checks a tutorial document. Anything the engine cannot run is refused here, whole, with a code; a loaded set never fails to run for a reason that could have been seen in the file.</summary>
public static class TutorialLoader
{
    public const int Format = 1;

    /// <summary>The longest document read at all, in characters.</summary>
    public const int MaxCharacters = 8 * 1024 * 1024;

    /// <param name="json">The exact text of the document; its SHA-256 becomes the digest of the set.</param>
    public static TutorialSet Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaxCharacters) throw new EngineRefusedException(TutorialCodes.Range, "a tutorial document is at most " + MaxCharacters + " characters");
        return new TutorialReader(json).Read();
    }
}

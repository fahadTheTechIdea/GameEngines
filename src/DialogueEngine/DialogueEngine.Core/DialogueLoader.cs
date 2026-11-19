using EngineCommon;

namespace DialogueEngine;

/// <summary>Reads and checks a dialogue document. Anything the engine cannot run is refused here, whole, with a code; a loaded set never fails to run for a reason that could have been seen in the file.</summary>
public static class DialogueLoader
{
    public const int Format = 3;

    /// <summary>The longest document that is read at all, in characters: well above the 18 MB document of 130,816 nodes that the scale tests load, which is the largest the format's own limits make likely.</summary>
    public const int MaxCharacters = 64 * 1024 * 1024;

    /// <param name="json">The exact text of the document; its SHA-256 becomes the digest of the set.</param>
    /// <param name="speakers">The game catalog of who can speak.</param>
    public static DialogueSet Load(string json, ISpeakerCatalog speakers)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(speakers);
        if (json.Length > MaxCharacters) throw new EngineRefusedException(DialogueCodes.Range, "a dialogue document is at most " + MaxCharacters + " characters");
        return new DialogueReader(json, speakers).Read();
    }
}

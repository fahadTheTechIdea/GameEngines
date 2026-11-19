namespace DialogueEngine;

/// <summary>What a blocked content warning does to a line or answer that carries its tag.</summary>
public enum WarningAction
{
    /// <summary>A line is passed without being shown; an answer is hidden.</summary>
    Skip,
    /// <summary>A line or answer is shown with its declared alternative key instead.</summary>
    Replace,
}

/// <summary>
/// A content warning a document declares: the tag lines and answers carry (always <c>cw.</c> followed by a name), the boolean external fact the host answers with (true: this player has blocked it),
/// and what happens to blocked content.
/// </summary>
public sealed record ContentWarningDef(string Tag, string Fact, WarningAction Action)
{
    public const string Prefix = "cw.";
}

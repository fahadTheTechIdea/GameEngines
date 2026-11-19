namespace GameContent.Catalog;

/// <summary>The range a stat or skill draws from at each training level of a catalog formation.</summary>
public sealed record TrainingRanges(LevelRange Raw, LevelRange Regular, LevelRange Veteran, LevelRange Elite)
{
    // Indexed by TrainingLevel; a value outside the enum fails the app (IndexOutOfRangeException) instead of being named by a default arm.
    public LevelRange For(TrainingLevel level) => new[] { Raw, Regular, Veteran, Elite }[(int)level];
}

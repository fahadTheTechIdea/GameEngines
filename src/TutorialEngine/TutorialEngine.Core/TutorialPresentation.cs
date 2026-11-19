namespace TutorialEngine;

/// <summary>
/// What the player's accessibility settings do to what is presented: reduced motion turns every authored movement into none (a static outline), and enlarged text lengthens the time a tip stays so that it can
/// still be read. The engine states these; the host draws them. Whole numbers only.
/// </summary>
public static class TutorialPresentation
{
    /// <summary>The motion to draw: the authored one, or none when the player asked for reduced motion.</summary>
    public static Motion Effective(Motion authored, bool reducedMotion) => reducedMotion ? Motion.None : authored;

    /// <summary>The steps a tip stays for a text scale in permille (1000 is normal, 2000 is twice the size): the duration scaled, rounded up, at least one.</summary>
    public static long TipDuration(int duration, int textScalePermille) => Math.Max(1, ((long)duration * textScalePermille + 999) / 1000);
}

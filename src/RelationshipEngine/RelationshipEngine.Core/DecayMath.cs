namespace RelationshipEngine;

/// <summary>
/// The value of a timed modifier at an age, as a pure function of its magnitude and its definition: no accumulated rounding, no dependence on how often or in what order it is read. Division truncates toward
/// zero everywhere. <c>Fixed</c> holds the magnitude until the duration; <c>Linear</c> falls straight to 0 at the duration; <c>HalfLife</c> halves every half-life, joined by chords (so the value is exact at each
/// whole half-life and at most 6.15 percent above the true curve between them). Every shape is 0 from the duration on.
/// </summary>
public static class DecayMath
{
    public static long Value(long magnitude, long age, int duration, DecayDef decay)
    {
        ArgumentNullException.ThrowIfNull(decay);
        if (age < 0 || age >= duration) return 0;
        switch (decay.Kind)
        {
            case DecayKind.Fixed: return magnitude;
            case DecayKind.Linear: return magnitude * (duration - age) / duration;
            case DecayKind.HalfLife: break;
        }
        var whole = age / decay.HalfLife;
        var rest = age % decay.HalfLife;
        if (whole >= 62) return 0;
        var low = magnitude / (1L << (int)whole);
        var high = magnitude / (1L << (int)(whole + 1));
        return low - (low - high) * rest / decay.HalfLife;
    }

    /// <summary>A value scaled by a permille, truncating toward zero.</summary>
    public static long Scale(long value, int permille) => value * permille / 1000;

    /// <summary>The urgency of a need at a level: linear between the points of its curve, truncating toward zero.</summary>
    public static long Urgency(NeedDef need, long level)
    {
        ArgumentNullException.ThrowIfNull(need);
        var points = need.Urgency;
        if (level <= points[0].Level) return points[0].Urgency;
        for (var i = 1; i < points.Count; i++)
        {
            if (level > points[i].Level) continue;
            var a = points[i - 1];
            var b = points[i];
            return a.Urgency + (b.Urgency - a.Urgency) * (level - a.Level) / (b.Level - a.Level);
        }
        return points[^1].Urgency;
    }

    /// <summary>The step a value falls in, ignoring any history (the first state of something new).</summary>
    public static int PlainStep(IReadOnlyList<StepDef> steps, long value)
    {
        var index = 0;
        while (index + 1 < steps.Count && value >= steps[index + 1].From) index++;
        return index;
    }

    /// <summary>The step after a value changed: up as far as the value reaches, or, if it did not move up, down while it is below the current step's start by more than the hysteresis.</summary>
    public static int Resolve(IReadOnlyList<StepDef> steps, int current, long value, long hysteresis)
    {
        var index = current;
        while (index + 1 < steps.Count && value >= steps[index + 1].From) index++;
        if (index != current) return index;
        while (index > 0 && value < steps[index].From - hysteresis) index--;
        return index;
    }
}

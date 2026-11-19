using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TutorialEngine;

internal sealed class ImageEvent
{
    [JsonRequired] public long Count { get; set; }
    [JsonRequired] public long LastAt { get; set; }
}

internal sealed class ImageStep
{
    [JsonRequired] public int Status { get; set; }
    [JsonRequired] public long ActivatedAt { get; set; }
    [JsonRequired] public long DoneAt { get; set; }
    [JsonRequired] public int Hint { get; set; }
    [JsonRequired] public List<long> Baseline { get; set; } = new();
}

internal sealed class ImageTip
{
    [JsonRequired] public int Shows { get; set; }
    [JsonRequired] public bool Acknowledged { get; set; }
    [JsonRequired] public bool Visible { get; set; }
    [JsonRequired] public long ShownAt { get; set; }
    [JsonRequired] public long Until { get; set; }
    [JsonRequired] public long EndedAt { get; set; }
}

internal sealed class ImageJourney
{
    [JsonRequired] public int State { get; set; }
    [JsonRequired] public string Language { get; set; } = "";
    [JsonRequired] public int Page { get; set; }
    [JsonRequired] public bool StorySkipped { get; set; }
    [JsonRequired] public bool Replaying { get; set; }
    [JsonRequired] public string Entry { get; set; } = "";
    [JsonRequired] public string Intent { get; set; } = "";
}

/// <summary>
/// The state of a runtime as plain data, exactly what a save holds. Every field is required: a save with a field missing is refused, never completed with a default. The digest is taken over this data and nothing
/// else, so it can be checked before the data is understood, and the data is checked against the document afterwards.
/// </summary>
internal sealed class TutorialImage
{
    [JsonRequired] public int Format { get; set; }
    [JsonRequired] public string Document { get; set; } = "";
    [JsonRequired] public string Id { get; set; } = "";
    [JsonRequired] public long Step { get; set; }
    [JsonRequired] public long LastActivity { get; set; }
    [JsonRequired] public long LastTipShown { get; set; }
    [JsonRequired] public int Phase { get; set; }
    [JsonRequired] public bool IgnoreMastery { get; set; }
    [JsonRequired] public bool ReducedMotion { get; set; }
    [JsonRequired] public int TextScale { get; set; }
    [JsonRequired] public bool TipsEnabled { get; set; }
    [JsonRequired] public ImageJourney Journey { get; set; } = new();
    [JsonRequired] public List<ImageEvent> Events { get; set; } = new();
    [JsonRequired] public List<long> Skills { get; set; } = new();
    [JsonRequired] public List<ImageStep> Steps { get; set; } = new();
    [JsonRequired] public List<ImageTip> Tips { get; set; } = new();
    [JsonRequired] public string Digest { get; set; } = "";

    public static TutorialImage Of(TutorialRuntime runtime)
    {
        var journey = runtime.Journey;
        var image = new TutorialImage
        {
            Format = TutorialSave.Format, Document = runtime.Set.Digest, Id = runtime.Set.Id, Step = runtime.Step, LastActivity = runtime.LastActivity, LastTipShown = runtime.LastTipShown,
            Phase = (int)runtime.Phase, IgnoreMastery = runtime.IgnoreMastery, ReducedMotion = runtime.ReducedMotion, TextScale = runtime.TextScale, TipsEnabled = runtime.TipsEnabled,
            Journey = new ImageJourney { State = (int)journey.State, Language = journey.Language, Page = journey.Page, StorySkipped = journey.StorySkipped, Replaying = journey.Replaying, Entry = journey.Entry, Intent = journey.Intent },
        };
        for (var e = 0; e < runtime.EventCounts.Count; e++) image.Events.Add(new ImageEvent { Count = runtime.EventCounts[e], LastAt = runtime.EventLastAt[e] });
        image.Skills.AddRange(runtime.SkillPoints);
        foreach (var step in runtime.Steps)
            image.Steps.Add(new ImageStep { Status = (int)step.Status, ActivatedAt = step.ActivatedAt, DoneAt = step.DoneAt, Hint = step.Hint, Baseline = step.Baseline.ToList() });
        foreach (var tip in runtime.Tips)
            image.Tips.Add(new ImageTip { Shows = tip.Shows, Acknowledged = tip.Acknowledged, Visible = tip.Visible, ShownAt = tip.ShownAt, Until = tip.Until, EndedAt = tip.EndedAt });
        image.Digest = EngineCommon.Digest.Of(Canonical(image));
        return image;
    }

    private static string Text(string value) => JsonSerializer.Serialize(value);

    /// <summary>The text the digest is taken over: every part in a fixed order, numbers in the invariant culture, texts escaped.</summary>
    public static string Canonical(TutorialImage image)
    {
        var text = new StringBuilder("tutorial-state-1");
        var j = image.Journey;
        text.Append(CultureInfo.InvariantCulture, $"\ndoc={image.Document}\nid={image.Id}\nstep={image.Step}\nactivity={image.LastActivity}\ntipShown={image.LastTipShown}\nphase={image.Phase}\nignoreMastery={image.IgnoreMastery}");
        text.Append(CultureInfo.InvariantCulture, $"\nsettings {image.ReducedMotion}|{image.TextScale}|{image.TipsEnabled}");
        text.Append(CultureInfo.InvariantCulture, $"\njourney {j.State}|{Text(j.Language)}|{j.Page}|{j.StorySkipped}|{j.Replaying}|{Text(j.Entry)}|{Text(j.Intent)}");
        text.Append("\nevents ").Append(string.Join(',', image.Events.Select(e => e.Count.ToString(CultureInfo.InvariantCulture) + "@" + e.LastAt.ToString(CultureInfo.InvariantCulture))));
        text.Append("\nskills ").Append(string.Join(',', image.Skills.Select(s => s.ToString(CultureInfo.InvariantCulture))));
        foreach (var step in image.Steps) text.Append(CultureInfo.InvariantCulture, $"\nstep {step.Status}|{step.ActivatedAt}|{step.DoneAt}|{step.Hint}|{string.Join(',', step.Baseline.Select(b => b.ToString(CultureInfo.InvariantCulture)))}");
        foreach (var tip in image.Tips) text.Append(CultureInfo.InvariantCulture, $"\ntip {tip.Shows}|{tip.Acknowledged}|{tip.Visible}|{tip.ShownAt}|{tip.Until}|{tip.EndedAt}");
        return text.ToString();
    }
}

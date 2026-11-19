using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RelationshipEngine;

internal sealed class ImageTaste
{
    [JsonRequired] public string Tag { get; set; } = "";
    [JsonRequired] public int Level { get; set; }
}

internal sealed class ImageNeed
{
    [JsonRequired] public long Level { get; set; }
    [JsonRequired] public bool Critical { get; set; }
}

internal sealed class ImageMoodlet
{
    [JsonRequired] public int Moodlet { get; set; }
    [JsonRequired] public long Start { get; set; }
    [JsonRequired] public int Scale { get; set; }
    [JsonRequired] public string Source { get; set; } = "";
}

internal sealed class ImageMemory
{
    [JsonRequired] public int Memory { get; set; }
    [JsonRequired] public long Start { get; set; }
    [JsonRequired] public int Scale { get; set; }
    [JsonRequired] public int Hops { get; set; }
    [JsonRequired] public string Origin { get; set; } = "";
    [JsonRequired] public bool Told { get; set; }
}

internal sealed class ImageCooldown
{
    [JsonRequired] public int Interaction { get; set; }
    [JsonRequired] public long ReadyAt { get; set; }
}

internal sealed class ImageEdge
{
    [JsonRequired] public string To { get; set; } = "";
    [JsonRequired] public long Touched { get; set; }
    [JsonRequired] public List<long> Base { get; set; } = new();
    [JsonRequired] public List<int> Ladder { get; set; } = new();
    [JsonRequired] public List<int> Bonds { get; set; } = new();
    [JsonRequired] public List<ImageMemory> Memories { get; set; } = new();
    [JsonRequired] public List<ImageCooldown> Cooldowns { get; set; } = new();
}

internal sealed class ImageActor
{
    [JsonRequired] public string Id { get; set; } = "";
    [JsonRequired] public List<string> Traits { get; set; } = new();
    [JsonRequired] public List<ImageTaste> Tastes { get; set; } = new();
    [JsonRequired] public string Activity { get; set; } = "";
    [JsonRequired] public string Community { get; set; } = "";
    [JsonRequired] public long Standing { get; set; }
    [JsonRequired] public int MoodState { get; set; }
    [JsonRequired] public long Joined { get; set; }
    [JsonRequired] public List<ImageNeed> Needs { get; set; } = new();
    [JsonRequired] public List<ImageMoodlet> Moodlets { get; set; } = new();
    [JsonRequired] public List<ImageEdge> Edges { get; set; } = new();
}

/// <summary>
/// The state of a runtime as plain data, exactly what a save holds. Every field is required: a save with a field missing is refused, never completed with a default. The digest is taken over this data and nothing
/// else, so it can be checked before the data is understood, and the data is checked against the document afterwards.
/// </summary>
internal sealed class RelationshipImage
{
    [JsonRequired] public int Format { get; set; }
    [JsonRequired] public string Document { get; set; } = "";
    [JsonRequired] public string Id { get; set; } = "";
    [JsonRequired] public long Step { get; set; }
    [JsonRequired] public long Rolls { get; set; }
    [JsonRequired] public List<ImageActor> Actors { get; set; } = new();
    [JsonRequired] public string Digest { get; set; } = "";

    public static RelationshipImage Of(RelationshipRuntime runtime)
    {
        var image = new RelationshipImage { Format = RelationshipSave.Format, Document = runtime.Set.Digest, Id = runtime.Set.Id, Step = runtime.Step, Rolls = runtime.Rolls };
        foreach (var actor in runtime.Actors.Values)
        {
            var saved = new ImageActor
            {
                Id = actor.Id, Traits = actor.Traits.ToList(), Tastes = actor.Tastes.Select(p => new ImageTaste { Tag = p.Key, Level = p.Value }).ToList(), Activity = actor.Activity, Community = actor.Community, Standing = actor.Standing, MoodState = actor.MoodState, Joined = actor.Joined,
            };
            for (var n = 0; n < actor.Need.Length; n++) saved.Needs.Add(new ImageNeed { Level = actor.Need[n], Critical = actor.Critical[n] });
            foreach (var moodlet in actor.Moodlets) saved.Moodlets.Add(new ImageMoodlet { Moodlet = moodlet.Moodlet, Start = moodlet.Start, Scale = moodlet.Scale, Source = moodlet.Source });
            foreach (var edge in actor.Edges.Values)
            {
                var savedEdge = new ImageEdge { To = edge.To, Touched = edge.Touched, Base = edge.Base.ToList(), Ladder = edge.Ladder.ToList(), Bonds = edge.Bonds.ToList() };
                foreach (var memory in edge.Memories) savedEdge.Memories.Add(new ImageMemory { Memory = memory.Memory, Start = memory.Start, Scale = memory.Scale, Hops = memory.Hops, Origin = memory.Origin, Told = memory.Told });
                foreach (var cooldown in edge.Cooldowns.OrderBy(c => c.Interaction)) savedEdge.Cooldowns.Add(new ImageCooldown { Interaction = cooldown.Interaction, ReadyAt = cooldown.ReadyAt });
                saved.Edges.Add(savedEdge);
            }
            image.Actors.Add(saved);
        }
        image.Digest = EngineCommon.Digest.Of(Canonical(image));
        return image;
    }

    private static string Text(string value) => JsonSerializer.Serialize(value);

    /// <summary>The text the digest is taken over: every part in a fixed order, numbers in the invariant culture, texts escaped. Memories and moodlets are in the order the state holds them, which is part of the state.</summary>
    public static string Canonical(RelationshipImage image)
    {
        var text = new StringBuilder("relationship-state-2");
        text.Append(CultureInfo.InvariantCulture, $"\ndoc={image.Document}\nid={image.Id}\nstep={image.Step}\nrolls={image.Rolls}");
        foreach (var actor in image.Actors)
        {
            text.Append(CultureInfo.InvariantCulture, $"\nactor {Text(actor.Id)}|{Text(actor.Activity)}|{Text(actor.Community)}|{actor.Standing}|{actor.MoodState}|{actor.Joined}|traits {string.Join(',', actor.Traits.Select(Text))}|tastes {string.Join(',', actor.Tastes.Select(p => Text(p.Tag) + "=" + p.Level.ToString(CultureInfo.InvariantCulture)))}");
            text.Append("\n needs ").Append(string.Join(',', actor.Needs.Select(n => n.Level.ToString(CultureInfo.InvariantCulture) + (n.Critical ? "!" : ""))));
            foreach (var moodlet in actor.Moodlets) text.Append(CultureInfo.InvariantCulture, $"\n moodlet {moodlet.Moodlet}|{moodlet.Start}|{moodlet.Scale}|{Text(moodlet.Source)}");
            foreach (var edge in actor.Edges)
            {
                text.Append(CultureInfo.InvariantCulture, $"\n edge {Text(edge.To)}|{edge.Touched}|base {string.Join(',', edge.Base)}|ladder {string.Join(',', edge.Ladder)}|bonds {string.Join(',', edge.Bonds)}");
                foreach (var memory in edge.Memories) text.Append(CultureInfo.InvariantCulture, $"\n  memory {memory.Memory}|{memory.Start}|{memory.Scale}|{memory.Hops}|{Text(memory.Origin)}|{memory.Told}");
                foreach (var cooldown in edge.Cooldowns) text.Append(CultureInfo.InvariantCulture, $"\n  cooldown {cooldown.Interaction}|{cooldown.ReadyAt}");
            }
        }
        return text.ToString();
    }
}

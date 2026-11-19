using System.Globalization;
using System.Text;
using System.Text.Json;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace AIEngine;

internal sealed class ImageValue
{
    public string Kind { get; set; } = "";
    public long Number { get; set; }
    public string? Text { get; set; }
}

internal sealed class ImageKey
{
    public string Id { get; set; } = "";
    public ImageValue Value { get; set; } = new();
}

internal sealed class ImagePercept
{
    public string Kind { get; set; } = "";
    public string Id { get; set; } = "";
    public string Owner { get; set; } = "";
    public long Seen { get; set; }
    public List<ImageValue> Values { get; set; } = new();
}

internal sealed class ImageAgent
{
    public string Id { get; set; } = "";
    public string Behavior { get; set; } = "";
    public long Due { get; set; }
    public long Roll { get; set; }
    public List<ImageKey> Keys { get; set; } = new();
    public List<long> Slots { get; set; } = new();
    public List<ImagePercept> Knowledge { get; set; } = new();
}

/// <summary>
/// The state of a runtime as plain data, exactly what a save holds. The digest is taken over this data and nothing else, so it can be checked before the data
/// is understood, and the data is checked against the document afterwards.
/// </summary>
internal sealed class AiImage
{
    public int Format { get; set; }
    public string Document { get; set; } = "";
    public string Id { get; set; } = "";
    public string Seed { get; set; } = "";
    public long LastTick { get; set; }
    public List<ImageAgent> Agents { get; set; } = new();
    public string Digest { get; set; } = "";

    private static ImageValue Pack(ValueDef def, Val value) =>
        new() { Kind = def.Kind.ToString(), Number = def.Kind == FactKind.String ? 0 : value.Number, Text = def.Kind == FactKind.String ? value.Text : null };

    public static AiImage Of(AiRuntime runtime)
    {
        var set = runtime.Set;
        var image = new AiImage { Format = AiSave.Format, Document = set.Digest, Id = set.Id, Seed = runtime.Seed.ToString(CultureInfo.InvariantCulture), LastTick = runtime.LastTick };
        foreach (var (id, agent) in runtime.States)
        {
            var row = new ImageAgent { Id = id, Behavior = agent.Behavior.Id, Due = agent.Due, Roll = agent.RollCounter, Slots = agent.Slots.ToList() };
            foreach (var key in set.Keys) row.Keys.Add(new ImageKey { Id = key.Id, Value = Pack(key.Value, agent.Keys[key.Index]) });
            foreach (var kind in set.Percepts)
                foreach (var percept in agent.Knowledge[kind.Index].Values)
                    row.Knowledge.Add(new ImagePercept { Kind = kind.Id, Id = percept.Id, Owner = percept.Owner, Seen = percept.Seen, Values = kind.Attrs.Select(a => Pack(a, percept.Values[a.Index])).ToList() });
            image.Agents.Add(row);
        }
        image.Digest = EngineCommon.Digest.Of(Canonical(image));
        return image;
    }

    private static void Append(StringBuilder text, ImageValue value) =>
        text.Append(CultureInfo.InvariantCulture, $"{value.Kind}:{value.Number}:{JsonSerializer.Serialize(value.Text)}");

    /// <summary>The text the digest is taken over: every part in a fixed order, numbers in the invariant culture, texts escaped.</summary>
    public static string Canonical(AiImage image)
    {
        var text = new StringBuilder("ai-state-1");
        text.Append(CultureInfo.InvariantCulture, $"\ndoc={image.Document}\nid={image.Id}\nseed={image.Seed}\ntick={image.LastTick}");
        foreach (var agent in image.Agents)
        {
            text.Append(CultureInfo.InvariantCulture, $"\nagent {agent.Id}|{agent.Behavior}|due={agent.Due}|roll={agent.Roll}|slots={string.Join(',', agent.Slots)}");
            foreach (var key in agent.Keys) { text.Append("\n key ").Append(key.Id).Append('='); Append(text, key.Value); }
            foreach (var know in agent.Knowledge)
            {
                text.Append(CultureInfo.InvariantCulture, $"\n know {know.Kind}|{know.Id}|{JsonSerializer.Serialize(know.Owner)}|{know.Seen}=");
                foreach (var value in know.Values) { Append(text, value); text.Append(';'); }
            }
        }
        return text.ToString();
    }
}
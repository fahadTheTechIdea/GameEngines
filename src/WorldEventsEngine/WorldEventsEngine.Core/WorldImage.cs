using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorldEventsEngine;

internal sealed class ImageRegion
{
    [JsonRequired] public int Weather { get; set; }
    [JsonRequired] public long Anomaly { get; set; }
    [JsonRequired] public long Temp { get; set; }
    [JsonRequired] public long Precip { get; set; }
    [JsonRequired] public long Wind { get; set; }
    [JsonRequired] public long Wetness { get; set; }
    [JsonRequired] public long SoilTenths { get; set; }
    [JsonRequired] public long River { get; set; }
    [JsonRequired] public long Snow { get; set; }
    [JsonRequired] public long DryStreak { get; set; }
    [JsonRequired] public List<long> Susceptible { get; set; } = new();
    [JsonRequired] public List<long> Exposed { get; set; } = new();
    [JsonRequired] public List<long> Infectious { get; set; } = new();
    [JsonRequired] public List<long> Recovered { get; set; } = new();
    [JsonRequired] public List<long> Dead { get; set; } = new();
    [JsonRequired] public List<long> Peak { get; set; } = new();
    [JsonRequired] public List<int> Outbreak { get; set; } = new();
    [JsonRequired] public List<int> Interventions { get; set; } = new();
}

internal sealed class ImageHazard
{
    [JsonRequired] public int Hazard { get; set; }
    [JsonRequired] public int Region { get; set; }
    [JsonRequired] public int Rung { get; set; }
    [JsonRequired] public int Phase { get; set; }
    [JsonRequired] public long PhaseStart { get; set; }
    [JsonRequired] public int PhaseLength { get; set; }
    [JsonRequired] public int PeakLength { get; set; }
    [JsonRequired] public int Power { get; set; }
}

internal sealed class ImageTimed
{
    [JsonRequired] public int Card { get; set; }
    [JsonRequired] public int Region { get; set; }
    [JsonRequired] public int Modifier { get; set; }
    [JsonRequired] public int Delta { get; set; }
    [JsonRequired] public long Until { get; set; }
}

internal sealed class ImageAgenda
{
    [JsonRequired] public long Due { get; set; }
    [JsonRequired] public int Card { get; set; }
    [JsonRequired] public int Region { get; set; }
    [JsonRequired] public int Parent { get; set; }
    [JsonRequired] public int Link { get; set; }
}

/// <summary>
/// The state of a runtime as plain data, exactly what a save holds. Every field is required: a save with a field missing is refused, never completed with a default. The digest is taken over this data and nothing
/// else, so it can be checked before the data is understood, and the data is checked against the document afterwards. There is no random-generator state in it: every random number is a function of the document's seed,
/// a stream and the step or the draw count, both of which are here.
/// </summary>
internal sealed class WorldImage
{
    [JsonRequired] public int Format { get; set; }
    [JsonRequired] public string Document { get; set; } = "";
    [JsonRequired] public string Id { get; set; } = "";
    [JsonRequired] public long Step { get; set; }
    [JsonRequired] public int Season { get; set; }
    [JsonRequired] public int Regime { get; set; }
    [JsonRequired] public int Storyteller { get; set; }
    [JsonRequired] public int Difficulty { get; set; }
    [JsonRequired] public long Stress { get; set; }
    [JsonRequired] public int Phase { get; set; }
    [JsonRequired] public long PhaseStart { get; set; }
    [JsonRequired] public long LastCard { get; set; }
    [JsonRequired] public long Draws { get; set; }
    [JsonRequired] public long Suppressed { get; set; }
    [JsonRequired] public long Dropped { get; set; }
    [JsonRequired] public long Played { get; set; }
    [JsonRequired] public List<long> CardReadyAt { get; set; } = new();
    [JsonRequired] public List<long> GroupLockedUntil { get; set; } = new();
    [JsonRequired] public List<long> HazardReadyAt { get; set; } = new();
    [JsonRequired] public List<long> Excitation { get; set; } = new();
    [JsonRequired] public List<ImageRegion> Regions { get; set; } = new();
    [JsonRequired] public List<ImageHazard> Hazards { get; set; } = new();
    [JsonRequired] public List<ImageTimed> Timed { get; set; } = new();
    [JsonRequired] public List<ImageAgenda> Agenda { get; set; } = new();
    [JsonRequired] public string Digest { get; set; } = "";

    public static WorldImage Of(WorldRuntime runtime)
    {
        var image = new WorldImage
        {
            Format = WorldSave.Format, Document = runtime.Set.Digest, Id = runtime.Set.Id, Step = runtime.Step, Season = runtime.Season, Regime = runtime.Regime, Storyteller = runtime.Storyteller,
            Difficulty = runtime.Difficulty, Stress = runtime.Stress, Phase = (int)runtime.Phase, PhaseStart = runtime.PhaseStart, LastCard = runtime.LastCard, Draws = runtime.Draws,
            Suppressed = runtime.Suppressed, Dropped = runtime.Dropped, Played = runtime.Played,
        };
        image.CardReadyAt.AddRange(runtime.CardReadyAt);
        image.GroupLockedUntil.AddRange(runtime.GroupLockedUntil);
        image.HazardReadyAt.AddRange(runtime.HazardReadyAt);
        image.Excitation.AddRange(runtime.Excitation);
        foreach (var region in runtime.Regions)
        {
            var item = new ImageRegion
            {
                Weather = region.Weather, Anomaly = region.Anomaly, Temp = region.Temp, Precip = region.Precip, Wind = region.Wind, Wetness = region.Wetness, SoilTenths = region.SoilTenths,
                River = region.River, Snow = region.Snow, DryStreak = region.DryStreak,
            };
            item.Susceptible.AddRange(region.Susceptible);
            item.Exposed.AddRange(region.Exposed);
            item.Infectious.AddRange(region.Infectious);
            item.Recovered.AddRange(region.Recovered);
            item.Dead.AddRange(region.Dead);
            item.Peak.AddRange(region.Peak);
            item.Outbreak.AddRange(region.Outbreak.Select(o => (int)o));
            item.Interventions.AddRange(region.Interventions.Select(f => (int)f));
            image.Regions.Add(item);
        }
        foreach (var h in runtime.ActiveHazards)
            image.Hazards.Add(new ImageHazard { Hazard = h.Hazard, Region = h.Region, Rung = h.Rung, Phase = (int)h.Phase, PhaseStart = h.PhaseStart, PhaseLength = h.PhaseLength, PeakLength = h.PeakLength, Power = h.Power });
        foreach (var t in runtime.TimedModifiers) image.Timed.Add(new ImageTimed { Card = t.Card, Region = t.Region, Modifier = t.Modifier, Delta = t.Delta, Until = t.Until });
        foreach (var a in runtime.Agenda) image.Agenda.Add(new ImageAgenda { Due = a.Due, Card = a.Card, Region = a.Region, Parent = a.Parent, Link = a.Link });
        image.Digest = EngineCommon.Digest.Of(Canonical(image));
        return image;
    }

    private static string Join<T>(IEnumerable<T> values) where T : IFormattable => string.Join(',', values.Select(v => v.ToString(null, CultureInfo.InvariantCulture)));

    private static string Text(string value) => JsonSerializer.Serialize(value);

    /// <summary>The text the digest is taken over: every part in a fixed order, numbers in the invariant culture, texts escaped.</summary>
    public static string Canonical(WorldImage image)
    {
        var text = new StringBuilder("world-state-2");
        text.Append(CultureInfo.InvariantCulture, $"\ndoc={image.Document}\nid={Text(image.Id)}\nstep={image.Step}\nseason={image.Season}\nregime={image.Regime}\ntold={image.Storyteller}\ndiff={image.Difficulty}");
        text.Append(CultureInfo.InvariantCulture, $"\ndirector {image.Stress}|{image.Phase}|{image.PhaseStart}|{image.LastCard}|{image.Draws}|{image.Suppressed}|{image.Dropped}|{image.Played}");
        text.Append("\ncards ").Append(Join(image.CardReadyAt)).Append("\ngroups ").Append(Join(image.GroupLockedUntil)).Append("\nready ").Append(Join(image.HazardReadyAt)).Append("\nexcite ").Append(Join(image.Excitation));
        foreach (var r in image.Regions)
        {
            text.Append(CultureInfo.InvariantCulture, $"\nregion {r.Weather}|{r.Anomaly}|{r.Temp}|{r.Precip}|{r.Wind}|{r.Wetness}|{r.SoilTenths}|{r.River}|{r.Snow}|{r.DryStreak}");
            text.Append("\n S ").Append(Join(r.Susceptible)).Append("\n E ").Append(Join(r.Exposed)).Append("\n I ").Append(Join(r.Infectious)).Append("\n R ").Append(Join(r.Recovered));
            text.Append("\n D ").Append(Join(r.Dead)).Append("\n P ").Append(Join(r.Peak)).Append("\n O ").Append(Join(r.Outbreak)).Append("\n V ").Append(Join(r.Interventions));
        }
        foreach (var h in image.Hazards) text.Append(CultureInfo.InvariantCulture, $"\nhazard {h.Hazard}|{h.Region}|{h.Rung}|{h.Phase}|{h.PhaseStart}|{h.PhaseLength}|{h.PeakLength}|{h.Power}");
        foreach (var t in image.Timed) text.Append(CultureInfo.InvariantCulture, $"\ntimed {t.Card}|{t.Region}|{t.Modifier}|{t.Delta}|{t.Until}");
        foreach (var a in image.Agenda) text.Append(CultureInfo.InvariantCulture, $"\nagenda {a.Due}|{a.Card}|{a.Region}|{a.Parent}|{a.Link}");
        return text.ToString();
    }
}

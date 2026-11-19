using EngineCommon;
using EngineCommon.Graph;
using ScenarioEngine.Contracts;

namespace WorldEventsEngine;

/// <summary>A loaded world-events document: every declaration plus the digest of the exact text it was read from. It is immutable and holds no world; the runtime holds that. Every query is a pure function of it.</summary>
public sealed class WorldSet
{
    internal WorldSet(WorldParts p)
    {
        Id = p.Id; Digest = p.Digest; Seed = p.Seed; StepsPerDay = p.StepsPerDay; Limits = p.Limits; Hydrology = p.Hydrology; Seasons = p.Seasons; Weathers = p.Weathers; Modifiers = p.Modifiers;
        Zones = p.Zones; Regimes = p.Regimes; Regions = p.Regions; Links = p.Links; Hazards = p.Hazards; Diseases = p.Diseases; Interventions = p.Interventions; Storytellers = p.Storytellers;
        Difficulties = p.Difficulties; Cards = p.Cards; Groups = p.Groups; HostFacts = p.HostFacts; LocalFacts = p.LocalFacts; GlobalFacts = p.GlobalFacts; StartStoryteller = p.StartStoryteller; StartDifficulty = p.StartDifficulty;
        SeasonById = Seasons.ToDictionary(s => s.Id, StringComparer.Ordinal);
        RegionById = Regions.ToDictionary(r => r.Id, StringComparer.Ordinal);
        HazardById = Hazards.ToDictionary(h => h.Id, StringComparer.Ordinal);
        DiseaseById = Diseases.ToDictionary(d => d.Id, StringComparer.Ordinal);
        InterventionById = Interventions.ToDictionary(i => i.Id, StringComparer.Ordinal);
        CardById = Cards.ToDictionary(c => c.Id, StringComparer.Ordinal);
        StorytellerById = Storytellers.ToDictionary(s => s.Id, StringComparer.Ordinal);
        DifficultyById = Difficulties.ToDictionary(d => d.Id, StringComparer.Ordinal);
        ModifierById = Modifiers.ToDictionary(m => m.Id, StringComparer.Ordinal);
        WeatherById = Weathers.ToDictionary(w => w.Id, StringComparer.Ordinal);
        YearSteps = Seasons.Sum(s => s.Steps);
        var effects = Hazards.SelectMany(h => h.Ladder).SelectMany(r => r.Effects).Concat(Diseases.SelectMany(d => d.OnOutbreak)).Concat(Cards.SelectMany(c => c.Effects)).ToList();
        Signals = effects.Where(e => e.Kind == EffectKind.Signal).Select(e => e.Name).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Shocks = effects.Where(e => e.Kind == EffectKind.Shock).Select(e => e.Name).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Graph = BuildGraph(Regions.Count, Links);
    }

    /// <summary>The region graph, built once by the shared graph library: every link is two edges whose weight is the link's flow (millionths). Regions are its nodes by index; a region's edges come in ascending order of the other end.</summary>
    private static CsrGraph BuildGraph(int regions, IReadOnlyList<LinkDef> links)
    {
        var created = GraphBuilder.Create(regions, out var builder);
        if (created.Length != 0) throw new EngineRefusedException(WorldCodes.Links, "the region graph was refused: " + created);
        foreach (var link in links)
        {
            var added = builder!.AddUndirected(link.A, link.B, link.FlowPpm);
            if (added.Length != 0) throw new EngineRefusedException(WorldCodes.Links, "the region graph was refused: " + added);
        }
        var built = builder!.Build();
        if (!built.Ok) throw new EngineRefusedException(WorldCodes.Links, "the region graph was refused: " + built.Code);
        return built.Graph!;
    }

    public string Id { get; }

    /// <summary>The SHA-256 of the exact text of the document.</summary>
    public string Digest { get; }

    public ulong Seed { get; }

    /// <summary>How many steps make a day of the game: the contract with the calendar (a step is a whole number of its ticks).</summary>
    public int StepsPerDay { get; }

    public LimitsDef Limits { get; }
    public HydrologyDef Hydrology { get; }
    public IReadOnlyList<SeasonDef> Seasons { get; }
    public IReadOnlyList<WeatherDef> Weathers { get; }
    public IReadOnlyList<ModifierDef> Modifiers { get; }
    public IReadOnlyList<ZoneDef> Zones { get; }
    public IReadOnlyList<RegimeDef> Regimes { get; }
    public IReadOnlyList<RegionDef> Regions { get; }
    public IReadOnlyList<LinkDef> Links { get; }
    public IReadOnlyList<HazardDef> Hazards { get; }
    public IReadOnlyList<DiseaseDef> Diseases { get; }
    public IReadOnlyList<InterventionDef> Interventions { get; }
    public IReadOnlyList<StorytellerDef> Storytellers { get; }
    public IReadOnlyList<DifficultyDef> Difficulties { get; }
    public IReadOnlyList<CardDef> Cards { get; }

    /// <summary>The ids of the exclusion groups the cards name, in ordinal order; a card's group is an index into it.</summary>
    public IReadOnlyList<string> Groups { get; }

    /// <summary>The facts the host supplies, with their types.</summary>
    public IReadOnlyDictionary<string, FactDefinition> HostFacts { get; }

    /// <summary>The facts a region condition may read: the region's own, the world's and the host's.</summary>
    public IReadOnlyDictionary<string, FactDefinition> LocalFacts { get; }

    /// <summary>The facts a global card condition may read: the world's and the host's.</summary>
    public IReadOnlyDictionary<string, FactDefinition> GlobalFacts { get; }

    public int StartStoryteller { get; }
    public int StartDifficulty { get; }

    public IReadOnlyDictionary<string, SeasonDef> SeasonById { get; }
    public IReadOnlyDictionary<string, RegionDef> RegionById { get; }
    public IReadOnlyDictionary<string, HazardDef> HazardById { get; }
    public IReadOnlyDictionary<string, DiseaseDef> DiseaseById { get; }
    public IReadOnlyDictionary<string, InterventionDef> InterventionById { get; }
    public IReadOnlyDictionary<string, CardDef> CardById { get; }
    public IReadOnlyDictionary<string, StorytellerDef> StorytellerById { get; }
    public IReadOnlyDictionary<string, DifficultyDef> DifficultyById { get; }
    public IReadOnlyDictionary<string, ModifierDef> ModifierById { get; }
    public IReadOnlyDictionary<string, WeatherDef> WeatherById { get; }

    /// <summary>Every signal name any hazard, outbreak or card issues, once, in ordinal order: what the story layers can listen for.</summary>
    public IReadOnlyList<string> Signals { get; }

    /// <summary>Every economy shock id any hazard, outbreak or card asks for, once, in ordinal order: what the economy must have.</summary>
    public IReadOnlyList<string> Shocks { get; }

    /// <summary>The steps of the document's own year (the seasons' lengths added), which a game without a calendar cycles through.</summary>
    public int YearSteps { get; }

    /// <summary>The regions and their links as one graph (see <see cref="BuildGraph"/>): the neighbours of a region for the spread of a hazard, the mixing of an infection and the tilt of the weather.</summary>
    public CsrGraph Graph { get; }

    /// <summary>The season of the document's own cycle at a step: the seasons in order, each for its steps, the year again and again. A game with a calendar takes its seasons from the calendar instead.</summary>
    public int CycleSeason(long step)
    {
        var inYear = (int)(step % YearSteps);
        for (var s = 0; s < Seasons.Count; s++)
        {
            if (inYear < Seasons[s].Steps) return s;
            inYear -= Seasons[s].Steps;
        }
        return Seasons.Count - 1;
    }
}

/// <summary>What a reader hands to <see cref="WorldSet"/>: named, so the constructor does not take thirty positional arguments.</summary>
internal sealed class WorldParts
{
    public string Id = "", Digest = "";
    public ulong Seed;
    public int StepsPerDay, StartStoryteller, StartDifficulty;
    public LimitsDef Limits = new(1, 1, 1);
    public HydrologyDef Hydrology = new(0, 1, 0, 0, 0, 0, 0, 0, 0);
    public IReadOnlyList<SeasonDef> Seasons = [];
    public IReadOnlyList<WeatherDef> Weathers = [];
    public IReadOnlyList<ModifierDef> Modifiers = [];
    public IReadOnlyList<ZoneDef> Zones = [];
    public IReadOnlyList<RegimeDef> Regimes = [];
    public IReadOnlyList<RegionDef> Regions = [];
    public IReadOnlyList<LinkDef> Links = [];
    public IReadOnlyList<HazardDef> Hazards = [];
    public IReadOnlyList<DiseaseDef> Diseases = [];
    public IReadOnlyList<InterventionDef> Interventions = [];
    public IReadOnlyList<StorytellerDef> Storytellers = [];
    public IReadOnlyList<DifficultyDef> Difficulties = [];
    public IReadOnlyList<CardDef> Cards = [];
    public IReadOnlyList<string> Groups = [];
    public IReadOnlyDictionary<string, FactDefinition> HostFacts = new Dictionary<string, FactDefinition>();
    public IReadOnlyDictionary<string, FactDefinition> LocalFacts = new Dictionary<string, FactDefinition>();
    public IReadOnlyDictionary<string, FactDefinition> GlobalFacts = new Dictionary<string, FactDefinition>();
}

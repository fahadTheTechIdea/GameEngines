using EngineCommon;
using EngineCommon.Graph;

namespace WorldEventsEngine;

/// <summary>A hazard that is happening in one region: which rung it is, where it is in its life, and how strongly it reaches this region (per mille of full strength; less than 1,000 where it spread to).</summary>
public sealed class HazardInstance
{
    public int Hazard { get; set; }
    public int Region { get; set; }
    public int Rung { get; set; }
    public HazardPhase Phase { get; set; }
    public long PhaseStart { get; set; }
    public int PhaseLength { get; set; }
    public int PeakLength { get; set; }
    public int Power { get; set; }
}

/// <summary>A change a card made to a modifier for a while: in one region, or in the whole world (region -1), until a step.</summary>
public sealed class TimedModifier
{
    public int Card { get; set; }
    public int Region { get; set; }
    public int Modifier { get; set; }
    public int Delta { get; set; }
    public long Until { get; set; }
}

/// <summary>A card that a chain will play when its step comes, in the region the chain was played in (-1 for none). <c>Parent</c> and <c>Link</c> name the chain entry that scheduled it, whose condition is checked when it comes due.</summary>
public sealed class AgendaItem
{
    public long Due { get; set; }
    public int Card { get; set; }
    public int Region { get; set; }
    public int Parent { get; set; }
    public int Link { get; set; }
}

/// <summary>Everything one region is: its weather and the water in it, and, for each disease, how many of its people are susceptible, exposed, infectious, recovered and dead.</summary>
public sealed class RegionState
{
    public RegionState(int diseases, int interventions)
    {
        Susceptible = new long[diseases];
        Exposed = new long[diseases];
        Infectious = new long[diseases];
        Recovered = new long[diseases];
        Dead = new long[diseases];
        Peak = new long[diseases];
        Outbreak = new OutbreakState[diseases];
        Interventions = new byte[interventions];
    }

    public int Weather { get; set; }
    public long Anomaly { get; set; }
    public long Temp { get; set; }
    public long Precip { get; set; }
    public long Wind { get; set; }
    public long Wetness { get; set; }
    public long SoilTenths { get; set; }
    public long River { get; set; }
    public long Snow { get; set; }
    public long DryStreak { get; set; }
    public long[] Susceptible { get; }
    public long[] Exposed { get; }
    public long[] Infectious { get; }
    public long[] Recovered { get; }
    public long[] Dead { get; }
    public long[] Peak { get; }
    public OutbreakState[] Outbreak { get; }

    /// <summary>Per intervention: bit 0 is set when the player or a script started it, bit 1 when its automatic trigger did.</summary>
    public byte[] Interventions { get; }

    public long Living(int disease) => Susceptible[disease] + Exposed[disease] + Infectious[disease] + Recovered[disease];
}

/// <summary>
/// The world: weather, hazards, diseases, and the director that spaces the trouble they make. It is a pure function of the document, the steps it was given (with the season of each and the host facts) and the requests
/// it answered, and of nothing else: no clock, no wall time, no floating point, no random generator with a state. Every random number is the counter-based <see cref="DeterministicRoll"/> of the seed, a stream name and a
/// step (or a draw count kept in the state), so a replay from a save reproduces everything, and a save is a complete copy of the state.
/// </summary>
public sealed partial class WorldRuntime
{
    /// <summary>The most steps one call to <see cref="Advance"/> goes (a skip of ten years of days is refused; the host skips in smaller parts).</summary>
    public const int MaxAdvance = 4000;

    private const int WeatherSlots = 16, DiseaseSlots = 16, HazardSlots = 4, RegionSlots = 4096;

    private readonly string[] _weatherStreams;
    private readonly string[] _diseaseStreams;
    private readonly string[] _hazardStreams;
    private readonly List<HazardInstance> _hazards = new();
    private readonly List<TimedModifier> _timed = new();
    private readonly List<AgendaItem> _agenda = new();
    private readonly HazardInstance?[] _hazardAt;
    private readonly long[][] _snapshot;
    private readonly long[] _excitation;
    private readonly int[] _weatherBefore;
    private readonly BfsResult _reach = new();
    private int _season = -1;
    private int _regime;
    private int _storyteller;
    private int _difficulty;
    private long _stress;
    private DirectorPhase _phase;
    private long _phaseStart;
    private long _lastCard = -1;
    private long _draws;
    private long _suppressed;
    private long _dropped;
    private long _played;

    private WorldRuntime(WorldSet set)
    {
        Set = set;
        Regions = set.Regions.Select(_ => new RegionState(set.Diseases.Count, set.Interventions.Count)).ToArray();
        _weatherStreams = set.Regions.Select(r => "wx:" + r.Id).ToArray();
        _diseaseStreams = set.Regions.Select(r => "ds:" + r.Id).ToArray();
        _hazardStreams = set.Hazards.Select(h => "hz:" + h.Id).ToArray();
        _hazardAt = new HazardInstance?[set.Hazards.Count * set.Regions.Count];
        _snapshot = set.Diseases.Select(_ => new long[set.Regions.Count]).ToArray();
        _excitation = new long[set.Hazards.Count * set.Regions.Count];
        _weatherBefore = new int[set.Regions.Count];
        CardReadyAt = new long[set.Cards.Count];
        GroupLockedUntil = new long[set.Groups.Count];
        HazardReadyAt = new long[set.Hazards.Count * set.Regions.Count];
        _storyteller = set.StartStoryteller;
        _difficulty = set.StartDifficulty;
    }

    public static WorldRuntime Create(WorldSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var runtime = new WorldRuntime(set);
        foreach (var region in set.Regions)
        {
            var state = runtime.Regions[region.Index];
            var climate = set.Zones[region.Zone].Seasons[0];
            state.Temp = climate.Temp;
            state.Wind = climate.Wind;
            state.SoilTenths = (long)set.Hydrology.InitialSoil * set.Hydrology.SoilCapacity / WorldMath.Permille;
            for (var d = 0; d < set.Diseases.Count; d++)
            {
                var immune = WorldMath.Permilled(region.Population, region.Immune[d]);
                state.Susceptible[d] = region.Population - immune;
                state.Recovered[d] = immune;
            }
        }
        return runtime;
    }

    public WorldSet Set { get; }

    /// <summary>The last step the world has lived; -1 before the first.</summary>
    public long Step { get; private set; } = -1;

    public IReadOnlyList<RegionState> Regions { get; }

    public IReadOnlyList<HazardInstance> ActiveHazards => _hazards;
    public IReadOnlyList<TimedModifier> TimedModifiers => _timed;
    public IReadOnlyList<AgendaItem> Agenda => _agenda;

    /// <summary>The season now (an index into the document's seasons), -1 before the first step.</summary>
    public int Season => _season;

    public int Regime => _regime;
    public int Storyteller => _storyteller;
    public int Difficulty => _difficulty;

    /// <summary>How tense the director believes the player is, 0 to 1,000.</summary>
    public long Stress => _stress;

    public DirectorPhase Phase => _phase;
    public long PhaseStart => _phaseStart;

    /// <summary>The step a card was last played by the director (or a script), -1 for never.</summary>
    public long LastCard => _lastCard;

    /// <summary>How many random draws the director, the chains and the scripted calls have used: the counter their streams run on.</summary>
    public long Draws => _draws;

    /// <summary>How many onsets and seedings the director held back because it was not building.</summary>
    public long Suppressed => _suppressed;

    /// <summary>How many starts were dropped because a list was full (active hazards, timed modifiers, the agenda).</summary>
    public long Dropped => _dropped;

    /// <summary>How many cards have been played.</summary>
    public long Played => _played;

    public long[] CardReadyAt { get; }
    public long[] GroupLockedUntil { get; }
    public long[] HazardReadyAt { get; }

    /// <summary>
    /// The excitation of every (hazard, region) pair in millionths of an onset per step, hazard-major (index = hazard x regions + region): what earlier onsets of the hazard in the region still add to the chance of the next
    /// (see <see cref="ExciteDef"/>). Always 0 for a hazard that does not excite itself.
    /// </summary>
    public IReadOnlyList<long> Excitation => _excitation;

    /// <summary>The excitation of one hazard in one region now, in millionths of an onset per step.</summary>
    public long ExcitationAt(int hazard, int region) => _excitation[hazard * Set.Regions.Count + region];

    internal long[] ExcitationArray => _excitation;

    internal HazardInstance? ActiveAt(int hazard, int region) => _hazardAt[hazard * Set.Regions.Count + region];

    internal void Place(HazardInstance instance)
    {
        _hazards.Add(instance);
        _hazardAt[instance.Hazard * Set.Regions.Count + instance.Region] = instance;
    }

    internal void Remove(HazardInstance instance)
    {
        _hazards.Remove(instance);
        _hazardAt[instance.Hazard * Set.Regions.Count + instance.Region] = null;
    }

    private long Roll(string stream, long index, long bound) => DeterministicRoll.Below(Set.Seed, stream, index, bound);

    private ulong RollBits(string stream, long index) => DeterministicRoll.Next(Set.Seed, stream, index);

    private long Draw(long bound) => Roll("dir", _draws++, bound);
}

using EngineCommon;
using ScenarioEngine.Contracts;

namespace DiplomacyEngine;

/// <summary>
/// The diplomacy of a game world: realms, what they think of each other, the treaties and wars between them, the balance of power, espionage and each realm's internal politics. It is a pure function of the document, the
/// steps it was given (each with its season and the host facts) and the calls it answered, and of nothing else: no clock, no wall time, no floating point, no random generator with a state. Every random number is the
/// counter-based <see cref="DeterministicRoll"/> of the seed, a stream named by the ids involved and a step or an id, so a replay from a save reproduces everything, and a save is a complete copy of the state.
/// </summary>
public sealed partial class DipRuntime
{
    /// <summary>The most steps one call to <see cref="Advance"/> goes (a skip of ten years of days is refused; the host skips in smaller parts).</summary>
    public const int MaxAdvance = 4000;

    /// <summary>A treaty with no end counts this many days when a proposal's terms are weighed.</summary>
    public const int PermanentDays = 3650;

    private readonly DipState _s;
    private readonly int _n;
    private readonly List<DeedState>?[] _deedsAt;
    private IReadOnlyDictionary<string, FactValue> _host = new Dictionary<string, FactValue>();

    private DipRuntime(DipSet set, DipState state)
    {
        Set = set;
        _s = state;
        _n = set.Realms.Count;
        _deedsAt = new List<DeedState>?[_n * _n];
        foreach (var deed in state.Deeds) IndexDeed(deed);
    }

    public static DipRuntime Create(DipSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var n = set.Realms.Count;
        var state = new DipState { Format = DipSave.Format, Document = set.Digest, Id = set.Id, Step = -1, Season = -1 };
        foreach (var realm in set.Realms)
        {
            var item = new RealmState
            {
                Might = realm.Might, Stability = realm.Stability, Legitimacy = realm.Legitimacy, Trust = realm.Trust * 1000L, Fear = realm.Fear * 1000L, Honour = realm.Honour * 1000L, Infamy = realm.Infamy * 1000L,

            };
            item.Band = (int)BandFor(set, realm.Stability, StabilityBand.Stable);
            foreach (var faction in realm.Factions) item.Satisfaction.Add(faction.Satisfaction);
            state.Realms.Add(item);
        }
        for (var i = 0; i < n * n; i++)
        {
            state.Leader.Add(0);
            state.Intel.Add(0);
        }
        foreach (var agent in set.Agents) state.Agents.Add(new AgentRecord { Id = agent.Id, Owner = agent.Owner, Skill = agent.Skill, State = (int)AgentState.Idle, Mission = -1, FreeAt = 0 });
        return new DipRuntime(set, state);
    }

    public DipSet Set { get; }

    /// <summary>The last step lived; -1 before the first.</summary>
    public long Step => _s.Step;

    /// <summary>The season now (an index into the document's seasons), -1 before the first step.</summary>
    public int Season => _s.Season;

    internal DipState State => _s;

    public IReadOnlyList<RealmState> Realms => _s.Realms;
    public IReadOnlyList<TreatyState> Treaties => _s.Treaties;
    public IReadOnlyList<ProposalState> Proposals => _s.Proposals;
    public IReadOnlyList<WarState> Wars => _s.Wars;
    public IReadOnlyList<CallState> Calls => _s.Calls;
    public IReadOnlyList<GrantState> Grants => _s.Grants;
    public IReadOnlyList<CoalitionState> Coalitions => _s.Coalitions;
    public IReadOnlyList<AgentRecord> Agents => _s.Agents;
    public IReadOnlyList<MissionRecord> Missions => _s.Missions;

    /// <summary>The diplomatic-incident log, oldest first, at most <c>limits.maxIncidents</c> long.</summary>
    public IReadOnlyList<IncidentRecord> Incidents => _s.Incidents;

    /// <summary>The digest of the state; equal states have equal digests.</summary>
    public string StateDigest() => DipSave.StateDigest(this);

    // ---- lookups ---------------------------------------------------------------------------------------------------------------------

    internal RealmDef FindRealm(string id)
    {
        if (!Set.RealmById.TryGetValue(id, out var realm)) throw new EngineRefusedException(DipCodes.UnknownRealm, "there is no realm '" + id + "'");
        return realm;
    }

    internal TreatyTypeDef FindType(string id)
    {
        if (!Set.TypeById.TryGetValue(id, out var type)) throw new EngineRefusedException(DipCodes.UnknownType, "there is no treaty type '" + id + "'");
        return type;
    }

    internal TreatyState TreatyById(long id)
    {
        var treaty = _s.Treaties.FirstOrDefault(t => t.Id == id);
        if (treaty is null) throw new EngineRefusedException(DipCodes.UnknownTreaty, "there is no treaty " + id);
        return treaty;
    }

    internal WarState WarById(long id)
    {
        var war = _s.Wars.FirstOrDefault(w => w.Id == id);
        if (war is null) throw new EngineRefusedException(DipCodes.UnknownWar, "there is no war " + id);
        return war;
    }

    private static bool InSide(ulong side, int realm) => (side & (1UL << realm)) != 0;

    private static ulong Bit(int realm) => 1UL << realm;

    private int Pair(int holder, int subject) => holder * _n + subject;

    private bool Between(TreatyState treaty, int a, int b) => (treaty.First == a && treaty.Second == b) || (treaty.First == b && treaty.Second == a);

    private IEnumerable<TreatyState> TreatiesBetween(int a, int b) => _s.Treaties.Where(t => Between(t, a, b));

    /// <summary>The war the two realms fight on opposite sides of (a ceasefire is still a war), or null.</summary>
    internal WarState? WarBetween(int a, int b) =>
        _s.Wars.FirstOrDefault(w => (InSide(w.AttackerSide, a) && InSide(w.DefenderSide, b)) || (InSide(w.AttackerSide, b) && InSide(w.DefenderSide, a)));

    public bool AtWar(int a, int b) => WarBetween(a, b) is not null;

    internal int WarsOf(int realm) => _s.Wars.Count(w => InSide(w.AttackerSide, realm) || InSide(w.DefenderSide, realm));

    internal int TreatyCountOf(int realm) => _s.Treaties.Count(t => t.First == realm || t.Second == realm);

    internal long Might(int realm) => _s.Realms[realm].Might;

    private long TotalMight() => _s.Realms.Sum(r => r.Might);

    // ---- standing and deeds ----------------------------------------------------------------------------------------------------------

    private void AddTrust(int realm, long deltaPermille) => _s.Realms[realm].Trust = DipMath.Clamp(_s.Realms[realm].Trust + deltaPermille * 1000, -DipMath.Million, DipMath.Million);

    private void AddFear(int realm, long deltaPermille) => _s.Realms[realm].Fear = DipMath.Clamp(_s.Realms[realm].Fear + deltaPermille * 1000, -DipMath.Million, DipMath.Million);

    private void AddHonour(int realm, long deltaPermille) => _s.Realms[realm].Honour = DipMath.Clamp(_s.Realms[realm].Honour + deltaPermille * 1000, -DipMath.Million, DipMath.Million);

    private void AddInfamy(int realm, long deltaPermille) => _s.Realms[realm].Infamy = DipMath.Clamp(_s.Realms[realm].Infamy + deltaPermille * 1000, 0, DipMath.Million);

    private void IndexDeed(DeedState deed)
    {
        var at = Pair(deed.Holder, deed.Subject);
        (_deedsAt[at] ??= new List<DeedState>()).Add(deed);
    }

    /// <summary>Holder remembers something the subject did, at a strength (per mille of the deed's value). A pair remembers at most <c>limits.maxDeedsPerPair</c>: past that the oldest is forgotten first.</summary>
    private void AddDeed(int holder, int subject, int deed, int scale)
    {
        if (deed < 0 || holder == subject) return;
        var record = new DeedState { Holder = holder, Subject = subject, Deed = deed, Start = _s.Step, Scale = scale };
        var list = _deedsAt[Pair(holder, subject)] ??= new List<DeedState>();
        if (list.Count >= Set.Limits.MaxDeedsPerPair)
        {
            _s.Deeds.Remove(list[0]);
            list.RemoveAt(0);
        }
        list.Add(record);
        _s.Deeds.Add(record);
    }

    private long Roll(string stream, long index, long bound) => DeterministicRoll.Below(Set.Seed, stream, index, bound);

    /// <summary>Adds a line to the log of incidents, forgetting the oldest past the limit, and says it as a change.</summary>
    private void Log(IncidentKind kind, int a, int b, int index, long amount, DipChanges changes)
    {
        _s.Incidents.Add(new IncidentRecord { Step = _s.Step, Kind = (int)kind, A = a, B = b, Index = index, Amount = amount });
        if (_s.Incidents.Count > Set.Limits.MaxIncidents) _s.Incidents.RemoveAt(0);
        changes.Add(ChangeKind.Incident, _s.Step, a, b, index, (int)kind, 0, amount);
    }

    internal static StabilityBand BandFor(DipSet set, int stability, StabilityBand from)
    {
        var rules = set.Politics;
        switch (from)
        {
            case StabilityBand.Stable: return stability < rules.RevoltLine ? StabilityBand.Revolt : stability < rules.UnrestLine ? StabilityBand.Unrest : StabilityBand.Stable;
            case StabilityBand.Unrest: return stability < rules.RevoltLine ? StabilityBand.Revolt : stability >= rules.UnrestLine + rules.Hysteresis ? StabilityBand.Stable : StabilityBand.Unrest;
            case StabilityBand.Revolt: return stability >= rules.RevoltLine + rules.Hysteresis ? (stability >= rules.UnrestLine + rules.Hysteresis ? StabilityBand.Stable : StabilityBand.Unrest) : StabilityBand.Revolt;
        }
        throw new InvalidOperationException("unknown stability band " + from);
    }
}

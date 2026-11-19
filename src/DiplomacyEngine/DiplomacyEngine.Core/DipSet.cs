using ScenarioEngine.Contracts;

namespace DiplomacyEngine;

/// <summary>A loaded diplomacy document: every declaration plus the digest of the exact text it was read from. It is immutable and holds no world; the runtime holds that.</summary>
public sealed class DipSet
{
    internal DipSet(DipParts p)
    {
        Id = p.Id; Digest = p.Digest; Seed = p.Seed; StepsPerDay = p.StepsPerDay; Limits = p.Limits; Seasons = p.Seasons; Ideologies = p.Ideologies; Traits = p.Traits; Deeds = p.Deeds; Realms = p.Realms;
        Types = p.Types; CasusBelli = p.CasusBelli; Missions = p.Missions; Agents = p.Agents; Regard = p.Regard; Standing = p.Standing; War = p.War; Acceptance = p.Acceptance; Balance = p.Balance;
        Politics = p.Politics; Espionage = p.Espionage; HostFacts = p.HostFacts; PairFacts = p.PairFacts;
        SeasonById = Seasons.ToDictionary(s => s.Id, StringComparer.Ordinal);
        RealmById = Realms.ToDictionary(r => r.Id, StringComparer.Ordinal);
        TypeById = Types.ToDictionary(t => t.Id, StringComparer.Ordinal);
        CbById = CasusBelli.ToDictionary(c => c.Id, StringComparer.Ordinal);
        DeedById = Deeds.ToDictionary(d => d.Id, StringComparer.Ordinal);
        MissionById = Missions.ToDictionary(m => m.Id, StringComparer.Ordinal);
        TraitById = Traits.ToDictionary(t => t.Id, StringComparer.Ordinal);
        var effects = Types.SelectMany(t => t.OnSign.Concat(t.OnEnd).Concat(t.OnBreach)).ToList();
        var rules = new[] { Politics.UnrestSignal, Politics.RevoltSignal, Politics.CalmSignal, Politics.SuccessionSignal, Politics.ContestedSignal }.Where(s => s.Length > 0);
        Signals = effects.Where(e => e.Kind == EffectKind.Signal).Select(e => e.Name).Concat(rules).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Shocks = effects.Where(e => e.Kind == EffectKind.Shock).Select(e => e.Name).Concat(Missions.Where(m => m.Shock.Length > 0).Select(m => m.Shock)).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    public string Id { get; }

    /// <summary>The SHA-256 of the exact text of the document.</summary>
    public string Digest { get; }

    public ulong Seed { get; }

    /// <summary>How many steps make a day: durations in a document are in days, the engine lives in steps.</summary>
    public int StepsPerDay { get; }

    public LimitsDef Limits { get; }
    public IReadOnlyList<SeasonDef> Seasons { get; }
    public IReadOnlyList<IdeologyDef> Ideologies { get; }
    public IReadOnlyList<TraitDef> Traits { get; }
    public IReadOnlyList<DeedDef> Deeds { get; }
    public IReadOnlyList<RealmDef> Realms { get; }
    public IReadOnlyList<TreatyTypeDef> Types { get; }
    public IReadOnlyList<CasusBelliDef> CasusBelli { get; }
    public IReadOnlyList<MissionDef> Missions { get; }
    public IReadOnlyList<AgentDef> Agents { get; }
    public RegardRules Regard { get; }
    public StandingRules Standing { get; }
    public WarRules War { get; }
    public AcceptanceRules Acceptance { get; }
    public BalanceRules Balance { get; }
    public PoliticsRules Politics { get; }
    public EspionageRules Espionage { get; }
    public IReadOnlyDictionary<string, FactDefinition> HostFacts { get; }
    public IReadOnlyDictionary<string, FactDefinition> PairFacts { get; }
    public IReadOnlyDictionary<string, SeasonDef> SeasonById { get; }
    public IReadOnlyDictionary<string, RealmDef> RealmById { get; }
    public IReadOnlyDictionary<string, TreatyTypeDef> TypeById { get; }
    public IReadOnlyDictionary<string, CasusBelliDef> CbById { get; }
    public IReadOnlyDictionary<string, DeedDef> DeedById { get; }
    public IReadOnlyDictionary<string, MissionDef> MissionById { get; }
    public IReadOnlyDictionary<string, TraitDef> TraitById { get; }

    /// <summary>Every signal name any treaty or rule issues, once, in ordinal order: what the story layers can listen for.</summary>
    public IReadOnlyList<string> Signals { get; }

    /// <summary>Every economy shock id any treaty or mission asks for, once, in ordinal order: what the economy must have.</summary>
    public IReadOnlyList<string> Shocks { get; }

    /// <summary>The steps of a number of days.</summary>
    public long Steps(int days) => (long)days * StepsPerDay;

    /// <summary>The season at a step of the document's own cycle: its seasons in order, each as long as one season's share of a year of 360 days. A game with a calendar takes its seasons from the calendar instead.</summary>
    public string CycleSeasonAt(long step)
    {
        var length = Math.Max(1, 360L * StepsPerDay / Seasons.Count);
        return Seasons[(int)(step / length % Seasons.Count)].Id;
    }
}

internal sealed class DipParts
{
    public string Id = "", Digest = "";
    public ulong Seed;
    public int StepsPerDay;
    public LimitsDef Limits = new(1, 1, 1, 1, 1, 1, 1, 1, 1);
    public IReadOnlyList<SeasonDef> Seasons = [];
    public IReadOnlyList<IdeologyDef> Ideologies = [];
    public IReadOnlyList<TraitDef> Traits = [];
    public IReadOnlyList<DeedDef> Deeds = [];
    public IReadOnlyList<RealmDef> Realms = [];
    public IReadOnlyList<TreatyTypeDef> Types = [];
    public IReadOnlyList<CasusBelliDef> CasusBelli = [];
    public IReadOnlyList<MissionDef> Missions = [];
    public IReadOnlyList<AgentDef> Agents = [];
    public RegardRules Regard = new(0, 0, 0, 0, 0);
    public StandingRules Standing = new(0, 0, 0, 0, -1, 0, 0, 0, 0);
    public WarRules War = new(false, 0, 0, -1, 0, 0, 0, 0, 0, 0, 0, -1);
    public AcceptanceRules Acceptance = new(0, 0, 1);
    public BalanceRules Balance = new(1, 0, 0, 0, 1, 0, -1, 0);
    public PoliticsRules Politics = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1, 0, "", "", "", "", "");
    public EspionageRules Espionage = new(0, 0, 0, 0, 0, 0, [], [], 1);
    public IReadOnlyDictionary<string, FactDefinition> HostFacts = new Dictionary<string, FactDefinition>();
    public IReadOnlyDictionary<string, FactDefinition> PairFacts = new Dictionary<string, FactDefinition>();
}

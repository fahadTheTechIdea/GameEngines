using EngineCommon;
using ScenarioEngine.Contracts;

namespace DiplomacyEngine;

public sealed partial class DipRuntime
{
    private long DeedSum(int holder, int subject)
    {
        var list = _deedsAt[Pair(holder, subject)];
        if (list is null) return 0;
        long sum = 0;
        foreach (var deed in list)
        {
            var def = Set.Deeds[deed.Deed];
            var length = Set.Steps(def.Duration);
            var remaining = length - Math.Max(0, _s.Step - deed.Start);
            if (remaining <= 0) continue;
            sum += (long)def.Value * deed.Scale / 1000 * remaining / length;
        }
        return sum;
    }

    /// <summary>
    /// What <paramref name="holder"/> thinks of <paramref name="subject"/>, minus 1,000 to 1,000: how much their ideologies like each other, the deeds the holder remembers (fading in a straight line to nothing over their days),
    /// what the treaties between them add while they last, the holder's leader's own regard (from the relationship layer), the subject's trust and honour in the world, and a penalty while the holder balances against the subject
    /// in a coalition. Computed on asking from the state; nothing about it is stored but the deeds and the leader's regard.
    /// </summary>
    public int RegardOf(int holder, int subject) => DipMath.ClampInt(RegardTerms(holder, subject).Sum(), -1000, 1000);

    /// <summary>The parts <see cref="RegardOf(int, int)"/> adds up, so a tool can say where a regard came from.</summary>
    internal RegardParts RegardTerms(int holder, int subject)
    {
        var h = Set.Realms[holder];
        var s = Set.Realms[subject];
        var rules = Set.Regard;
        var standing = _s.Realms[subject];
        return new RegardParts((long)Set.Ideologies[h.Ideology].Affinity[s.Ideology] * rules.Ideology / 1000, DeedSum(holder, subject), TreatiesBetween(holder, subject).Sum(t => (long)Set.Types[t.Type].Regard),
            (long)_s.Leader[Pair(holder, subject)] * rules.Leader / 1000, standing.Trust / 1000 * rules.Trust / 1000 + standing.Honour / 1000 * rules.Honour / 1000, InCoalitionAgainst(holder, subject) ? -rules.Coalition : 0);
    }

    /// <summary>The regard <paramref name="holder"/> has by name, for tools and tests.</summary>
    public int RegardOf(string holder, string subject)
    {
        var a = FindRealm(holder);
        var b = FindRealm(subject);
        return RegardOf(a.Index, b.Index);
    }

    internal bool InCoalitionAgainst(int member, int target)
    {
        foreach (var coalition in _s.Coalitions)
            if (coalition.Target == target) return InSide(coalition.Members, member);
        return false;
    }

    internal long AllyMight(int realm)
    {
        var total = _s.Realms[realm].Might;
        foreach (var treaty in _s.Treaties)
        {
            var defend = Set.Types[treaty.Type].Clause(ClauseKind.Defend);
            if (defend is null) continue;
            if (treaty.Second == realm) total += _s.Realms[treaty.First].Might / 2;
            else if (treaty.First == realm && defend.Mutual) total += _s.Realms[treaty.Second].Might / 2;
        }
        return total;
    }

    internal int ThreatOf(int realm)
    {
        var total = TotalMight();
        if (total == 0) return 0;
        var share = AllyMight(realm) * 1000 / total;
        return DipMath.ClampInt(share * (1000 + _s.Realms[realm].Infamy / 1000) / 1000, 0, 2000);
    }

    // ---- facts ------------------------------------------------------------------------------------------------------------------------

    private void AddRealmFacts(Dictionary<string, FactValue> facts, string prefix, int index)
    {
        var realm = _s.Realms[index];
        facts[prefix + "stability"] = FactValue.From((long)realm.Stability);
        facts[prefix + "legitimacy"] = FactValue.From((long)realm.Legitimacy);
        facts[prefix + "trust"] = FactValue.From(realm.Trust / 1000);
        facts[prefix + "fear"] = FactValue.From(realm.Fear / 1000);
        facts[prefix + "honour"] = FactValue.From(realm.Honour / 1000);
        facts[prefix + "infamy"] = FactValue.From(realm.Infamy / 1000);
        facts[prefix + "might"] = FactValue.From(DipMath.Clamp(realm.Might, 0, DipFacts.Saturate));
        facts[prefix + "wars"] = FactValue.From((long)WarsOf(index));
        facts[prefix + "treaties"] = FactValue.From((long)TreatyCountOf(index));
    }

    private int RatioOf(int first, int second)
    {
        var a = _s.Realms[first].Might;
        var b = _s.Realms[second].Might;
        return a + b == 0 ? 500 : (int)(a * 1000 / (a + b));
    }

    private bool HoldsCb(int holder, int target, int cb)
    {
        var def = Set.CasusBelli[cb];
        if (def.Source == CbSource.Granted) return _s.Grants.Any(g => g.Holder == holder && g.Target == target && g.Cb == cb && g.Until > _s.Step);
        return Holds(def.Requires!, PairFacts(holder, target));
    }

    /// <summary>The facts a condition reads about the pair, the first being <paramref name="first"/>. <c>cb.&lt;id&gt;</c> is true for a granted casus belli the first holds against the second (a standing one is its own condition and cannot be read by another).</summary>
    internal Dictionary<string, FactValue> PairFacts(int first, int second)
    {
        var facts = new Dictionary<string, FactValue>(_host.Count + 64, StringComparer.Ordinal);
        foreach (var (id, value) in _host) facts[id] = value;
        facts["step"] = FactValue.From(DipMath.Clamp(_s.Step, 0, DipFacts.Saturate));
        facts["season"] = FactValue.From(_s.Season < 0 ? "" : Set.Seasons[_s.Season].Id);
        facts["regard"] = FactValue.From((long)RegardOf(first, second));
        facts["reverse"] = FactValue.From((long)RegardOf(second, first));
        facts["ratio"] = FactValue.From((long)RatioOf(first, second));
        facts["at_war"] = FactValue.From(AtWar(first, second));
        facts["treaties"] = FactValue.From((long)TreatiesBetween(first, second).Count());
        facts["coalition"] = FactValue.From(InCoalitionAgainst(first, second));
        facts["intel"] = FactValue.From((long)LevelOf(_s.Intel[Pair(first, second)]));
        AddRealmFacts(facts, "first.", first);
        AddRealmFacts(facts, "second.", second);
        foreach (var type in Set.Types) facts["treaty." + type.Id] = FactValue.From(TreatiesBetween(first, second).Any(t => t.Type == type.Index));
        foreach (var cb in Set.CasusBelli.Where(c => c.Source == CbSource.Granted)) facts["cb." + cb.Id] = FactValue.From(HoldsCb(first, second, cb.Index));
        return facts;
    }

    /// <summary>Evaluates a condition, refusing (instead of failing inside the evaluator) when it reads a host fact the host has not given.</summary>
    private bool Holds(ScenarioEngine.Contracts.Condition condition, Dictionary<string, FactValue> facts)
    {
        foreach (var id in Conditions.FactsOf(condition))
            if (!facts.ContainsKey(id)) throw new EngineRefusedException(DipCodes.FactMissing, "a condition reads the host fact '" + id + "' and the host has not given it");
        return Conditions.Evaluate(condition, facts);
    }

    /// <summary>
    /// The world as facts for the other layers, under <paramref name="prefix"/>: the realm's (<c>diplomacy.aurelia.stability</c>) and, for the other realm named, the pair's (<c>diplomacy.aurelia.verdant.regard</c>); an empty
    /// <paramref name="other"/> gives the realm's only. An unknown realm is refused.
    /// </summary>
    public IReadOnlyDictionary<string, FactValue> Facts(string prefix, string realm, string other = "")
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(other);
        var a = FindRealm(realm).Index;
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        var at = prefix + "diplomacy." + realm + ".";
        AddRealmFacts(facts, at, a);
        facts[at + "band"] = FactValue.From(EnumNames.Name((StabilityBand)_s.Realms[a].Band));
        if (other.Length == 0) return facts;
        var b = FindRealm(other).Index;
        if (a == b) throw new EngineRefusedException(DipCodes.SameRealm, "a realm has no pair facts with itself");
        at += other + ".";
        facts[at + "regard"] = FactValue.From((long)RegardOf(a, b));
        facts[at + "reverse"] = FactValue.From((long)RegardOf(b, a));
        facts[at + "ratio"] = FactValue.From((long)RatioOf(a, b));
        facts[at + "at_war"] = FactValue.From(AtWar(a, b));
        facts[at + "treaties"] = FactValue.From((long)TreatiesBetween(a, b).Count());
        facts[at + "coalition"] = FactValue.From(InCoalitionAgainst(a, b));
        facts[at + "intel"] = FactValue.From((long)LevelOf(_s.Intel[Pair(a, b)]));
        foreach (var type in Set.Types) facts[at + "treaty." + type.Id] = FactValue.From(TreatiesBetween(a, b).Any(t => t.Type == type.Index));
        return facts;
    }

    /// <summary>The facts a condition reads about a pair, with the host facts given to the last <see cref="Advance"/> (the explain tools give theirs).</summary>
    internal IReadOnlyDictionary<string, FactValue> PairFactsFor(int first, int second, IReadOnlyDictionary<string, FactValue> host)
    {
        _host = host;
        return PairFacts(first, second);
    }

    // ---- acceptance -------------------------------------------------------------------------------------------------------------------

    /// <summary>Measures everything an acceptance reads for <paramref name="to"/> weighing an offer from <paramref name="from"/>. <paramref name="amount"/> is what <paramref name="to"/> pays each period (negative: receives).</summary>
    internal AcceptanceInputs Measure(TreatyTypeDef type, int from, int to, long termSteps, long amount)
    {
        var raw = new long[Enum.GetValues<FactorSource>().Length];
        var sender = _s.Realms[from];
        var target = _s.Realms[to];
        raw[(int)FactorSource.Regard] = RegardOf(to, from);
        raw[(int)FactorSource.Trust] = sender.Trust / 1000;
        raw[(int)FactorSource.Fear] = sender.Fear / 1000;
        raw[(int)FactorSource.Honour] = sender.Honour / 1000;
        raw[(int)FactorSource.PowerRatio] = RatioOf(from, to) * 2 - 1000;
        raw[(int)FactorSource.SharedEnemy] = Math.Min(3, SharedEnemies(from, to)) * 333;
        raw[(int)FactorSource.Ideology] = Set.Ideologies[Set.Realms[to].Ideology].Affinity[Set.Realms[from].Ideology];
        raw[(int)FactorSource.Ties] = Math.Min(4, TreatiesBetween(from, to).Count()) * 250;
        raw[(int)FactorSource.Weariness] = _s.Wars.Where(w => InSide(w.AttackerSide, to) || InSide(w.DefenderSide, to)).Select(w => ExhaustionOf(w, to) / 1000).DefaultIfEmpty(0).Max();
        raw[(int)FactorSource.Threat] = DipMath.Clamp(ThreatOf(from), 0, 1000);
        raw[(int)FactorSource.Stability] = DipMath.Clamp((500 - target.Stability) * 2, -1000, 1000);
        raw[(int)FactorSource.Coalition] = InCoalitionAgainst(to, from) ? -1000 : SharedCoalition(from, to) ? 500 : 0;
        var days = termSteps == 0 ? PermanentDays : Math.Max(1, termSteps / Set.StepsPerDay);
        var burden = Math.Min(1000, Math.Min(Math.Abs(amount), Set.Acceptance.TermUnit) * days * 1000 / Set.Acceptance.TermUnit);
        raw[(int)FactorSource.Terms] = amount > 0 ? -burden : burden;
        var war = WarBetween(from, to);
        if (war is not null)
        {
            var targetIsAttacker = InSide(war.AttackerSide, to);
            raw[(int)FactorSource.WarScore] = targetIsAttacker ? war.Score : -war.Score;
            raw[(int)FactorSource.Exhaustion] = ExhaustionOf(war, to) / 1000;
        }
        var traitBias = Set.Realms[to].Traits.Sum(t => Set.Traits[t].Acceptance);
        var facts = PairFacts(from, to);
        var modifiers = new List<(string, int)>();
        foreach (var modifier in type.Modifiers)
            if (Holds(modifier.When, facts)) modifiers.Add((modifier.Id, modifier.Value));
        return new AcceptanceInputs(raw, traitBias, modifiers);
    }

    private int SharedEnemies(int a, int b)
    {
        var count = 0;
        for (var e = 0; e < _n; e++)
        {
            if (e == a || e == b) continue;
            var enemyOfA = AtWar(a, e) || InCoalitionAgainst(a, e);
            var enemyOfB = AtWar(b, e) || InCoalitionAgainst(b, e);
            if (enemyOfA && enemyOfB) count++;
        }
        return count;
    }

    private bool SharedCoalition(int a, int b) => _s.Coalitions.Any(c => InSide(c.Members, a) && InSide(c.Members, b));

    private long ExhaustionOf(WarState war, int realm) => InSide(war.AttackerSide, realm) ? war.AttackerExhaustion : war.DefenderExhaustion;

    /// <summary>
    /// What realm <paramref name="to"/> would make of a proposal from realm <paramref name="from"/>, without making it: the verdict with every line of how the score was made. <paramref name="termDays"/> is 0 for a treaty with no
    /// end; <paramref name="amount"/> is what <paramref name="to"/> pays each period (negative: it receives). It changes nothing.
    /// </summary>
    public Verdict Evaluate(string from, string to, string type, int termDays, long amount)
    {
        var a = FindRealm(from);
        var b = FindRealm(to);
        var def = FindType(type);
        if (a.Index == b.Index) throw new EngineRefusedException(DipCodes.SameRealm, "a realm makes no treaty with itself");
        CheckTerms(def, termDays, amount);
        return Acceptance.Evaluate(def, Set.Acceptance, Measure(def, a.Index, b.Index, Set.Steps(termDays), amount));
    }
}

/// <summary>What a regard is made of, each in the same units as the regard.</summary>
internal readonly record struct RegardParts(long Ideology, long Deeds, long Treaties, long Leader, long Standing, long Coalition)
{
    public long Sum() => Ideology + Deeds + Treaties + Leader + Standing + Coalition;
}

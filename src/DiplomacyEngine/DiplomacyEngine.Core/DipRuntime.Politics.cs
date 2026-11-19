using EngineCommon;

namespace DiplomacyEngine;

public sealed partial class DipRuntime
{
    /// <summary>Something happened in a realm that its factions care about: each faction that minds changes its contentment by its delta times the scale (per mille).</summary>
    private void Apply(int realm, PoliticalEvent political, int scale)
    {
        var state = _s.Realms[realm];
        foreach (var faction in Set.Realms[realm].Factions)
        {
            var like = faction.Likes.FirstOrDefault(l => l.Event == political);
            if (like is null) continue;
            state.Satisfaction[faction.Index] = DipMath.ClampInt(state.Satisfaction[faction.Index] + (long)like.Delta * scale / 1000, 0, 1000);
        }
    }

    private void Signal(string name, int realm, string source, DipChanges changes)
    {
        if (name.Length > 0) changes.Add(ChangeKind.Effect, _s.Step, realm, -1, (int)EffectKind.Signal, 0, 0, 0, -1, name, source);
    }

    /// <summary>
    /// A realm's order. Each faction's contentment drifts back to the middle; legitimacy drifts to its base; the target stability is the factions' contentment (weighted by their share) and the legitimacy (weighted), less the
    /// exhaustion of the realm's worst war (weighted); stability moves towards it with inertia. Crossing a line is announced once (with hysteresis) and, in revolt, the realm cannot make treaties or war.
    /// </summary>
    private void LivePolitics(long step, DipChanges changes)
    {
        var rules = Set.Politics;
        for (var i = 0; i < _n; i++)
        {
            var realm = _s.Realms[i];
            var def = Set.Realms[i];
            long total = 0, weighted = 0;
            foreach (var faction in def.Factions)
            {
                realm.Satisfaction[faction.Index] = (int)DipMath.Approach(realm.Satisfaction[faction.Index], 500, rules.DriftPpm);
                total += faction.Share;
                weighted += (long)faction.Share * realm.Satisfaction[faction.Index];
            }
            realm.Legitimacy = (int)DipMath.Approach(realm.Legitimacy, rules.LegitimacyBase, rules.LegitimacyDriftPpm);
            var exhaustion = _s.Wars.Where(w => InSide(w.AttackerSide, i) || InSide(w.DefenderSide, i)).Select(w => ExhaustionOf(w, i) / 1000).DefaultIfEmpty(0).Max();
            var target = DipMath.Clamp(weighted / total * (1000 - rules.LegitimacyWeight) / 1000 + (long)realm.Legitimacy * rules.LegitimacyWeight / 1000 - exhaustion * rules.ExhaustionWeight / 1000, 0, 1000);
            realm.Stability = (int)DipMath.Approach(realm.Stability, target, rules.InertiaPpm);
            var before = (StabilityBand)realm.Band;
            var after = BandFor(Set, realm.Stability, before);
            if (after == before) continue;
            realm.Band = (int)after;
            var change = after == StabilityBand.Stable ? PoliticsChange.Calm : after == StabilityBand.Unrest ? PoliticsChange.Unrest : PoliticsChange.Revolt;
            changes.Add(ChangeKind.Politics, step, i, -1, 0, 0, (int)change, realm.Stability);
            Signal(after == StabilityBand.Stable ? rules.CalmSignal : after == StabilityBand.Unrest ? rules.UnrestSignal : rules.RevoltSignal, i, "politics", changes);
            if (after == StabilityBand.Revolt) Log(IncidentKind.Revolt, i, -1, 0, realm.Stability, changes);
        }
    }

    private void LiveSuccession(long step, DipChanges changes)
    {
        for (var i = 0; i < _n; i++)
        {
            var chance = (long)Set.Realms[i].SuccessionPpm * (2000 - _s.Realms[i].Legitimacy) / 1000;
            if (chance > 0 && Roll("succ:" + Set.Realms[i].Id, step, DipMath.Million) < chance) Succeed(i, changes);
        }
    }

    /// <summary>
    /// The ruler changes. Legitimacy drops to the document's level (half of it when contested, which is rolled from the realm's own stream and likelier when legitimacy was low); a contested succession also shakes the
    /// realm and gives the realm's bond partners a claim against it. Treaties that end with the ruler end; the factions react.
    /// </summary>
    private void Succeed(int realm, DipChanges changes)
    {
        var rules = Set.Politics;
        var state = _s.Realms[realm];
        var contested = Roll("succ2:" + Set.Realms[realm].Id, state.Generation, 1000) < rules.ContestedBase + (long)(1000 - state.Legitimacy) * rules.ContestedPerLack / 1000;
        state.Generation++;
        state.Legitimacy = contested ? rules.SuccessionLegitimacy / 2 : rules.SuccessionLegitimacy;
        if (contested) state.Stability = DipMath.ClampInt(state.Stability - rules.ContestedStability, 0, 1000);
        if (contested && rules.ClaimCb >= 0)
            foreach (var bond in _s.Treaties.Where(t => (t.First == realm || t.Second == realm) && Set.Types[t.Type].Has(ClauseKind.Bond)).ToList())
                Grant(bond.First == realm ? bond.Second : bond.First, realm, rules.ClaimCb, Set.Steps(rules.ClaimDays), changes);
        foreach (var treaty in _s.Treaties.Where(t => (t.First == realm || t.Second == realm) && Set.Types[t.Type].EndsOnSuccession).ToList()) EndTreaty(treaty, TreatyChange.Ended, changes);
        Apply(realm, PoliticalEvent.Succession, contested ? 1000 : 500);
        changes.Add(ChangeKind.Politics, _s.Step, realm, -1, 0, 0, (int)(contested ? PoliticsChange.ContestedSuccession : PoliticsChange.Succession), state.Stability);
        Signal(contested ? rules.ContestedSignal : rules.SuccessionSignal, realm, "politics", changes);
        Log(IncidentKind.Succession, realm, -1, contested ? 1 : 0, state.Generation, changes);
    }

    // ---- what the host and the other layers tell the diplomacy ------------------------------------------------------------------------

    /// <summary>The ruler of a realm changes now, by the host's script (a death in a story): the same succession as the one the engine rolls.</summary>
    public DipChanges Succession(string realm)
    {
        var index = FindRealm(realm).Index;
        var changes = new DipChanges();
        Succeed(index, changes);
        return changes;
    }

    /// <summary>The realm's might changes (an army raised or lost, an economy grown): the one number power and threat are made of.</summary>
    public DipChanges SetMight(string realm, long might)
    {
        var index = FindRealm(realm).Index;
        if (might < 0 || might > DipFacts.Saturate) throw new EngineRefusedException(DipCodes.Terms, "might is 0 to " + DipFacts.Saturate + ", not " + might);
        _s.Realms[index].Might = might;
        return new DipChanges();
    }

    /// <summary>The regard one realm's leader has of the other's, from the relationship layer (minus 1,000 to 1,000), which <see cref="RegardOf(int, int)"/> weighs in.</summary>
    public DipChanges SetLeaderRegard(string holder, string subject, int value)
    {
        var a = FindRealm(holder).Index;
        var b = FindRealm(subject).Index;
        if (a == b) throw new EngineRefusedException(DipCodes.SameRealm, "a leader's regard is of another realm");
        if (value < -1000 || value > 1000) throw new EngineRefusedException(DipCodes.Terms, "a regard is -1000 to 1000, not " + value);
        _s.Leader[Pair(a, b)] = value;
        return new DipChanges();
    }

    /// <summary>The host reports a deed: <paramref name="doer"/> did it to <paramref name="receiver"/>, who remembers it at <paramref name="scale"/> per mille of its value (an insult, a gift, a border crossing).</summary>
    public DipChanges Remember(string doer, string receiver, string deed, int scale)
    {
        var a = FindRealm(doer).Index;
        var b = FindRealm(receiver).Index;
        if (!Set.DeedById.TryGetValue(deed, out var def)) throw new EngineRefusedException(DipCodes.UnknownDeed, "there is no deed '" + deed + "'");
        if (a == b) throw new EngineRefusedException(DipCodes.SameRealm, "a realm does not do a deed to itself");
        if (scale < 1 || scale > 1000) throw new EngineRefusedException(DipCodes.Terms, "a deed's scale is 1 to 1000 per mille, not " + scale);
        AddDeed(b, a, def.Index, scale);
        return new DipChanges();
    }

    /// <summary>Something happened in a realm (a plague, a famine, a flood) that its factions care about, by name (<see cref="PoliticalEvent"/>), at a scale per mille.</summary>
    public DipChanges Happen(string realm, string political, int scale)
    {
        var index = FindRealm(realm).Index;
        var kind = EnumNames.Parse<PoliticalEvent>(political);
        if (kind is null) throw new EngineRefusedException(DipCodes.UnknownEvent, "there is no political event '" + political + "'; use " + EnumNames.Choices<PoliticalEvent>());
        if (scale < 1 || scale > 1000) throw new EngineRefusedException(DipCodes.Terms, "an event's scale is 1 to 1000 per mille, not " + scale);
        Apply(index, kind.Value, scale);
        return new DipChanges();
    }
}

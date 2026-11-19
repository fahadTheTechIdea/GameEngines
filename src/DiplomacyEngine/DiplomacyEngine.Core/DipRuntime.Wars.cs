using EngineCommon;

namespace DiplomacyEngine;

public sealed partial class DipRuntime
{
    /// <summary>
    /// <paramref name="attacker"/> declares war on <paramref name="target"/> with a casus belli it holds (empty for none, when the document allows a war without one). A war without justification, or on a casus belli that costs, adds
    /// infamy and loses honour. Declaring war on a realm one has a treaty of peace-keeping with (non-aggression, a pact, vassalage) breaks it; any other treaty between them just ends. The target's defenders are called to arms.
    /// Declaring war on a realm one is in a ceasefire with resumes that war, which breaks the ceasefire.
    /// </summary>
    public DipChanges DeclareWar(string attacker, string target, string casusBelli)
    {
        ArgumentNullException.ThrowIfNull(casusBelli);
        var a = FindRealm(attacker).Index;
        var t = FindRealm(target).Index;
        if (a == t) throw new EngineRefusedException(DipCodes.SameRealm, "a realm does not declare war on itself");
        if (_s.Realms[a].Band == (int)StabilityBand.Revolt) throw new EngineRefusedException(DipCodes.Revolt, "'" + attacker + "' is in revolt and declares no wars");
        var changes = new DipChanges();
        var existing = WarBetween(a, t);
        if (existing is not null)
        {
            if (!existing.Ceasefire) throw new EngineRefusedException(DipCodes.AtWar, "they are already at war");
            if (existing.Attacker != a && existing.Defender != a) throw new EngineRefusedException(DipCodes.NotPrincipal, "only a realm the war began between resumes it");
            foreach (var truce in _s.Treaties.Where(x => x.War == existing.Id).ToList()) Breach(truce, a, changes);
            return changes;
        }
        if (_s.Wars.Any(w => (InSide(w.AttackerSide, a) && InSide(w.AttackerSide, t)) || (InSide(w.DefenderSide, a) && InSide(w.DefenderSide, t))))
            throw new EngineRefusedException(DipCodes.Blocked, "they fight on the same side of a war");
        var cb = -1;
        if (casusBelli.Length > 0)
        {
            if (!Set.CbById.TryGetValue(casusBelli, out var def)) throw new EngineRefusedException(DipCodes.UnknownCb, "there is no casus belli '" + casusBelli + "'");
            if (!HoldsCb(a, t, def.Index)) throw new EngineRefusedException(DipCodes.NoCasusBelli, "'" + attacker + "' does not hold '" + casusBelli + "' against '" + target + "'");
            cb = def.Index;
        }
        else if (!Set.War.AllowNoCb) throw new EngineRefusedException(DipCodes.NoCasusBelli, "a war needs a casus belli");
        if (_s.Wars.Count >= Set.Limits.MaxWars) throw new EngineRefusedException(DipCodes.Capacity, "the world holds " + Set.Limits.MaxWars + " wars");
        var callees = Callees(a, t);
        if (_s.Calls.Count + callees.Count > Set.Limits.MaxCalls) throw new EngineRefusedException(DipCodes.Capacity, "the world holds " + Set.Limits.MaxCalls + " calls to arms");

        foreach (var treaty in TreatiesBetween(a, t).ToList())
        {
            var type = Set.Types[treaty.Type];
            if (type.Has(ClauseKind.NonAggression) || type.Has(ClauseKind.Defend) || type.Has(ClauseKind.Vassal) || type.Has(ClauseKind.Truce)) Breach(treaty, a, changes);
            else EndTreaty(treaty, TreatyChange.Ended, changes);
        }
        if (cb < 0)
        {
            AddInfamy(a, Set.War.NoCbInfamy);
            AddHonour(a, -Set.War.NoCbHonour);
        }
        else
        {
            AddInfamy(a, Set.CasusBelli[cb].Infamy);
            AddHonour(a, -Set.CasusBelli[cb].Honour);
            var grant = _s.Grants.FirstOrDefault(g => g.Holder == a && g.Target == t && g.Cb == cb);
            if (grant is not null)
            {
                _s.Grants.Remove(grant);
                changes.Add(ChangeKind.Grant, _s.Step, a, t, cb, 0, (int)GrantChange.Used);
            }
        }
        var war = new WarState { Id = _s.NextWar++, Attacker = a, Defender = t, AttackerSide = Bit(a), DefenderSide = Bit(t), Cb = cb, Start = _s.Step };
        _s.Wars.Add(war);
        changes.Add(ChangeKind.War, _s.Step, a, t, (int)war.Id, cb, (int)WarChange.Declared, 0);
        AddDeed(t, a, Set.War.DeclaredDeed, 1000);
        Apply(a, PoliticalEvent.WarDeclared, 1000);
        Apply(t, PoliticalEvent.WarOnUs, 1000);
        Log(IncidentKind.WarDeclared, a, t, cb, war.Id, changes);
        foreach (var (treaty, callee, grace) in callees)
        {
            var call = new CallState { Id = _s.NextCall++, War = war.Id, Caller = t, Callee = callee, Treaty = treaty, Due = Math.Max(0, _s.Step) + Set.Steps(grace) };
            _s.Calls.Add(call);
            changes.Add(ChangeKind.Obligation, _s.Step, callee, t, (int)treaty, (int)ClauseKind.Defend, (int)ObligationChange.Called, call.Id);
            AddDeed(callee, a, Set.War.WarOnUsDeed, 1000);
            if (Set.Realms[callee].Control == Control.Ai && Roll("call:" + call.Id, 0, 1000) < CallChance(callee)) JoinInternal(war, callee, t, changes);
        }
        return changes;
    }

    /// <summary>How likely a realm the engine decides for is to answer a call to arms: the document's base, the realm's traits, and a quarter of its honour (per mille).</summary>
    internal int CallChance(int realm) =>
        DipMath.ClampInt(Set.War.CallBase + Set.Realms[realm].Traits.Sum(t => Set.Traits[t].Reliability) + _s.Realms[realm].Honour / 4000, 0, 1000);

    /// <summary>Who owes the target help against the attacker: the protector of each pact that covers the target, once each, in treaty order.</summary>
    private List<(long Treaty, int Callee, int Grace)> Callees(int attacker, int target)
    {
        var list = new List<(long, int, int)>();
        foreach (var treaty in _s.Treaties)
        {
            var clause = Set.Types[treaty.Type].Clause(ClauseKind.Defend);
            if (clause is null) continue;
            int callee;
            if (treaty.Second == target) callee = treaty.First;
            else if (clause.Mutual && treaty.First == target) callee = treaty.Second;
            else continue;
            if (callee == attacker || list.Any(x => x.Item2 == callee)) continue;
            list.Add((treaty.Id, callee, clause.Grace));
        }
        return list;
    }

    private void JoinInternal(WarState war, int realm, int support, DipChanges changes)
    {
        var defenders = support == war.Defender;
        if (defenders) war.DefenderSide |= Bit(realm);
        else war.AttackerSide |= Bit(realm);
        changes.Add(ChangeKind.War, _s.Step, war.Attacker, war.Defender, (int)war.Id, war.Cb, (int)WarChange.Joined, war.Score, realm);
        var call = _s.Calls.FirstOrDefault(c => c.War == war.Id && c.Callee == realm);
        if (call is null || !defenders) return;
        _s.Calls.Remove(call);
        AddTrust(realm, Set.Standing.HonouredTrust);
        AddHonour(realm, Set.Standing.HonouredHonour);
        changes.Add(ChangeKind.Obligation, _s.Step, realm, call.Caller, (int)call.Treaty, (int)ClauseKind.Defend, (int)ObligationChange.Honoured, call.Id);
    }

    /// <summary>A realm joins a war on the side of one of its two first realms. Answering a call to arms on the defender's side keeps the word.</summary>
    public DipChanges JoinWar(long warId, string realm, string support)
    {
        var war = WarById(warId);
        var r = FindRealm(realm).Index;
        var s = FindRealm(support).Index;
        if (s != war.Attacker && s != war.Defender) throw new EngineRefusedException(DipCodes.NotPrincipal, "a realm joins on the side of '" + Set.Realms[war.Attacker].Id + "' or '" + Set.Realms[war.Defender].Id + "', not '" + support + "'");
        if (InSide(war.AttackerSide, r) || InSide(war.DefenderSide, r)) throw new EngineRefusedException(DipCodes.Blocked, "'" + realm + "' is already in war " + warId);
        if (_s.Realms[r].Band == (int)StabilityBand.Revolt) throw new EngineRefusedException(DipCodes.Revolt, "'" + realm + "' is in revolt and joins no wars");
        var changes = new DipChanges();
        JoinInternal(war, r, s, changes);
        return changes;
    }

    /// <summary>A realm that joined leaves a war. The two realms it began between make peace instead.</summary>
    public DipChanges LeaveWar(long warId, string realm)
    {
        var war = WarById(warId);
        var r = FindRealm(realm).Index;
        if (r == war.Attacker || r == war.Defender) throw new EngineRefusedException(DipCodes.NotPrincipal, "'" + realm + "' began war " + warId + " and ends it with a peace");
        if (!InSide(war.AttackerSide, r) && !InSide(war.DefenderSide, r)) throw new EngineRefusedException(DipCodes.NotInWar, "'" + realm + "' is not in war " + warId);
        war.AttackerSide &= ~Bit(r);
        war.DefenderSide &= ~Bit(r);
        var changes = new DipChanges();
        changes.Add(ChangeKind.War, _s.Step, war.Attacker, war.Defender, (int)war.Id, war.Cb, (int)WarChange.Left, war.Score, r);
        return changes;
    }

    /// <summary>
    /// The host reports that a realm's side gained ground (<paramref name="amount"/>, 0 to 1,000, weighed by the document for the source: a battle, an occupation, a blockade, an event). A war that is paused takes no score.
    /// </summary>
    public DipChanges Score(long warId, string favoured, int amount, string source)
    {
        var war = WarById(warId);
        var realm = FindRealm(favoured).Index;
        var kind = EnumNames.Parse<ScoreSource>(source);
        if (kind is null) throw new EngineRefusedException(DipCodes.UnknownSource, "there is no score source '" + source + "'; use " + EnumNames.Choices<ScoreSource>());
        if (amount < 0 || amount > 1000) throw new EngineRefusedException(DipCodes.Terms, "a score is 0 to 1000, not " + amount);
        var attackers = InSide(war.AttackerSide, realm);
        if (!attackers && !InSide(war.DefenderSide, realm)) throw new EngineRefusedException(DipCodes.NotInWar, "'" + favoured + "' is not in war " + warId);
        if (war.Ceasefire) throw new EngineRefusedException(DipCodes.Blocked, "war " + warId + " is paused by a ceasefire");
        var weight = WeightOf(kind.Value);
        var delta = amount * weight / 1000;
        war.Score = DipMath.ClampInt(war.Score + (attackers ? delta : -delta), -1000, 1000);
        var changes = new DipChanges();
        changes.Add(ChangeKind.War, _s.Step, war.Attacker, war.Defender, (int)war.Id, war.Cb, (int)WarChange.Score, war.Score, realm);
        return changes;
    }

    private int WeightOf(ScoreSource source)
    {
        switch (source)
        {
            case ScoreSource.Battle: return Set.War.SourceBattle;
            case ScoreSource.Occupation: return Set.War.SourceOccupation;
            case ScoreSource.Blockade: return Set.War.SourceBlockade;
            case ScoreSource.Event: return Set.War.SourceEvent;
        }
        throw new InvalidOperationException("unknown score source " + source);
    }

    /// <summary>The war is over (a peace was signed): the winning principal is feared, both principals' factions react, the treaty that paused it ends, and the calls it made are void.</summary>
    private void EndWar(WarState war, DipChanges changes)
    {
        if (war.Score != 0)
        {
            var winner = war.Score > 0 ? war.Attacker : war.Defender;
            var loser = war.Score > 0 ? war.Defender : war.Attacker;
            AddFear(winner, Math.Abs(war.Score) * Set.War.FearPerScore / 1000);
            Apply(winner, PoliticalEvent.WarWon, 1000);
            Apply(loser, PoliticalEvent.WarLost, 1000);
        }
        foreach (var truce in _s.Treaties.Where(x => x.War == war.Id).ToList())
        {
            truce.War = -1;
            EndTreaty(truce, TreatyChange.Ended, changes);
        }
        _s.Wars.Remove(war);
        _s.Calls.RemoveAll(c => c.War == war.Id);
        changes.Add(ChangeKind.War, _s.Step, war.Attacker, war.Defender, (int)war.Id, war.Cb, (int)WarChange.Ended, war.Score);
        Log(IncidentKind.WarEnded, war.Attacker, war.Defender, war.Cb, war.Id, changes);
    }

    private void LiveWars(long step, DipChanges changes)
    {
        var cost = Set.Seasons[_s.Season].WarCost;
        foreach (var war in _s.Wars)
        {
            if (war.Ceasefire) continue;
            war.AttackerExhaustion = Tire(war.AttackerExhaustion, cost, Math.Max(0, -war.Score));
            war.DefenderExhaustion = Tire(war.DefenderExhaustion, cost, Math.Max(0, war.Score));
        }
    }

    private long Tire(long exhaustion, int seasonCost, int losing) =>
        DipMath.Clamp(exhaustion + (long)Set.War.ExhaustPpm * seasonCost / 1000 * (1000 + losing) / 1000, 0, DipMath.Million);

    private void LiveCalls(long step, DipChanges changes)
    {
        foreach (var call in _s.Calls.ToList())
        {
            if (step <= call.Due) continue;
            _s.Calls.Remove(call);
            var treaty = _s.Treaties.FirstOrDefault(t => t.Id == call.Treaty);
            if (treaty is null) continue;
            changes.Add(ChangeKind.Obligation, step, call.Callee, call.Caller, (int)treaty.Id, (int)ClauseKind.Defend, (int)ObligationChange.Ignored, call.Id);
            Log(IncidentKind.CallIgnored, call.Callee, call.Caller, treaty.Type, call.Id, changes);
            Breach(treaty, call.Callee, changes);
        }
    }

    // ---- casus belli ------------------------------------------------------------------------------------------------------------------

    private void Grant(int holder, int target, int cb, long steps, DipChanges changes)
    {
        var until = Math.Max(0, _s.Step) + steps;
        var existing = _s.Grants.FirstOrDefault(g => g.Holder == holder && g.Target == target && g.Cb == cb);
        if (existing is not null)
        {
            existing.Until = Math.Max(existing.Until, until);
            return;
        }
        if (_s.Grants.Count >= Set.Limits.MaxGrants)
        {
            var oldest = _s.Grants[0];
            _s.Grants.RemoveAt(0);
            changes.Add(ChangeKind.Grant, _s.Step, oldest.Holder, oldest.Target, oldest.Cb, 0, (int)GrantChange.Expired);
        }
        _s.Grants.Add(new GrantState { Holder = holder, Target = target, Cb = cb, Until = until });
        changes.Add(ChangeKind.Grant, _s.Step, holder, target, cb, 0, (int)GrantChange.Granted);
    }

    /// <summary>The host grants a realm a casus belli (one the document declares as granted) against another for a number of days: a border incident, an insult.</summary>
    public DipChanges GrantCasusBelli(string holder, string target, string casusBelli, int days)
    {
        var h = FindRealm(holder).Index;
        var t = FindRealm(target).Index;
        if (!Set.CbById.TryGetValue(casusBelli, out var def)) throw new EngineRefusedException(DipCodes.UnknownCb, "there is no casus belli '" + casusBelli + "'");
        if (h == t) throw new EngineRefusedException(DipCodes.SameRealm, "a realm has no casus belli against itself");
        if (def.Source != CbSource.Granted) throw new EngineRefusedException(DipCodes.UnknownCb, "'" + casusBelli + "' holds whenever its condition does and is never granted");
        if (days < 1 || days > DipReader.MaxDays) throw new EngineRefusedException(DipCodes.Terms, "a grant lasts 1 to " + DipReader.MaxDays + " days, not " + days);
        var changes = new DipChanges();
        Grant(h, t, def.Index, Set.Steps(days), changes);
        return changes;
    }

    /// <summary>Whether the realm holds the casus belli against the target now, by name.</summary>
    public bool Holds(string holder, string target, string casusBelli)
    {
        var h = FindRealm(holder).Index;
        var t = FindRealm(target).Index;
        if (!Set.CbById.TryGetValue(casusBelli, out var def)) throw new EngineRefusedException(DipCodes.UnknownCb, "there is no casus belli '" + casusBelli + "'");
        return h != t && HoldsCb(h, t, def.Index);
    }
}

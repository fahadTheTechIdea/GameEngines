using EngineCommon;

namespace DiplomacyEngine;

public sealed partial class DipRuntime
{
    /// <summary>The largest amount of tribute a treaty names.</summary>
    public const long MaxAmount = 1_000_000_000_000;

    internal void CheckTerms(TreatyTypeDef type, int termDays, long amount)
    {
        var tribute = type.Clause(ClauseKind.Tribute);
        if (tribute is null && amount != 0) throw new EngineRefusedException(DipCodes.Terms, "'" + type.Id + "' has no tribute, so its amount must be 0, not " + amount);
        if (tribute is not null && amount == 0 && !tribute.Optional) throw new EngineRefusedException(DipCodes.Terms, "'" + type.Id + "' is a tribute and needs an amount");
        if (amount < -MaxAmount || amount > MaxAmount) throw new EngineRefusedException(DipCodes.Terms, "an amount is at most " + MaxAmount + " either way, not " + amount);
        if (termDays == 0)
        {
            if (!type.Permanent) throw new EngineRefusedException(DipCodes.Terms, "'" + type.Id + "' has a term of " + type.MinTerm + " to " + type.MaxTerm + " days, not none");
        }
        else if (termDays < type.MinTerm || termDays > type.MaxTerm || termDays < 0)
            throw new EngineRefusedException(DipCodes.Terms, "'" + type.Id + "' has a term of " + type.MinTerm + " to " + type.MaxTerm + " days, not " + termDays);
    }

    /// <summary>Why a treaty of this type cannot be made between the proposer and the target now, or null when it can. A refusal that is returned, not thrown, so a proposal that became impossible while it waited is answered, not lost.</summary>
    internal EngineRefusedException? Blocker(TreatyTypeDef type, int proposer, int target)
    {
        if (_s.Realms[proposer].Band == (int)StabilityBand.Revolt) return new EngineRefusedException(DipCodes.Revolt, "'" + Set.Realms[proposer].Id + "' is in revolt and makes no treaties");
        foreach (var treaty in TreatiesBetween(proposer, target))
        {
            if (treaty.Type == type.Index) return new EngineRefusedException(DipCodes.AlreadyActive, "'" + type.Id + "' is already in force between them");
            if (type.Excludes.Contains(treaty.Type) || Set.Types[treaty.Type].Excludes.Contains(type.Index))
                return new EngineRefusedException(DipCodes.Blocked, "'" + type.Id + "' cannot stand beside the treaty '" + Set.Types[treaty.Type].Id + "' between them");
        }
        var war = WarBetween(proposer, target);
        if (war is not null && !type.DuringWar) return new EngineRefusedException(DipCodes.AtWar, "they are at war, and '" + type.Id + "' is not a treaty of war");
        if (war is null && type.DuringWar) return new EngineRefusedException(DipCodes.NotAtWar, "'" + type.Id + "' is a treaty of war and they are not at war");
        if (war is not null && (type.EndsWar || type.Has(ClauseKind.Truce)))
        {
            if (!((war.Attacker == proposer && war.Defender == target) || (war.Attacker == target && war.Defender == proposer)))
                return new EngineRefusedException(DipCodes.NotPrincipal, "only the two realms the war began between end or pause it");
            if (war.Ceasefire && type.Has(ClauseKind.Truce)) return new EngineRefusedException(DipCodes.Blocked, "the war is already paused");
        }
        if (type.Requires is not null && !Holds(type.Requires, PairFacts(proposer, target)))
            return new EngineRefusedException(DipCodes.Blocked, "the requirement of '" + type.Id + "' does not hold between them");
        if (_s.Treaties.Count >= Set.Limits.MaxTreaties) return new EngineRefusedException(DipCodes.Capacity, "the world holds " + Set.Limits.MaxTreaties + " treaties");
        return null;
    }

    // ---- proposals ------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// <paramref name="from"/> puts a treaty to <paramref name="to"/>. <paramref name="termDays"/> is 0 for no end; <paramref name="amount"/> is what <paramref name="to"/> pays each period (negative: what <paramref name="from"/> pays).
    /// An imposed treaty (an embargo) is made at once. Otherwise the offer waits: a realm the engine decides for answers after the type's days of consideration by its acceptance score, and a realm the host decides for waits
    /// for <see cref="Respond"/> until the offer expires.
    /// </summary>
    public DipChanges Propose(string from, string to, string type, int termDays, long amount)
    {
        var a = FindRealm(from);
        var b = FindRealm(to);
        var def = FindType(type);
        if (a.Index == b.Index) throw new EngineRefusedException(DipCodes.SameRealm, "a realm makes no treaty with itself");
        CheckTerms(def, termDays, amount);
        var blocker = Blocker(def, a.Index, b.Index);
        if (blocker is not null) throw blocker;
        var changes = new DipChanges();
        var termSteps = Set.Steps(termDays);
        if (def.Consent == Consent.Imposed)
        {
            Sign(def, a.Index, b.Index, termSteps, amount, changes);
            return changes;
        }
        if (_s.Proposals.Any(p => p.From == a.Index && p.To == b.Index && p.Type == def.Index)) throw new EngineRefusedException(DipCodes.AlreadyActive, "the same offer is already waiting for an answer");
        if (_s.Proposals.Count >= Set.Limits.MaxProposals) throw new EngineRefusedException(DipCodes.Capacity, "the world holds " + Set.Limits.MaxProposals + " proposals");
        var step = Math.Max(0, _s.Step);
        var aiTarget = b.Control == Control.Ai;
        var proposal = new ProposalState
        {
            Id = _s.NextProposal++, Type = def.Index, From = a.Index, To = b.Index, Term = termSteps, Amount = amount, Made = _s.Step,
            DecideAt = aiTarget ? step + Set.Steps(def.Consider) : -1, ExpireAt = step + Set.Steps(def.Expire),
        };
        _s.Proposals.Add(proposal);
        changes.Add(ChangeKind.Proposal, _s.Step, a.Index, b.Index, (int)proposal.Id, def.Index, (int)ProposalChange.Made);
        if (aiTarget && def.Consider == 0) Decide(proposal, changes);
        return changes;
    }

    private ProposalState FindProposal(long id)
    {
        var proposal = _s.Proposals.FirstOrDefault(p => p.Id == id);
        if (proposal is null) throw new EngineRefusedException(DipCodes.UnknownProposal, "there is no proposal " + id);
        return proposal;
    }

    /// <summary>The realm a proposal is put to, when the host decides for it, answers.</summary>
    public DipChanges Respond(long proposalId, bool accept)
    {
        var proposal = FindProposal(proposalId);
        if (Set.Realms[proposal.To].Control != Control.Host) throw new EngineRefusedException(DipCodes.NotHost, "'" + Set.Realms[proposal.To].Id + "' decides by its acceptance score, not by a response");
        var changes = new DipChanges();
        var def = Set.Types[proposal.Type];
        var blocker = Blocker(def, proposal.From, proposal.To);
        _s.Proposals.Remove(proposal);
        if (blocker is not null)
        {
            changes.Add(ChangeKind.Proposal, _s.Step, proposal.From, proposal.To, (int)proposal.Id, def.Index, (int)ProposalChange.Invalid);
            return changes;
        }
        changes.Add(ChangeKind.Proposal, _s.Step, proposal.From, proposal.To, (int)proposal.Id, def.Index, (int)(accept ? ProposalChange.Accepted : ProposalChange.Rejected));
        if (accept) Sign(def, proposal.From, proposal.To, proposal.Term, proposal.Amount, changes);
        return changes;
    }

    /// <summary>The proposer takes an offer back.</summary>
    public DipChanges Withdraw(long proposalId, string by)
    {
        var proposal = FindProposal(proposalId);
        var realm = FindRealm(by);
        if (proposal.From != realm.Index) throw new EngineRefusedException(DipCodes.NotParty, "only the realm that made the offer, '" + Set.Realms[proposal.From].Id + "', takes it back");
        _s.Proposals.Remove(proposal);
        var changes = new DipChanges();
        changes.Add(ChangeKind.Proposal, _s.Step, proposal.From, proposal.To, (int)proposal.Id, proposal.Type, (int)ProposalChange.Withdrawn);
        return changes;
    }

    /// <summary>The engine decides for a realm: weighs the offer, rolls its chance from the proposal's own stream, and signs or refuses.</summary>
    private void Decide(ProposalState proposal, DipChanges changes)
    {
        _s.Proposals.Remove(proposal);
        var def = Set.Types[proposal.Type];
        if (Blocker(def, proposal.From, proposal.To) is not null)
        {
            changes.Add(ChangeKind.Proposal, _s.Step, proposal.From, proposal.To, (int)proposal.Id, def.Index, (int)ProposalChange.Invalid);
            return;
        }
        var verdict = Acceptance.Evaluate(def, Set.Acceptance, Measure(def, proposal.From, proposal.To, proposal.Term, proposal.Amount));
        var accepted = verdict.Chance >= 1000 || (verdict.Chance > 0 && Roll("prop:" + proposal.Id, 0, 1000) < verdict.Chance);
        changes.Add(ChangeKind.Proposal, _s.Step, proposal.From, proposal.To, (int)proposal.Id, def.Index, (int)(accepted ? ProposalChange.Accepted : ProposalChange.Rejected), verdict.Score);
        if (accepted) Sign(def, proposal.From, proposal.To, proposal.Term, proposal.Amount, changes);
    }

    private void LiveProposals(long step, DipChanges changes)
    {
        foreach (var proposal in _s.Proposals.ToList())
        {
            if (proposal.DecideAt >= 0 && proposal.DecideAt <= step) Decide(proposal, changes);
            else if (proposal.ExpireAt <= step)
            {
                _s.Proposals.Remove(proposal);
                changes.Add(ChangeKind.Proposal, step, proposal.From, proposal.To, (int)proposal.Id, proposal.Type, (int)ProposalChange.Expired);
            }
        }
    }

    // ---- treaties ---------------------------------------------------------------------------------------------------------------------

    private void Emit(IReadOnlyList<EffectDef> effects, TreatyState treaty, DipChanges changes)
    {
        var source = "treaty:" + Set.Types[treaty.Type].Id;
        foreach (var effect in effects)
        {
            if (effect.Target != EffectTarget.Second) changes.Add(ChangeKind.Effect, _s.Step, treaty.First, treaty.Second, (int)effect.Kind, 0, 0, effect.Scale, -1, effect.Name, source);
            if (effect.Target != EffectTarget.First) changes.Add(ChangeKind.Effect, _s.Step, treaty.Second, treaty.First, (int)effect.Kind, 0, 0, effect.Scale, -1, effect.Name, source);
        }
    }

    /// <summary>Makes the treaty: <paramref name="amount"/> is what the target pays the proposer each period; a negative one has the proposer pay, so the target becomes the first (benefiting) party.</summary>
    private TreatyState Sign(TreatyTypeDef type, int proposer, int target, long termSteps, long amount, DipChanges changes)
    {
        var first = amount < 0 ? target : proposer;
        var second = amount < 0 ? proposer : target;
        var tribute = type.Clause(ClauseKind.Tribute);
        var step = Math.Max(0, _s.Step);
        var treaty = new TreatyState
        {
            Id = _s.NextTreaty++, Type = type.Index, First = first, Second = second, Start = _s.Step, End = termSteps == 0 ? -1 : step + termSteps, Amount = Math.Abs(amount), NextDue = -1, Awaiting = false, Deadline = -1, Missed = 0, War = -1,
        };
        if (tribute is not null && tribute.DueSeason < 0 && treaty.Amount > 0) treaty.NextDue = step + Set.Steps(tribute.Interval);
        _s.Treaties.Add(treaty);
        changes.Add(ChangeKind.Treaty, _s.Step, first, second, (int)treaty.Id, type.Index, (int)TreatyChange.Signed, treaty.Amount);
        if (type.EndsWar) EndWar(WarBetween(first, second)!, changes);
        if (type.Has(ClauseKind.Truce))
        {
            var war = WarBetween(first, second)!;
            war.Ceasefire = true;
            treaty.War = war.Id;
            changes.Add(ChangeKind.War, _s.Step, war.Attacker, war.Defender, (int)war.Id, war.Cb, (int)WarChange.Ceasefire, war.Score);
            Log(IncidentKind.Ceasefire, first, second, type.Index, war.Id, changes);
        }
        AddDeed(first, second, type.SignDeed, 1000);
        AddDeed(second, first, type.SignDeed, 1000);
        Apply(first, type.EndsWar ? PoliticalEvent.PeaceSigned : PoliticalEvent.TreatySigned, 1000);
        Apply(second, type.EndsWar ? PoliticalEvent.PeaceSigned : PoliticalEvent.TreatySigned, 1000);
        if (type.Has(ClauseKind.Embargo)) Log(IncidentKind.Embargo, first, second, type.Index, treaty.Id, changes);
        Emit(type.OnSign, treaty, changes);
        return treaty;
    }

    /// <summary>Removes a treaty that is over without a breach: it ran out, was cancelled, or a war or a succession ended it.</summary>
    private void EndTreaty(TreatyState treaty, TreatyChange how, DipChanges changes)
    {
        var type = Set.Types[treaty.Type];
        Remove(treaty, changes);
        changes.Add(ChangeKind.Treaty, _s.Step, treaty.First, treaty.Second, (int)treaty.Id, type.Index, (int)how, treaty.Amount);
        if (how == TreatyChange.Expired)
        {
            AddTrust(treaty.First, Set.Standing.ExpireTrust);
            AddTrust(treaty.Second, Set.Standing.ExpireTrust);
        }
        Emit(type.OnEnd, treaty, changes);
    }

    private void Remove(TreatyState treaty, DipChanges changes)
    {
        _s.Treaties.Remove(treaty);
        _s.Calls.RemoveAll(c => c.Treaty == treaty.Id);
        if (treaty.War >= 0)
        {
            var war = _s.Wars.FirstOrDefault(w => w.Id == treaty.War);
            if (war is not null && war.Ceasefire)
            {
                war.Ceasefire = false;
                changes.Add(ChangeKind.War, _s.Step, war.Attacker, war.Defender, (int)war.Id, war.Cb, (int)WarChange.Resumed, war.Score);
            }
        }
    }

    /// <summary>
    /// A realm broke its word. The treaty ends. The culprit loses trust and honour as the type says; the wronged remembers the deed (it fades, so the wronged is provocable and forgiving, as the strategies that did best in
    /// Axelrod's tournaments were), may be granted a casus belli against the culprit, and every other realm remembers the breach in proportion to the trust lost. The culprit's factions mind.
    /// </summary>
    private void Breach(TreatyState treaty, int culprit, DipChanges changes)
    {
        var type = Set.Types[treaty.Type];
        var wronged = treaty.First == culprit ? treaty.Second : treaty.First;
        Remove(treaty, changes);
        changes.Add(ChangeKind.Treaty, _s.Step, treaty.First, treaty.Second, (int)treaty.Id, type.Index, (int)TreatyChange.Broken, treaty.Amount);
        AddTrust(culprit, -type.BreachTrust);
        AddHonour(culprit, -type.BreachHonour);
        AddDeed(wronged, culprit, type.BreachDeed, 1000);
        if (Set.Standing.WitnessDeed >= 0 && type.BreachTrust > 0)
            for (var other = 0; other < _n; other++)
                if (other != culprit && other != wronged) AddDeed(other, culprit, Set.Standing.WitnessDeed, type.BreachTrust);
        if (type.GrantsCb >= 0) Grant(wronged, culprit, type.GrantsCb, Set.Steps(type.CbDays), changes);
        Apply(culprit, PoliticalEvent.TreatyBreached, 1000);
        Log(IncidentKind.Breach, culprit, wronged, type.Index, treaty.Id, changes);
        Emit(type.OnBreach, treaty, changes);
    }

    /// <summary>Ends a treaty early. A free one simply ends; one that breaks the word counts as the canceller's breach; one that cannot be ended is refused. Only the realm that imposed an imposed treaty can end it.</summary>
    public DipChanges Cancel(long treatyId, string by)
    {
        var treaty = TreatyById(treatyId);
        var realm = FindRealm(by);
        var type = Set.Types[treaty.Type];
        if (treaty.First != realm.Index && treaty.Second != realm.Index) throw new EngineRefusedException(DipCodes.NotParty, "'" + realm.Id + "' is not a party to treaty " + treatyId);
        if (type.Consent == Consent.Imposed && treaty.First != realm.Index) throw new EngineRefusedException(DipCodes.NotParty, "only the realm that imposed treaty " + treatyId + " ends it");
        if (type.Cancel == CancelRule.Never) throw new EngineRefusedException(DipCodes.CannotCancel, "'" + type.Id + "' cannot be ended early");
        var changes = new DipChanges();
        if (type.Cancel == CancelRule.Free) EndTreaty(treaty, TreatyChange.Cancelled, changes);
        else Breach(treaty, realm.Index, changes);
        return changes;
    }

    private void LiveTreaties(long step, bool seasonChanged, DipChanges changes)
    {
        foreach (var treaty in _s.Treaties.ToList())
        {
            if (!_s.Treaties.Contains(treaty)) continue;
            if (treaty.End >= 0 && treaty.End <= step)
            {
                EndTreaty(treaty, TreatyChange.Expired, changes);
                continue;
            }
            var clause = Set.Types[treaty.Type].Clause(ClauseKind.Tribute);
            if (clause is null || treaty.Amount == 0) continue;
            if (treaty.Awaiting && step > treaty.Deadline)
            {
                Miss(treaty, clause, changes);
                if (!_s.Treaties.Contains(treaty)) continue;
            }
            var due = clause.DueSeason < 0 ? treaty.NextDue >= 0 && treaty.NextDue <= step : seasonChanged && _s.Season == clause.DueSeason;
            if (treaty.Awaiting || !due) continue;
            treaty.Awaiting = true;
            treaty.Deadline = step + Set.Steps(clause.Grace);
            changes.Add(ChangeKind.Obligation, step, treaty.Second, treaty.First, (int)treaty.Id, (int)ClauseKind.Tribute, (int)ObligationChange.Due, treaty.Amount);
            changes.Add(ChangeKind.Effect, step, treaty.Second, treaty.First, (int)EffectKind.Payment, 0, 0, treaty.Amount, treaty.Id, Set.Types[treaty.Type].Id, "treaty:" + Set.Types[treaty.Type].Id);
        }
    }

    /// <summary>The payer did not pay in time (or the host said the payment failed): a small deed and loss of trust for each miss, a breach at the type's limit.</summary>
    private void Miss(TreatyState treaty, ClauseDef clause, DipChanges changes)
    {
        var type = Set.Types[treaty.Type];
        treaty.Awaiting = false;
        treaty.Missed++;
        changes.Add(ChangeKind.Obligation, _s.Step, treaty.Second, treaty.First, (int)treaty.Id, (int)ClauseKind.Tribute, (int)ObligationChange.Missed, treaty.Amount);
        Apply(treaty.Second, PoliticalEvent.TributeMissed, 1000);
        if (treaty.Missed >= clause.MaxMissed)
        {
            Breach(treaty, treaty.Second, changes);
            return;
        }
        AddDeed(treaty.First, treaty.Second, type.BreachDeed, 1000 / clause.MaxMissed);
        AddTrust(treaty.Second, -type.BreachTrust / clause.MaxMissed);
        treaty.NextDue = clause.DueSeason < 0 ? _s.Step + Set.Steps(clause.Interval) : -1;
    }

    /// <summary>
    /// The host says whether a payment that fell due was made. A tribute is the one obligation the engine cannot see being kept, so it waits for this: paid restores trust and schedules the next payment; unpaid counts as a miss
    /// at once.
    /// </summary>
    public DipChanges Fulfil(long treatyId, bool paid)
    {
        var treaty = TreatyById(treatyId);
        if (!treaty.Awaiting) throw new EngineRefusedException(DipCodes.NotDue, "treaty " + treatyId + " is not waiting for a payment");
        var type = Set.Types[treaty.Type];
        var clause = type.Clause(ClauseKind.Tribute)!;
        var changes = new DipChanges();
        if (!paid)
        {
            Miss(treaty, clause, changes);
            return changes;
        }
        treaty.Awaiting = false;
        treaty.Missed = 0;
        treaty.NextDue = clause.DueSeason < 0 ? _s.Step + Set.Steps(clause.Interval) : -1;
        AddTrust(treaty.Second, Set.Standing.PaidTrust);
        Apply(treaty.Second, PoliticalEvent.TributePaid, 1000);
        changes.Add(ChangeKind.Obligation, _s.Step, treaty.Second, treaty.First, (int)treaty.Id, (int)ClauseKind.Tribute, (int)ObligationChange.Paid, treaty.Amount);
        return changes;
    }
}

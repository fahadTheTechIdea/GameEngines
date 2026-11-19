using EngineCommon;

namespace DiplomacyEngine;

public sealed partial class DipRuntime
{
    /// <summary>Builds a runtime from a save that has already been checked (<see cref="DipSave.Open"/> is the only way here).</summary>
    internal static DipRuntime Restore(DipSet set, DipState state) => new(set, state);
}

/// <summary>
/// What a diplomacy state may be. <see cref="CheckRanges"/> is about the data alone (counts, indexes, bounds) and runs before the save's digest is read; <see cref="CheckAgainstDocument"/> is about what the state says about
/// its document (a tribute only where the type has one, a ceasefire only with the treaty that pauses it, a mission only with its agent away) and runs last. Both refuse with <see cref="DipCodes.SaveState"/> and say what is wrong.
/// </summary>
internal static class DipStateRules
{
    private const long Limit = 1_000_000_000_000;

    private static EngineRefusedException Bad(string why) => new(DipCodes.SaveState, "the save's state is not possible: " + why);

    private static void In(long value, long minimum, long maximum, string what)
    {
        if (value < minimum || value > maximum) throw Bad(what + " is " + value + ", not " + minimum + " to " + maximum);
    }

    private static void Count(int count, int expected, string what)
    {
        if (count != expected) throw Bad(what + " holds " + count + " entries, the document needs " + expected);
    }

    private static void Unique(IEnumerable<long> ids, long next, string what)
    {
        var seen = new HashSet<long>();
        foreach (var id in ids)
        {
            In(id, 0, Math.Max(0, next - 1), what + " id");
            if (!seen.Add(id)) throw Bad(what + " " + id + " appears twice");
        }
    }

    public static void CheckRanges(DipSet set, DipState s)
    {
        var n = set.Realms.Count;
        var limits = set.Limits;
        In(s.Step, -1, Limit, "step");
        In(s.Season, -1, set.Seasons.Count - 1, "season");
        In(s.NextTreaty, 0, Limit, "next treaty");
        In(s.NextProposal, 0, Limit, "next proposal");
        In(s.NextWar, 0, Limit, "next war");
        In(s.NextMission, 0, Limit, "next mission");
        In(s.NextCall, 0, Limit, "next call");
        Count(s.Realms.Count, n, "the realms");
        for (var i = 0; i < n; i++)
        {
            var r = s.Realms[i];
            In(r.Might, 0, DipFacts.Saturate, "might");
            In(r.Stability, 0, 1000, "stability");
            In(r.Legitimacy, 0, 1000, "legitimacy");
            In(r.Band, 0, Enum.GetValues<StabilityBand>().Length - 1, "stability band");
            In(r.Trust, -DipMath.Million, DipMath.Million, "trust");
            In(r.Fear, -DipMath.Million, DipMath.Million, "fear");
            In(r.Honour, -DipMath.Million, DipMath.Million, "honour");
            In(r.Infamy, 0, DipMath.Million, "infamy");
            In(r.CounterIntel, 0, 1000, "counter-intelligence");
            In(r.Generation, 0, 1_000_000, "generation");
            Count(r.Satisfaction.Count, set.Realms[i].Factions.Count, "the faction contentment of realm " + set.Realms[i].Id);
            foreach (var value in r.Satisfaction) In(value, 0, 1000, "a faction's contentment");
        }
        Count(s.Leader.Count, n * n, "the leaders' regard");
        Count(s.Intel.Count, n * n, "the intelligence");
        for (var i = 0; i < n * n; i++)
        {
            In(s.Leader[i], -1000, 1000, "a leader's regard");
            In(s.Intel[i], 0, DipMath.Million, "intelligence");
            if (i / n == i % n && (s.Leader[i] != 0 || s.Intel[i] != 0)) throw Bad("a realm has regard or intelligence of itself");
        }
        var perPair = new Dictionary<int, int>();
        In(s.Deeds.Count, 0, (long)n * n * limits.MaxDeedsPerPair, "the number of deeds");
        foreach (var d in s.Deeds)
        {
            In(d.Holder, 0, n - 1, "a deed's holder");
            In(d.Subject, 0, n - 1, "a deed's subject");
            if (d.Holder == d.Subject) throw Bad("a realm remembers a deed of its own");
            In(d.Deed, 0, set.Deeds.Count - 1, "a deed");
            In(d.Start, -1, Math.Max(0, s.Step), "a deed's step");
            In(d.Scale, 1, 1000, "a deed's scale");
            var pair = d.Holder * n + d.Subject;
            perPair[pair] = perPair.GetValueOrDefault(pair) + 1;
            In(perPair[pair], 0, limits.MaxDeedsPerPair, "the deeds of a pair");
        }
        In(s.Treaties.Count, 0, limits.MaxTreaties, "the number of treaties");
        Unique(s.Treaties.Select(t => t.Id), s.NextTreaty, "treaty");
        foreach (var t in s.Treaties)
        {
            In(t.Type, 0, set.Types.Count - 1, "a treaty's type");
            In(t.First, 0, n - 1, "a treaty's first realm");
            In(t.Second, 0, n - 1, "a treaty's second realm");
            if (t.First == t.Second) throw Bad("a realm has a treaty with itself");
            In(t.Start, -1, Math.Max(0, s.Step), "a treaty's start");
            In(t.End, -1, Limit, "a treaty's end");
            In(t.Amount, 0, DipRuntime.MaxAmount, "a tribute");
            In(t.NextDue, -1, Limit, "a payment's step");
            In(t.Deadline, -1, Limit, "a payment's deadline");
            In(t.Missed, 0, 1000, "missed payments");
            In(t.War, -1, Math.Max(0, s.NextWar - 1), "a treaty's war");
        }
        In(s.Proposals.Count, 0, limits.MaxProposals, "the number of proposals");
        Unique(s.Proposals.Select(p => p.Id), s.NextProposal, "proposal");
        foreach (var p in s.Proposals)
        {
            In(p.Type, 0, set.Types.Count - 1, "a proposal's type");
            In(p.From, 0, n - 1, "a proposal's proposer");
            In(p.To, 0, n - 1, "a proposal's target");
            if (p.From == p.To) throw Bad("a realm proposes to itself");
            In(p.Term, 0, Limit, "a proposal's term");
            In(p.Amount, -DipRuntime.MaxAmount, DipRuntime.MaxAmount, "a proposal's amount");
            In(p.Made, -1, Math.Max(0, s.Step), "a proposal's step");
            In(p.DecideAt, -1, Limit, "a proposal's decision step");
            In(p.ExpireAt, 0, Limit, "a proposal's expiry");
        }
        var all = n == 64 ? ulong.MaxValue : (1UL << n) - 1;
        In(s.Wars.Count, 0, limits.MaxWars, "the number of wars");
        Unique(s.Wars.Select(w => w.Id), s.NextWar, "war");
        foreach (var w in s.Wars)
        {
            In(w.Attacker, 0, n - 1, "a war's attacker");
            In(w.Defender, 0, n - 1, "a war's defender");
            if (w.Attacker == w.Defender) throw Bad("a realm is at war with itself");
            if ((w.AttackerSide & ~all) != 0 || (w.DefenderSide & ~all) != 0) throw Bad("a war names a realm that does not exist");
            if ((w.AttackerSide & w.DefenderSide) != 0) throw Bad("a realm fights on both sides of war " + w.Id);
            if ((w.AttackerSide & (1UL << w.Attacker)) == 0 || (w.DefenderSide & (1UL << w.Defender)) == 0) throw Bad("war " + w.Id + " lacks the realm it began with on its side");
            In(w.Cb, -1, set.CasusBelli.Count - 1, "a war's casus belli");
            In(w.Start, -1, Math.Max(0, s.Step), "a war's start");
            In(w.Score, -1000, 1000, "a war's score");
            In(w.AttackerExhaustion, 0, DipMath.Million, "exhaustion");
            In(w.DefenderExhaustion, 0, DipMath.Million, "exhaustion");
        }
        In(s.Calls.Count, 0, limits.MaxCalls, "the number of calls to arms");
        Unique(s.Calls.Select(c => c.Id), s.NextCall, "call");
        foreach (var c in s.Calls)
        {
            In(c.War, 0, Math.Max(0, s.NextWar - 1), "a call's war");
            In(c.Caller, 0, n - 1, "a call's caller");
            In(c.Callee, 0, n - 1, "a call's callee");
            In(c.Treaty, 0, Math.Max(0, s.NextTreaty - 1), "a call's treaty");
            In(c.Due, 0, Limit, "a call's deadline");
        }
        In(s.Grants.Count, 0, limits.MaxGrants, "the number of granted casus belli");
        foreach (var g in s.Grants)
        {
            In(g.Holder, 0, n - 1, "a grant's holder");
            In(g.Target, 0, n - 1, "a grant's target");
            if (g.Holder == g.Target) throw Bad("a realm holds a casus belli against itself");
            In(g.Cb, 0, set.CasusBelli.Count - 1, "a grant's casus belli");
            In(g.Until, 0, Limit, "a grant's end");
        }
        var targets = new HashSet<int>();
        foreach (var c in s.Coalitions)
        {
            In(c.Target, 0, n - 1, "a coalition's target");
            if (!targets.Add(c.Target)) throw Bad("two coalitions against one realm");
            if ((c.Members & ~all) != 0 || (c.Members & (1UL << c.Target)) != 0) throw Bad("a coalition's members are not other realms");
            In(c.Since, 0, Math.Max(0, s.Step), "a coalition's start");
        }
        In(s.Agents.Count, 0, limits.MaxAgents, "the number of agents");
        foreach (var a in s.Agents)
        {
            if (a.Id.Length == 0 || a.Id.Length > 128 || a.Id.Any(ch => !(ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9' || ch is '-' or '.' or '_'))) throw Bad("an agent id is not a valid id");
            In(a.Owner, 0, n - 1, "an agent's owner");
            In(a.Skill, 0, 1000, "an agent's skill");
            In(a.State, 0, Enum.GetValues<AgentState>().Length - 1, "an agent's state");
            In(a.Mission, -1, Math.Max(0, s.NextMission - 1), "an agent's mission");
            In(a.FreeAt, 0, Limit, "an agent's release");
        }
        In(s.Missions.Count, 0, limits.MaxMissions, "the number of missions");
        Unique(s.Missions.Select(m => m.Id), s.NextMission, "mission");
        foreach (var m in s.Missions)
        {
            In(m.Agent, 0, Math.Max(0, s.Agents.Count - 1), "a mission's agent");
            In(m.Target, 0, n - 1, "a mission's target");
            In(m.Type, 0, set.Missions.Count - 1, "a mission's type");
            In(m.Start, -1, Math.Max(0, s.Step), "a mission's start");
            In(m.Due, 0, Limit, "a mission's due step");
        }
        In(s.Incidents.Count, 0, limits.MaxIncidents, "the number of incidents");
        foreach (var i in s.Incidents)
        {
            In(i.Kind, 0, Enum.GetValues<IncidentKind>().Length - 1, "an incident's kind");
            In(i.A, -1, n - 1, "an incident's first realm");
            In(i.B, -1, n - 1, "an incident's second realm");
            In(i.Step, -1, Math.Max(0, s.Step), "an incident's step");
        }
    }

    public static void CheckAgainstDocument(DipSet set, DipState s)
    {
        var pairs = new HashSet<(int, int, int)>();
        foreach (var t in s.Treaties)
        {
            var type = set.Types[t.Type];
            if (!pairs.Add((Math.Min(t.First, t.Second), Math.Max(t.First, t.Second), t.Type))) throw Bad("two treaties of '" + type.Id + "' between the same realms");
            var tribute = type.Clause(ClauseKind.Tribute);
            if (tribute is null && (t.Amount != 0 || t.NextDue != -1 || t.Awaiting || t.Missed != 0)) throw Bad("treaty " + t.Id + " has a tribute and '" + type.Id + "' has none");
            if (tribute is not null && t.Awaiting && t.Amount == 0) throw Bad("treaty " + t.Id + " waits for a payment of nothing");
            if (tribute is not null && tribute.DueSeason >= 0 && t.NextDue != -1) throw Bad("treaty " + t.Id + " is paid by season and has a day to pay");
            if (tribute is not null && t.Missed >= tribute.MaxMissed) throw Bad("treaty " + t.Id + " has missed as many payments as break it");
            if (t.Awaiting && tribute is null) throw Bad("treaty " + t.Id + " waits for a payment");
            if (t.War >= 0)
            {
                var war = s.Wars.FirstOrDefault(w => w.Id == t.War);
                if (!type.Has(ClauseKind.Truce) || war is null || !war.Ceasefire) throw Bad("treaty " + t.Id + " pauses a war that is not paused by it");
            }
            else if (type.Has(ClauseKind.Truce)) throw Bad("treaty " + t.Id + " is a truce and pauses no war");
        }
        foreach (var w in s.Wars.Where(w => w.Ceasefire))
            if (!s.Treaties.Any(t => t.War == w.Id)) throw Bad("war " + w.Id + " is paused and no treaty pauses it");
        var offers = new HashSet<(int, int, int)>();
        foreach (var p in s.Proposals)
        {
            var type = set.Types[p.Type];
            if (type.Consent != Consent.Mutual) throw Bad("proposal " + p.Id + " is for an imposed treaty, which is never put to anyone");
            if (!offers.Add((p.From, p.To, p.Type))) throw Bad("the same offer waits twice");
            if ((p.DecideAt >= 0) != (set.Realms[p.To].Control == Control.Ai)) throw Bad("proposal " + p.Id + " has a decision step and its target decides otherwise");
            CheckTerms(type, p);
        }
        foreach (var c in s.Calls)
        {
            var war = s.Wars.FirstOrDefault(w => w.Id == c.War);
            var treaty = s.Treaties.FirstOrDefault(t => t.Id == c.Treaty);
            if (war is null || treaty is null || !set.Types[treaty.Type].Has(ClauseKind.Defend)) throw Bad("call " + c.Id + " is owed under a pact or in a war that is not there");
            if ((war.DefenderSide & (1UL << c.Callee)) != 0) throw Bad("call " + c.Id + " is owed by a realm that already fights for the caller");
        }
        foreach (var g in s.Grants)
            if (set.CasusBelli[g.Cb].Source != CbSource.Granted) throw Bad("a casus belli that is never granted is held by grant");
        for (var i = 0; i < s.Agents.Count; i++)
        {
            var agent = s.Agents[i];
            var mission = s.Missions.FirstOrDefault(m => m.Agent == i);
            if ((agent.State == (int)AgentState.Away) != (mission is not null)) throw Bad("agent '" + agent.Id + "' is away without a mission, or on one while home");
            if (mission is not null && agent.Mission != mission.Id) throw Bad("agent '" + agent.Id + "' is on another mission than the one that names it");
            if (mission is null && agent.Mission != -1) throw Bad("agent '" + agent.Id + "' names a mission that is not there");
            if (s.Agents.Take(i).Any(o => o.Id == agent.Id)) throw Bad("two agents are called '" + agent.Id + "'");
        }
        if (s.Missions.GroupBy(m => m.Agent).Any(g => g.Count() > 1)) throw Bad("an agent is on two missions");
        foreach (var m in s.Missions)
        {
            var kind = set.Missions[m.Type].Kind;
            if ((kind == MissionKind.Counter) != (m.Target == s.Agents[m.Agent].Owner)) throw Bad("mission " + m.Id + " is not against the right realm for its kind");
        }
    }

    private static void CheckTerms(TreatyTypeDef type, ProposalState p)
    {
        var tribute = type.Clause(ClauseKind.Tribute);
        if (tribute is null && p.Amount != 0) throw Bad("proposal " + p.Id + " names a tribute and '" + type.Id + "' has none");
        if (tribute is not null && p.Amount == 0 && !tribute.Optional) throw Bad("proposal " + p.Id + " names no tribute and '" + type.Id + "' needs one");
        if (p.Term == 0 && !type.Permanent) throw Bad("proposal " + p.Id + " has no end and '" + type.Id + "' always has one");
    }
}

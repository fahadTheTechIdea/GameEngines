using System.Numerics;

namespace DiplomacyEngine;

public sealed partial class DipRuntime
{
    /// <summary>
    /// The balance of power, every <c>interval</c> steps. A realm's threat is its share of all might (its pacts' protected partners count half) grown by its infamy (Walt: states balance against threat, not power alone;
    /// aggressive expansion makes a realm threatening). Above the threat line, the realms that do not love it, are not tied to it and are not too weak to risk it (Walt: weak states bandwagon with a feared neighbour) form a
    /// coalition against it, each holding the document's casus belli while it lasts. Below the relax line it dissolves. Members' regard of the target falls while they balance (the regard rule).
    /// </summary>
    private void LiveBalance(long step, DipChanges changes)
    {
        var rules = Set.Balance;
        if (step % rules.Interval != 0) return;
        for (var target = 0; target < _n; target++)
        {
            var threat = ThreatOf(target);
            var coalition = _s.Coalitions.FirstOrDefault(c => c.Target == target);
            if (coalition is null)
            {
                if (threat < rules.ThreatLine) continue;
                var members = Members(target, 0);
                if (BitOperations.PopCount(members) < rules.MinMembers) continue;
                coalition = new CoalitionState { Target = target, Members = members, Since = step };
                _s.Coalitions.Add(coalition);
                changes.Add(ChangeKind.Coalition, step, target, -1, 0, 0, (int)CoalitionChange.Formed, BitOperations.PopCount(members));
                Log(IncidentKind.Coalition, target, -1, 0, BitOperations.PopCount(members), changes);
                for (var r = 0; r < _n; r++)
                    if (InSide(members, r)) GrantBalance(r, target, changes);
                continue;
            }
            var now = Members(target, coalition.Members);
            if (threat <= rules.RelaxLine || BitOperations.PopCount(now) < rules.MinMembers)
            {
                _s.Coalitions.Remove(coalition);
                changes.Add(ChangeKind.Coalition, step, target, -1, 0, 0, (int)CoalitionChange.Dissolved, 0);
                Log(IncidentKind.Coalition, target, -1, 1, 0, changes);
                continue;
            }
            var before = coalition.Members;
            coalition.Members = now;
            for (var r = 0; r < _n; r++)
            {
                if (InSide(now, r) && !InSide(before, r)) changes.Add(ChangeKind.Coalition, step, target, r, 0, 0, (int)CoalitionChange.Joined, BitOperations.PopCount(now));
                else if (!InSide(now, r) && InSide(before, r)) changes.Add(ChangeKind.Coalition, step, target, r, 0, 0, (int)CoalitionChange.Left, BitOperations.PopCount(now));
                if (InSide(now, r)) GrantBalance(r, target, changes);
            }
        }
    }

    private void GrantBalance(int member, int target, DipChanges changes)
    {
        if (Set.Balance.Cb >= 0) Grant(member, target, Set.Balance.Cb, Set.Steps(Set.Balance.CbDays), changes);
    }

    /// <summary>The realms that balance against the target now: the existing members are kept unless their regard has risen 100 past the joining line (so a coalition does not flicker).</summary>
    private ulong Members(int target, ulong existing)
    {
        var rules = Set.Balance;
        ulong members = 0;
        var feared = _s.Realms[target].Fear / 1000 >= rules.BandwagonFear;
        for (var r = 0; r < _n; r++)
        {
            if (r == target || _s.Realms[r].Band == (int)StabilityBand.Revolt || _s.Realms[r].Might == 0) continue;
            if (feared && _s.Realms[r].Might < _s.Realms[target].Might / 2) continue;
            if (TreatiesBetween(r, target).Any(t => Set.Types[t.Type].Has(ClauseKind.Defend) || Set.Types[t.Type].Has(ClauseKind.Vassal) || Set.Types[t.Type].Has(ClauseKind.Bond))) continue;
            var line = rules.JoinRegard + (InSide(existing, r) ? 100 : 0);
            if (RegardOfWithout(r, target, InSide(existing, r)) <= line) members |= Bit(r);
        }
        return members;
    }

    /// <summary>The regard a realm has of the target without the penalty the coalition itself adds, so that forming a coalition does not keep the coalition together by itself.</summary>
    private int RegardOfWithout(int holder, int target, bool member) => member ? DipMath.ClampInt(RegardOf(holder, target) + Set.Regard.Coalition, -1000, 1000) : RegardOf(holder, target);
}

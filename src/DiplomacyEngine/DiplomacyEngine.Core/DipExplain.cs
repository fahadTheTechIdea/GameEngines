using System.Globalization;
using System.Text;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace DiplomacyEngine;

/// <summary>Says why a proposal would be accepted or refused, where a regard comes from, how a mission would go, why a coalition forms or does not, and how a realm stands, from the same numbers the runtime uses: a pure reading that changes nothing.</summary>
public static class DipExplain
{
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static void CheckHost(DipRuntime runtime, IReadOnlyDictionary<string, FactValue> host)
    {
        foreach (var id in runtime.Set.HostFacts.Keys)
            if (!host.ContainsKey(id)) throw new EngineRefusedException(DipCodes.FactMissing, "the host gave no fact '" + id + "', which the document declares");
    }

    /// <summary>Why a proposal would be accepted, with the blocker that would stop it first, the acceptance lines, and the verdict.</summary>
    public static string Proposal(DipRuntime runtime, string from, string to, string type, int termDays, long amount, IReadOnlyDictionary<string, FactValue> host)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(host);
        CheckHost(runtime, host);
        var a = runtime.FindRealm(from);
        var b = runtime.FindRealm(to);
        var def = runtime.FindType(type);
        if (a.Index == b.Index) throw new EngineRefusedException(DipCodes.SameRealm, "a realm makes no treaty with itself");
        runtime.CheckTerms(def, termDays, amount);
        runtime.PairFactsFor(a.Index, b.Index, host);
        var text = new StringBuilder("proposal " + def.Id + " from " + a.Id + " to " + b.Id + ", term " + (termDays == 0 ? "none" : Number(termDays) + " days") + ", amount " + Number(amount) + " (" + b.Id + " is decided by " + EnumNames.Name(b.Control) + ")");
        var blocker = runtime.Blocker(def, a.Index, b.Index);
        if (blocker is not null) return text.Append("\n  cannot be made: ").Append(blocker.Code).Append(": ").Append(blocker.Message).ToString();
        if (def.Consent == Consent.Imposed) return text.Append("\n  imposed: it is made at once, whatever ").Append(b.Id).Append(" thinks").ToString();
        var verdict = Acceptance.Evaluate(def, runtime.Set.Acceptance, runtime.Measure(def, a.Index, b.Index, runtime.Set.Steps(termDays), amount));
        return text.Append('\n').Append(Acceptance.Describe(verdict, runtime.Set.Acceptance)).ToString();
    }

    /// <summary>Where a regard comes from, part by part.</summary>
    public static string Regard(DipRuntime runtime, string holder, string subject)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var a = runtime.FindRealm(holder);
        var b = runtime.FindRealm(subject);
        if (a.Index == b.Index) throw new EngineRefusedException(DipCodes.SameRealm, "a realm has no regard of itself");
        var parts = runtime.RegardTerms(a.Index, b.Index);
        return "regard of " + a.Id + " for " + b.Id + ": " + Number(runtime.RegardOf(a.Index, b.Index)) + "\n  ideology " + Number(parts.Ideology) + "\n  deeds " + Number(parts.Deeds) + "\n  treaties " + Number(parts.Treaties) +
            "\n  leader " + Number(parts.Leader) + "\n  standing " + Number(parts.Standing) + "\n  coalition " + Number(parts.Coalition) + "\n  (the sum is kept within -1000 and 1000)";
    }

    /// <summary>The chances of a mission, and how they were made.</summary>
    public static string Mission(DipRuntime runtime, string agent, string target, string mission)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var odds = runtime.OddsOf(agent, target, mission);
        return "mission " + mission + " by " + agent + " against " + target + ": succeeds " + Number(odds.Success) + " per mille, exposed " + Number(odds.Exposure) + " per mille\n  " + odds.Why;
    }

    /// <summary>Why a coalition against a realm does or does not stand: its threat against the lines, and each other realm's regard and ties.</summary>
    public static string Coalition(DipRuntime runtime, string target)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var t = runtime.FindRealm(target);
        var set = runtime.Set;
        var text = new StringBuilder("balance against " + t.Id + ": threat " + Number(runtime.ThreatOf(t.Index)) + " (joins above " + Number(set.Balance.ThreatLine) + ", dissolves at or below " + Number(set.Balance.RelaxLine) + ")");
        var coalition = runtime.Coalitions.FirstOrDefault(c => c.Target == t.Index);
        text.Append(coalition is null ? "\n  no coalition stands" : "\n  a coalition of " + Number(System.Numerics.BitOperations.PopCount(coalition.Members)) + " stands since step " + Number(coalition.Since));
        foreach (var other in set.Realms.Where(r => r.Index != t.Index))
            text.Append("\n  ").Append(other.Id).Append(": regard ").Append(Number(runtime.RegardOf(other.Index, t.Index))).Append(", joins at or below ").Append(Number(set.Balance.JoinRegard)).Append(coalition is not null && runtime.InCoalitionAgainst(other.Index, t.Index) ? ", is a member" : "");
        return text.ToString();
    }

    /// <summary>How a realm stands: its standing in the world, its order and its factions.</summary>
    public static string Realm(DipRuntime runtime, string realm)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var def = runtime.FindRealm(realm);
        var state = runtime.Realms[def.Index];
        var text = new StringBuilder("realm " + def.Id + " (" + EnumNames.Name(def.Control) + "): might " + Number(state.Might) + ", stability " + Number(state.Stability) + " (" + EnumNames.Name((StabilityBand)state.Band) + "), legitimacy " + Number(state.Legitimacy) +
            ", trust " + Number(state.Trust / 1000) + ", fear " + Number(state.Fear / 1000) + ", honour " + Number(state.Honour / 1000) + ", infamy " + Number(state.Infamy / 1000) + ", generation " + Number(state.Generation) +
            ", wars " + Number(runtime.WarsOf(def.Index)) + ", treaties " + Number(runtime.TreatyCountOf(def.Index)));
        foreach (var faction in def.Factions) text.Append("\n  faction ").Append(faction.Id).Append(" share ").Append(Number(faction.Share)).Append(" content ").Append(Number(state.Satisfaction[faction.Index]));
        return text.ToString();
    }
}

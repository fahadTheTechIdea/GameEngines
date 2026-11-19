using System.Globalization;

namespace DiplomacyEngine;

/// <summary>What kind of thing happened between the realms.</summary>
public enum ChangeKind { Season, Proposal, Treaty, Obligation, War, Coalition, Mission, Agent, Intel, Politics, Incident, Effect, Grant }

/// <summary>
/// One thing that happened, in the order it happened. One record serves every kind so the order across kinds is kept exactly; what the fields hold depends on the kind:
/// <list type="bullet">
/// <item>Season: <c>Index</c> the season now, <c>Sub</c> the season before (-1 at the first step).</item>
/// <item>Proposal: <c>Index</c> the proposal id; <c>A</c> from, <c>B</c> to; <c>Sub</c> the treaty type; <c>Sub2</c> a <see cref="ProposalChange"/>; <c>Amount</c> the acceptance score when it was decided.</item>
/// <item>Treaty: <c>Index</c> the treaty id; <c>A</c> first, <c>B</c> second; <c>Sub</c> the type; <c>Sub2</c> a <see cref="TreatyChange"/>; <c>Amount</c> the tribute per payment.</item>
/// <item>Obligation: <c>Index</c> the treaty id; <c>A</c> the realm owing; <c>B</c> the realm owed; <c>Sub</c> the clause kind; <c>Sub2</c> an <see cref="ObligationChange"/>; <c>Amount</c> the payment or the call id.</item>
/// <item>War: <c>Index</c> the war id; <c>A</c> attacker, <c>B</c> defender; <c>Sub</c> the casus belli (-1 none); <c>Sub2</c> a <see cref="WarChange"/>; <c>Amount</c> the attacker's score; <c>Amount2</c> the realm concerned for joined and left.</item>
/// <item>Coalition: <c>A</c> the target; <c>B</c> the realm that joined or left (-1 for the whole); <c>Sub2</c> a <see cref="CoalitionChange"/>; <c>Amount</c> the members now.</item>
/// <item>Mission: <c>Index</c> the mission id; <c>A</c> owner, <c>B</c> target; <c>Sub</c> the mission type; <c>Sub2</c> a <see cref="MissionChange"/>; <c>Amount</c> the agent.</item>
/// <item>Agent: <c>Index</c> the agent; <c>A</c> the owner; <c>Sub2</c> an <see cref="AgentChange"/>.</item>
/// <item>Intel: <c>A</c> observer, <c>B</c> target; <c>Index</c> the level now; <c>Sub</c> the level before.</item>
/// <item>Politics: <c>A</c> the realm; <c>Sub2</c> a <see cref="PoliticsChange"/>; <c>Amount</c> its stability.</item>
/// <item>Incident: <c>Sub</c> an <see cref="IncidentKind"/>; <c>A</c>, <c>B</c> the realms; <c>Index</c> and <c>Amount</c> as the log holds them.</item>
/// <item>Effect: what a treaty, a mission or a rule asks of the other layers. <c>Index</c> the <see cref="EffectKind"/>; <c>Name</c> the signal or shock id; <c>A</c> the realm it is for, <c>B</c> the other realm; <c>Amount2</c> the treaty a payment is for (-1 for any other effect); <c>Amount</c> the scale of a shock
/// (per mille) or the payment; <c>Source</c> says what asked, as <c>treaty:alliance</c>, <c>mission:sabotage</c> or <c>politics</c>.</item>
/// <item>Grant: <c>A</c> holder, <c>B</c> target; <c>Index</c> the casus belli; <c>Sub2</c> a <see cref="GrantChange"/>.</item>
/// </list>
/// </summary>
public sealed record DipChange(ChangeKind Kind, long Step, int A, int B, int Index, int Sub, int Sub2, long Amount, long Amount2, string Name, string Source);

/// <summary>Everything a call changed, in order. Empty when nothing happened.</summary>
public sealed class DipChanges
{
    private readonly List<DipChange> _items = new();

    public IReadOnlyList<DipChange> Items => _items;

    internal void Add(ChangeKind kind, long step, int a = -1, int b = -1, int index = 0, int sub = 0, int sub2 = 0, long amount = 0, long amount2 = 0, string name = "", string source = "") =>
        _items.Add(new DipChange(kind, step, a, b, index, sub, sub2, amount, amount2, name, source));

    internal void AddRange(DipChanges other) => _items.AddRange(other._items);
}

/// <summary>One line of plain text for each change, by ids, never by words in a language: the tools print these and tests compare them. A game renders its own words from <see cref="DipNames"/>.</summary>
public static class DipChangeText
{
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Line(DipSet set, DipChange change)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(change);
        var at = Number(change.Step) + " ";
        var a = change.A < 0 ? "-" : set.Realms[change.A].Id;
        var b = change.B < 0 ? "-" : set.Realms[change.B].Id;
        switch (change.Kind)
        {
            case ChangeKind.Season: return at + "season " + (change.Sub < 0 ? "-" : set.Seasons[change.Sub].Id) + " -> " + set.Seasons[change.Index].Id;
            case ChangeKind.Proposal: return at + "proposal " + Number(change.Index) + " " + set.Types[change.Sub].Id + " " + a + " -> " + b + " " + EngineCommon.EnumNames.Name((ProposalChange)change.Sub2) + (change.Sub2 is (int)ProposalChange.Accepted or (int)ProposalChange.Rejected ? " score " + Number(change.Amount) : "");
            case ChangeKind.Treaty: return at + "treaty " + Number(change.Index) + " " + set.Types[change.Sub].Id + " " + a + " / " + b + " " + EngineCommon.EnumNames.Name((TreatyChange)change.Sub2) + (change.Amount != 0 ? " tribute " + Number(change.Amount) : "");
            case ChangeKind.Obligation: return at + "obligation " + EngineCommon.EnumNames.Name((ClauseKind)change.Sub) + " of treaty " + Number(change.Index) + " " + a + " -> " + b + " " + EngineCommon.EnumNames.Name((ObligationChange)change.Sub2) + (change.Amount != 0 ? " " + Number(change.Amount) : "");
            case ChangeKind.War: return at + "war " + Number(change.Index) + " " + a + " vs " + b + " " + EngineCommon.EnumNames.Name((WarChange)change.Sub2) + (change.Sub >= 0 ? " cb " + set.CasusBelli[change.Sub].Id : "") + " score " + Number(change.Amount) + (change.Sub2 is (int)WarChange.Joined or (int)WarChange.Left ? " realm " + set.Realms[(int)change.Amount2].Id : "");
            case ChangeKind.Coalition: return at + "coalition against " + a + " " + EngineCommon.EnumNames.Name((CoalitionChange)change.Sub2) + (change.B >= 0 ? " " + b : "") + " members " + Number(change.Amount);
            case ChangeKind.Mission: return at + "mission " + Number(change.Index) + " " + set.Missions[change.Sub].Id + " " + a + " -> " + b + " " + EngineCommon.EnumNames.Name((MissionChange)change.Sub2);
            case ChangeKind.Agent: return at + "agent " + Number(change.Index) + " of " + a + " " + EngineCommon.EnumNames.Name((AgentChange)change.Sub2);
            case ChangeKind.Intel: return at + "intel " + a + " on " + b + " " + EngineCommon.EnumNames.Name((IntelLevel)change.Sub) + " -> " + EngineCommon.EnumNames.Name((IntelLevel)change.Index);
            case ChangeKind.Politics: return at + "politics " + a + " " + EngineCommon.EnumNames.Name((PoliticsChange)change.Sub2) + " stability " + Number(change.Amount);
            case ChangeKind.Incident: return at + "incident " + EngineCommon.EnumNames.Name((IncidentKind)change.Sub) + " " + a + " / " + b + " " + Number(change.Index) + " " + Number(change.Amount);
            case ChangeKind.Effect: return at + "effect " + EngineCommon.EnumNames.Name((EffectKind)change.Index) + " " + change.Name + " for " + a + (change.B >= 0 ? " against " + b : "") + (change.Index != (int)EffectKind.Signal ? " scale " + Number(change.Amount) : "") + " from " + change.Source;
            case ChangeKind.Grant: return at + "casus belli " + set.CasusBelli[change.Index].Id + " " + a + " against " + b + " " + EngineCommon.EnumNames.Name((GrantChange)change.Sub2);
        }
        throw new InvalidOperationException("unknown change kind " + change.Kind);
    }
}

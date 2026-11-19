using System.Text.Json.Serialization;

namespace DiplomacyEngine;

/// <summary>
/// The state of a runtime as plain data, exactly what a save holds. Every field is required: a save with a field missing is refused, never completed with a default. The digest is taken over this data and nothing else,
/// so it can be checked before the data is understood, and the data is checked against the document afterwards. There is no random-generator state in it: every random number is a function of the document's seed, a stream
/// named by the ids involved, and a step or an id, all of which are here.
/// </summary>
public sealed class DipState
{
    [JsonRequired] public int Format { get; set; }
    [JsonRequired] public string Document { get; set; } = "";
    [JsonRequired] public string Id { get; set; } = "";
    [JsonRequired] public long Step { get; set; }
    [JsonRequired] public int Season { get; set; }
    [JsonRequired] public long NextTreaty { get; set; }
    [JsonRequired] public long NextProposal { get; set; }
    [JsonRequired] public long NextWar { get; set; }
    [JsonRequired] public long NextMission { get; set; }
    [JsonRequired] public long NextCall { get; set; }
    [JsonRequired] public List<RealmState> Realms { get; set; } = new();
    [JsonRequired] public List<int> Leader { get; set; } = new();
    [JsonRequired] public List<int> Intel { get; set; } = new();
    [JsonRequired] public List<DeedState> Deeds { get; set; } = new();
    [JsonRequired] public List<TreatyState> Treaties { get; set; } = new();
    [JsonRequired] public List<ProposalState> Proposals { get; set; } = new();
    [JsonRequired] public List<WarState> Wars { get; set; } = new();
    [JsonRequired] public List<CallState> Calls { get; set; } = new();
    [JsonRequired] public List<GrantState> Grants { get; set; } = new();
    [JsonRequired] public List<CoalitionState> Coalitions { get; set; } = new();
    [JsonRequired] public List<AgentRecord> Agents { get; set; } = new();
    [JsonRequired] public List<MissionRecord> Missions { get; set; } = new();
    [JsonRequired] public List<IncidentRecord> Incidents { get; set; } = new();
    [JsonRequired] public string Digest { get; set; } = "";
}

/// <summary>A realm now: its might, its order, its standing with the world in millionths of the axis (trust, fear and honour from minus one million to one million; infamy from 0), and the contentment of each faction.</summary>
public sealed class RealmState
{
    [JsonRequired] public long Might { get; set; }
    [JsonRequired] public int Stability { get; set; }
    [JsonRequired] public int Legitimacy { get; set; }
    [JsonRequired] public int Band { get; set; }
    [JsonRequired] public long Trust { get; set; }
    [JsonRequired] public long Fear { get; set; }
    [JsonRequired] public long Honour { get; set; }
    [JsonRequired] public long Infamy { get; set; }
    [JsonRequired] public int CounterIntel { get; set; }
    [JsonRequired] public int Generation { get; set; }
    [JsonRequired] public List<int> Satisfaction { get; set; } = new();
}

/// <summary>Something <c>Subject</c> did that <c>Holder</c> remembers: the deed, the step it happened and how strongly (per mille of its value).</summary>
public sealed class DeedState
{
    [JsonRequired] public int Holder { get; set; }
    [JsonRequired] public int Subject { get; set; }
    [JsonRequired] public int Deed { get; set; }
    [JsonRequired] public long Start { get; set; }
    [JsonRequired] public int Scale { get; set; }
}

/// <summary>
/// A treaty in force. For an asymmetric one the first realm is the one that benefits (the overlord, the receiver of tribute, the protected) and the second the one bound. <c>End</c> is -1 for no end. A tribute payment is due at
/// <c>NextDue</c> (-1 for none); once due the treaty waits (<c>Awaiting</c>) for the host to say it was paid until <c>Deadline</c>. <c>War</c> is the war a ceasefire pauses, -1 for none.
/// </summary>
public sealed class TreatyState
{
    [JsonRequired] public long Id { get; set; }
    [JsonRequired] public int Type { get; set; }
    [JsonRequired] public int First { get; set; }
    [JsonRequired] public int Second { get; set; }
    [JsonRequired] public long Start { get; set; }
    [JsonRequired] public long End { get; set; }
    [JsonRequired] public long Amount { get; set; }
    [JsonRequired] public long NextDue { get; set; }
    [JsonRequired] public bool Awaiting { get; set; }
    [JsonRequired] public long Deadline { get; set; }
    [JsonRequired] public int Missed { get; set; }
    [JsonRequired] public long War { get; set; }
}

/// <summary>An offer waiting for an answer. <c>Amount</c> is what the realm it is put to pays the proposer each period (negative: what the proposer pays). <c>Term</c> is in steps, 0 for no end.</summary>
public sealed class ProposalState
{
    [JsonRequired] public long Id { get; set; }
    [JsonRequired] public int Type { get; set; }
    [JsonRequired] public int From { get; set; }
    [JsonRequired] public int To { get; set; }
    [JsonRequired] public long Term { get; set; }
    [JsonRequired] public long Amount { get; set; }
    [JsonRequired] public long Made { get; set; }
    [JsonRequired] public long DecideAt { get; set; }
    [JsonRequired] public long ExpireAt { get; set; }
}

/// <summary>A war. Each side is the realms in it as a bit set of realm indexes. The score is the attacker's, from minus 1,000 to 1,000; exhaustion is each side's, in millionths.</summary>
public sealed class WarState
{
    [JsonRequired] public long Id { get; set; }
    [JsonRequired] public int Attacker { get; set; }
    [JsonRequired] public int Defender { get; set; }
    [JsonRequired] public ulong AttackerSide { get; set; }
    [JsonRequired] public ulong DefenderSide { get; set; }
    [JsonRequired] public int Cb { get; set; }
    [JsonRequired] public long Start { get; set; }
    [JsonRequired] public int Score { get; set; }
    [JsonRequired] public long AttackerExhaustion { get; set; }
    [JsonRequired] public long DefenderExhaustion { get; set; }
    [JsonRequired] public bool Ceasefire { get; set; }
}

/// <summary>A call to arms: <c>Caller</c> was attacked and <c>Callee</c> owes help under <c>Treaty</c> until <c>Due</c>.</summary>
public sealed class CallState
{
    [JsonRequired] public long Id { get; set; }
    [JsonRequired] public long War { get; set; }
    [JsonRequired] public int Caller { get; set; }
    [JsonRequired] public int Callee { get; set; }
    [JsonRequired] public long Treaty { get; set; }
    [JsonRequired] public long Due { get; set; }
}

/// <summary>A casus belli <c>Holder</c> was granted against <c>Target</c> until a step.</summary>
public sealed class GrantState
{
    [JsonRequired] public int Holder { get; set; }
    [JsonRequired] public int Target { get; set; }
    [JsonRequired] public int Cb { get; set; }
    [JsonRequired] public long Until { get; set; }
}

/// <summary>Realms balancing against one target: the members as a bit set, and the step it formed.</summary>
public sealed class CoalitionState
{
    [JsonRequired] public int Target { get; set; }
    [JsonRequired] public ulong Members { get; set; }
    [JsonRequired] public long Since { get; set; }
}

/// <summary>An agent of a realm, where it is and, away, on which mission.</summary>
public sealed class AgentRecord
{
    [JsonRequired] public string Id { get; set; } = "";
    [JsonRequired] public int Owner { get; set; }
    [JsonRequired] public int Skill { get; set; }
    [JsonRequired] public int State { get; set; }
    [JsonRequired] public long Mission { get; set; }
    [JsonRequired] public long FreeAt { get; set; }
}

/// <summary>A mission under way, due at a step.</summary>
public sealed class MissionRecord
{
    [JsonRequired] public long Id { get; set; }
    [JsonRequired] public int Agent { get; set; }
    [JsonRequired] public int Target { get; set; }
    [JsonRequired] public int Type { get; set; }
    [JsonRequired] public long Start { get; set; }
    [JsonRequired] public long Due { get; set; }
}

/// <summary>One line of the diplomatic-incident log: what happened (<see cref="IncidentKind"/>), between whom, and one number whose meaning depends on the kind.</summary>
public sealed class IncidentRecord
{
    [JsonRequired] public long Step { get; set; }
    [JsonRequired] public int Kind { get; set; }
    [JsonRequired] public int A { get; set; }
    [JsonRequired] public int B { get; set; }
    [JsonRequired] public int Index { get; set; }
    [JsonRequired] public long Amount { get; set; }
}

namespace GameRuntime;

/// <summary>What a request asks for. One list, so a component that serves requests, the journal that records them and a script that makes them cannot disagree about which requests exist.</summary>
public enum DiplomacyRequestKind { Propose, Respond, Withdraw, Cancel, Declare, Join, Leave, Score, Fulfil, Mission, Recruit, Release, Grant, Deed, Happen, Succeed, Might, Leader, Evaluate, Standing, Estimate }

/// <summary>The names of the request kinds on the wire and in the journal: one place to turn a kind into its name and a name into its kind.</summary>
public static class DiplomacyRequestNames
{
    public static string NameOf(DiplomacyRequestKind kind) => EngineCommon.EnumNames.Name(kind);

    /// <summary>The kind a name stands for, or null when the name is none of them (the component answers that with a refusal, it does not drop it).</summary>
    public static DiplomacyRequestKind? KindOf(string name) => EngineCommon.EnumNames.Parse<DiplomacyRequestKind>(name);
}

/// <summary>
/// One queued request: the kind as the sender wrote it (it may be none the component knows), the id, three names and two numbers. Propose: from, to, type; term in days, amount. Respond: proposal id in the number, 1 or 0 in
/// the second. Withdraw: proposal id, by in A. Cancel: treaty id, by in A. Declare: attacker, target, casus belli. Join: war id, realm, support. Leave: war id, realm. Score: war id, favoured realm, source; amount in the second.
/// Fulfil: treaty id; 1 paid or 0 not in the second. Mission: agent, target, mission. Recruit: owner, agent id; skill. Release: agent. Grant: holder, target, casus belli; days. Deed: doer, receiver, deed; scale.
/// Happen: realm, event; scale. Succeed: realm. Might: realm; might. Leader: holder, subject; regard. Evaluate: from, to, type; term, amount. Standing: realm, other (empty for the realm alone). Estimate: observer, target.
/// </summary>
internal readonly record struct DiplomacyPending(string Kind, string RequestId, string A, string B, string C, long Number, long Number2);

/// <summary>The one place the diplomacy request event is connected: the handler gets the request channel and the request as a <see cref="DiplomacyPending"/>.</summary>
internal static class DiplomacyRequestPort
{
    public static void Connect(IEventPort events, Action<string, DiplomacyPending> handler) =>
        events.Connect<DiplomacyRequested>(r => handler(r.Channel, new DiplomacyPending(r.Kind, r.RequestId, r.A, r.B, r.C, r.Number, r.Number2)));
}

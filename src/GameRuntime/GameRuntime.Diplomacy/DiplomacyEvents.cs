namespace GameRuntime;

// Events of the diplomacy component. Flat, primitive fields only, so a Godot signal or a journal can carry them unchanged. Names are ids, never text: the host renders them in its locale with DipNames.
//
// Every request names a channel (empty by default) and every answer names the channel and the component it comes from. One request gets one answer: from the component that serves its channel, or, from the
// DiplomacyChannels registry, a refusal with DIP_CHANNEL_UNKNOWN for a channel nobody serves. A request is one event with a kind (see DiplomacyRequestKind), three names and two numbers, so the set of requests is one list.
//
// Order of what one Update raises: the answers to the requests in arrival order (each followed by the changes it made), after what the time of this Update changed (the steps lived, in the order they happened), at most
// the component's maximum per Update; the rest is owed and follows in the next Updates, in the same order, none lost. Everything is deferred: it is delivered after the Update returns. The other layers read three
// kinds of event: DiplomacyEffectIssued (a signal, a shock or a payment to act on), the events of what is happening (treaties, wars, coalitions, missions, politics), and, by facts, the state itself.

/// <summary>A request to the diplomacy: <c>kind</c> is one of the names in <see cref="DiplomacyRequestKind"/>; <c>a</c>, <c>b</c> and <c>c</c> are the ids it takes and the numbers what it counts. Only queued.</summary>
public readonly struct DiplomacyRequested
{
    public DiplomacyRequested(string requestId, string kind, string a = "", string b = "", string c = "", long number = 0, long number2 = 0, string channel = "")
    { RequestId = requestId; Kind = kind; A = a; B = b; C = c; Number = number; Number2 = number2; Channel = channel; }
    public string RequestId { get; }
    public string Kind { get; }
    public string A { get; }
    public string B { get; }
    public string C { get; }
    public long Number { get; }
    public long Number2 { get; }
    public string Channel { get; }
}

/// <summary>A request was applied; the changes it made follow as their own events.</summary>
public readonly struct DiplomacyApplied
{
    public DiplomacyApplied(string source, string channel, string requestId, long tick) { Source = source; Channel = channel; RequestId = requestId; Tick = tick; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
}

/// <summary>A request was refused with this code; the diplomacy is exactly as it was. A request for a channel nobody serves is refused by the registry with <see cref="DiplomacyChannels.UnknownChannel"/>.</summary>
public readonly struct DiplomacyRefused
{
    public DiplomacyRefused(string source, string channel, string requestId, long tick, string code) { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Code = code; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Code { get; }
}

/// <summary>The answer to an evaluation: what the target would make of the offer. <c>score</c> is the acceptance score, <c>chance</c> the chance of yes in per mille, <c>why</c> the lines of how the score was made.</summary>
public readonly struct DiplomacyEvaluated
{
    public DiplomacyEvaluated(string source, string channel, string requestId, long tick, string from, string to, string type, long score, int chance, string why)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; From = from; To = to; Type = type; Score = score; Chance = chance; Why = why; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string From { get; }
    public string To { get; }
    public string Type { get; }
    public long Score { get; }
    public int Chance { get; }
    public string Why { get; }
}

/// <summary>The answer to a standing request: the realm's order, its standing in the world, its might, how many wars and treaties it has, and, when another realm was named, the regard between them.</summary>
public readonly struct DiplomacyStandingAnswered
{
    public DiplomacyStandingAnswered(string source, string channel, string requestId, long tick, string realm, string other, int stability, int legitimacy, long trust, long fear, long honour, long infamy, long might, string band,
        int wars, int treaties, int regard, int reverse, bool atWar)
    {
        Source = source; Channel = channel; RequestId = requestId; Tick = tick; Realm = realm; Other = other; Stability = stability; Legitimacy = legitimacy; Trust = trust; Fear = fear; Honour = honour; Infamy = infamy;
        Might = might; Band = band; Wars = wars; Treaties = treaties; Regard = regard; Reverse = reverse; AtWar = atWar;
    }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Realm { get; }
    public string Other { get; }
    public int Stability { get; }
    public int Legitimacy { get; }
    public long Trust { get; }
    public long Fear { get; }
    public long Honour { get; }
    public long Infamy { get; }
    public long Might { get; }
    public string Band { get; }
    public int Wars { get; }
    public int Treaties { get; }
    public int Regard { get; }
    public int Reverse { get; }
    public bool AtWar { get; }
}

/// <summary>The answer to an estimate request: how well the observer knows the target and the band its might is believed to be in.</summary>
public readonly struct DiplomacyEstimated
{
    public DiplomacyEstimated(string source, string channel, string requestId, long tick, string observer, string target, string level, long low, long estimate, long high)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Observer = observer; Target = target; Level = level; Low = low; Estimate = estimate; High = high; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Observer { get; }
    public string Target { get; }
    public string Level { get; }
    public long Low { get; }
    public long Estimate { get; }
    public long High { get; }
}

/// <summary>The season changed (the first step has <c>from</c> empty). <c>step</c> is the diplomacy's own step, <c>tick</c> the session's.</summary>
public readonly struct DiplomacySeasonChanged
{
    public DiplomacySeasonChanged(string source, string channel, long tick, long step, string season, string from) { Source = source; Channel = channel; Tick = tick; Step = step; Season = season; From = from; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Season { get; }
    public string From { get; }
}

/// <summary>A proposal was made, accepted, refused, left to expire, withdrawn or found impossible. <c>score</c> is the acceptance score when it was decided.</summary>
public readonly struct DiplomacyProposalChanged
{
    public DiplomacyProposalChanged(string source, string channel, long tick, long step, long proposal, string type, string from, string to, string change, long score)
    { Source = source; Channel = channel; Tick = tick; Step = step; Proposal = proposal; Type = type; From = from; To = to; Change = change; Score = score; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public long Proposal { get; }
    public string Type { get; }
    public string From { get; }
    public string To { get; }
    public string Change { get; }
    public long Score { get; }
}

/// <summary>A treaty was signed, ran out, was cancelled, broken or ended by a war or a succession. <c>first</c> is the benefiting party; <c>amount</c> the tribute per payment.</summary>
public readonly struct DiplomacyTreatyChanged
{
    public DiplomacyTreatyChanged(string source, string channel, long tick, long step, long treaty, string type, string first, string second, string change, long amount)
    { Source = source; Channel = channel; Tick = tick; Step = step; Treaty = treaty; Type = type; First = first; Second = second; Change = change; Amount = amount; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public long Treaty { get; }
    public string Type { get; }
    public string First { get; }
    public string Second { get; }
    public string Change { get; }
    public long Amount { get; }
}

/// <summary>An obligation of a treaty: a payment fell due, was paid or missed, or a call to arms was made, answered or ignored. <c>owing</c> owes <c>owed</c>.</summary>
public readonly struct DiplomacyObligationChanged
{
    public DiplomacyObligationChanged(string source, string channel, long tick, long step, long treaty, string clause, string owing, string owed, string change, long amount)
    { Source = source; Channel = channel; Tick = tick; Step = step; Treaty = treaty; Clause = clause; Owing = owing; Owed = owed; Change = change; Amount = amount; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public long Treaty { get; }
    public string Clause { get; }
    public string Owing { get; }
    public string Owed { get; }
    public string Change { get; }
    public long Amount { get; }
}

/// <summary>A war was declared, joined, left, paused, resumed, scored or ended. <c>realm</c> is the realm that joined or left; <c>score</c> the attacker's.</summary>
public readonly struct DiplomacyWarChanged
{
    public DiplomacyWarChanged(string source, string channel, long tick, long step, long war, string attacker, string defender, string change, string casusBelli, long score, string realm)
    { Source = source; Channel = channel; Tick = tick; Step = step; War = war; Attacker = attacker; Defender = defender; Change = change; CasusBelli = casusBelli; Score = score; Realm = realm; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public long War { get; }
    public string Attacker { get; }
    public string Defender { get; }
    public string Change { get; }
    public string CasusBelli { get; }
    public long Score { get; }
    public string Realm { get; }
}

/// <summary>Realms balancing against a target formed a coalition, one joined or left it, or it dissolved. <c>realm</c> is empty for the whole.</summary>
public readonly struct DiplomacyCoalitionChanged
{
    public DiplomacyCoalitionChanged(string source, string channel, long tick, long step, string target, string realm, string change, long members)
    { Source = source; Channel = channel; Tick = tick; Step = step; Target = target; Realm = realm; Change = change; Members = members; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Target { get; }
    public string Realm { get; }
    public string Change { get; }
    public long Members { get; }
}

/// <summary>A mission started or ended (succeeded or failed, exposed or not).</summary>
public readonly struct DiplomacyMissionChanged
{
    public DiplomacyMissionChanged(string source, string channel, long tick, long step, long mission, string type, string owner, string target, string change, string agent)
    { Source = source; Channel = channel; Tick = tick; Step = step; Mission = mission; Type = type; Owner = owner; Target = target; Change = change; Agent = agent; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public long Mission { get; }
    public string Type { get; }
    public string Owner { get; }
    public string Target { get; }
    public string Change { get; }
    public string Agent { get; }
}

/// <summary>An agent was recruited, captured or released.</summary>
public readonly struct DiplomacyAgentChanged
{
    public DiplomacyAgentChanged(string source, string channel, long tick, long step, string agent, string owner, string change)
    { Source = source; Channel = channel; Tick = tick; Step = step; Agent = agent; Owner = owner; Change = change; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Agent { get; }
    public string Owner { get; }
    public string Change { get; }
}

/// <summary>What one realm knows of another moved to another level (none, rumour, report, detailed, complete).</summary>
public readonly struct DiplomacyIntelChanged
{
    public DiplomacyIntelChanged(string source, string channel, long tick, long step, string observer, string target, string level, string before)
    { Source = source; Channel = channel; Tick = tick; Step = step; Observer = observer; Target = target; Level = level; Before = before; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Observer { get; }
    public string Target { get; }
    public string Level { get; }
    public string Before { get; }
}

/// <summary>A realm's order changed band (unrest, revolt, calm) or its ruler changed (succession, contested succession). <c>stability</c> is its stability then.</summary>
public readonly struct DiplomacyPoliticsChanged
{
    public DiplomacyPoliticsChanged(string source, string channel, long tick, long step, string realm, string change, long stability)
    { Source = source; Channel = channel; Tick = tick; Step = step; Realm = realm; Change = change; Stability = stability; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Realm { get; }
    public string Change { get; }
    public long Stability { get; }
}

/// <summary>A casus belli was granted, used or ran out.</summary>
public readonly struct DiplomacyGrantChanged
{
    public DiplomacyGrantChanged(string source, string channel, long tick, long step, string holder, string target, string casusBelli, string change)
    { Source = source; Channel = channel; Tick = tick; Step = step; Holder = holder; Target = target; CasusBelli = casusBelli; Change = change; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Holder { get; }
    public string Target { get; }
    public string CasusBelli { get; }
    public string Change { get; }
}

/// <summary>A line of the diplomatic-incident log: what happened (<c>kind</c>), between whom, and the number the kind gives (<c>index</c> and <c>amount</c> as the log holds them).</summary>
public readonly struct DiplomacyIncidentLogged
{
    public DiplomacyIncidentLogged(string source, string channel, long tick, long step, string kind, string a, string b, long index, long amount)
    { Source = source; Channel = channel; Tick = tick; Step = step; Kind = kind; A = a; B = b; Index = index; Amount = amount; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Kind { get; }
    public string A { get; }
    public string B { get; }
    public long Index { get; }
    public long Amount { get; }
}

/// <summary>
/// What a treaty, a mission or a rule asks of the other layers: <c>kind</c> is <c>signal</c> (a named story event for dialogue, quests and the tutorial), <c>shock</c> (a request for the economy: <c>name</c> is the shock's id,
/// <c>scale</c> per mille, <c>market</c> the realm's market) or <c>payment</c> (<c>realm</c> pays <c>other</c> <c>scale</c> units, from <c>market</c> to <c>otherMarket</c>; the host says whether it was paid). <c>other</c> is empty
/// when the effect has no second realm; <c>origin</c> says what asked, as <c>treaty:alliance</c>, <c>mission:sabotage</c> or <c>politics</c>; <c>treaty</c> is the treaty a payment is for.
/// </summary>
public readonly struct DiplomacyEffectIssued
{
    public DiplomacyEffectIssued(string source, string channel, long tick, long step, string kind, string name, string realm, string other, string market, string otherMarket, long scale, string origin, long treaty)
    { Source = source; Channel = channel; Tick = tick; Step = step; Kind = kind; Name = name; Realm = realm; Other = other; Market = market; OtherMarket = otherMarket; Scale = scale; Origin = origin; Treaty = treaty; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Kind { get; }
    public string Name { get; }
    public string Realm { get; }
    public string Other { get; }
    public string Market { get; }
    public string OtherMarket { get; }
    public long Scale { get; }
    public string Origin { get; }

    /// <summary>The treaty a payment is for, which the host names when it says whether it was paid; -1 for any other effect.</summary>
    public long Treaty { get; }
}

namespace GameRuntime;

// Events of the world-events component. Flat, primitive fields only, so a Godot signal or a journal can carry them unchanged. Names are ids, never text: the host renders them in its locale with WorldNames.
//
// Every request names a channel (empty by default) and every answer names the channel and the component it comes from. One request gets one answer: from the component that serves its channel, or, from the
// WorldChannels registry, a refusal with WE_CHANNEL_UNKNOWN for a channel nobody serves. A request is one event with a kind (see WorldRequestKind) and three names and a number, so the set of requests is one list.
//
// Order of what one Update raises: the answers to the requests in arrival order (each followed by the changes it made), after what the time of this Update changed (the steps lived, in the order they happened), at most the
// component's maximum per Update; the rest is owed and follows in the next Updates, in the same order, none lost. Everything is deferred: it is delivered after the Update returns. The other layers read three kinds of
// event: WorldEffectIssued (a signal or a shock to act on), WorldHazardChanged / WorldOutbreakChanged / WorldWeatherChanged (what is happening), and, by facts, the state itself.

/// <summary>A request to the world: <c>kind</c> is one of the names in <see cref="WorldRequestKind"/>; <c>a</c>, <c>b</c> and <c>c</c> are the ids it takes and <c>number</c> its number. Only queued.</summary>
public readonly struct WorldRequested
{
    public WorldRequested(string requestId, string kind, string a = "", string b = "", string c = "", long number = 1, string channel = "") { RequestId = requestId; Kind = kind; A = a; B = b; C = c; Number = number; Channel = channel; }
    public string RequestId { get; }
    public string Kind { get; }
    public string A { get; }
    public string B { get; }
    public string C { get; }
    public long Number { get; }
    public string Channel { get; }
}

/// <summary>A request was applied; the changes it made follow as their own events.</summary>
public readonly struct WorldApplied
{
    public WorldApplied(string source, string channel, string requestId, long tick) { Source = source; Channel = channel; RequestId = requestId; Tick = tick; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
}

/// <summary>A request was refused with this code; the world is exactly as it was. A request for a channel nobody serves is refused by the registry with <see cref="WorldChannels.UnknownChannel"/>.</summary>
public readonly struct WorldRefused
{
    public WorldRefused(string source, string channel, string requestId, long tick, string code) { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Code = code; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Code { get; }
}

/// <summary>The answer to a weather request: the region's weather now, temperature in hundredths of a degree, precipitation in tenths of a millimetre, wind in tenths of a metre a second, soil water per mille, river and snow stores in tenths of a millimetre.</summary>
public readonly struct WorldWeatherAnswered
{
    public WorldWeatherAnswered(string source, string channel, string requestId, long tick, string region, string weather, long temp, long precip, long wind, long soil, long river, long snow)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Region = region; Weather = weather; Temp = temp; Precip = precip; Wind = wind; Soil = soil; River = river; Snow = snow; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Region { get; }
    public string Weather { get; }
    public long Temp { get; }
    public long Precip { get; }
    public long Wind { get; }
    public long Soil { get; }
    public long River { get; }
    public long Snow { get; }
}

/// <summary>The answer to a disease request: the people of the region in each compartment, and whether the disease is absent, introduced or an outbreak.</summary>
public readonly struct WorldDiseaseAnswered
{
    public WorldDiseaseAnswered(string source, string channel, string requestId, long tick, string region, string disease, long susceptible, long exposed, long infectious, long recovered, long dead, string state)
    { Source = source; Channel = channel; RequestId = requestId; Tick = tick; Region = region; Disease = disease; Susceptible = susceptible; Exposed = exposed; Infectious = infectious; Recovered = recovered; Dead = dead; State = state; }
    public string Source { get; }
    public string Channel { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Region { get; }
    public string Disease { get; }
    public long Susceptible { get; }
    public long Exposed { get; }
    public long Infectious { get; }
    public long Recovered { get; }
    public long Dead { get; }
    public string State { get; }
}

/// <summary>The season changed (the first step of a world has <c>from</c> empty). <c>step</c> is the world's own step, <c>tick</c> the session's.</summary>
public readonly struct WorldSeasonChanged
{
    public WorldSeasonChanged(string source, string channel, long tick, long step, string season, string from) { Source = source; Channel = channel; Tick = tick; Step = step; Season = season; From = from; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Season { get; }
    public string From { get; }
}

/// <summary>The large-scale pattern (the front over everything) changed.</summary>
public readonly struct WorldRegimeChanged
{
    public WorldRegimeChanged(string source, string channel, long tick, long step, string regime, string from) { Source = source; Channel = channel; Tick = tick; Step = step; Regime = regime; From = from; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Regime { get; }
    public string From { get; }
}

/// <summary>A region's weather changed to another weather; <c>temp</c> is in hundredths of a degree.</summary>
public readonly struct WorldWeatherChanged
{
    public WorldWeatherChanged(string source, string channel, long tick, long step, string region, string weather, string from, long temp) { Source = source; Channel = channel; Tick = tick; Step = step; Region = region; Weather = weather; From = from; Temp = temp; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Region { get; }
    public string Weather { get; }
    public string From { get; }
    public long Temp { get; }
}

/// <summary>A hazard started, escalated, began to recede or ended in a region. <c>kind</c> is the hazard's kind (flood, fire, storm, quake, drought, cold, other), <c>severity</c> the rung's id, <c>power</c> per mille of full strength (less where it spread to).</summary>
public readonly struct WorldHazardChanged
{
    public WorldHazardChanged(string source, string channel, long tick, long step, string region, string hazard, string kind, string severity, string change, int power)
    { Source = source; Channel = channel; Tick = tick; Step = step; Region = region; Hazard = hazard; Kind = kind; Severity = severity; Change = change; Power = power; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Region { get; }
    public string Hazard { get; }
    public string Kind { get; }
    public string Severity { get; }
    public string Change { get; }
    public int Power { get; }
}

/// <summary>A disease was introduced into a region, became an outbreak, was contained or ended. <c>infected</c> are the exposed and infectious people, <c>permille</c> their share of the region.</summary>
public readonly struct WorldOutbreakChanged
{
    public WorldOutbreakChanged(string source, string channel, long tick, long step, string region, string disease, string change, long infected, long permille)
    { Source = source; Channel = channel; Tick = tick; Step = step; Region = region; Disease = disease; Change = change; Infected = infected; Permille = permille; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Region { get; }
    public string Disease { get; }
    public string Change { get; }
    public long Infected { get; }
    public long Permille { get; }
}

/// <summary>A card was played (drawn by the director, chained from another, or forced by a call). <c>region</c> is empty for a global card; <c>stress</c> the director's after it.</summary>
public readonly struct WorldCardPlayed
{
    public WorldCardPlayed(string source, string channel, long tick, long step, string card, string region, string tier, string reason, long stress)
    { Source = source; Channel = channel; Tick = tick; Step = step; Card = card; Region = region; Tier = tier; Reason = reason; Stress = stress; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Card { get; }
    public string Region { get; }
    public string Tier { get; }
    public string Reason { get; }
    public long Stress { get; }
}

/// <summary>The director moved between building, peak and relax.</summary>
public readonly struct WorldDirectorChanged
{
    public WorldDirectorChanged(string source, string channel, long tick, long step, string from, string to, long stress) { Source = source; Channel = channel; Tick = tick; Step = step; From = from; To = to; Stress = stress; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string From { get; }
    public string To { get; }
    public long Stress { get; }
}

/// <summary>
/// What a hazard, an outbreak or a card asks of the other layers: <c>kind</c> is <c>signal</c> (a named story event for dialogue, quests and the tutorial) or <c>shock</c> (a request for the economy: <c>name</c> is the shock's id,
/// <c>scale</c> per mille, <c>market</c> the region's market). <c>region</c> is empty for the whole world; <c>origin</c> says what asked, as <c>hazard:flood</c>, <c>disease:plague</c> or <c>card:raiders</c>.
/// </summary>
public readonly struct WorldEffectIssued
{
    public WorldEffectIssued(string source, string channel, long tick, long step, string kind, string name, string region, string market, long scale, string origin)
    { Source = source; Channel = channel; Tick = tick; Step = step; Kind = kind; Name = name; Region = region; Market = market; Scale = scale; Origin = origin; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Kind { get; }
    public string Name { get; }
    public string Region { get; }
    public string Market { get; }
    public long Scale { get; }
    public string Origin { get; }
}

/// <summary>An intervention started or stopped in a region; <c>automatic</c> when its own trigger did it.</summary>
public readonly struct WorldInterventionChanged
{
    public WorldInterventionChanged(string source, string channel, long tick, long step, string region, string intervention, bool on, bool automatic)
    { Source = source; Channel = channel; Tick = tick; Step = step; Region = region; Intervention = intervention; On = on; Automatic = automatic; }
    public string Source { get; }
    public string Channel { get; }
    public long Tick { get; }
    public long Step { get; }
    public string Region { get; }
    public string Intervention { get; }
    public bool On { get; }
    public bool Automatic { get; }
}

namespace GameRuntime;

// Events of the AI component. Flat, primitive fields only, so a Godot signal or a journal can carry them unchanged. A session can hold several AI components,
// so every request names the component it is for (its id) and every answer names the component it comes from.
// Values travel as "name=value" entries joined by ';' (see AiValues): numbers in the invariant culture, true or false, and text with % ; = written as %25 %3B %3D.

/// <summary>A request to add an agent running a behaviour. Only queued; the AI component applies it in its next Update.</summary>
public readonly struct AiAgentAddRequested
{
    public AiAgentAddRequested(string component, string requestId, string agent, string behavior) { Component = component; RequestId = requestId; Agent = agent; Behavior = behavior; }
    public string Component { get; }
    public string RequestId { get; }
    public string Agent { get; }
    public string Behavior { get; }
}

/// <summary>A request to remove an agent.</summary>
public readonly struct AiAgentRemoveRequested
{
    public AiAgentRemoveRequested(string component, string requestId, string agent) { Component = component; RequestId = requestId; Agent = agent; }
    public string Component { get; }
    public string RequestId { get; }
    public string Agent { get; }
}

/// <summary>What an agent is told about one thing (an update if it knows it already). <see cref="Owner"/> is empty for a kind without owners.</summary>
public readonly struct AiPerceptPosted
{
    public AiPerceptPosted(string component, string requestId, string agent, string kind, string id, string owner, string attrs) { Component = component; RequestId = requestId; Agent = agent; Kind = kind; Id = id; Owner = owner; Attrs = attrs; }
    public string Component { get; }
    public string RequestId { get; }
    public string Agent { get; }
    public string Kind { get; }
    public string Id { get; }
    public string Owner { get; }
    public string Attrs { get; }
}

/// <summary>An agent is told a thing is gone.</summary>
public readonly struct AiPerceptLost
{
    public AiPerceptLost(string component, string requestId, string agent, string kind, string id) { Component = component; RequestId = requestId; Agent = agent; Kind = kind; Id = id; }
    public string Component { get; }
    public string RequestId { get; }
    public string Agent { get; }
    public string Kind { get; }
    public string Id { get; }
}

/// <summary>A key the host owns is written. <see cref="Value"/> is the plain value, not an entry.</summary>
public readonly struct AiKeyPosted
{
    public AiKeyPosted(string component, string requestId, string agent, string key, string value) { Component = component; RequestId = requestId; Agent = agent; Key = key; Value = value; }
    public string Component { get; }
    public string RequestId { get; }
    public string Agent { get; }
    public string Key { get; }
    public string Value { get; }
}

/// <summary>What an agent asks the host to do. <see cref="Args"/> are entries of the intent arguments. The host (or the component that owns the concern) decides what it means.</summary>
public readonly struct AiIntentRaised
{
    public AiIntentRaised(string source, long tick, string agent, string node, string name, string args) { Source = source; Tick = tick; Agent = agent; Node = node; Name = name; Args = args; }

    /// <summary>The id of the AI component whose agent decided.</summary>
    public string Source { get; }
    public long Tick { get; }
    public string Agent { get; }
    public string Node { get; }
    public string Name { get; }
    public string Args { get; }
}

/// <summary>The AI component finished a tick in which agents thought: how many, the work units spent, how many were due and had to wait, and the longest any waited.</summary>
public readonly struct AiStepDone
{
    public AiStepDone(long tick, string source, int thought, long spent, int deferred, long longestWait, int intents)
    { Tick = tick; Source = source; Thought = thought; Spent = spent; Deferred = deferred; LongestWait = longestWait; Intents = intents; }
    public long Tick { get; }

    /// <summary>The id of the AI component that thought, so a game with several tells their steps apart.</summary>
    public string Source { get; }
    public int Thought { get; }
    public long Spent { get; }
    public int Deferred { get; }
    public long LongestWait { get; }
    public int Intents { get; }
}

/// <summary>A request was refused with this code; the agents are exactly as they were.</summary>
public readonly struct AiRefused
{
    public AiRefused(string source, string requestId, long tick, string code) { Source = source; RequestId = requestId; Tick = tick; Code = code; }

    /// <summary>The id of the AI component that refused.</summary>
    public string Source { get; }
    public string RequestId { get; }
    public long Tick { get; }
    public string Code { get; }
}
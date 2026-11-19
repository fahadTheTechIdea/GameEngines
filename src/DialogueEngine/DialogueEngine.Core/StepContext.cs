using ScenarioEngine.Contracts;
using EngineCommon;

namespace DialogueEngine;

/// <summary>
/// One call into the runtime. Every change goes through here and is written to an undo log, so a call that is refused part-way (a missing fact, an
/// illegal quest move) leaves the state exactly as it found it. The only way out of a refusal is the rollback.
/// </summary>
internal sealed class StepContext(DialogueSet set, DialogueState state, IReadOnlyDictionary<string, FactValue> external, ConversationDef? conversation)
{
    /// <summary>The most a node's visit count can reach; the document declares <c>visits.*</c> facts from 0 to this.</summary>
    public const int MaxVisits = 1_000_000_000;

    private readonly List<Action> _undo = new();

    /// <summary>The number of history entries ever recorded when this call began: entries from this turn on are this call's.</summary>
    private readonly long _firstTurn = state.Recorded;

    public DialogueSet Set { get; } = set;
    public DialogueState State { get; } = state;
    public ConversationDef? Conversation { get; set; } = conversation;

    /// <summary>True while the node being entered is one that was waiting and is shown again after an interruption, so it is not a new visit.</summary>
    public bool Reentering { get; set; }
    public List<DialogueEvent> Events { get; } = new();
    public TraceRecorder? Trace { get; set; }

    public void Rollback()
    {
        for (var i = _undo.Count - 1; i >= 0; i--) _undo[i]();
        _undo.Clear();
    }

    // ---- facts ------------------------------------------------------------------------------------------------------------------------

    public FactValue Fact(string id)
    {
        if (id.StartsWith("var.", StringComparison.Ordinal)) return State.VariableValues[id[4..]];
        if (id.StartsWith("visits.", StringComparison.Ordinal)) return FactValue.From((long)State.VisitCounts.GetValueOrDefault(id[7..]));
        if (id.StartsWith("quest.", StringComparison.Ordinal))
        {
            var dot = id.LastIndexOf('.');
            var progress = State.QuestValues[id[6..dot]];
            return id[(dot + 1)..] == "status" ? FactValue.From(QuestStatuses.Name(progress.Status)) : FactValue.From(progress.Stage);
        }
        if (id.StartsWith("ext.", StringComparison.Ordinal)) return External(id[4..]);
        throw new EngineRefusedException(DialogueCodes.Reference, "unknown fact '" + id + "'");
    }

    private FactValue External(string name)
    {
        if (!Set.Externals.TryGetValue(name, out var definition)) throw new EngineRefusedException(DialogueCodes.Reference, "unknown external fact '" + name + "'");
        if (!external.TryGetValue("ext." + name, out var value)) throw new EngineRefusedException(DialogueCodes.FactMissing, "the host did not supply 'ext." + name + "'");
        if (value.Kind != definition.Kind) throw new EngineRefusedException(DialogueCodes.FactInvalid, "'ext." + name + "' is " + value.Kind + ", declared " + definition.Kind);
        if (value.Kind == FactKind.Integer && (value.Integer < definition.Minimum || value.Integer > definition.Maximum))
            throw new EngineRefusedException(DialogueCodes.FactInvalid, "'ext." + name + "' is " + value.Integer + ", outside " + definition.Minimum + " to " + definition.Maximum);
        return value;
    }

    public bool Test(Guard guard)
    {
        var facts = new Dictionary<string, FactValue>(guard.Facts.Count, StringComparer.Ordinal);
        foreach (var id in guard.Facts) facts[id] = Fact(id);
        return Conditions.Evaluate(guard.Condition, facts);
    }

    /// <summary>Why a guard held or did not: the condition with the value each fact has now. Only called while a call is being explained.</summary>
    public string Explain(Guard guard)
    {
        var facts = new Dictionary<string, FactValue>(guard.Facts.Count, StringComparer.Ordinal);
        foreach (var id in guard.Facts) facts[id] = Fact(id);
        return ConditionExplanation.Describe(guard.Condition, facts);
    }

    // ---- changes ----------------------------------------------------------------------------------------------------------------------

    public void SetVariable(VariableDef definition, FactValue value)
    {
        var old = State.VariableValues[definition.Id];
        if (old == value) return;
        State.VariableValues[definition.Id] = value;
        _undo.Add(() => State.VariableValues[definition.Id] = old);
        Events.Add(new VariableEvent(definition.Id, value));
    }

    public void SetQuest(QuestDef quest, QuestProgress progress)
    {
        var old = State.QuestValues[quest.Id];
        State.QuestValues[quest.Id] = progress;
        _undo.Add(() => State.QuestValues[quest.Id] = old);
        Events.Add(new QuestEvent(quest.Id, progress.Status, progress.Stage));
    }

    public void Visit(string conversation, string node)
    {
        var key = conversation + "/" + node;
        var count = State.VisitCounts.GetValueOrDefault(key);
        EnteredBefore = count;
        State.VisitCounts[key] = Math.Min(count + 1, MaxVisits);   // a count stops at the bound the visits.* facts are declared with, so no condition ever reads a value outside its declared range
        _undo.Add(() => { if (count == 0) State.VisitCounts.Remove(key); else State.VisitCounts[key] = count; });
    }

    /// <summary>How many times the node being entered was entered before this entry, taken when the runtime counted the entry (so no second key is built). Not set for a node shown again after an interruption, which is not an entry.</summary>
    public long EnteredBefore { get; private set; }

    public void PushFrame(DialogueFrame frame)
    {
        State.FrameList.Add(frame);
        _undo.Add(() => State.FrameList.RemoveAt(State.FrameList.Count - 1));
    }

    public DialogueFrame PopFrame()
    {
        var frame = State.FrameList[^1];
        State.FrameList.RemoveAt(State.FrameList.Count - 1);
        _undo.Add(() => State.FrameList.Add(frame));
        return frame;
    }

    public void ClearFrames()
    {
        var old = State.FrameList.ToArray();
        State.FrameList.Clear();
        _undo.Add(() => State.FrameList.AddRange(old));
    }

    public long NextRoll()
    {
        var index = State.RollCounter;
        State.RollCounter = checked(index + 1);
        _undo.Add(() => State.RollCounter = index);
        return index;
    }

    public void SetCursor(string? conversation, string? node, Waiting waiting)
    {
        var (c, n, w) = (State.Conversation, State.Node, State.WaitingFor);
        State.Conversation = conversation;
        State.Node = node;
        State.WaitingFor = waiting;
        _undo.Add(() => { State.Conversation = c; State.Node = n; State.WaitingFor = w; });
    }

    // ---- priority, queue and history -----------------------------------------------------------------------------------------------------

    /// <summary>The number the host gave the runtime for this call; written on history entries, never read from a clock.</summary>
    public long Tick { get; init; }

    /// <summary>Takes back the visit that was just counted: a line passed by a content warning was not shown, so it was not seen.</summary>
    public void Unvisit(string conversation, string node)
    {
        var key = conversation + "/" + node;
        var count = State.VisitCounts[key];
        if (count <= 1) State.VisitCounts.Remove(key); else State.VisitCounts[key] = count - 1;
        // No undo of its own: the undo of the Visit it takes back restores the count that was there before that Visit, which is the count a rollback needs.

    }

    public void Enqueue(string conversation)
    {
        State.QueueList.Add(conversation);
        _undo.Add(() => State.QueueList.RemoveAt(State.QueueList.Count - 1));
    }

    /// <summary>Takes the next conversation out of the queue: the highest priority, and among equals the one queued first.</summary>
    public string Dequeue()
    {
        var best = 0;
        for (var i = 1; i < State.QueueList.Count; i++)
            if (Set.Conversations[State.QueueList[i]].Priority > Set.Conversations[State.QueueList[best]].Priority) best = i;
        var name = State.QueueList[best];
        State.QueueList.RemoveAt(best);
        var index = best;
        _undo.Add(() => State.QueueList.Insert(index, name));
        return name;
    }

    /// <summary>Whether the document keeps a history at all; with none, a node does not even build the entry it would have recorded.</summary>
    public bool RecordsHistory => Set.HistoryLimit > 0;

    /// <summary>Keeps an entry, pushing the oldest out when the document's limit is reached. Only called when <see cref="RecordsHistory"/>.</summary>
    public void Record(HistoryEntry entry)
    {
        var limit = Set.HistoryLimit;
        State.HistoryList.Add(entry);
        var oldTurn = State.Recorded;
        State.Recorded = checked(oldTurn + 1);
        HistoryEntry? pushedOut = null;
        if (State.HistoryList.Count > limit)
        {
            pushedOut = State.HistoryList[0];
            State.HistoryList.RemoveFirst();
        }
        _undo.Add(() =>
        {
            if (pushedOut is not null) State.HistoryList.InsertFirst(pushedOut);
            State.HistoryList.RemoveLast();
            State.Recorded = oldTurn;
        });
    }

    /// <summary>The next turn number an entry recorded now will have.</summary>
    public long NextTurn => State.Recorded;

    /// <summary>
    /// Marks the newest history entry as passed by the skip-seen rule, when it is the entry of this line and was recorded in this very call. That last condition is why it needs no undo: a refused call
    /// takes back every entry it recorded, the marked one with them (a line shown again after a preemption was recorded by an earlier call and is not marked).
    /// </summary>
    public void MarkLastSkipped(string conversation, string node)
    {
        if (State.HistoryList.Count == 0) return;
        var at = State.HistoryList.Count - 1;
        var old = State.HistoryList[at];
        if (old.Turn < _firstTurn || old.Kind != HistoryKind.Line || old.Skipped || old.Conversation != conversation || old.Node != node) return;
        State.HistoryList[at] = old with { Skipped = true };
    }
}

using EngineCommon;
using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>
/// Runs conversations and quests of one document. It has no clock and no world: the host asks for the next step and supplies the external facts a
/// step needs, and gets back what to show and what happened. Every call is all-or-nothing: a refusal leaves the state as it was.
/// </summary>
/// <remarks>
/// One conversation is open at a time, with the calls, interruptions and preemptions it is inside. What starting another one does while a conversation is open is the starting conversation's
/// own rule (<see cref="BusyPolicy"/>): refuse, wait in a bounded queue, or break into the open one when its priority is strictly higher and the open one allows it. A preempted conversation is
/// put back, exactly where it was, when its preempter finishes (a stack of frames, which is how Ink's tunnels and Yarn Spinner's detours come back; the priority, queue and preempt rules themselves are this engine's own design, see docs/DIALOGUE_ENGINE.md section 9); queued ones start when
/// everything open has finished.
/// </remarks>
public sealed partial class DialogueRuntime
{
    /// <summary>Nodes that do not wait the engine will pass through in one call before it refuses. The loader already refuses a document with a cycle of them; this is the guard behind it.</summary>
    public const int SilentLimit = 64;

    /// <summary>How many times a quest may change stage in one update before the engine calls it a loop.</summary>
    public const int QuestChainLimit = 8;

    /// <summary>How deep calls, interruptions and preemptions may nest.</summary>
    public const int CallDepthLimit = 8;

    /// <summary>How many conversations may wait in the queue.</summary>
    public const int QueueLimit = 8;

    /// <summary>The most already-seen lines one skip passes before it stops and shows the line it reached.</summary>
    public const int SkipLimit = 256;

    public DialogueRuntime(DialogueSet set, ulong seed) : this(set, DialogueState.Initial(set, seed)) { }

    internal DialogueRuntime(DialogueSet set, DialogueState state)
    {
        Set = set;
        State = state;
    }

    public DialogueSet Set { get; private set; }
    public DialogueState State { get; private set; }
    public bool InConversation => State.Conversation is not null;

    /// <summary>The number written on the history entries the next call records (the host's tick). The engine never reads a clock; it only writes what the host says.</summary>
    public long Tick { get; set; }

    /// <summary>The conversation the current level of the stack belongs to: the one that was started or that preempted (not a callee inside it); null when nothing is open.</summary>
    public string? RootConversation => State.Conversation is null ? null : LevelRoot(State, State.FrameList.FindLastIndex(f => f.Preempted));

    private static string LevelRoot(DialogueState state, int lastPreempt) => state.FrameList.Count > lastPreempt + 1 ? state.FrameList[lastPreempt + 1].Conversation : state.Conversation!;

    /// <summary>
    /// Starts a conversation. With nothing open it runs to the first line or choice. While one is open the starting conversation's <see cref="BusyPolicy"/> decides: refuse
    /// (<see cref="DialogueCodes.Busy"/>), queue (a <see cref="StepKind.Queued"/> step; <see cref="DialogueCodes.QueueFull"/> or <see cref="DialogueCodes.AlreadyQueued"/> when it cannot be), or
    /// preempt (<see cref="DialogueCodes.Priority"/> unless its priority is strictly higher than the open conversation's, <see cref="DialogueCodes.NotInterruptible"/> if that one forbids it).
    /// </summary>
    public DialogueStep Start(string conversation, IReadOnlyDictionary<string, FactValue> external)
    {
        Set.Conversations.TryGetValue(conversation, out var def);
        if (State.Conversation is not null)
        {
            if (def is null) throw new EngineRefusedException(DialogueCodes.Busy, "conversation '" + State.Conversation + "' is open");
            switch (def.OnBusy)
            {
                case BusyPolicy.Refuse: throw new EngineRefusedException(DialogueCodes.Busy, "conversation '" + State.Conversation + "' is open");
                case BusyPolicy.Queue: return Execute(external, def, context => Enqueue(context, def));
                case BusyPolicy.Preempt: return Preempt(def, external);
            }
        }
        if (def is null) throw new EngineRefusedException(DialogueCodes.NoConversation, "there is no conversation '" + conversation + "'");
        return Execute(external, def, context => Run(context, def, def.Start));
    }

    private static DialogueStep Enqueue(StepContext context, ConversationDef def)
    {
        var queue = context.State.QueueList;
        if (queue.Contains(def.Id)) throw new EngineRefusedException(DialogueCodes.AlreadyQueued, "conversation '" + def.Id + "' is already in the queue");
        if (queue.Count >= QueueLimit) throw new EngineRefusedException(DialogueCodes.QueueFull, "the queue already holds " + QueueLimit + " conversations");
        var place = 1 + queue.Count(q => context.Set.Conversations[q].Priority >= def.Priority);
        context.Enqueue(def.Id);
        context.Events.Add(new ConversationEvent(ConversationChange.Queued, def.Id, "", place));
        return new DialogueStep(StepKind.Queued, null, null, def.Id, Array.Empty<DialogueEvent>());
    }

    private DialogueStep Preempt(ConversationDef def, IReadOnlyDictionary<string, FactValue> external)
    {
        var rootName = RootConversation!;
        var root = Set.Conversations[rootName];
        if (def.Priority <= root.Priority) throw new EngineRefusedException(DialogueCodes.Priority, "'" + def.Id + "' (priority " + def.Priority + ") does not outrank the open '" + rootName + "' (priority " + root.Priority + ")");
        if (!root.Interruptible) throw new EngineRefusedException(DialogueCodes.NotInterruptible, "'" + rootName + "' cannot be broken into");
        return Execute(external, def, context =>
        {
            if (context.State.FrameList.Count >= CallDepthLimit) throw new EngineRefusedException(DialogueCodes.CallDepth, "calls, interruptions and preemptions nest deeper than " + CallDepthLimit);
            context.PushFrame(new DialogueFrame(context.State.Conversation!, context.State.Node!, true, true));
            context.Events.Add(new ConversationEvent(ConversationChange.Preempted, rootName, def.Id, 0));
            return Run(context, def, def.Start);
        });
    }

    /// <summary>After a line: goes on to whatever follows it.</summary>
    public DialogueStep Next(IReadOnlyDictionary<string, FactValue> external)
    {
        var (def, node) = Cursor(Waiting.Line);
        return Execute(external, def, context => Run(context, def, ((LineNode)node).Next));
    }

    /// <summary>After a choice was offered: takes the answer.</summary>
    public DialogueStep Choose(string option, IReadOnlyDictionary<string, FactValue> external)
    {
        var (def, node) = Cursor(Waiting.Choice);
        return Execute(external, def, context =>
        {
            var outcome = ((ChoiceNode)node).Choose(context, option);
            return Run(context, def, outcome.Next!);
        });
    }

    private (ConversationDef, Node) Cursor(Waiting expected)
    {
        if (State.WaitingFor != expected || State.Conversation is null) throw new EngineRefusedException(DialogueCodes.NotWaiting, "the session is waiting for " + State.WaitingFor + ", not " + expected);
        var def = Set.Conversations[State.Conversation];
        return (def, def.Nodes[State.Node!]);
    }

    /// <summary>
    /// Runs from a node until the conversation has to wait or ends. A call goes into another conversation and a return comes back to the node after the call (or, after an interruption, to
    /// the node that was waiting, shown again without counting another visit). Nodes that do not wait are passed through up to <see cref="SilentLimit"/>.
    /// </summary>
    private DialogueStep Run(StepContext context, ConversationDef conversation, string first, bool countFirstVisit = true)
    {
        context.Conversation = conversation;
        context.SetCursor(conversation.Id, first, Waiting.Nothing);
        var id = first;
        var skipVisit = !countFirstVisit;
        for (var passed = 0; passed <= SilentLimit; passed++)
        {
            var node = conversation.Nodes[id];
            context.Reentering = skipVisit;
            if (!skipVisit) context.Visit(conversation.Id, node.Id);
            skipVisit = false;
            context.Trace?.Entered(conversation.Id, node);
            var outcome = node.Enter(context);
            switch (outcome.Kind)
            {
                case OutcomeKind.Continue:
                    id = outcome.Next!;
                    continue;
                case OutcomeKind.Wait:
                    return outcome.Step!;
                case OutcomeKind.Finish:
                    if (Leave(context, outcome.Result, ref conversation, ref id, ref skipVisit)) { passed = -1; continue; }
                    return Finish(context, outcome.Result);
                case OutcomeKind.Call:
                    if (context.State.FrameList.Count >= CallDepthLimit) throw new EngineRefusedException(DialogueCodes.CallDepth, "calls and interruptions nest deeper than " + CallDepthLimit);
                    context.PushFrame(new DialogueFrame(conversation.Id, node.Id, false));
                    conversation = Set.Conversations[outcome.Caller!.Conversation];
                    context.Conversation = conversation;
                    id = conversation.Start;
                    context.SetCursor(conversation.Id, id, Waiting.Nothing);
                    continue;
                case OutcomeKind.Return:
                    if (context.State.FrameList.Count == 0 || context.State.FrameList[^1].Preempted)
                    {
                        // The conversation at this level returned from its top: it is over, like an end.
                        if (Leave(context, outcome.Result, ref conversation, ref id, ref skipVisit)) { passed = -1; continue; }
                        return Finish(context, outcome.Result);
                    }
                    var frame = context.PopFrame();
                    conversation = Set.Conversations[frame.Conversation];
                    context.Conversation = conversation;
                    skipVisit = frame.Reenter;
                    id = frame.Reenter ? frame.Node : ((CallNode)conversation.Nodes[frame.Node]).Next;
                    context.SetCursor(conversation.Id, id, Waiting.Nothing);
                    continue;
            }
        }
        throw new EngineRefusedException(DialogueCodes.StepLimit, "more than " + SilentLimit + " nodes passed without waiting");
    }

    /// <summary>
    /// A conversation at the top of its level has ended. If it had preempted another, that one is put back where it was (the stack is cut down to and including its preempt frame, which also drops
    /// the calls and interruptions inside the ended one); otherwise, if conversations are queued, the next one starts. Returns false when nothing follows and the whole session is over.
    /// The silent-node budget starts again for whatever follows, which is why the loader checks each segment on its own.
    /// </summary>
    private bool Leave(StepContext context, string? result, ref ConversationDef conversation, ref string id, ref bool skipVisit)
    {
        var frames = context.State.FrameList;
        var last = frames.FindLastIndex(f => f.Preempted);
        var ending = LevelRoot(context.State, last);
        if (last >= 0)
        {
            DialogueFrame frame = default;
            while (frames.Count > last) frame = context.PopFrame();
            context.Events.Add(new ConversationEvent(ConversationChange.Ended, ending, result ?? "", 0));
            conversation = Set.Conversations[frame.Conversation];
            context.Conversation = conversation;
            id = frame.Node;
            skipVisit = true;
            context.SetCursor(conversation.Id, id, Waiting.Nothing);
            context.Events.Add(new ConversationEvent(ConversationChange.Resumed, LevelRoot(context.State, frames.FindLastIndex(f => f.Preempted)), ending, 0));
            return true;
        }
        if (context.State.QueueList.Count == 0) return false;
        context.ClearFrames();
        var next = Set.Conversations[context.Dequeue()];
        context.Events.Add(new ConversationEvent(ConversationChange.Ended, ending, result ?? "", 0));
        context.Events.Add(new ConversationEvent(ConversationChange.Started, next.Id, ending, 0));
        conversation = next;
        context.Conversation = next;
        id = next.Start;
        skipVisit = false;
        context.SetCursor(next.Id, id, Waiting.Nothing);
        return true;
    }

    private static DialogueStep Finish(StepContext context, string? result)
    {
        var root = context.State.Conversation is null ? null : LevelRoot(context.State, context.State.FrameList.FindLastIndex(f => f.Preempted));
        context.ClearFrames();
        context.SetCursor(null, null, Waiting.Nothing);
        return new DialogueStep(StepKind.Ended, null, null, result, Array.Empty<DialogueEvent>()) { EndedConversation = root };
    }

    /// <summary>
    /// The host's timer ran out on a choice that has a timeout: takes the answer the choice names for it, as if the player had picked it. Refused when the session is not at a choice
    /// or the choice has no timeout.
    /// </summary>
    public DialogueStep Timeout(IReadOnlyDictionary<string, FactValue> external)
    {
        var (def, node) = Cursor(Waiting.Choice);
        var choice = (ChoiceNode)node;
        if (choice.Timeout is null) throw new EngineRefusedException(DialogueCodes.NoTimeout, "choice '" + choice.Id + "' has no timeout");
        return Execute(external, def, context => Run(context, def, choice.Choose(context, choice.Timeout.Option).Next!));
    }

    /// <summary>
    /// Breaks into the open conversation with another one (a shout, an alarm) while it waits at a line or a choice. The interrupter runs; when it ends with <c>return</c> the waiting line or
    /// choice is shown again, without another visit count. If it ends with <c>end</c> the whole conversation ends.
    /// </summary>
    public DialogueStep Interrupt(string conversation, IReadOnlyDictionary<string, FactValue> external)
    {
        if (State.Conversation is null || State.WaitingFor == Waiting.Nothing) throw new EngineRefusedException(DialogueCodes.NotWaiting, "there is no line or choice waiting to be interrupted");
        if (!Set.Conversations.TryGetValue(conversation, out var def)) throw new EngineRefusedException(DialogueCodes.NoConversation, "there is no conversation '" + conversation + "'");
        return Execute(external, def, context =>
        {
            if (context.State.FrameList.Count >= CallDepthLimit) throw new EngineRefusedException(DialogueCodes.CallDepth, "calls and interruptions nest deeper than " + CallDepthLimit);
            context.PushFrame(new DialogueFrame(context.State.Conversation!, context.State.Node!, true));
            return Run(context, def, def.Start);
        });
    }
    private DialogueStep Execute(IReadOnlyDictionary<string, FactValue> external, ConversationDef? conversation, Func<StepContext, DialogueStep> body)
    {
        ArgumentNullException.ThrowIfNull(external);
        var recorder = Explaining ? new TraceRecorder() : null;
        var context = new StepContext(Set, State, external, conversation) { Trace = recorder, Tick = Tick };
        try
        {
            var step = body(context);
            if (recorder is not null) LastTrace = recorder.Finish(null);
            return step with { Events = context.Events.ToArray() };
        }
        catch (Exception exception)
        {
            // Any failure, refusal or not, puts the state back; the exception goes on to the caller unchanged.
            if (recorder is not null) LastTrace = recorder.Finish(exception is EngineRefusedException refusal ? refusal.Code : exception.GetType().Name);
            context.Rollback();
            throw;
        }
    }

    /// <summary>While true, each call records why it did what it did in <see cref="LastTrace"/>. Recording changes no outcome, state or save.</summary>
    public bool Explaining { get; set; }

    /// <summary>The trace of the last call made while <see cref="Explaining"/> was on, refused calls included; null before any.</summary>
    public DialogueTrace? LastTrace { get; private set; }

    /// <summary>
    /// Why each answer of the open choice is available, shown disabled or hidden, with the condition and the values it read. Changes nothing; refused when the session is not at a choice.
    /// </summary>
    public IReadOnlyList<OptionExplanation> ExplainChoice(IReadOnlyDictionary<string, FactValue> external)
    {
        ArgumentNullException.ThrowIfNull(external);
        var (def, node) = Cursor(Waiting.Choice);
        return ((ChoiceNode)node).Explain(new StepContext(Set, State, external, def));
    }

    // ---- quests ------------------------------------------------------------------------------------------------------------------------

    /// <summary>Starts a quest from the host (a game event) instead of from a conversation.</summary>
    public IReadOnlyList<DialogueEvent> StartQuest(string quest, IReadOnlyDictionary<string, FactValue> external)
    {
        if (!Set.Quests.TryGetValue(quest, out var def)) throw new EngineRefusedException(DialogueCodes.Reference, "there is no quest '" + quest + "'");
        return Execute(external, null, context => { new StartQuestEffect(def).Apply(context); return Empty(); }).Events;
    }

    /// <summary>
    /// Looks at every active quest, in id order, and moves it along its stage rules while one holds, up to a bounded number of moves. The rules read
    /// facts (dialogue variables, other quests, the host external facts such as an objective status); nothing here reads or stores an objective.
    /// </summary>
    public IReadOnlyList<DialogueEvent> UpdateQuests(IReadOnlyDictionary<string, FactValue> external) =>
        Execute(external, null, context =>
        {
            foreach (var quest in Set.Quests.Values.OrderBy(q => q.Id, StringComparer.Ordinal))
            {
                for (var moves = 0; context.State.QuestValues[quest.Id].Status == QuestStatus.Active; moves++)
                {
                    var rule = quest.Stage(context.State.QuestValues[quest.Id].Stage).Advance.FirstOrDefault(r => context.Test(r.When));
                    if (rule is null) break;
                    if (moves >= QuestChainLimit) throw new EngineRefusedException(DialogueCodes.QuestLoop, "quest '" + quest.Id + "' changed stage more than " + QuestChainLimit + " times in one update");
                    Quests.Enter(context, quest, rule.To);
                }
            }
            return Empty();
        }).Events;

    private static DialogueStep Empty() => new(StepKind.Ended, null, null, null, Array.Empty<DialogueEvent>());

    // ---- reload ------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Swaps the document for an edited one while the session goes on. It is accepted only if the state still belongs to the new document: the
    /// variables and quests it holds exist with the same types and stages, and an open conversation is still at a node of the same kind.
    /// New variables start at their initial value, new quests not started, and counts of removed nodes are dropped (reported). A document that
    /// does not fit is refused and nothing changes. With a <paramref name="map"/> the state follows what the map says was renamed: variables, quests,
    /// stages, the open conversation, the calls it is inside and the visit counts all move to their new names; the map is checked against both documents first.
    /// </summary>
    public ReloadReport Reload(DialogueSet next, ReloadMode mode = ReloadMode.Refuse, ReloadMap? map = null)
    {
        ArgumentNullException.ThrowIfNull(next);
        map ??= ReloadMap.Empty;
        if (!map.IsEmpty) map.Check(Set, next);
        var moved = DialogueState.Initial(next, State.Seed);
        moved.RollCounter = State.RollCounter;
        if (State.Conversation is not null)
        {
            var (conversation, node) = map.Node(State.Conversation, State.Node!);
            moved.Conversation = conversation;
            moved.Node = node;
        }
        moved.WaitingFor = State.WaitingFor;
        foreach (var frame in State.FrameList)
        {
            var (conversation, node) = map.Node(frame.Conversation, frame.Node);
            moved.FrameList.Add(new DialogueFrame(conversation, node, frame.Reenter, frame.Preempted));
        }
        moved.Recorded = State.Recorded;
        foreach (var queued in State.QueueList) moved.QueueList.Add(map.Conversation(queued));
        var droppedHistory = 0;
        foreach (var entry in State.HistoryList)
        {
            var (conversation, node) = map.Node(entry.Conversation, entry.Node);
            if (next.Conversations.TryGetValue(conversation, out var found) && found.Nodes.TryGetValue(node, out var target) && StateRules.HistoryMatches(entry with { Conversation = conversation, Node = node }, target)) moved.HistoryList.Add(entry with { Conversation = conversation, Node = node });
            else droppedHistory++;
        }
        while (moved.HistoryList.Count > next.HistoryLimit) { moved.HistoryList.RemoveFirst(); droppedHistory++; }
        var carried = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, value) in State.VariableValues)
        {
            var to = map.Variable(id);
            if (!next.Variables.ContainsKey(to)) throw new EngineRefusedException(DialogueCodes.ReloadIncompatible, "the new document drops variable '" + id + "', which the session holds");
            moved.VariableValues[to] = value;
            carried.Add(to);
        }
        var carriedQuests = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, progress) in State.QuestValues)
        {
            var to = map.Quest(id);
            if (!next.Quests.ContainsKey(to)) throw new EngineRefusedException(DialogueCodes.ReloadIncompatible, "the new document drops quest '" + id + "', which the session holds");
            moved.QuestValues[to] = progress with { Stage = map.Stage(id, progress.Stage) };
            carriedQuests.Add(to);
        }
        var dropped = 0;
        var renamed = 0;
        foreach (var (key, count) in State.VisitCounts)
        {
            var dot = key.IndexOf('/');
            var (conversation, node) = map.Node(key[..dot], key[(dot + 1)..]);
            if (next.Conversations.TryGetValue(conversation, out var found) && found.Nodes.ContainsKey(node))
            {
                var to = conversation + "/" + node;
                moved.VisitCounts[to] = count;
                if (to != key) renamed++;
            }
            else dropped++;
        }
        string? closed = null;
        var droppedQueued = 0;
        try { StateRules.Check(next, moved, DialogueCodes.ReloadIncompatible); }
        catch (EngineRefusedException) when (mode == ReloadMode.RestartConversation && moved.Conversation is not null)
        {
            // outcome: the report names where the conversation was (ClosedConversation) so the host knows it was closed.
            // The conversation cannot go on in the new document. Close it, and check the rest of the state on its own: if that still does not fit, the reload is refused all the same.
            closed = State.Conversation + "/" + State.Node;
            moved.Conversation = null;
            moved.Node = null;
            moved.WaitingFor = Waiting.Nothing;
            moved.FrameList.Clear();
            droppedQueued = moved.QueueList.Count;
            moved.QueueList.Clear();
            StateRules.Check(next, moved, DialogueCodes.ReloadIncompatible);
        }
        var report = new ReloadReport(next.Variables.Keys.Where(k => !carried.Contains(k)).Order(StringComparer.Ordinal).ToList(),
            next.Quests.Keys.Where(k => !carriedQuests.Contains(k)).Order(StringComparer.Ordinal).ToList(), dropped, closed, renamed, droppedHistory, droppedQueued);
        Set = next;
        State = moved;
        return report;
    }
}

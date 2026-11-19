using EngineCommon;
using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>
/// The one definition of a state that belongs to a document: every variable, quest and node count it names, in range and consistent. A save is
/// checked with it on load and a reload on swap, with the caller own code, so neither can bring in a state the document could not have made.
/// </summary>
internal static class StateRules
{
    /// <summary>
    /// The calls and interruptions a conversation is inside must be ones the document could have made: no deeper than the limit, each at a node of the right kind, and each call to the
    /// conversation that comes next in the chain (the next call, or the one the cursor is in).
    /// </summary>
    private static void CheckFrames(DialogueSet set, DialogueState state, Func<string, EngineRefusedException> refuse)
    {
        var frames = state.FrameList;
        if (frames.Count > DialogueRuntime.CallDepthLimit) throw refuse("the state is " + frames.Count + " calls deep, the limit is " + DialogueRuntime.CallDepthLimit);
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            if (!set.Conversations.TryGetValue(frame.Conversation, out var conversation) || !conversation.Nodes.TryGetValue(frame.Node, out var node))
                throw refuse("call " + i + " is at a node the document does not have");
            if (frame.Preempted && !frame.Reenter) throw refuse("call " + i + " is a preemption that does not show its node again");
            if (frame.Reenter)
            {
                if (!(node is LineNode || node is ChoiceNode { Seeded: false })) throw refuse("call " + i + " interrupted a node of kind '" + node.Kind + "', which does not wait");
                continue;
            }
            if (node is not CallNode call) throw refuse("call " + i + " is at a node of kind '" + node.Kind + "', not a call");
            var into = i + 1 < frames.Count ? frames[i + 1].Conversation : state.Conversation;
            if (call.Conversation != into) throw refuse("call " + i + " goes to '" + call.Conversation + "' but the chain is in '" + into + "'");
        }
    }
    /// <summary>The conversations at each level of the stack must be ones the rules could have stacked: every preempter outranks the one it broke into, and that one allowed it.</summary>
    private static void CheckPriorities(DialogueSet set, DialogueState state, Func<string, EngineRefusedException> refuse)
    {
        var frames = state.FrameList;
        var roots = new List<string>();
        var previous = -1;
        for (var p = 0; p < frames.Count; p++)
        {
            if (!frames[p].Preempted) continue;
            roots.Add(previous + 1 < p ? frames[previous + 1].Conversation : frames[p].Conversation);
            previous = p;
        }
        roots.Add(frames.Count > previous + 1 ? frames[previous + 1].Conversation : state.Conversation!);
        for (var i = 1; i < roots.Count; i++)
        {
            var lower = set.Conversations[roots[i - 1]];
            var upper = set.Conversations[roots[i]];
            if (upper.Priority <= lower.Priority) throw refuse("'" + upper.Id + "' preempts '" + lower.Id + "' without outranking it");
            if (!lower.Interruptible) throw refuse("'" + lower.Id + "' was broken into but forbids it");
        }
    }

    /// <summary>
    /// Whether a history entry could have been made by this node of this document: a line shows the node's values under its key (or its content-warning alternative), an answer is one the choice has under its key
    /// (or alternative). A reload keeps only the entries that still match; a save is refused when one does not.
    /// </summary>
    internal static bool HistoryMatches(HistoryEntry entry, Node node)
    {
        if (entry.Kind == HistoryKind.Line)
        {
            if (node is not LineNode line || entry.Chosen.Length != 0 || entry.Speaker != line.Speaker) return false;
            if (entry.Key != (entry.Replaced ? line.Alternative : line.Key)) return false;
            if (entry.Args.Count != line.Args.Count) return false;
            for (var i = 0; i < entry.Args.Count; i++)
                if (entry.Args[i].Name != line.Args[i].Key || (entry.Args[i].Value.Kind == FactKind.String && (entry.Args[i].Value.Text is null || entry.Args[i].Value.Text!.Length > 256))) return false;
            return true;
        }
        if (node is not ChoiceNode choice || entry.Args.Count != 0 || entry.Speaker.Length != 0 || entry.Seen || entry.Skipped) return false;
        var option = choice.Options.FirstOrDefault(o => o.Id == entry.Chosen);
        return option is not null && entry.Key == (entry.Replaced ? option.Alternative : option.Key);
    }

    private static void CheckQueueAndHistory(DialogueSet set, DialogueState state, Func<string, EngineRefusedException> refuse)
    {
        if (state.QueueList.Count > DialogueRuntime.QueueLimit) throw refuse("the queue holds " + state.QueueList.Count + " conversations, the limit is " + DialogueRuntime.QueueLimit);
        if (state.QueueList.Count > 0 && state.Conversation is null) throw refuse("the state has queued conversations but none is open");
        foreach (var queued in state.QueueList)
            if (!set.Conversations.TryGetValue(queued, out var def) || def.OnBusy != BusyPolicy.Queue) throw refuse("'" + queued + "' is queued but is not a conversation that queues");
        if (state.QueueList.Distinct(StringComparer.Ordinal).Count() != state.QueueList.Count) throw refuse("a conversation is queued twice");
        if (state.Recorded < 0) throw refuse("the history counter is negative");
        if (state.HistoryList.Count > set.HistoryLimit) throw refuse("the history holds " + state.HistoryList.Count + " entries, the document keeps " + set.HistoryLimit);
        if (state.HistoryList.Count > state.Recorded) throw refuse("the history holds more entries than were ever recorded");
        var last = -1L;
        foreach (var entry in state.HistoryList)
        {
            if (entry.Turn <= last || entry.Turn >= state.Recorded) throw refuse("history turn " + entry.Turn + " is out of order or beyond the counter");
            last = entry.Turn;
            if (entry.Tick < 0) throw refuse("a history entry has a negative tick");
            if (!set.Conversations.TryGetValue(entry.Conversation, out var conversation) || !conversation.Nodes.TryGetValue(entry.Node, out var node)) throw refuse("a history entry is at a node the document does not have");
            if (entry.Key.Length == 0 || entry.Key.Length > 128 || entry.Speaker.Length > 128 || entry.Chosen.Length > 128) throw refuse("a history entry has a name longer than 128 characters or no key");
            if (!Enum.IsDefined(entry.Kind)) throw refuse("a history entry has an unknown kind");
            if (entry.Skipped && (!entry.Seen || entry.Kind != HistoryKind.Line)) throw refuse("a history entry was skipped without having been seen, or is not a line");
            if (!HistoryMatches(entry, node)) throw refuse("a history entry does not match the node it names (its answer, key or values)");
        }
    }

    public static void Check(DialogueSet set, DialogueState state, string code)
    {
        EngineRefusedException Refuse(string message) => new(code, message);
        if (state.VariableValues.Count != set.Variables.Count) throw Refuse("the state has " + state.VariableValues.Count + " variables, the document " + set.Variables.Count);
        foreach (var (id, value) in state.VariableValues)
        {
            if (!set.Variables.TryGetValue(id, out var def)) throw Refuse("the state has a variable '" + id + "' the document does not");
            if (value.Kind != def.Kind) throw Refuse("variable '" + id + "' is " + value.Kind + ", the document says " + def.Kind);
            if (def.Kind == FactKind.Integer && (value.Integer < def.Minimum || value.Integer > def.Maximum)) throw Refuse("variable '" + id + "' is outside " + def.Minimum + " to " + def.Maximum);
            if (def.Kind == FactKind.String && (value.Text is null || value.Text.Length > 256 || (def.Allowed.Count > 0 && !def.Allowed.Contains(value.Text)))) throw Refuse("variable '" + id + "' holds a value the document does not allow");
        }
        if (state.QuestValues.Count != set.Quests.Count) throw Refuse("the state has " + state.QuestValues.Count + " quests, the document " + set.Quests.Count);
        foreach (var (id, progress) in state.QuestValues)
        {
            if (!set.Quests.TryGetValue(id, out var quest)) throw Refuse("the state has a quest '" + id + "' the document does not");
            if (!Enum.IsDefined(progress.Status)) throw Refuse("quest '" + id + "' has an unknown status");
            if (progress.Status == QuestStatus.NotStarted ? progress.Stage.Length != 0 : !quest.HasStage(progress.Stage)) throw Refuse("quest '" + id + "' is at a stage the document does not have");
            if (progress.Status != QuestStatus.NotStarted && quest.Stage(progress.Stage).Ends is { } ends && ends != progress.Status && progress.Status == QuestStatus.Active)
                throw Refuse("quest '" + id + "' is active at a stage that ends it");
        }
        foreach (var (key, count) in state.VisitCounts)
        {
            if (count < 0 || count > StepContext.MaxVisits) throw Refuse("node count '" + key + "' is outside 0 to " + StepContext.MaxVisits);
            var dot = key.IndexOf('/');
            if (dot <= 0 || !set.Conversations.TryGetValue(key[..dot], out var conversation) || !conversation.Nodes.ContainsKey(key[(dot + 1)..]))
                throw Refuse("node count '" + key + "' is for a node the document does not have");
        }
        if (state.RollCounter < 0) throw Refuse("the roll counter is negative");
        CheckQueueAndHistory(set, state, Refuse);
        if (state.Conversation is null)
        {
            if (state.Node is not null || state.WaitingFor != Waiting.Nothing) throw Refuse("the state has a cursor but no conversation");
            if (state.FrameList.Count != 0) throw Refuse("the state has calls open but no conversation");
            return;
        }
        CheckFrames(set, state, Refuse);
        CheckPriorities(set, state, Refuse);
        if (!set.Conversations.TryGetValue(state.Conversation, out var running) || state.Node is null || !running.Nodes.TryGetValue(state.Node, out var node))
            throw Refuse("the cursor is at a node the document does not have");
        var consistent = state.WaitingFor == Waiting.Line ? node is LineNode : state.WaitingFor == Waiting.Choice && node is ChoiceNode { Seeded: false };
        if (!consistent) throw Refuse("the cursor waits for " + state.WaitingFor + " at a node of kind '" + node.Kind + "'");
    }
}
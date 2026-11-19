namespace DialogueEngine;

internal sealed partial class DialogueReader
{
    /// <summary>
    /// Every target exists, every node is reachable from the start, and no cycle runs through nodes that never wait for the player (that would
    /// loop for ever inside one call). All of it is seen from the file alone.
    /// </summary>
    /// <summary>Conversations may call each other but not in a circle: a call cycle would never come back.</summary>
    private void CheckCalls()
    {
        var done = new HashSet<string>(StringComparer.Ordinal);
        var onPath = new List<string>();
        void Visit(string id)
        {
            if (done.Contains(id)) return;
            var at = onPath.IndexOf(id);
            if (at >= 0) throw _json.Refuse(DialogueCodes.CallCycle, "conversations call each other in a circle: " + string.Join(" > ", onPath.Skip(at).Append(id)));
            onPath.Add(id);
            foreach (var callee in _calls[id].Order(StringComparer.Ordinal)) Visit(callee);
            onPath.RemoveAt(onPath.Count - 1);
            done.Add(id);
        }
        foreach (var id in _calls.Keys.Order(StringComparer.Ordinal)) Visit(id);
    }

    private void CheckConversation(ConversationDef conversation, string where)
    {
        if (!conversation.Nodes.ContainsKey(conversation.Start)) throw _json.Refuse(DialogueCodes.Reference, where + ".start '" + conversation.Start + "' is not one of its nodes");
        foreach (var node in conversation.Nodes.Values)
            foreach (var target in node.Targets)
                if (!conversation.Nodes.ContainsKey(target)) throw _json.Refuse(DialogueCodes.Reference, where + " node '" + node.Id + "' leads to '" + target + "', which does not exist");

        var reached = new HashSet<string>(StringComparer.Ordinal) { conversation.Start };
        var queue = new Queue<string>();
        queue.Enqueue(conversation.Start);
        while (queue.Count > 0)
            foreach (var target in conversation.Nodes[queue.Dequeue()].Targets)
                if (reached.Add(target)) queue.Enqueue(target);
        var lost = conversation.Nodes.Keys.Where(k => !reached.Contains(k)).Order(StringComparer.Ordinal).FirstOrDefault();
        if (lost is not null) throw _json.Refuse(DialogueCodes.Unreachable, where + " node '" + lost + "' is not reachable from the start");

        // Depth-first over the nodes that do not wait; meeting one that is already on the path is a cycle with no stop for the player.
        var onPath = new HashSet<string>(StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            var node = conversation.Nodes[id];
            if (node.Waits || done.Contains(id)) return;
            if (!onPath.Add(id)) throw _json.Refuse(DialogueCodes.Loop, where + " node '" + id + "' is part of a cycle that never waits for the player");
            foreach (var target in node.Targets) Visit(target);
            onPath.Remove(id);
            done.Add(id);
        }
        foreach (var id in conversation.Nodes.Keys.Order(StringComparer.Ordinal)) Visit(id);
    }
}
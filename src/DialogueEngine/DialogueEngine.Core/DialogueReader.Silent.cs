namespace DialogueEngine;

internal sealed partial class DialogueReader
{
    private const int NoPath = int.MinValue / 4;

    /// <summary>
    /// The runtime passes at most <see cref="DialogueRuntime.SilentLimit"/> + 1 nodes in one call before it waits, ends or refuses. A document whose longest run of nodes that do not wait (calls
    /// and the returns that come back from them included, wherever the run is picked up: the start, after an answer, after a line, after an interruption) is longer would refuse at play time for a
    /// reason the file shows, so it is refused here instead.
    /// </summary>
    private void CheckSilentChains()
    {
        const int limit = DialogueRuntime.SilentLimit + 1;
        var memo = new Dictionary<(string, string), (int Wait, int Back)>();

        // The most nodes entered from here until the run stops (Wait) or until it comes back out of the conversation through a return (Back, counted up to and including the return).
        (int Wait, int Back) Run(string conversation, string id)
        {
            if (memo.TryGetValue((conversation, id), out var known)) return known;
            var node = _conversations[conversation].Nodes[id];
            (int Wait, int Back) result;
            switch (node)
            {
                case ReturnNode: result = (1, 1); break;
                case CallNode call:
                {
                    var callee = Run(call.Conversation, _conversations[call.Conversation].Start);
                    var after = Run(conversation, call.Next);
                    var wait = Math.Max(callee.Wait, callee.Back == NoPath ? NoPath : callee.Back + after.Wait);
                    var back = callee.Back == NoPath || after.Back == NoPath ? NoPath : callee.Back + after.Back;
                    result = (1 + wait, back == NoPath ? NoPath : 1 + back);
                    break;
                }
                default:
                    if (node.Waits || !node.Targets.Any()) { result = (1, NoPath); break; }
                    var wait2 = NoPath;
                    var back2 = NoPath;
                    foreach (var target in node.Targets)
                    {
                        var next = Run(conversation, target);
                        wait2 = Math.Max(wait2, next.Wait);
                        back2 = Math.Max(back2, next.Back);
                    }
                    result = (1 + wait2, back2 == NoPath ? NoPath : 1 + back2);
                    break;
            }
            memo[(conversation, id)] = result;
            return result;
        }

        // What can follow a return out of a conversation: the node after a call to it, in whatever conversation that is, or (for an interruption) the waiting node shown again.
        var tails = new Dictionary<string, int>(StringComparer.Ordinal);
        int Tail(string conversation)
        {
            if (tails.TryGetValue(conversation, out var known)) return known;
            var worst = 1;
            foreach (var caller in _conversations.Values)
                foreach (var node in caller.Nodes.Values.OfType<CallNode>().Where(c => c.Conversation == conversation))
                {
                    var after = Run(caller.Id, node.Next);
                    worst = Math.Max(worst, Math.Max(after.Wait, after.Back == NoPath ? NoPath : after.Back + Tail(caller.Id)));
                }
            tails[conversation] = worst;
            return worst;
        }

        foreach (var conversation in _conversations.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
            foreach (var node in conversation.Nodes.Values.OrderBy(n => n.Id, StringComparer.Ordinal))
            {
                var run = Run(conversation.Id, node.Id);
                var longest = Math.Max(run.Wait, run.Back == NoPath ? NoPath : run.Back + Tail(conversation.Id));
                if (longest > limit)
                    throw _json.Refuse(DialogueCodes.SilentChain, "conversation '" + conversation.Id + "' node '" + node.Id + "' starts a run of " + longest + " nodes that never wait, the limit is " + limit);
            }
    }
}

using System.Text.Json.Nodes;

namespace DialogueEngine;

internal sealed partial class InkConversation
{
    private string End()
    {
        if (_end is not null) return _end;
        _end = _b.NewId("end");
        _b.Add(new JsonObject { ["id"] = _end, ["kind"] = "end", ["result"] = "done" });
        return _end;
    }

    private string Back()
    {
        if (_back is not null) return _back;
        _back = _b.NewId("back");
        _b.Add(new JsonObject { ["id"] = _back, ["kind"] = "return", ["result"] = "back" });
        return _back;
    }

    /// <summary>The node a place starts at in a mode. A place met again (a loop, or reached by two diverts) is made once; one still being made is named and filled in when it is done.</summary>
    private string EntryAt(Pos pos, Mode mode, bool glueTaken = false)
    {
        if (_entries.TryGetValue((pos, mode, glueTaken), out var known)) return known;
        var alias = "i" + (++_aliases);
        _entries[(pos, mode, glueTaken)] = "@" + alias;
        var entry = EmitFlows(FlowsAt(pos, mode), mode, glueTaken);
        _b.Alias(alias, entry);
        _entries[(pos, mode, glueTaken)] = entry;
        return entry;
    }

    /// <summary>The conversation that a tunnel to a place is: made once, entered in tunnel mode so that its return goes back to the caller.</summary>
    private string TunnelConversation(Pos target)
    {
        if (_tunnels.TryGetValue(target, out var known)) return known;
        var id = "tunnel-" + (_tunnels.Count + 1);
        _tunnels[target] = id;
        var entry = EntryAt(target, Mode.Tunnel);
        _specs.Add(new ConversationSpec(id, entry));
        return id;
    }

    private string EmitFlows(List<Flow> flows, Mode mode, bool glueTaken = false)
    {
        if (!glueTaken && flows.Any(f => f is LeadGlue))
            throw _b.Unsupported("story", "glue at the start of content reached other than by a divert straight after a said line (which line it joins depends on the path taken)");
        string? after = null;
        JsonObject? open = null;
        for (var i = flows.Count - 1; i >= 0; i--)
        {
            if (flows[i] is Do effect)
            {
                if (open is not null && ((JsonArray)open["effects"]!).Count < 16)
                {
                    ((JsonArray)open["effects"]!).Insert(0, effect.Effect);
                    continue;
                }
                var id = _b.NewId("ink-set");
                open = new JsonObject { ["id"] = id, ["kind"] = "effects", ["effects"] = new JsonArray(effect.Effect), ["next"] = after ?? End() };
                _b.Add(open);
                after = id;
                continue;
            }
            open = null;
            switch (flows[i])
            {
                case Stop:
                    after = End();
                    break;
                case Leave:
                    after = Back();
                    break;
                case Go go:
                    after = LeadsWithGlue(go.Target, mode) ? EntryAfterLine(flows, i, go.Target, mode) : EntryAt(go.Target, mode);
                    break;
                case LeadGlue:
                    break;
                case Tunnel tunnel:
                {
                    var id = _b.NewId("ink-tunnel");
                    _b.Add(new JsonObject { ["id"] = id, ["kind"] = "call", ["conversation"] = TunnelConversation(tunnel.Target), ["next"] = after ?? End() });
                    after = id;
                    break;
                }
                case When gate:
                {
                    var id = _b.NewId("ink-if");
                    _b.Add(new JsonObject
                    {
                        ["id"] = id, ["kind"] = "branch",
                        ["cases"] = new JsonArray(new JsonObject { ["when"] = gate.Condition.DeepClone(), ["next"] = EntryAt(gate.Target, mode) }),
                        ["otherwise"] = after ?? End(),
                    });
                    after = id;
                    break;
                }
                case Say say:
                {
                    var speaker = ImportOptions.Narrator;
                    _b.Speaker(speaker);
                    var id = _b.NewId("ink");
                    var node = new JsonObject
                    {
                        ["id"] = id, ["kind"] = "line", ["speaker"] = speaker, ["portrait"] = ImportOptions.Portrait,
                        ["line"] = _b.Message("ink", say.Text), ["next"] = after ?? End(),
                    };
                    if (say.Args.Count > 0)
                    {
                        var args = new JsonObject();
                        foreach (var (name, fact) in say.Args) args[name] = fact;
                        node["args"] = args;
                    }
                    if (say.Tags.Count > 0) node["tags"] = new JsonArray(say.Tags.Select(t => (JsonNode)t).ToArray());
                    if (say.Glued) node["glue"] = true;
                    _b.Add(node);
                    _b.Report.Add(ImportStatus.Converted, "text", "a line of text is a line spoken by '" + ImportOptions.Narrator + "'; its text is in the strings table");
                    after = id;
                    break;
                }
                case Pick pick:
                    after = EmitChoice(pick, mode);
                    break;
                case Sequence sequence:
                    after = EmitSequence(sequence, mode);
                    break;
                case Do:
                    throw new InvalidOperationException("effects are folded above");
            }
        }
        return after ?? End();
    }

    private bool LeadsWithGlue(Pos target, Mode mode) => FlowsAt(target, mode).Any(f => f is LeadGlue);

    /// <summary>A divert to content that starts with glue: the line said just before the divert (in this run, with only variable changes between) is the one the glue joins, so it is flagged; anywhere else it could not be said.</summary>
    private string EntryAfterLine(List<Flow> flows, int divertAt, Pos target, Mode mode)
    {
        var previous = flows.Take(divertAt).LastOrDefault(f => f is not Do) as Say
            ?? throw _b.Unsupported("story", "a divert to content that starts with glue, not straight after a said line (which line the glue joins depends on the path taken)");
        previous.Glued = true;
        _b.Report.Add(ImportStatus.Converted, "glue (<>)", "the line before the divert carries a glue flag, so the host joins it to the content the divert reaches");
        return EntryAt(target, mode, glueTaken: true);
    }

    private string EmitSequence(Sequence sequence, Mode mode)
    {
        if (sequence.Branches > 16) throw _b.Unsupported("story", "a sequence of more than 16 branches");
        var entries = sequence.Targets.Select(t => EntryAt(t, mode)).ToList();
        string kind;
        string? exhausted = null;
        if (sequence.Mode == "cycle")
        {
            kind = "cycle";
            _b.Report.Add(ImportStatus.Converted, "sequences, cycles, once and shuffle", "a variant node: each time the place is reached it takes the next branch");
        }
        else if (sequence.Mode == "shuffle")
        {
            kind = "shuffle";
            _b.Report.Add(ImportStatus.Approximated, "shuffle", "each branch once in a shuffled order, a new order every round; the order is this engine's seeded one, not Ink's");
        }
        else if (sequence.Mode == "shuffle-stopping")
        {
            // shuffle once / stopping: the branches before the last come in a shuffled order, each once, then the last branch for ever (stopping) or nothing (once). The shuffled pass is a shuffle node that a once
            // node enters one time per branch, so the shuffle never reaches a second round.
            var shuffled = entries.GetRange(0, entries.Count - 1);
            if (shuffled.Count == 0) throw _b.Unsupported("story", "a shuffle once or stopping with nothing to shuffle");
            var pass = _b.NewId("ink-shuffle");
            _b.Add(new JsonObject { ["id"] = pass, ["kind"] = "variant", ["mode"] = "shuffle", ["options"] = new JsonArray(shuffled.Select(e => (JsonNode)e).ToArray()) });
            kind = "once";
            exhausted = entries[^1];
            entries = shuffled.Select(_ => pass).ToList();
            _b.Report.Add(ImportStatus.Approximated, "shuffle once / stopping", "a shuffled pass over the branches before the last, then the last for ever (or nothing, for once); the order is this engine's seeded one, not Ink's");
        }
        else if (sequence.Mode == "stopping")
        {
            // {a|b|} and {!a|b} are the same: after the last real branch the sequence says nothing and goes on.
            var last = sequence.Targets[^1];
            var empty = FlowsAt(last, mode) is [Go g] && g.Target == sequence.Join && sequence.Branches >= 2;
            if (empty)
            {
                kind = "once";
                exhausted = entries[^1];
                entries.RemoveAt(entries.Count - 1);
            }
            else kind = "sequence";
            _b.Report.Add(ImportStatus.Converted, "sequences, cycles, once and shuffle", "a variant node: each time the place is reached it takes the next branch");
        }
        else throw new InvalidOperationException("unknown sequence mode " + sequence.Mode);
        var id = _b.NewId("ink-seq");
        var node = new JsonObject { ["id"] = id, ["kind"] = "variant", ["mode"] = kind, ["options"] = new JsonArray(entries.Select(e => (JsonNode)e).ToArray()) };
        if (exhausted is not null) node["exhausted"] = exhausted;
        _b.Add(node);
        return id;
    }

    private string EmitChoice(Pick pick, Mode mode)
    {
        if (pick.Offers.Count > 16) throw _b.Unsupported("story", "more than 16 choices in one place");
        var options = new JsonArray();
        var once = pick.Offers.Any(o => (o.Flags & 0x10) != 0);
        var number = 0;
        foreach (var offer in pick.Offers)
        {
            var answer = new JsonObject { ["id"] = "o" + (++number), ["line"] = _b.Message("ink.option", ImportBuilder.EscapeBraces(offer.Text)) };
            JsonNode? when = offer.Condition?.DeepClone();
            if ((offer.Flags & 0x10) != 0)
            {
                // Once-only: the answer is spent when taken. A variable of the document remembers it (Ink counts visits of the branch, which comes to the same unless the branch is reached another way).
                var taken = "ink-taken-" + (++_taken);
                _b.Variable(new JsonObject { ["id"] = taken, ["type"] = "bool", ["initial"] = false });
                var fresh = new JsonObject { ["fact"] = "var." + taken, ["op"] = "eq", ["value"] = false };
                when = when is null ? fresh : new JsonObject { ["all"] = new JsonArray(fresh, when) };
                answer["effects"] = new JsonArray(new JsonObject { ["set"] = taken, ["value"] = true });
                _b.Report.Add(ImportStatus.Approximated, "once-only choices (*)", "spent by a true/false variable set when the answer is taken, not by the visit count of its branch");
            }
            else _b.Report.Add(ImportStatus.Converted, "sticky choices (+)", "an answer that can be taken again");
            if (when is not null)
            {
                answer["when"] = when;
                answer["unavailable"] = "hide";
            }
            answer["next"] = EntryAt(new Pos(offer.Branch, 0), mode);
            options.Add(answer);
        }
        var id = _b.NewId("ink-choice");
        var node = new JsonObject { ["id"] = id, ["kind"] = "choice", ["pick"] = "player", ["options"] = options };
        if (once || pick.Offers.Any(o => o.Condition is not null)) node["fallback"] = End();   // every answer spent or unavailable: the story has nothing more to offer
        _b.Add(node);
        _b.Report.Add(ImportStatus.Converted, "choices", "a choice node; the text shown is the start content followed by the choice-only content");
        return id;
    }
}

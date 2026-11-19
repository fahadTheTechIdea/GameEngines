using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DialogueEngine;

internal sealed partial class InkConversation
{
    private sealed class Scan
    {
        public Mode Mode { get; init; }
        public bool Declaring { get; init; }
        public bool Evaluating { get; set; }
        public bool Done { get; set; }
        public bool GluePending { get; set; }

        /// <summary>Set by the conditional divert inside an evaluation that skips a shuffle once / stopping, until the seq that follows it.</summary>
        public bool ShuffleSkip { get; set; }
        public List<Ast> Stack { get; } = new();
        public List<Flow> Flows { get; } = new();
        public StringBuilder Text { get; } = new();
        public List<string> Tags { get; } = new();
        public SortedDictionary<string, string> Args { get; } = new(StringComparer.Ordinal);
        public List<Offer> Offers { get; } = new();
        public List<(SeqIndex Index, long Value, Pos Target)> Dispatch { get; } = new();

        /// <summary>Says the text written so far as a line of its own (glued to the next when asked), and starts the next one.</summary>
        public void Flush(bool glue = false)
        {
            var text = Text.ToString().Trim();
            if (text.Length > 0) Flows.Add(new Say(text, new List<string>(Tags), new SortedDictionary<string, string>(Args, StringComparer.Ordinal), glue || GluePending));
            Text.Clear();
            Tags.Clear();
            Args.Clear();
            GluePending = false;
        }
    }

    // ---- scanning from a place into a straight run of things to do ------------------------------------------------------------------------

    /// <summary>
    /// Walks the story from <paramref name="start"/> the way the runtime does: through the items of a container, down into the containers inside it, and, when one ends, on in the container it
    /// was in. A divert ends the run (a conditional one only adds a test), as do <c>done</c>, <c>end</c>, a tunnel return and the dispatch of a sequence.
    /// </summary>
    private List<Flow> FlowsAt(Pos start, Mode mode)
    {
        if (_flows.TryGetValue((start, mode), out var known)) return known;
        var made = ScanFrom(start, mode);
        _flows[(start, mode)] = made;
        return made;
    }

    private List<Flow> ScanFrom(Pos start, Mode mode)
    {
        var scan = new Scan { Mode = mode };
        var box = start.Box;
        var i = start.Index;
        if (i == 0) Count(box, scan);
        for (var steps = 0; !scan.Done; steps++)
        {
            if (steps > 1_000_000) throw _b.Unsupported("story", "a run of content too long to convert");
            if (i >= box.Items.Count)
            {
                if (box.Index < 0 || box.Parent is null)
                {
                    Finish(scan);
                    scan.Flows.Add(new Stop());
                    scan.Done = true;
                    break;
                }
                i = box.Index + 1;
                box = box.Parent;
                continue;
            }
            var item = box.Items[i];
            if (scan.Evaluating && !(item.ValueKind == JsonValueKind.String && item.GetString() == "/ev"))
            {
                Evaluate(box, ref i, scan);
                i++;
                continue;
            }
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    i = Word(box, i, item.GetString()!, scan) + 1;
                    break;
                case JsonValueKind.Object:
                    i = Divert(box, i, item, scan) + 1;
                    break;
                case JsonValueKind.Array:
                    box = box.Children[i];
                    i = 0;
                    Count(box, scan);
                    break;
                case JsonValueKind.Null:
                    i++;
                    break;
                case JsonValueKind.Undefined:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    throw _b.Unsupported("story", "a bare value (" + item.GetRawText() + ") in the content; values belong to expressions");
            }
        }
        return scan.Flows;
    }

    /// <summary>Entering a container that a <c>CNT?</c> reads adds one to its counter.</summary>
    private static void Count(Box box, Scan scan)
    {
        if (box.CountVariable is not null) scan.Flows.Add(new Do(new JsonObject { ["add"] = box.CountVariable, ["by"] = 1 }));
    }

    /// <summary>Text and tags written before a stop belong to a line that has not met its newline; it is said first. Offers made so far become the choice.</summary>
    private static void Finish(Scan scan)
    {
        scan.Flush();
        if (scan.Offers.Count > 0) scan.Flows.Add(new Pick(new List<Offer>(scan.Offers)));
        scan.Offers.Clear();
    }

    private void AddTag(Scan scan, string text)
    {
        var tag = text.Trim().ToLowerInvariant().Replace(' ', '-');
        if (tag.Length == 0 || tag.Length > 128 || !tag.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '.' or '_'))
        {
            _b.Report.Add(ImportStatus.Dropped, "tags that are not ids", "the tag '" + text.Trim() + "' uses characters a dialogue tag cannot (a-z, 0-9, hyphen, dot, underscore)");
            return;
        }
        if (!scan.Tags.Contains(tag) && scan.Tags.Count < 8) scan.Tags.Add(tag);
        _b.Report.Add(ImportStatus.Converted, "tags", "kept as tags of the line");
    }

    /// <summary>A string item outside an evaluation. Returns the index of the last item it used.</summary>
    private int Word(Box box, int index, string word, Scan scan)
    {
        if (word.StartsWith('^')) { scan.Text.Append(ImportBuilder.EscapeBraces(Text(word))); return index; }
        switch (word)
        {
            case "\n":
                scan.Flush();
                return index;
            case "done":
            case "end":
                Finish(scan);
                scan.Flows.Add(new Stop());
                scan.Done = true;
                _b.Report.Add(ImportStatus.Converted, "done / end", "ends the conversation");
                return index;
            case "ev":
                scan.Evaluating = true;
                return index;
            case "/ev":
                scan.Evaluating = false;
                return index;
            case "nop":
                if (scan.Dispatch.Count > 0) FinishDispatch(box, index, scan);
                return index;
            case "pop":
                if (scan.Stack.Count > 0) scan.Stack.RemoveAt(scan.Stack.Count - 1);
                return index;
            case "#":
            {
                var tag = new StringBuilder();
                var at = index + 1;
                for (; at < box.Items.Count && !(box.Items[at].ValueKind == JsonValueKind.String && box.Items[at].GetString() == "/#"); at++)
                {
                    if (box.Items[at].ValueKind != JsonValueKind.String || !box.Items[at].GetString()!.StartsWith('^')) throw _b.Unsupported("story", "a tag that is not plain text");
                    tag.Append(Text(box.Items[at].GetString()!));
                }
                if (at >= box.Items.Count) throw _b.Syntax("story", "a tag is never closed with /#");
                AddTag(scan, tag.ToString());
                return at;
            }
            case "<>":
                Glue(scan);
                return index;
            case "->->":
                if (scan.Mode != Mode.Tunnel) throw _b.Unsupported("story", "a tunnel return (->->) outside a tunnel");
                Finish(scan);
                scan.Flows.Add(new Leave());
                scan.Done = true;
                _b.Report.Add(ImportStatus.Converted, "tunnels", "a tunnel is a conversation that is called and returns");
                return index;
            case "thread":
                return Thread(box, index, scan);
        }
        throw _b.Unsupported("story", "the control command '" + word + "'");
    }

    /// <summary>
    /// Glue joins the text on either side of it. Text written before it is said glued to the next line; with no text before it, it glues the line said just before (when that is in this same run of
    /// content). The Ink runtime joins by removing the newline; a dialogue line carries a <c>glue</c> flag instead and the host joins the two lines.
    /// </summary>
    private void Glue(Scan scan)
    {
        if (scan.Text.ToString().Trim().Length > 0) { scan.GluePending = true; _b.Report.Add(ImportStatus.Converted, "glue (<>)", "the line before the glue carries a glue flag, so the host joins it to the next line"); return; }
        var previous = scan.Flows.LastOrDefault(f => f is not Do) as Say;
        if (previous is null)
        {
            // Nothing was said before it in this run: it glues whatever line came before the place was reached. A divert that comes straight after a said line can say that (EmitFlows).
            if (scan.Flows.Any(f => f is not Do)) throw _b.Unsupported("story", "glue after a conditional divert, a choice or a tunnel call (which line it joins depends on the path taken)");
            scan.Flows.Add(new LeadGlue());
            _b.Report.Add(ImportStatus.Converted, "glue (<>)", "glue at the start of content that a divert reaches glues the line said just before the divert, when the divert follows a said line");
            return;
        }
        previous.Glued = true;
        _b.Report.Add(ImportStatus.Converted, "glue (<>)", "the line before the glue carries a glue flag, so the host joins it to the next line");
    }

    /// <summary>
    /// <c>thread</c> followed by a divert (<c>&lt;- knot</c>): the knot is run alongside and what it offers joins the choices here. That can be said when the knot only offers choices, which is how
    /// threads are used to gather options; a thread that carries text or logic is refused.
    /// </summary>
    private int Thread(Box box, int index, Scan scan)
    {
        var next = index + 1;
        if (next >= box.Items.Count || box.Items[next].ValueKind != JsonValueKind.Object || !box.Items[next].TryGetProperty("->", out var path) || box.Items[next].EnumerateObject().Count() != 1)
            throw _b.Unsupported("story", "a thread that is not <- followed by a plain divert");
        var target = ResolvePos(path.GetString()!, box);
        scan.Flush(glue: false);
        var flows = FlowsAt(target, scan.Mode);
        foreach (var flow in flows)
            if (flow is not (Say or Do or Pick or Stop)) throw _b.Unsupported("story", "a thread whose content goes on with a " + flow.GetType().Name.ToLowerInvariant() + " (a divert, a condition, a tunnel or a sequence); only a thread that says text, sets variables and offers choices is converted");
        // A thread runs to its end before the flow that pulled it in goes on, so what it says and sets comes here, in order, and what it offers joins the choices of this place.
        foreach (var flow in flows)
            if (flow is Say or Do) scan.Flows.Add(flow);
        foreach (var pick in flows.OfType<Pick>()) scan.Offers.AddRange(pick.Offers);
        _b.Report.Add(ImportStatus.Converted, flows.Any(f => f is Say or Do) ? "threads (text and choices)" : "threads (choices only)", "the thread runs where it is pulled in: its text and variable changes come in order, its choices join the choices of the place");
        return next;
    }

    /// <summary>An object item outside an evaluation: a divert, a tunnel call, a choice point or a tag. Returns the index of the last item it used.</summary>
    private int Divert(Box box, int index, JsonElement item, Scan scan)
    {
        var names = item.EnumerateObject().Select(p => p.Name).ToList();
        if (names.Contains("*"))
        {
            ChoicePoint(box, item, scan);
            return index;
        }
        if (names.Contains("->t->"))
        {
            if (names.Count != 1) throw _b.Unsupported("story", "a tunnel call with " + string.Join(", ", names.Where(n => n != "->t->")));
            var target = ResolvePos(item.GetProperty("->t->").GetString()!, box);
            scan.Flush(glue: false);
            scan.Flows.Add(new Tunnel(target));
            _b.Report.Add(ImportStatus.Converted, "tunnels", "a tunnel is a conversation that is called and returns");
            return index;
        }
        if (names.Contains("->"))
        {
            foreach (var other in names) if (other is not ("->" or "c")) throw _b.Unsupported("story", "a divert with " + other + " (variable diverts and function calls are not converted)");
            var target = ResolvePos(item.GetProperty("->").GetString()!, box);
            if (names.Contains("c"))
            {
                var condition = Pop(scan, "a conditional divert");
                if (condition is Binary { Operator: "==", Left: SeqIndex sequence, Right: Const { Value: long value } })
                {
                    scan.Dispatch.Add((sequence, value, target));
                    return index;
                }
                scan.Flush(glue: scan.Text.ToString().Trim().Length > 0);
                scan.Flows.Add(new When(Condition(condition), target));
                _b.Report.Add(ImportStatus.Converted, "conditional diverts", "a branch node on the condition; conditional text and if/else blocks are made of these");
                return index;
            }
            if (target.Index == 0 && IsInline(target.Box))
            {
                scan.Text.Append(ImportBuilder.EscapeBraces(InlineText(target.Box)));
                return index;
            }
            Finish(scan);
            scan.Flows.Add(new Go(target));
            scan.Done = true;
            _b.Report.Add(ImportStatus.Converted, "divert", "a divert is an edge of the conversation");
            return index;
        }
        if (names.Count == 1 && names[0] == "^->") return index;                                                     // a divert target pushed for an inline return
        if (names.Count == 1 && names[0] == "temp=" && item.GetProperty("temp=").GetString() == "$r")   // the inline return label
        {
            if (scan.Stack.Count > 0 && scan.Stack[^1] is Marker) scan.Stack.RemoveAt(scan.Stack.Count - 1);
            return index;
        }
        if (names.Count == 1 && names[0] == "#" && item.GetProperty("#").ValueKind == JsonValueKind.String) { AddTag(scan, item.GetProperty("#").GetString()!); return index; }
        throw _b.Unsupported("story", "the instruction {" + string.Join(", ", names.Select(n => "\"" + n + "\"")) + "} outside an evaluation");
    }

    /// <summary>
    /// The dispatch of a sequence ended at its <c>nop</c>: every branch was a conditional divert on the same position (<c>visit</c> taken at most, modulo or shuffled, compared with 0, 1, 2 ...). The
    /// branches become the options of one variant node and the content after the <c>nop</c> is where they all come back to.
    /// </summary>
    private void FinishDispatch(Box box, int index, Scan scan)
    {
        var first = scan.Dispatch[0].Index;
        if (scan.Dispatch.Any(d => d.Index != first)) throw _b.Unsupported("story", "a sequence whose branches are not chosen by one position");
        for (var k = 0; k < scan.Dispatch.Count; k++)
            if (scan.Dispatch[k].Value != k) throw _b.Syntax("story", "the branches of a sequence are not numbered 0, 1, 2 ... in order");
        if (scan.Stack.Count > 0 && scan.Stack[^1] == first) scan.Stack.RemoveAt(scan.Stack.Count - 1);
        var branches = scan.Dispatch.Count;
        var expected = first.Mode is "stopping" or "shuffle-stopping" ? first.Count + 1 : first.Count;
        if (branches != expected) throw _b.Syntax("story", "a " + first.Mode + " sequence of " + first.Count + " has " + branches + " branches");
        scan.Flush(glue: scan.Text.ToString().Trim().Length > 0);
        scan.Flows.Add(new Sequence(first.Mode, branches, scan.Dispatch.Select(d => d.Target).ToList(), new Pos(box, index)));
        scan.Dispatch.Clear();
        scan.Done = true;
    }

    private void ChoicePoint(Box box, JsonElement item, Scan scan)
    {
        foreach (var property in item.EnumerateObject())
            if (property.Name is not ("*" or "flg")) throw _b.Unsupported("story", "a choice with '" + property.Name + "'");
        var flags = item.TryGetProperty("flg", out var flg) && flg.TryGetInt32(out var number) ? number : 0;
        if ((flags & 0x8) != 0) throw _b.Unsupported("story", "an invisible default choice (a fallback choice)");
        if ((flags & ~0x1F) != 0) throw _b.Unsupported("story", "a choice with the unknown flags " + flags);
        if ((flags & 0x6) == 0) throw _b.Unsupported("story", "a choice without text of its own");
        // The runtime takes the condition, the choice-only text and the start text from the stack; whichever order the compiler pushed them in, the texts are the constants and the condition is the other.
        var wanted = ((flags & 0x2) != 0 ? 1 : 0) + ((flags & 0x4) != 0 ? 1 : 0) + ((flags & 0x1) != 0 ? 1 : 0);
        if (scan.Stack.Count < wanted) throw _b.Syntax("story", "a choice finds " + scan.Stack.Count + " values on the stack, it needs " + wanted);
        var taken = scan.Stack.GetRange(scan.Stack.Count - wanted, wanted);
        scan.Stack.RemoveRange(scan.Stack.Count - wanted, wanted);
        var texts = taken.Where(t => t is Const { Value: string }).Select(t => (string)((Const)t).Value).ToList();
        var others = taken.Where(t => t is not Const { Value: string }).ToList();
        if (texts.Count != wanted - ((flags & 0x1) != 0 ? 1 : 0) || others.Count != ((flags & 0x1) != 0 ? 1 : 0)) throw _b.Syntax("story", "a choice's text and condition are not what its flags say");
        var branch = ResolveBox(item.GetProperty("*").GetString()!, box);
        scan.Flush();
        scan.Offers.Add(new Offer(string.Concat(texts).Trim(), flags, branch, others.Count == 1 ? Condition(others[0]) : null));
        if (others.Count == 1) _b.Report.Add(ImportStatus.Converted, "conditional choices", "an answer with a condition; one that is false is hidden");
    }
}

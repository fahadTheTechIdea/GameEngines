using System.Text.Json;

namespace DialogueEngine;

internal sealed partial class DialogueReader
{
    private void ReadConversation(JsonElement element)
    {
        _json.Object(element, "conversation", ["id", "start", "nodes"], "priority", "onBusy", "interruptible");
        var id = _json.Id(element, "id", "conversation");
        _current = id;
        _calls[id] = new HashSet<string>(StringComparer.Ordinal);
        var where = "conversation '" + id + "'";
        var start = _json.Id(element, "start", where);
        var nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var node in _json.Array(element, "nodes", where, 1, 512).EnumerateArray())
        {
            var read = ReadNode(node, where);
            nodes.Add(read.Id, read);
        }
        var priority = _json.Has(element, "priority") ? _json.Int(element, "priority", where, 0, 1000) : 0;
        var onBusy = BusyPolicy.Refuse;
        if (_json.Has(element, "onBusy"))
            onBusy = _json.Text(element, "onBusy", where, 8) switch
            {
                "refuse" => BusyPolicy.Refuse,
                "queue" => BusyPolicy.Queue,
                "preempt" => BusyPolicy.Preempt,
                _ => (BusyPolicy?)null,
            } ?? throw _json.Refuse(DialogueCodes.Format, where + ".onBusy must be refuse, queue or preempt");
        if (onBusy == BusyPolicy.Preempt && priority == 0) throw _json.Refuse(DialogueCodes.Range, where + " preempts but has priority 0, which outranks nothing");
        var interruptible = !_json.Has(element, "interruptible") || _json.Bool(element, "interruptible", where);
        var conversation = new ConversationDef(id, start, nodes, priority, onBusy, interruptible);
        CheckConversation(conversation, where);
        _conversations[id] = conversation;
    }

    private Node ReadNode(JsonElement element, string conversation)
    {
        if (!_json.Has(element, "kind")) throw _json.Refuse(DialogueCodes.MissingField, conversation + ": a node is missing 'kind'");
        var id = _json.Id(element, "id", conversation);
        var where = conversation + " node '" + id + "'";
        var kind = _json.Text(element, "kind", where, 16);
        switch (kind)
        {
            case "line": return ReadLine(element, id, where);
            case "choice": return ReadChoice(element, id, where);
            case "branch": return ReadBranch(element, id, where);
            case "random": return ReadRandom(element, id, where);
            case "effects":
                _json.Object(element, where, ["id", "kind", "effects", "next"]);
                return new EffectsNode(id, ReadEffects(element, "effects", where, 1), _json.Id(element, "next", where));
            case "call":
            {
                _json.Object(element, where, ["id", "kind", "conversation", "next"]);
                var callee = _json.Id(element, "conversation", where);
                if (!_nodeIds.ContainsKey(callee)) throw _json.Refuse(DialogueCodes.Reference, where + ": calls conversation '" + callee + "', which does not exist");
                _calls[_current].Add(callee);
                return new CallNode(id, callee, _json.Id(element, "next", where));
            }
            case "return":
                _json.Object(element, where, ["id", "kind", "result"]);
                return new ReturnNode(id, _json.Id(element, "result", where));
            case "variant":
                return ReadVariant(element, id, where);
            case "end":
                _json.Object(element, where, ["id", "kind", "result"]);
                return new EndNode(id, _json.Id(element, "result", where));
        }
        throw _json.Refuse(DialogueCodes.Format, where + ": unknown node kind '" + kind + "'");
    }

    private LineNode ReadLine(JsonElement element, string id, string where)
    {
        _json.Object(element, where, ["id", "kind", "speaker", "portrait", "line", "next"], "args", "tags", "voice", "emotion", "autoAdvanceMs", "glue", "alternative");
        var speaker = _json.Id(element, "speaker", where);
        if (!_speakers.TryGetValue(speaker, out var def)) throw _json.Refuse(DialogueCodes.UnknownSpeaker, where + ": speaker '" + speaker + "' is not declared in the document speakers");
        var portrait = _json.Id(element, "portrait", where);
        if (!def.Portraits.Contains(portrait)) throw _json.Refuse(DialogueCodes.UnknownPortrait, where + ": speaker '" + speaker + "' has no portrait '" + portrait + "'");
        var args = new List<KeyValuePair<string, string>>();
        if (_json.Has(element, "args"))
        {
            var holder = element.GetProperty("args");
            if (holder.ValueKind != JsonValueKind.Object) throw _json.Refuse(DialogueCodes.Format, where + ".args must be an object of placeholder name to fact");
            foreach (var property in holder.EnumerateObject())
            {
                var name = _json.CheckId(property.Name, where + ".args");
                if (property.Value.ValueKind != JsonValueKind.String) throw _json.Refuse(DialogueCodes.Format, where + ".args." + name + " must name a fact");
                var fact = property.Value.GetString()!;
                if (!_facts.ContainsKey(fact)) throw _json.Refuse(DialogueCodes.Reference, where + ".args." + name + ": unknown fact '" + fact + "'");
                args.Add(new KeyValuePair<string, string>(name, fact));
            }
        }
        var key = Key(element, "line", where, args.Select(a => a.Key));
        var tags = _json.Has(element, "tags") ? Ids(_json.Array(element, "tags", where, 1, 8), where + ".tags") : [];
        var warnings = WarningsOf(tags, where);
        CheckAlternative(element, warnings, where);
        var alternative = _json.Has(element, "alternative") ? Key(element, "alternative", where, args.Select(a => a.Key)) : null;
        var glue = _json.Has(element, "glue") && _json.Bool(element, "glue", where);
        var voice = _json.Has(element, "voice") ? _json.Id(element, "voice", where) : null;
        string? emotion = null;
        if (_json.Has(element, "emotion"))
        {
            emotion = _json.Id(element, "emotion", where);
            if (!def.Emotions.Contains(emotion)) throw _json.Refuse(DialogueCodes.Reference, where + ": speaker '" + speaker + "' has no emotion '" + emotion + "'");
        }
        var auto = _json.Has(element, "autoAdvanceMs") ? _json.Int(element, "autoAdvanceMs", where, 0, 600_000) : 0;
        return new LineNode(id, speaker, portrait, key, args, _json.Id(element, "next", where), tags, voice, emotion, auto, glue, alternative, warnings);
    }

    private ChoiceNode ReadChoice(JsonElement element, string id, string where)
    {
        _json.Object(element, where, ["id", "kind", "pick", "options"], "fallback", "timeout");
        var pick = _json.Text(element, "pick", where, 16);
        if (pick is not ("player" or "seeded")) throw _json.Refuse(DialogueCodes.Format, where + ".pick must be player or seeded");
        var seeded = pick == "seeded";
        var options = new List<OptionDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in _json.Array(element, "options", where, 1, 16).EnumerateArray())
        {
            var at = where + ".options";
            _json.Object(option, at, ["id", "line", "next"], "when", "unavailable", "weight", "effects", "tags", "alternative");
            var optionId = _json.Id(option, "id", at);
            if (!seen.Add(optionId)) throw _json.Refuse(DialogueCodes.Duplicate, where + " has two answers called '" + optionId + "'");
            at = where + " answer '" + optionId + "'";
            if (seeded) { Require(option, "weight", at); Forbid(option, "unavailable", at); }
            else Forbid(option, "weight", at);
            var showDisabled = false;
            if (_json.Has(option, "unavailable"))
            {
                var mode = _json.Text(option, "unavailable", at, 8);
                if (mode is not ("hide" or "show")) throw _json.Refuse(DialogueCodes.Format, at + ".unavailable must be hide or show");
                showDisabled = mode == "show";
            }
            var weight = seeded ? _json.Int(option, "weight", at, 1, 1000) : 0;
            var when = _json.Has(option, "when") ? ReadGuard(option.GetProperty("when"), at + ".when") : null;
            var effects = _json.Has(option, "effects") ? ReadEffects(option, "effects", at, 1) : [];
            var tags = _json.Has(option, "tags") ? Ids(_json.Array(option, "tags", at, 1, 8), at + ".tags") : [];
            var warnings = WarningsOf(tags, at);
            CheckAlternative(option, warnings, at);
            var alternative = _json.Has(option, "alternative") ? Key(option, "alternative", at, []) : null;
            options.Add(new OptionDef(optionId, Key(option, "line", at, []), when, showDisabled, weight, effects, _json.Id(option, "next", at), tags, alternative, warnings));
        }
        var fallback = _json.Has(element, "fallback") ? _json.Id(element, "fallback", where) : null;
        ChoiceTimeout? timeout = null;
        if (_json.Has(element, "timeout"))
        {
            if (seeded) throw _json.Refuse(DialogueCodes.UnknownField, where + ": a seeded choice never waits, so it has no timeout");
            var holder = element.GetProperty("timeout");
            _json.Object(holder, where + ".timeout", ["afterMs", "option"]);
            var option = _json.Id(holder, "option", where + ".timeout");
            if (!seen.Contains(option)) throw _json.Refuse(DialogueCodes.Reference, where + ".timeout names answer '" + option + "', which the choice does not have");
            timeout = new ChoiceTimeout(_json.Int(holder, "afterMs", where + ".timeout", 100, 3_600_000), option);
        }
        return new ChoiceNode(id, seeded, options, fallback, timeout);
    }

    private VariantNode ReadVariant(JsonElement element, string id, string where)
    {
        _json.Object(element, where, ["id", "kind", "mode", "options"], "exhausted");
        var mode = _json.Text(element, "mode", where, 16) switch
        {
            "sequence" => VariantMode.Sequence,
            "cycle" => VariantMode.Cycle,
            "once" => VariantMode.Once,
            "shuffle" => VariantMode.Shuffle,
            _ => (VariantMode?)null,
        } ?? throw _json.Refuse(DialogueCodes.Format, where + ".mode must be sequence, cycle, once or shuffle");
        var options = _json.Array(element, "options", where, 1, 16).EnumerateArray()
            .Select(o => o.ValueKind == JsonValueKind.String ? _json.CheckId(o.GetString()!, where + ".options") : throw _json.Refuse(DialogueCodes.Format, where + ".options must hold node ids")).ToList();
        string? exhausted = null;
        if (mode == VariantMode.Once) { Require(element, "exhausted", where); exhausted = _json.Id(element, "exhausted", where); }
        else Forbid(element, "exhausted", where);
        return new VariantNode(id, mode, options, exhausted);
    }

    private BranchNode ReadBranch(JsonElement element, string id, string where)
    {
        _json.Object(element, where, ["id", "kind", "cases", "otherwise"]);
        var cases = new List<BranchCase>();
        foreach (var branch in _json.Array(element, "cases", where, 1, 16).EnumerateArray())
        {
            _json.Object(branch, where + ".cases", ["when", "next"]);
            cases.Add(new BranchCase(ReadGuard(branch.GetProperty("when"), where + ".cases.when"), _json.Id(branch, "next", where + ".cases")));
        }
        return new BranchNode(id, cases, _json.Id(element, "otherwise", where));
    }

    private RandomNode ReadRandom(JsonElement element, string id, string where)
    {
        _json.Object(element, where, ["id", "kind", "outcomes"]);
        var outcomes = new List<RandomOutcome>();
        foreach (var outcome in _json.Array(element, "outcomes", where, 1, 16).EnumerateArray())
        {
            _json.Object(outcome, where + ".outcomes", ["weight", "next"]);
            outcomes.Add(new RandomOutcome(_json.Int(outcome, "weight", where + ".outcomes", 1, 1000), _json.Id(outcome, "next", where + ".outcomes")));
        }
        return new RandomNode(id, outcomes);
    }
}
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>
/// The save of a dialogue session: its format, the digest of the document it was taken from, the whole state and a digest of the state. Loading
/// checks them in that order and then checks the state against the document; it never repairs or migrates.
/// </summary>
public static class DialogueSave
{
    public const int Format = 3;

    /// <summary>The largest save that is read at all. A real save is bounded by its document (at most 256 variables, 256 quests and 512 nodes in each of 256 conversations); anything bigger is refused before it is parsed.</summary>
    public const int MaxBytes = 16 * 1024 * 1024;

    private sealed class Envelope
    {
        public int Format { get; set; }
        public string Document { get; set; } = "";
        public string Id { get; set; } = "";
        public string Seed { get; set; } = "";
        public long Counter { get; set; }
        public string? Conversation { get; set; }
        public string? Node { get; set; }
        public string Waiting { get; set; } = "";
        public List<VariableRow> Variables { get; set; } = new();
        public List<QuestRow> Quests { get; set; } = new();
        public List<VisitRow> Visits { get; set; } = new();
        public List<FrameRow> Frames { get; set; } = new();
        public long Recorded { get; set; }
        public List<string> Queue { get; set; } = new();
        public List<HistoryRow> History { get; set; } = new();
        public string Digest { get; set; } = "";
    }

    private sealed class VariableRow { public string Id { get; set; } = ""; public string Kind { get; set; } = ""; public long Integer { get; set; } public bool Boolean { get; set; } public string? Text { get; set; } }

    private sealed class QuestRow { public string Id { get; set; } = ""; public string Status { get; set; } = ""; public string Stage { get; set; } = ""; }

    private sealed class VisitRow { public string Key { get; set; } = ""; public int Count { get; set; } }

    private sealed class FrameRow { public string Conversation { get; set; } = ""; public string Node { get; set; } = ""; public bool Reenter { get; set; } public bool Preempted { get; set; } }

    private sealed class HistoryRow
    {
        public long Turn { get; set; }
        public long Tick { get; set; }
        public string Kind { get; set; } = "";
        public string Conversation { get; set; } = "";
        public string Node { get; set; } = "";
        public string Speaker { get; set; } = "";
        public string Key { get; set; } = "";
        public List<VariableRow> Args { get; set; } = new();
        public string Chosen { get; set; } = "";
        public bool Seen { get; set; }
        public bool Skipped { get; set; }
        public bool Replaced { get; set; }
    }

    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = false };

    private static readonly string[] WaitingNames = ["nothing", "line", "choice"];

    /// <summary>The text the state digest is taken over: every part of the state in a fixed order, numbers in the invariant culture.</summary>
    public static string Canonical(DialogueState state)
    {
        var text = new StringBuilder("dialogue-state-3");
        text.Append(CultureInfo.InvariantCulture, $"\nseed={state.Seed}\ncounter={state.RollCounter}\ncursor={state.Conversation ?? "-"}|{state.Node ?? "-"}|{WaitingNames[(int)state.WaitingFor]}");
        foreach (var (id, value) in state.VariableValues)
            text.Append(CultureInfo.InvariantCulture, $"\nvar {id}={value.Kind}:{JsonSerializer.Serialize(value.Kind == FactKind.Integer ? value.Integer.ToString(CultureInfo.InvariantCulture) : value.Kind == FactKind.Boolean ? (value.Boolean ? "true" : "false") : value.Text)}");
        foreach (var (id, progress) in state.QuestValues) text.Append(CultureInfo.InvariantCulture, $"\nquest {id}={QuestStatuses.Name(progress.Status)}|{progress.Stage}");
        foreach (var (key, count) in state.VisitCounts) text.Append(CultureInfo.InvariantCulture, $"\nvisit {key}={count}");
        foreach (var frame in state.FrameList) text.Append(CultureInfo.InvariantCulture, $"\nframe {frame.Conversation}|{frame.Node}|{(frame.Preempted ? "preempt" : frame.Reenter ? "reenter" : "return")}");
        text.Append(CultureInfo.InvariantCulture, $"\nrecorded={state.Recorded}");
        foreach (var queued in state.QueueList) text.Append("\nqueue ").Append(queued);
        foreach (var entry in state.HistoryList)
        {
            text.Append(CultureInfo.InvariantCulture, $"\nhistory {entry.Turn}|{entry.Tick}|{entry.Kind}|{entry.Conversation}|{entry.Node}|{JsonSerializer.Serialize(entry.Speaker)}|{JsonSerializer.Serialize(entry.Key)}|{JsonSerializer.Serialize(entry.Chosen)}|{entry.Seen}|{entry.Skipped}|{entry.Replaced}");
            foreach (var arg in entry.Args)
                text.Append(CultureInfo.InvariantCulture, $"\n arg {arg.Name}={arg.Value.Kind}:{JsonSerializer.Serialize(arg.Value.Kind == FactKind.Integer ? arg.Value.Integer.ToString(CultureInfo.InvariantCulture) : arg.Value.Kind == FactKind.Boolean ? (arg.Value.Boolean ? "true" : "false") : arg.Value.Text)}");
        }
        return text.ToString();
    }

    public static string StateDigest(DialogueState state) => Digest.Of(Canonical(state));

    public static byte[] Write(DialogueRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var state = runtime.State;
        var envelope = new Envelope
        {
            Format = Format, Document = runtime.Set.Digest, Id = runtime.Set.Id, Seed = state.Seed.ToString(CultureInfo.InvariantCulture), Counter = state.RollCounter,
            Conversation = state.Conversation, Node = state.Node, Waiting = WaitingNames[(int)state.WaitingFor], Digest = StateDigest(state),
        };
        foreach (var (id, value) in state.VariableValues)
            envelope.Variables.Add(new VariableRow { Id = id, Kind = value.Kind.ToString(), Integer = value.Integer, Boolean = value.Boolean, Text = value.Text });
        foreach (var (id, progress) in state.QuestValues) envelope.Quests.Add(new QuestRow { Id = id, Status = QuestStatuses.Name(progress.Status), Stage = progress.Stage });
        foreach (var (key, count) in state.VisitCounts) envelope.Visits.Add(new VisitRow { Key = key, Count = count });
        foreach (var frame in state.FrameList) envelope.Frames.Add(new FrameRow { Conversation = frame.Conversation, Node = frame.Node, Reenter = frame.Reenter, Preempted = frame.Preempted });
        envelope.Recorded = state.Recorded;
        envelope.Queue.AddRange(state.QueueList);
        foreach (var entry in state.HistoryList)
        {
            var row = new HistoryRow
            {
                Turn = entry.Turn, Tick = entry.Tick, Kind = entry.Kind.ToString(), Conversation = entry.Conversation, Node = entry.Node, Speaker = entry.Speaker, Key = entry.Key,
                Chosen = entry.Chosen, Seen = entry.Seen, Skipped = entry.Skipped, Replaced = entry.Replaced,
            };
            foreach (var arg in entry.Args) row.Args.Add(new VariableRow { Id = arg.Name, Kind = arg.Value.Kind.ToString(), Integer = arg.Value.Integer, Boolean = arg.Value.Boolean, Text = arg.Value.Text });
            envelope.History.Add(row);
        }
        return JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
    }

    public static DialogueRuntime Open(DialogueSet set, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (bytes.Length > MaxBytes) throw new EngineRefusedException(DialogueCodes.SaveFormat, "a dialogue save is at most " + MaxBytes + " bytes");
        Envelope? envelope;
        try { envelope = JsonSerializer.Deserialize<Envelope>(bytes.Span, Options); }
        catch (JsonException exception) { throw new EngineRefusedException(DialogueCodes.SaveFormat, "not a dialogue save: " + exception.Message); }
        if (envelope is null || envelope.Format != Format) throw new EngineRefusedException(DialogueCodes.SaveFormat, "unsupported dialogue save format");
        if (envelope.Document != set.Digest || envelope.Id != set.Id) throw new EngineRefusedException(DialogueCodes.SaveDocument, "the save was taken from another document");

        var state = new DialogueState();
        if (!ulong.TryParse(envelope.Seed, NumberStyles.None, CultureInfo.InvariantCulture, out var seed)) throw new EngineRefusedException(DialogueCodes.SaveFormat, "the seed is not a number");
        state.Seed = seed;
        state.RollCounter = envelope.Counter;
        state.Conversation = envelope.Conversation;
        state.Node = envelope.Node;
        var waiting = Array.IndexOf(WaitingNames, envelope.Waiting);
        if (waiting < 0) throw new EngineRefusedException(DialogueCodes.SaveFormat, "unknown waiting state '" + envelope.Waiting + "'");
        state.WaitingFor = (Waiting)waiting;
        foreach (var row in envelope.Variables)
        {
            var value = row.Kind switch
            {
                "Integer" => FactValue.From(row.Integer),
                "Boolean" => FactValue.From(row.Boolean),
                "String" => row.Text is null ? throw new EngineRefusedException(DialogueCodes.SaveFormat, "variable '" + row.Id + "' has no text") : FactValue.From(row.Text),
                _ => null,
            } ?? throw new EngineRefusedException(DialogueCodes.SaveFormat, "variable '" + row.Id + "' has an unknown kind");
            if (!state.VariableValues.TryAdd(row.Id, value)) throw new EngineRefusedException(DialogueCodes.SaveFormat, "variable '" + row.Id + "' appears twice");
        }
        foreach (var row in envelope.Quests)
        {
            if (!QuestStatuses.TryParse(row.Status, out var status)) throw new EngineRefusedException(DialogueCodes.SaveFormat, "quest '" + row.Id + "' has an unknown status");
            if (!state.QuestValues.TryAdd(row.Id, new QuestProgress(status, row.Stage))) throw new EngineRefusedException(DialogueCodes.SaveFormat, "quest '" + row.Id + "' appears twice");
        }
        foreach (var row in envelope.Frames) state.FrameList.Add(new DialogueFrame(row.Conversation, row.Node, row.Reenter, row.Preempted));
        state.Recorded = envelope.Recorded;
        state.QueueList.AddRange(envelope.Queue);
        foreach (var row in envelope.History)
        {
            if (!Enum.TryParse<HistoryKind>(row.Kind, out var kind) || !Enum.IsDefined(kind)) throw new EngineRefusedException(DialogueCodes.SaveFormat, "a history entry has an unknown kind");
            var args = new List<MessageArg>(row.Args.Count);
            foreach (var arg in row.Args)
                args.Add(new MessageArg(arg.Id, arg.Kind switch
                {
                    "Integer" => FactValue.From(arg.Integer),
                    "Boolean" => FactValue.From(arg.Boolean),
                    "String" => arg.Text is null ? throw new EngineRefusedException(DialogueCodes.SaveFormat, "a history value has no text") : FactValue.From(arg.Text),
                    _ => null,
                } ?? throw new EngineRefusedException(DialogueCodes.SaveFormat, "a history value has an unknown kind")));
            state.HistoryList.Add(new HistoryEntry(row.Turn, row.Tick, kind, row.Conversation, row.Node, row.Speaker, row.Key, args, row.Chosen, row.Seen, row.Skipped, row.Replaced));
        }
        foreach (var row in envelope.Visits)
            if (!state.VisitCounts.TryAdd(row.Key, row.Count)) throw new EngineRefusedException(DialogueCodes.SaveFormat, "node count '" + row.Key + "' appears twice");

        // The state is checked against the document before its digest is taken: the digest is not a secret, so a forged one must not bring in a state the document could not have made, and
        // the digest is never computed over counts, lengths or values the document does not allow.
        StateRules.Check(set, state, DialogueCodes.SaveState);
        if (envelope.Digest != StateDigest(state)) throw new EngineRefusedException(DialogueCodes.SaveDigest, "the save digest does not match its state");
        return new DialogueRuntime(set, state);
    }
}
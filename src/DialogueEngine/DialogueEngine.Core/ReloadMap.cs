using System.Text.Json;
using EngineCommon;

namespace DialogueEngine;

/// <summary>
/// What an edited document calls the things the old one called something else, so a running session can follow a rename instead of losing the state that belonged to the old name. It is a
/// strict JSON document of its own (the dialogue document is not changed by it): <c>format</c> 1 and any of the tables <c>conversations</c>, <c>nodes</c> (<c>conversation/node</c> to
/// <c>conversation/node</c>, for a node renamed or moved to another conversation), <c>variables</c>, <c>quests</c> and <c>stages</c> (<c>quest/stage</c>, the quest being the old quest's id, to the new
/// stage id). A renamed conversation takes its nodes along under their own ids unless <c>nodes</c> says otherwise. Checked against both documents when a reload uses it: every old name must exist
/// in the old document, every new name in the new one, and two old things may not become one.
/// </summary>
public sealed class ReloadMap
{
    public const int Format = 1;
    private const int MaxEntries = 4096;

    /// <summary>The longest map read at all, in characters: five tables of 4,096 entries with two ids of up to 128 characters each come to about 5.4 million characters at the very most.</summary>
    public const int MaxCharacters = 8 * 1024 * 1024;
    private static readonly StrictJson Json = new(DialogueCodes.ForMap);

    public static readonly ReloadMap Empty = new(new(), new(), new(), new(), new());

    private ReloadMap(Dictionary<string, string> conversations, Dictionary<string, string> nodes, Dictionary<string, string> variables, Dictionary<string, string> quests, Dictionary<string, string> stages)
    {
        Conversations = conversations; Nodes = nodes; Variables = variables; Quests = quests; Stages = stages;
    }

    public IReadOnlyDictionary<string, string> Conversations { get; }
    public IReadOnlyDictionary<string, string> Nodes { get; }
    public IReadOnlyDictionary<string, string> Variables { get; }
    public IReadOnlyDictionary<string, string> Quests { get; }
    public IReadOnlyDictionary<string, string> Stages { get; }

    public bool IsEmpty => Conversations.Count + Nodes.Count + Variables.Count + Quests.Count + Stages.Count == 0;

    public static ReloadMap Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaxCharacters) throw Json.Refuse(DialogueCodes.MapFormat, "a reload map is at most " + MaxCharacters + " characters");
        using var document = Json.Parse(text, "reload map");
        var root = Json.Object(document.RootElement, "reload map", ["format"], "conversations", "nodes", "variables", "quests", "stages");
        if (root.GetProperty("format").ValueKind != JsonValueKind.Number || !root.GetProperty("format").TryGetInt32(out var format) || format != Format)
            throw Json.Refuse(DialogueCodes.MapFormat, "reload map format must be " + Format);
        return new ReloadMap(Table(root, "conversations", false, false), Table(root, "nodes", true, true), Table(root, "variables", false, false), Table(root, "quests", false, false), Table(root, "stages", true, false));
    }

    private static Dictionary<string, string> Table(JsonElement root, string field, bool qualifiedFrom, bool qualifiedTo)
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Json.Has(root, field)) return table;
        var holder = root.GetProperty(field);
        if (holder.ValueKind != JsonValueKind.Object) throw Json.Refuse(DialogueCodes.MapFormat, "reload map " + field + " must be an object of old name to new name");
        foreach (var property in holder.EnumerateObject())
        {
            if (table.Count >= MaxEntries) throw Json.Refuse(DialogueCodes.MapFormat, "reload map " + field + " has more than " + MaxEntries + " entries");
            if (property.Value.ValueKind != JsonValueKind.String) throw Json.Refuse(DialogueCodes.MapFormat, "reload map " + field + "." + property.Name + " must name its new name");
            Name(property.Name, qualifiedFrom, field);
            Name(property.Value.GetString()!, qualifiedTo, field);
            if (!table.TryAdd(property.Name, property.Value.GetString()!)) throw Json.Refuse(DialogueCodes.MapFormat, "reload map " + field + " names '" + property.Name + "' twice");
        }
        return table;
    }

    private static void Name(string name, bool qualified, string field)
    {
        var parts = name.Split('/');
        if (parts.Length != (qualified ? 2 : 1)) throw Json.Refuse(DialogueCodes.MapFormat, "reload map " + field + " name '" + name + "' must be " + (qualified ? "first/second" : "one id"));
        foreach (var part in parts) Json.CheckId(part, "reload map " + field);
    }

    // ---- the mapping --------------------------------------------------------------------------------------------------------------------

    public string Variable(string id) => Variables.GetValueOrDefault(id, id);

    public string Quest(string id) => Quests.GetValueOrDefault(id, id);

    /// <summary>The new id of a conversation of the old document.</summary>
    public string Conversation(string id) => Conversations.GetValueOrDefault(id, id);

    /// <summary>The new id of a stage of an old quest.</summary>
    public string Stage(string oldQuest, string stage) => stage.Length == 0 ? stage : Stages.GetValueOrDefault(oldQuest + "/" + stage, stage);

    /// <summary>Where a node of the old document is in the new one: named in <c>nodes</c>, else the same node id in the renamed conversation.</summary>
    public (string Conversation, string Node) Node(string conversation, string node)
    {
        if (Nodes.TryGetValue(conversation + "/" + node, out var to)) return Split(to);
        return (Conversations.GetValueOrDefault(conversation, conversation), node);
    }

    /// <summary>Checks the map against the document the session has and the one it is moving to (a reload does this itself; the tool does it without a session). Refuses with the first problem; nothing is changed.</summary>
    public void Check(DialogueSet old, DialogueSet next)
    {
        Sources(Conversations, old.Conversations.ContainsKey, next.Conversations.ContainsKey, "conversation");
        Sources(Variables, old.Variables.ContainsKey, next.Variables.ContainsKey, "variable");
        Sources(Quests, old.Quests.ContainsKey, next.Quests.ContainsKey, "quest");
        foreach (var (from, to) in Nodes)
        {
            var (fc, fn) = Split(from);
            var (tc, tn) = Split(to);
            if (!old.Conversations.TryGetValue(fc, out var oc) || !oc.Nodes.ContainsKey(fn)) throw new EngineRefusedException(DialogueCodes.MapSource, "the old document has no node '" + from + "'");
            if (!next.Conversations.TryGetValue(tc, out var nc) || !nc.Nodes.ContainsKey(tn)) throw new EngineRefusedException(DialogueCodes.MapTarget, "the new document has no node '" + to + "'");
        }
        foreach (var (from, to) in Stages)
        {
            var (quest, stage) = Split(from);
            if (!old.Quests.TryGetValue(quest, out var oq) || !oq.HasStage(stage)) throw new EngineRefusedException(DialogueCodes.MapSource, "the old document has no stage '" + from + "'");
            if (!next.Quests.TryGetValue(Quest(quest), out var nq) || !nq.HasStage(to)) throw new EngineRefusedException(DialogueCodes.MapTarget, "the new quest '" + Quest(quest) + "' has no stage '" + to + "'");
        }
        // Two old things may not become one: what each old one is called in the new document (when the new document has it) must differ from every other.
        Injective(old.Variables.Keys.Select(Variable).Where(next.Variables.ContainsKey).ToList(), "variable");
        Injective(old.Quests.Keys.Select(Quest).Where(next.Quests.ContainsKey).ToList(), "quest");
        var nodes = new List<string>();
        foreach (var conversation in old.Conversations.Values)
            foreach (var id in conversation.Nodes.Keys)
            {
                var (c, n) = Node(conversation.Id, id);
                if (next.Conversations.TryGetValue(c, out var target) && target.Nodes.ContainsKey(n)) nodes.Add(c + "/" + n);
            }
        Injective(nodes, "node");
        foreach (var quest in old.Quests.Values)
        {
            if (!next.Quests.TryGetValue(Quest(quest.Id), out var target)) continue;
            Injective(quest.Stages.Select(s => Stage(quest.Id, s.Id)).Where(target.HasStage).ToList(), "stage of quest '" + quest.Id + "'");
        }
    }

    private static void Sources(IReadOnlyDictionary<string, string> table, Func<string, bool> inOld, Func<string, bool> inNew, string what)
    {
        foreach (var (from, to) in table)
        {
            if (!inOld(from)) throw new EngineRefusedException(DialogueCodes.MapSource, "the old document has no " + what + " '" + from + "'");
            if (!inNew(to)) throw new EngineRefusedException(DialogueCodes.MapTarget, "the new document has no " + what + " '" + to + "'");
        }
    }

    private static void Injective(IReadOnlyCollection<string> targets, string what)
    {
        if (new HashSet<string>(targets, StringComparer.Ordinal).Count != targets.Count) throw new EngineRefusedException(DialogueCodes.MapConflict, "two old " + what + "s would become the same one");
    }

    private static (string, string) Split(string qualified)
    {
        var cut = qualified.IndexOf('/');
        return (qualified[..cut], qualified[(cut + 1)..]);
    }
}

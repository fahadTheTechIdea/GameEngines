using DialogueEngine;
using EngineCommon;
using WorldEventsEngine;

namespace GameRuntime;

/// <summary>One rule: when the world issues this signal (in this region, or in any when none is named), the dialogue engine starts the quest (a story trigger) or speaks the conversation. Exactly one of the two is named.</summary>
public sealed record WorldStoryRule(string Signal, string Quest = "", string Conversation = "", string Region = "");

/// <summary>
/// Story triggers from the world. When the world issues a signal that a rule names (a flood has started, the plague has broken out), the bridge asks the dialogue component to start the rule's quest or conversation
/// (events, so deferred and answered in that component's next Update, refused there if it cannot start). The dialogue engine never reads the world's state and the world never reads the quest log: they meet through
/// these events, and the facts of <see cref="WorldDialogueFacts"/>. Rules are checked when the game is assembled against both documents: the signal must be one the world can issue, the quest or conversation one the
/// dialogue document has.
/// </summary>
public sealed class WorldDialogueStory : IGameComponent
{
    public const string BadRule = "WE_STORY_RULE";

    private readonly Dictionary<string, List<WorldStoryRule>> _bySignal = new(StringComparer.Ordinal);
    private readonly string _worldChannel;
    private readonly string _dialogueChannel;
    private IEventPort? _events;
    private long _triggered;

    public WorldDialogueStory(string id, IEnumerable<WorldStoryRule> rules, WorldSet world, DialogueSet dialogue, string worldChannel = "", string dialogueChannel = "")
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(dialogue);
        Id = id;
        foreach (var rule in rules)
        {
            if (!world.Signals.Contains(rule.Signal)) throw new EngineRefusedException(BadRule, "the world document never issues the signal '" + rule.Signal + "'");
            if (rule.Region.Length > 0 && !world.RegionById.ContainsKey(rule.Region)) throw new EngineRefusedException(BadRule, "the world document has no region '" + rule.Region + "'");
            if ((rule.Quest.Length > 0) == (rule.Conversation.Length > 0)) throw new EngineRefusedException(BadRule, "the rule for '" + rule.Signal + "' names a quest or a conversation, one of the two");
            if (rule.Quest.Length > 0 && !dialogue.Quests.ContainsKey(rule.Quest)) throw new EngineRefusedException(BadRule, "the dialogue document has no quest '" + rule.Quest + "'");
            if (rule.Conversation.Length > 0 && !dialogue.Conversations.ContainsKey(rule.Conversation)) throw new EngineRefusedException(BadRule, "the dialogue document has no conversation '" + rule.Conversation + "'");
            if (!_bySignal.TryGetValue(rule.Signal, out var list)) _bySignal[rule.Signal] = list = new List<WorldStoryRule>();
            list.Add(rule);
        }
        _worldChannel = worldChannel;
        _dialogueChannel = dialogueChannel;
    }

    public string Id { get; }

    /// <summary>How many quests and conversations have been asked for.</summary>
    public long Triggered => _triggered;

    public void Init(in ComponentContext context)
    {
        _events = context.Events;
        context.Events.Connect<WorldEffectIssued>(OnEffect);
    }

    private void OnEffect(WorldEffectIssued e)
    {
        if (e.Channel != _worldChannel || e.Kind != Words.Name(EffectKind.Signal) || !_bySignal.TryGetValue(e.Name, out var rules)) return;
        foreach (var rule in rules.Where(r => r.Region.Length == 0 || r.Region == e.Region))
        {
            var request = Id + ":" + e.Step + ":" + _triggered;
            if (rule.Quest.Length > 0) _events!.Emit(new DialogueQuestStartRequested(request, rule.Quest, _dialogueChannel));
            else _events!.Emit(new DialogueStartRequested(request, rule.Conversation, _dialogueChannel));
            _triggered++;
        }
    }

    public void Update(in UpdateContext context)
    {
        // Nothing to do on a tick of its own: it acts on the world's effect events.
    }
}

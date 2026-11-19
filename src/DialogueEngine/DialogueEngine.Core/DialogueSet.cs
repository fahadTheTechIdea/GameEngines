using ScenarioEngine.Contracts;

namespace DialogueEngine;

/// <summary>
/// A loaded, checked document: variables, external facts, speakers, conversations and quests, and the digest of the exact text it was read from.
/// Immutable; a save names this digest.
/// </summary>
public sealed class DialogueSet
{
    internal DialogueSet(string id, string digest, IReadOnlyDictionary<string, VariableDef> variables, IReadOnlyDictionary<string, ExternalFactDef> externals,
        IReadOnlyDictionary<string, SpeakerDef> speakers, IReadOnlyDictionary<string, ConversationDef> conversations, IReadOnlyDictionary<string, QuestDef> quests,
        IReadOnlyDictionary<string, IReadOnlyList<string>> keys, IReadOnlyDictionary<string, IReadOnlyList<string>> keyUses,
        IReadOnlyDictionary<string, ContentWarningDef> warnings, int historyLimit)
    {
        KeyUses = keyUses;
        ContentWarnings = warnings;
        HistoryLimit = historyLimit;
        Id = id;
        Digest = digest;
        Variables = variables;
        Externals = externals;
        Speakers = speakers;
        Conversations = conversations;
        Quests = quests;
        Keys = keys;
    }

    public string Id { get; }

    /// <summary>SHA-256 of the exact UTF-8 text of the document (reformatting it changes the digest).</summary>
    public string Digest { get; }

    public IReadOnlyDictionary<string, VariableDef> Variables { get; }
    public IReadOnlyDictionary<string, ExternalFactDef> Externals { get; }
    public IReadOnlyDictionary<string, SpeakerDef> Speakers { get; }
    public IReadOnlyDictionary<string, ConversationDef> Conversations { get; }
    public IReadOnlyDictionary<string, QuestDef> Quests { get; }

    /// <summary>The content warnings the document declares, by tag.</summary>
    public IReadOnlyDictionary<string, ContentWarningDef> ContentWarnings { get; }

    /// <summary>How many shown lines and taken answers the session keeps for a history log (0: none).</summary>
    public int HistoryLimit { get; }

    /// <summary>The history a document keeps when it does not say.</summary>
    public const int DefaultHistoryLimit = 100;

    /// <summary>Every localisation key the document uses, with the placeholder names its text may use (empty for a key that takes none).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Keys { get; }

    /// <summary>Where each localisation key is used (<c>conversation 'main' node 'hi'</c>, <c>quest 'hold' stage 'order'</c>), for the context a translator is given.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> KeyUses { get; }
}
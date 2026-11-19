namespace DialogueEngine;

/// <summary>What a hot reload does when the open conversation cannot go on in the new document (its node is gone or is another kind of node, or a call it is inside is).</summary>
public enum ReloadMode
{
    /// <summary>Refuse the reload with <see cref="DialogueCodes.ReloadIncompatible"/>; nothing changes. The default.</summary>
    Refuse,
    /// <summary>Close the open conversation (and the calls it is inside) and keep everything else: the game starts the conversation again when it likes.</summary>
    RestartConversation,
}
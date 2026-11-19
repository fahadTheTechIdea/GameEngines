using System.Globalization;

namespace GameRuntime;

/// <summary>
/// How a game reacts to what a conversation did, in one place: a trigger is <c>signal:&lt;name&gt;</c> for a signal a conversation raised or <c>quest:&lt;id&gt;:&lt;status&gt;</c> for a quest that reached a status; the
/// text arguments (<c>s:</c>) of a signal fill <c>{0}</c>, <c>{1}</c>, ... in the ids of a rule. Every engine that turns dialogue outcomes into its own requests (relationship, economy) uses these, so the
/// vocabulary of triggers and arguments has one owner.
/// </summary>
public static class DialogueTriggers
{
    public const string SignalPrefix = "signal:";
    public const string QuestPrefix = "quest:";

    /// <summary>True when the text is shaped like a trigger.</summary>
    public static bool IsTrigger(string trigger) => trigger.StartsWith(SignalPrefix, StringComparison.Ordinal) || trigger.StartsWith(QuestPrefix, StringComparison.Ordinal);

    public static string ForSignal(string name) => SignalPrefix + name;

    public static string ForQuest(string quest, string status) => QuestPrefix + quest + ":" + status;

    /// <summary>The text arguments (<c>s:text</c>) of a signal in order, with the percent-encoding undone.</summary>
    public static List<string> TextArguments(string args) =>
        args.Length == 0 ? new List<string>() : args.Split(';').Where(a => a.StartsWith("s:", StringComparison.Ordinal)).Select(a => a[2..].Replace("%3B", ";", StringComparison.Ordinal).Replace("%25", "%", StringComparison.Ordinal)).ToList();

    /// <summary>The template with <c>{0}</c>, <c>{1}</c>, ... replaced by the text arguments.</summary>
    public static string Fill(string template, IReadOnlyList<string> texts)
    {
        var result = template;
        for (var i = 0; i < texts.Count; i++) result = result.Replace("{" + i.ToString(CultureInfo.InvariantCulture) + "}", texts[i], StringComparison.Ordinal);
        return result;
    }
}

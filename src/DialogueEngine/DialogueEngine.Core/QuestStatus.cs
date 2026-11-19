namespace DialogueEngine;

public enum QuestStatus { NotStarted, Active, Completed, Failed, Abandoned }

/// <summary>The names the statuses go by in documents, facts and saves.</summary>
public static class QuestStatuses
{
    private static readonly string[] Names = ["not-started", "active", "completed", "failed", "abandoned"];

    public static string Name(QuestStatus status) => Names[(int)status];

    public static IReadOnlyList<string> All => Names;

    public static bool TryParse(string name, out QuestStatus status)
    {
        var at = System.Array.IndexOf(Names, name);
        status = (QuestStatus)Math.Max(at, 0);
        return at >= 0;
    }
}
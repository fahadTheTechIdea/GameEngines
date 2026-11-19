using AIEngine;
using EngineCommon;

/// <summary>
/// The AI authoring tool. <c>check</c> reads a document the way a game does and says whether it is accepted, with the figures a designer needs (each behaviour's most expensive
/// think), or which rule refused it and where; <c>explain</c> runs one think of one agent on a scenario and prints why it chose what it did.
/// Exit codes: 0 done, 1 the document or the scenario was refused (the code is printed), 2 wrong command line or a file problem.
/// </summary>
public static partial class AiCli
{
    public const string Usage =
        "usage: AIEngine.Cli check <document.json>\n" +
        "       AIEngine.Cli explain <document.json> <scenario.json>\n" +
        "       AIEngine.Cli import-tree <tree.json> --id <document-id> --out <ai-document.json> [--trace <map.json>]\n" +
        "exit: 0 done | 1 refused (the code is printed) | 2 usage or file problem";

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count == 0 || args[0] is not ("check" or "explain" or "import-tree")) return Wrong(error, Usage);
        if (args[0] == "import-tree" && args.Count < 2) return Wrong(error, Usage);
        if (args[0] != "import-tree" && args.Count != (args[0] == "check" ? 2 : 3)) return Wrong(error, Usage);
        try
        {
            if (args[0] == "import-tree") return ImportTree(args, output, error);
            var document = File.ReadAllText(args[1]);
            return args[0] == "check" ? Check(document, output) : Explain(document, File.ReadAllText(args[2]), output);
        }
        catch (IOException exception) { return Wrong(error, "file problem: " + exception.Message); }
        catch (UnauthorizedAccessException exception) { return Wrong(error, "file problem: " + exception.Message); }
    }

    private static int Wrong(TextWriter error, string message)
    {
        error.WriteLine(message);
        return 2;
    }

    private static int Refused(TextWriter output, EngineRefusedException refusal)
    {
        output.WriteLine("refused " + refusal.Code + ": " + refusal.Message);
        return 1;
    }

    private static int Check(string document, TextWriter output)
    {
        AiSet set;
        try { set = AiLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        output.WriteLine($"ok {set.Id} digest {set.Digest} keys {set.Keys.Count} percept-kinds {set.Percepts.Count} intents {set.Intents.Count} behaviours {set.Behaviors.Count}");
        foreach (var (id, behavior) in set.Behaviors.OrderBy(b => b.Key, StringComparer.Ordinal))
            output.WriteLine($"behaviour {id} every {behavior.Every} nodes {behavior.NodeCount} memory-slots {behavior.SlotCount} max-cost {behavior.MaxCost}");
        output.WriteLine($"budget a step needs at least {set.MaxCost}");
        return 0;
    }
}

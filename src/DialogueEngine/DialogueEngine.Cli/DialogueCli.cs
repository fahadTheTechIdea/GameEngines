using System.Text.Json;
using DialogueEngine;
using EngineCommon;

/// <summary>
/// The dialogue authoring tool. <c>check</c> reads a document the way a game does and says whether it is accepted, or which rule refused it and where; <c>explain</c> plays a scripted session
/// and prints why each call did what it did; <c>export-strings</c> and <c>import-strings</c> carry text to and from translators; <c>coverage</c> says how much of a document each language covers;
/// <c>graph</c> draws the conversations for Graphviz; <c>import</c> converts a Yarn Spinner script or a compiled Ink story (and prints the capability matrix of either); <c>reload-map</c> checks a hot-reload rename map against two documents. Exit codes: 0 done (for <c>coverage</c>, every table complete), 1 refused or incomplete (the code or the lists are printed), 2 wrong command line or a file problem.
/// </summary>
public static partial class DialogueCli
{
    public const string Usage =
        "usage: DialogueEngine.Cli check <document.json>\n" +
        "       DialogueEngine.Cli explain <document.json> <session.json>\n" +
        "       DialogueEngine.Cli export-strings <document.json> <source-locale> <target-locale> [--source <table.json>] [--target <table.json>]\n" +
        "       DialogueEngine.Cli import-strings <document.json> <sheet.json> --out <table.json>\n" +
        "       DialogueEngine.Cli coverage <document.json> <table.json> [<table.json> ...]\n" +
        "       DialogueEngine.Cli graph <document.json> [<conversation>]\n" +
        "       DialogueEngine.Cli import yarn|ink <source> --id <document-id> --locale <locale> --out <document.json> --strings <table.json> [--start <node>] [--starts <a,b,..>] [--all-starts] [--function <signature=fact:type>]...\n" +
        "       DialogueEngine.Cli import matrix yarn|ink\n" +
        "       DialogueEngine.Cli reload-map <old-document.json> <new-document.json> <map.json>\n" +
        "exit: 0 done | 1 refused or incomplete (the code or the lists are printed) | 2 usage or file problem";

    /// <summary>The speakers a document declares, standing in for the game catalog: the tool checks the document, not the roster of the game.</summary>
    private sealed class DeclaredSpeakers(IReadOnlySet<string> ids) : ISpeakerCatalog
    {
        public bool HasSpeaker(string id) => ids.Contains(id);
    }

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count < 2) return Wrong(error, Usage);
        try
        {
            switch (args[0])
            {
                case "check" when args.Count == 2: return Check(File.ReadAllText(args[1]), output);
                case "explain" when args.Count == 3: return Explain(File.ReadAllText(args[1]), File.ReadAllText(args[2]), output);
                case "export-strings" when args.Count >= 4: return ExportStrings(args, output, error);
                case "import-strings" when args.Count == 5 && args[3] == "--out": return ImportStrings(File.ReadAllText(args[1]), File.ReadAllText(args[2]), args[4], output);
                case "coverage" when args.Count >= 3: return Coverage(File.ReadAllText(args[1]), args.Skip(2).Select(File.ReadAllText).ToList(), output);
                case "graph" when args.Count is 2 or 3: return Graph(File.ReadAllText(args[1]), args.Count == 3 ? args[2] : null, output);
                case "import" when args.Count >= 3: return Import(args, output, error);
                case "reload-map" when args.Count == 4: return ReloadMapCheck(File.ReadAllText(args[1]), File.ReadAllText(args[2]), File.ReadAllText(args[3]), output);
            }
        }
        catch (IOException exception) { return Wrong(error, "file problem: " + exception.Message); }
        catch (UnauthorizedAccessException exception) { return Wrong(error, "file problem: " + exception.Message); }
        return Wrong(error, Usage);
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

    private static DialogueSet Load(string document)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var parsed = JsonDocument.Parse(document);
            if (parsed.RootElement.ValueKind == JsonValueKind.Object && parsed.RootElement.TryGetProperty("speakers", out var speakers) && speakers.ValueKind == JsonValueKind.Array)
                foreach (var speaker in speakers.EnumerateArray())
                    if (speaker.ValueKind == JsonValueKind.Object && speaker.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String) ids.Add(id.GetString()!);
        }
        catch (JsonException unreadable)
        {
            // Not JSON at all: the loader gives the refusal code the author needs, and it is the reported outcome.
            // If the loader were ever to accept text the parser rejected, that disagreement fails loudly instead of passing.
            DialogueLoader.Load(document, new DeclaredSpeakers(ids));
            throw new InvalidOperationException("the loader accepted a document the JSON parser rejected: " + unreadable.Message, unreadable);
        }
        return DialogueLoader.Load(document, new DeclaredSpeakers(ids));
    }

    private static int Check(string document, TextWriter output)
    {
        DialogueSet set;
        try { set = Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        output.WriteLine($"ok {set.Id} digest {set.Digest} conversations {set.Conversations.Count} variables {set.Variables.Count} external-facts {set.Externals.Count} speakers {set.Speakers.Count} quests {set.Quests.Count} text-keys {set.Keys.Count}");
        foreach (var (id, conversation) in set.Conversations.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            var kinds = conversation.Nodes.Values.GroupBy(n => n.Kind).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + " " + g.Count());
            output.WriteLine($"conversation {id} start {conversation.Start} nodes {conversation.Nodes.Count} ({string.Join(", ", kinds)})");
        }
        return 0;
    }
}

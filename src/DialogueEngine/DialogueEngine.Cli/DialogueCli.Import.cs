using DialogueEngine;
using EngineCommon;

public static partial class DialogueCli
{
    /// <summary>
    /// <c>import yarn|ink &lt;source&gt; --id &lt;document id&gt; --locale &lt;locale&gt; --out &lt;document.json&gt; --strings &lt;table.json&gt; [--start &lt;node&gt;]</c> converts a Yarn Spinner script or a compiled Ink
    /// story and writes the document and the source-locale strings; <c>import matrix yarn|ink</c> prints what each feature of the format becomes.
    /// </summary>
    private static int Import(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count == 3 && args[1] == "matrix")
        {
            switch (args[2])
            {
                case "yarn": output.Write(ImportCapabilities.Render(ImportCapabilities.YarnSpinner)); return 0;
                case "ink": output.Write(ImportCapabilities.Render(ImportCapabilities.InkJson)); return 0;
            }
            return Wrong(error, Usage);
        }
        if (args.Count < 3 || args[1] is not ("yarn" or "ink")) return Wrong(error, Usage);
        string? id = null, locale = null, document = null, strings = null, start = null;
        IReadOnlyList<string>? starts = null;
        var allStarts = false;
        var functionTexts = new List<string>();
        for (var i = 3; i < args.Count; i += 2)
        {
            if (args[i] == "--all-starts") { allStarts = true; i--; continue; }
            if (i + 1 >= args.Count) return Wrong(error, Usage);
            switch (args[i])
            {
                case "--starts": starts = args[i + 1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries); break;
                case "--function": functionTexts.Add(args[i + 1]); break;
                case "--id": id = args[i + 1]; break;
                case "--locale": locale = args[i + 1]; break;
                case "--out": document = args[i + 1]; break;
                case "--strings": strings = args[i + 1]; break;
                case "--start": start = args[i + 1]; break;
                default: return Wrong(error, Usage);
            }
        }
        if (id is null || locale is null || document is null || strings is null) return Wrong(error, Usage);
        try
        {
            var source = File.ReadAllText(args[2]);
            var options = new ImportOptions(id, locale, start) { Starts = starts, AllStarts = allStarts, Functions = functionTexts.Select(ImportFunction.Parse).ToList() };
            var result = args[1] == "yarn" ? YarnImporter.Import(source, options) : InkImporter.Import(source, options);
            File.WriteAllText(document, result.Document);
            File.WriteAllText(strings, result.Strings);
            output.WriteLine($"converted {args[1]}: document {document}, strings {strings}");
            foreach (var note in result.Report.Notes) output.WriteLine($"{note.Status.ToString().ToLowerInvariant()} {note.Feature} x{note.Count}: {note.Detail}");
            output.WriteLine("speakers the game catalog must provide: " + string.Join(", ", result.Speakers));
            output.WriteLine("conversations the game can start: " + string.Join(", ", result.Entries.Select(e => e.Name + " -> " + e.Conversation)));
            if (result.Functions.Count > 0) output.WriteLine("external facts the host must supply: " + string.Join(", ", result.Functions.Select(f => "ext." + f.Fact + " (" + f.Kind + ")")));
            return 0;
        }
        catch (EngineRefusedException refusal)
        {
            return Refused(output, refusal);
        }
    }
}

using DialogueEngine;
using EngineCommon;

public static partial class DialogueCli
{
    /// <summary>Writes the translators' sheet for a document: <c>export-strings &lt;document&gt; &lt;source-locale&gt; &lt;target-locale&gt; [--source table] [--target table]</c>.</summary>
    private static int ExportStrings(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        string? sourcePath = null, targetPath = null;
        for (var i = 4; i < args.Count; i += 2)
        {
            if (i + 1 >= args.Count) return Wrong(error, Usage);
            switch (args[i])
            {
                case "--source": sourcePath = args[i + 1]; break;
                case "--target": targetPath = args[i + 1]; break;
                default: return Wrong(error, Usage);
            }
        }
        try
        {
            var set = Load(File.ReadAllText(args[1]));
            var source = sourcePath is null ? null : LocaleTable.Load(File.ReadAllText(sourcePath));
            var target = targetPath is null ? null : LocaleTable.Load(File.ReadAllText(targetPath));
            output.WriteLine(TranslationSheet.Export(set, args[2], args[3], source, target));
            return 0;
        }
        catch (EngineRefusedException refusal)
        {
            return Refused(output, refusal);
        }
    }

    /// <summary>Reads a translated sheet and writes the locale table to <paramref name="outPath"/>; prints what is still owed, what was refused and whether the sheet is older than the document.</summary>
    private static int ImportStrings(string document, string sheet, string outPath, TextWriter output)
    {
        try
        {
            var set = Load(document);
            var import = TranslationSheet.Import(set, sheet);
            File.WriteAllText(outPath, import.Table.ToText());
            output.WriteLine($"wrote {import.Table.Locale}: {import.Report.Translated} of {import.Report.Total} messages ({import.Report.Percent}%)" + (import.Stale ? "; the sheet was made from an older version of the document" : ""));
            foreach (var key in import.Report.Missing) output.WriteLine("missing " + key);
            foreach (var problem in import.Rejected) output.WriteLine("rejected " + problem.Key + ": " + problem.Why);
            return import.Rejected.Count == 0 ? 0 : 1;
        }
        catch (EngineRefusedException refusal)
        {
            return Refused(output, refusal);
        }
    }

    /// <summary>How much of the document each table covers, and everything that is missing, left over or misplaced.</summary>
    private static int Coverage(string document, IReadOnlyList<string> tables, TextWriter output)
    {
        try
        {
            var set = Load(document);
            var complete = true;
            foreach (var text in tables)
            {
                var report = LocaleCoverage.Report(set, LocaleTable.Load(text));
                output.WriteLine($"{report.Locale}: {report.Translated} of {report.Total} ({report.Percent}%)");
                foreach (var key in report.Missing) output.WriteLine($"  missing {key}");
                foreach (var key in report.Unknown) output.WriteLine($"  unknown {key}");
                foreach (var problem in report.Problems) output.WriteLine($"  problem {problem.Key}: {problem.Why}");
                complete &= report.IsComplete;
            }
            return complete ? 0 : 1;
        }
        catch (EngineRefusedException refusal)
        {
            return Refused(output, refusal);
        }
    }
}

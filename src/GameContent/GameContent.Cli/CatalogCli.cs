using GameContent.Catalog;

/// <summary>
/// The catalog tool. Exit codes: 0 done, 1 the pack (or library) was refused, 2 wrong command line or a file problem.
/// </summary>
public static class CatalogCli
{
    public const string Usage =
        "usage: GameContent.Cli list <packs>\n" +
        "       GameContent.Cli validate <packs> <pack>        (warnings are listed; they do not refuse)\n" +
        "       GameContent.Cli validate-all <packs>           (every pack, plus the cross-pack duplicate rules)\n" +
        "       GameContent.Cli stats <packs> <pack>\n" +
        "       GameContent.Cli alchemy <packs> <pack> [--notes]\n" +
        "       GameContent.Cli sim <packs> <pack> [--notes] [--out <dir>]\n" +
        "       GameContent.Cli new-pack <packs> <id> <genre> <author>\n" +
        "exit: 0 done | 1 refused | 2 usage or file problem";

    private static readonly string[] Commands = ["list", "validate", "validate-all", "stats", "alchemy", "sim", "economy", "new-pack"];

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count < 2 || !Commands.Contains(args[0])) return Usage2(error);
        var packs = args[1];
        try
        {
            if (args[0] == "new-pack") return NewPack(args, output, error);
            if (!Directory.Exists(packs)) { error.WriteLine("packs folder not found: " + packs); return 2; }
            return args[0] switch
            {
                "list" => List(packs, output),
                "validate-all" => ValidateAll(packs, output),
                "validate" or "stats" or "alchemy" or "sim" or "economy" => OnePack(args, packs, output, error),
                "new-pack" => NewPack(args, output, error),
                _ => Usage2(error),
            };
        }
        catch (IOException exception) { error.WriteLine("file problem: " + exception.Message); return 2; }
        catch (UnauthorizedAccessException exception) { error.WriteLine("file problem: " + exception.Message); return 2; }
        catch (ArgumentException exception) { error.WriteLine(exception.Message); return 2; }
    }

    private static IEnumerable<string> PackIds(string packs) =>
        Directory.GetDirectories(packs).Order(StringComparer.Ordinal).Where(d => File.Exists(Path.Combine(d, "pack.json"))).Select(d => Path.GetFileName(d)!);

    private static int List(string packs, TextWriter output)
    {
        foreach (var id in PackIds(packs)) output.WriteLine(id);
        return 0;
    }

    private static int NewPack(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count != 5) return Usage2(error);
        foreach (var path in CatalogScaffold.Create(args[1], args[2], args[3], args[4])) output.WriteLine("wrote " + path);
        output.WriteLine("next: edit the files, then run validate " + args[1] + " " + args[2]);
        return 0;
    }

    private static int ValidateAll(string packs, TextWriter output)
    {
        var sets = new List<CatalogSet>();
        var refused = false;
        foreach (var id in PackIds(packs))
        {
            var result = CatalogLoader.Load(new DirectoryCatalogSource(packs), id);
            foreach (var item in result.Report.Items) output.WriteLine(id + ": " + Line(item));
            if (!result.Report.IsValid) { refused = true; continue; }
            sets.Add(result.Required());
        }
        var library = CatalogLibrary.CheckDuplicates(sets);
        foreach (var item in library.Items) output.WriteLine("library: " + Line(item));
        refused |= !library.IsValid;
        output.WriteLine(refused ? "refused" : $"valid packs={sets.Count}");
        return refused ? 1 : 0;
    }

    private static string Line(CatalogDiagnostic item) =>
        (item.Severity == CatalogSeverity.Warning ? "warning " : "") + item.Code + " " + item.Location + ": " + item.Message;

    private static int OnePack(IReadOnlyList<string> args, string packs, TextWriter output, TextWriter error)
    {
        if (args.Count < 3) return Usage2(error);
        var result = CatalogLoader.Load(new DirectoryCatalogSource(packs), args[2]);
        foreach (var item in result.Report.Items) output.WriteLine(Line(item));
        if (!result.Report.IsValid)
        {
            output.WriteLine("refused: " + result.Report.Errors.Count() + " findings");
            return 1;
        }
        var set = result.Required();
        switch (args[0])
        {
            case "validate":
                output.WriteLine($"valid pack={set.Root.Id} version={set.Root.Version} packs={set.Packs.Count} resources={set.Resources.Count()} recipes={set.Recipes.Count()} warnings={result.Report.Warnings.Count()}");
                return 0;
            case "stats":
                output.WriteLine(CatalogStats.Of(set, result.Report).Format());
                return 0;
            case "alchemy":
                var alchemy = CatalogToAlchemy.Convert(set);
                output.WriteLine($"alchemy elements={alchemy.ElementCount} recipes={alchemy.RecipeCount} refused={alchemy.Report.Refusals.Count()} digest={alchemy.Content.Digest[..12]}");
                Counts(alchemy.Report, output);
                Notes(alchemy.Report, args, output);
                return 0;
            case "sim":
                var sim = CatalogToSim.Convert(set);
                output.WriteLine($"sim transformations={sim.Transformations.Count} stations={sim.Stations.Count} refused={sim.Report.Refusals.Count()}");
                Counts(sim.Report, output);
                Notes(sim.Report, args, output);
                var at = Array.IndexOf(args.ToArray(), "--out");
                if (at >= 0 && at + 1 < args.Count)
                {
                    Directory.CreateDirectory(args[at + 1]);
                    foreach (var (path, text) in sim.Files) File.WriteAllText(Path.Combine(args[at + 1], path), text);
                    output.WriteLine("wrote " + sim.Files.Count + " files to " + args[at + 1]);
                }
                return 0;
            case "economy":
                return Economy(set, args, output, error);
            default:
                return Usage2(error);
        }
    }

    private static int Economy(CatalogSet set, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        // The prices and the length of a period are the game's to state; there is no default for either. The goods use the town sample's tuning (`GoodTuning.Town`).
        if (!Option(args, "--milli-per-price", out var milli) || !long.TryParse(milli, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var perPrice)
            || !Option(args, "--period-seconds", out var seconds) || !decimal.TryParse(seconds, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var period))
            return Usage2(error);
        RecipeOwnership? ownership = null;
        if (Option(args, "--owners", out var owners))
        {
            ownership = RecipeOwnership.Parse(File.ReadAllText(owners));
            var unknown = ownership.Check(set);
            foreach (var item in unknown.Items) output.WriteLine(Line(item));
            if (!unknown.IsValid) return 1;
        }
        if (perPrice < 1 || period <= 0) return Usage2(error);
        var economy = CatalogToEconomy.Convert(set, new EconomyConversionOptions(perPrice, period, GoodTuning.Town, [], ownership));
        output.WriteLine($"economy goods={economy.Goods.Count} recipes={economy.Recipes.Count} refused={economy.Report.Refusals.Count()}");
        Counts(economy.Report, output);
        Notes(economy.Report, args, output);
        if (Option(args, "--out", out var directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "goods.json"), economy.GoodsJson);
            File.WriteAllText(Path.Combine(directory, "recipes.json"), economy.RecipesJson);
            output.WriteLine("wrote goods.json and recipes.json to " + directory);
        }
        return 0;
    }

    private static bool Option(IReadOnlyList<string> args, string name, out string value)
    {
        var at = args.ToList().IndexOf(name);
        value = at >= 0 && at + 1 < args.Count ? args[at + 1] : "";
        return value.Length > 0;
    }

    private static void Counts(ConversionReport report, TextWriter output)
    {
        foreach (var (code, count) in report.CountsByCode().OrderBy(c => c.Key, StringComparer.Ordinal))
            output.WriteLine($"  {code} x{count}");
    }

    /// <summary>With <c>--notes</c>, every refusal and reduction one by one, so an author can see exactly which recipe lost what.</summary>
    private static void Notes(ConversionReport report, IReadOnlyList<string> args, TextWriter output)
    {
        if (!args.Contains("--notes")) return;
        foreach (var note in report.Notes.OrderBy(n => n.Level).ThenBy(n => n.Code, StringComparer.Ordinal).ThenBy(n => n.Subject, StringComparer.Ordinal))
            output.WriteLine($"  {note.Level.ToString().ToLowerInvariant()} {note.Code} {note.Subject}: {note.Detail}");
    }

    private static int Usage2(TextWriter error)
    {
        error.WriteLine(Usage);
        return 2;
    }
}

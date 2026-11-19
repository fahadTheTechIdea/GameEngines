using EconomyEngine;
using EngineCommon;

/// <summary>
/// The economy authoring tool. <c>check</c> reads a document the way a game does and says whether it is accepted, or which rule refused it and where; <c>simulate</c> lives a scenario (host facts and a script of
/// shocks, trades, settlements and decisions) for a number of periods and prints what happened, then the state; <c>explain</c> says why a price, a loan, a currency, a route, a reach or a production line is what it is, in a save that <c>simulate</c>
/// wrote; <c>coverage</c> says how much of a document each locale table covers. Exit codes: 0 done (for <c>coverage</c>, every table complete; for <c>simulate</c>, every scripted call carried out), 1 refused or
/// incomplete (the code or the lists are printed), 2 wrong command line, scenario or file problem.
/// </summary>
public static partial class EconomyCli
{
    public const string Usage =
        "usage: EconomyEngine.Cli check <economy.json>\n" +
        "       EconomyEngine.Cli simulate <economy.json> <scenario.json> <periods> [--save <state.json>] [--verbose]\n" +
        "       EconomyEngine.Cli simulate <economy.json> <scenario.json> [<periods>] --seeds <n> [--from-seed <s>] [--days <d>] [--metrics <a,b,...>] [--rows]\n" +
        "       EconomyEngine.Cli explain <economy.json> <state.json> price<market> <good>\n" +
        "       EconomyEngine.Cli explain <economy.json> <state.json> loan <loan>\n" +
        "       EconomyEngine.Cli explain <economy.json> <state.json> currency <currency>\n" +
        "       EconomyEngine.Cli explain <economy.json> <state.json> route <route>\n" +
        "       EconomyEngine.Cli explain <economy.json> <state.json> reach <market> <good>\n" +
        "       EconomyEngine.Cli explain <economy.json> <state.json> production <market> <recipe>\n" +
        "       EconomyEngine.Cli coverage <economy.json> <locale.json> [<locale.json> ...]\n" +
        "exit: 0 done | 1 refused or incomplete (the code or the lists are printed) | 2 usage, scenario or file problem";

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count < 2) return Wrong(error, Usage);
        try
        {
            switch (args[0])
            {
                case "check" when args.Count == 2: return Check(File.ReadAllText(args[1]), output);
                case "simulate" when args.Count >= 3 && args.Contains("--seeds"): return SimulateSeeds(File.ReadAllText(args[1]), args, output, error);
                case "simulate" when args.Count >= 4: return Simulate(File.ReadAllText(args[1]), args, output, error);
                case "explain" when args.Count >= 5: return Explain(File.ReadAllText(args[1]), args, output, error);
                case "coverage" when args.Count >= 3: return Coverage(File.ReadAllText(args[1]), args.Skip(2).Select(File.ReadAllText).ToList(), output);
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

    private static int Check(string document, TextWriter output)
    {
        EconomySet set;
        try { set = EconomyLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        output.WriteLine($"ok {set.Id} digest {set.Digest} seed {set.Seed} steps-per-day {set.StepsPerDay} period-steps {set.PeriodSteps} currencies {set.Currencies.Count} goods {set.Goods.Count} markets {set.Markets.Count} " +
            $"pairs {set.PairCount} routes {set.Routes.Count} schedules {set.Schedules.Count} policies {set.Policies.Count} modifiers {set.Modifiers.Count} shocks {set.Shocks.Count} loans {set.Loans.Count} " +
            $"host-facts {set.HostFacts.Count} facts {set.Facts.Count} text-keys {EconomyNames.RequiredKeys(set).Count}");
        output.WriteLine($"routing {set.Routing.ToString().ToLowerInvariant()}");
        output.WriteLine($"limits shipments {set.Limits.MaxShipments} loans {set.Limits.MaxLoans} shocks {set.Limits.MaxShocks} history {set.Limits.History}");
        foreach (var currency in set.Currencies) output.WriteLine($"currency {currency.Id}{(currency.Reference ? " reference" : "")} anchor {currency.Anchor} growth {currency.GrowthPpm} max-rate-step {currency.MaxRateStepPpm}");
        foreach (var market in set.Markets) output.WriteLine($"market {market.Id} currency {set.Currencies[market.Currency].Id} pairs {market.Pairs.Count} policies {market.Policies.Count} budget {market.Budget.Count}");
        foreach (var route in set.Routes) output.WriteLine($"route {route.Id} {set.Markets[route.From].Id} -> {set.Markets[route.To].Id} delay {route.Delay} capacity {route.Capacity}");
        output.WriteLine($"recipes {set.Recipes.Count} lines {set.LineCount} locked {set.Recipes.Count(r => r.Locked)}");
        foreach (var index in set.RecipeOrder)
        {
            var recipe = set.Recipes[index];
            string Side(IReadOnlyList<RecipeAmount> amounts) => string.Join(" + ", amounts.Select(a => a.Amount + " " + set.Goods[a.Good].Id + (a is RecipeRequirement { Wear: { } wear } ? " (" + wear.Units + " lost in " + wear.Runs + " runs)" : "")));
            output.WriteLine($"recipe {recipe.Id}: {Side(recipe.Inputs)} -> {Side(recipe.Outputs)} in {recipe.Duration} periods{(recipe.Requires.Count > 0 ? ", requires in stock " + Side(recipe.Requires) : "")}{(recipe.Locked ? " (locked until discovered)" : "")}");
        }
        foreach (var market in set.Markets)
            foreach (var line in market.Lines) output.WriteLine($"line {market.Id} {set.Recipes[line.Recipe].Id} capacity {line.Capacity}{(line.Throttle is null ? "" : $" throttle stop {line.Throttle.Stop} full {line.Throttle.Full}{(line.Throttle.Costs is { } costs ? $" costs wage {costs.Wage} overhead {costs.Overhead} {(costs.Indexed ? "indexed" : "nominal")}" : "")}")}");
        for (var m = 0; m < set.Markets.Count; m++)
            foreach (var group in set.LinesInOrder[m].SelectMany(l => set.Recipes[l.Recipe].Requires.Select(r => (Good: r.Good, Recipe: set.Recipes[l.Recipe].Id, r.Amount))).GroupBy(t => t.Good).Where(g => g.Count() > 1))
                output.WriteLine($"shared requirement {set.Markets[m].Id} {set.Goods[group.Key].Id}: held by one line at a time, in line order {string.Join(", ", group.Select(t => t.Recipe + " " + t.Amount))}; the market needs {group.Sum(t => t.Amount)} for all of them to run in one period");
        return 0;
    }

    private static int Explain(string document, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            var set = EconomyLoader.Load(document);
            var runtime = EconomySave.Open(set, File.ReadAllBytes(args[2]));
            switch (args[3])
            {
                case "price" when args.Count == 6:
                    output.WriteLine(EconomyExplain.Price(runtime, args[4], args[5]).Text());
                    return 0;
                case "loan" when args.Count == 5:
                    output.WriteLine(EconomyExplain.Loan(runtime, args[4]).Text());
                    return 0;
                case "currency" when args.Count == 5:
                    output.WriteLine(EconomyExplain.Currency(runtime, args[4]).Text());
                    return 0;
                case "route" when args.Count == 5:
                    output.WriteLine(EconomyExplain.Route(runtime, args[4]).Text());
                    return 0;
                case "reach" when args.Count == 6:
                    output.WriteLine(EconomyExplain.Reach(runtime, args[4], args[5]).Text());
                    return 0;
                case "production" when args.Count == 6:
                    output.WriteLine(EconomyExplain.Production(runtime, args[4], args[5]).Text());
                    return 0;
            }
        }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        return Wrong(error, Usage);
    }

    private static int Coverage(string document, IReadOnlyList<string> tables, TextWriter output)
    {
        EconomySet set;
        try { set = EconomyLoader.Load(document); }
        catch (EngineRefusedException refusal) { return Refused(output, refusal); }
        var complete = true;
        foreach (var text in tables)
        {
            LocaleTable table;
            try { table = LocaleTable.Load(text); }
            catch (EngineRefusedException refusal) { return Refused(output, refusal); }
            var report = EconomyNames.Coverage(set, table);
            output.WriteLine($"{report.Locale}: {report.Translated}/{report.Total} ({report.Percent}%) missing {report.Missing.Count} unknown {report.Unknown.Count} problems {report.Problems.Count}");
            foreach (var key in report.Missing) output.WriteLine("  missing " + key);
            foreach (var key in report.Unknown) output.WriteLine("  unknown " + key);
            foreach (var problem in report.Problems) output.WriteLine("  problem " + problem.Key + ": " + problem.Why);
            complete &= report.IsComplete;
        }
        return complete ? 0 : 1;
    }
}

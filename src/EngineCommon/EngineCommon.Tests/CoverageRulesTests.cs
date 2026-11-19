using Xunit;

namespace EngineCommon.Tests;

/// <summary>The one coverage rule (every engine with words uses it): what it reports, and the order it refuses in.</summary>
public sealed class CoverageRulesTests
{
    private static LocaleTable Table(string messages) => LocaleTable.Load("{ \"format\": 1, \"locale\": \"en\", \"messages\": {" + messages + "} }");

    private static readonly Dictionary<string, IReadOnlyList<string>> Keys = new()
    {
        ["a"] = [],
        ["b"] = ["name"],
    };

    private static string Code(Action act) => Assert.Throws<EngineRefusedException>(act).Code;

    [Fact]
    public void ACompleteTableHasNoProblem()
    {
        var report = CoverageRules.Report(Keys, Table("\"a\": \"A\", \"b\": \"B {name}\""), _ => "a key does not give");
        Assert.True(report.IsComplete);
        Assert.Equal((2, 2, 100), (report.Total, report.Translated, report.Percent));
        CoverageRules.Check(report);
    }

    [Fact]
    public void AMissingKeyAStrayPlaceholderAndAMessageNobodyUsesAreEachReportedAndRefusedWithTheirOwnCode()
    {
        var missing = CoverageRules.Report(Keys, Table("\"a\": \"A\""), _ => "x");
        Assert.Equal(["b"], missing.Missing);
        Assert.Equal(LocaleCodes.MissingKey, Code(() => CoverageRules.Check(missing)));

        var stray = CoverageRules.Report(Keys, Table("\"a\": \"A {other}\", \"b\": \"B\""), key => "key '" + key + "' does not give");
        var problem = Assert.Single(stray.Problems);
        Assert.Equal(("a", "uses '{other}', which key 'a' does not give"), (problem.Key, problem.Why));
        Assert.Equal(LocaleCodes.Placeholder, Code(() => CoverageRules.Check(stray)));

        var unknown = CoverageRules.Report(Keys, Table("\"a\": \"A\", \"b\": \"B\", \"c\": \"C\""), _ => "x");
        Assert.Equal(["c"], unknown.Unknown);
        Assert.Equal(LocaleCodes.UnknownKey, Code(() => CoverageRules.Check(unknown)));
    }

    [Fact]
    public void AMissingKeyIsRefusedBeforeAStrayPlaceholderAndThatBeforeAnUnusedMessage()
    {
        var all = CoverageRules.Report(Keys, Table("\"a\": \"A {other}\", \"c\": \"C\""), _ => "x");
        Assert.Equal(LocaleCodes.MissingKey, Code(() => CoverageRules.Check(all)));
        var two = CoverageRules.Report(Keys, Table("\"a\": \"A {other}\", \"b\": \"B\", \"c\": \"C\""), _ => "x");
        Assert.Equal(LocaleCodes.Placeholder, Code(() => CoverageRules.Check(two)));
    }

    [Fact]
    public void ADocumentWithNoKeysIsFullyCovered()
    {
        var report = CoverageRules.Report(new Dictionary<string, IReadOnlyList<string>>(), Table(""), _ => "x");
        Assert.Equal(100, report.Percent);
        Assert.True(report.IsComplete);
    }

    [Fact]
    public void ANullArgumentIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => CoverageRules.Report(null!, Table(""), _ => "x"));
        Assert.Throws<ArgumentNullException>(() => CoverageRules.Report(Keys, null!, _ => "x"));
        Assert.Throws<ArgumentNullException>(() => CoverageRules.Report(Keys, Table(""), null!));
        Assert.Throws<ArgumentNullException>(() => CoverageRules.Check(null!));
    }
}

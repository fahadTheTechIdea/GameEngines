using EngineCommon;
using System.Globalization;
using DialogueEngine;
using ScenarioEngine.Contracts;
using Xunit;

namespace DialogueEngine.Tests;

/// <summary>The plural rules against the CLDR table itself: every example CLDR prints for a rule must fall in that rule's category, for every shipped locale, cardinal and ordinal, whole and decimal.</summary>
public sealed class PluralCldrTests
{
    /// <summary>The numbers an <c>@integer</c> or <c>@decimal</c> sample list names: single numbers and <c>a~b</c> ranges (expanded at the digits they are written with). Compact exponents and the ellipsis are not numbers to test.</summary>
    private static IEnumerable<string> Numbers(string list)
    {
        foreach (var raw in list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw == "…" || raw.Contains('c') || raw.Contains('e')) continue;
            var parts = raw.Split('~');
            if (parts.Length == 1) { yield return raw; continue; }
            var digits = parts[0].Contains('.') ? parts[0].Length - parts[0].IndexOf('.') - 1 : 0;
            var low = decimal.Parse(parts[0], CultureInfo.InvariantCulture);
            var high = decimal.Parse(parts[1], CultureInfo.InvariantCulture);
            var step = digits == 0 ? 1m : 1m / (decimal)Math.Pow(10, digits);
            for (var value = low; value <= high; value += step) yield return value.ToString("F" + digits, CultureInfo.InvariantCulture);
        }
    }

    private static IEnumerable<(string Integer, string Decimal)> Split(string samples)
    {
        var integer = "";
        var dec = "";
        var at = samples.IndexOf("@decimal", StringComparison.Ordinal);
        var head = at < 0 ? samples : samples[..at];
        if (head.Contains("@integer")) integer = head[(head.IndexOf("@integer", StringComparison.Ordinal) + 8)..];
        if (at >= 0) dec = samples[(at + 8)..];
        yield return (integer, dec);
    }

    public static IEnumerable<object[]> Cases() =>
        from locale in PluralRules.Supported
        from kind in new[] { PluralKind.Cardinal, PluralKind.Ordinal }
        select new object[] { locale, kind };

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryExampleCldrGivesForARuleFallsInThatRulesCategory(string locale, PluralKind kind)
    {
        var rules = PluralRules.Rules(locale, kind);
        var samples = PluralRules.Samples(locale, kind);
        Assert.Equal(rules.Count, samples.Count);
        var checkedNumbers = 0;
        for (var i = 0; i < rules.Count; i++)
        {
            var (integer, dec) = Split(samples[i]).Single();
            foreach (var number in Numbers(integer).Concat(Numbers(dec)))
            {
                Assert.True(rules[i].Category == PluralRules.Category(locale, kind, number), $"{locale} {kind} {number}: CLDR says {rules[i].Category}, got {PluralRules.Category(locale, kind, number)} (rule '{rules[i].Source}')");
                checkedNumbers++;
            }
        }
        Assert.True(checkedNumbers > 3, locale + " " + kind + " had " + checkedNumbers + " examples");
    }

    [Fact]
    public void TheTableCoversTheShippedLocalesAndSaysWhereItCameFrom()
    {
        Assert.Equal(["ar", "de", "en", "es", "fr", "he", "ja", "pl", "ru", "zh"], PluralRules.Supported);
        Assert.Contains("Unicode CLDR", PluralRules.Source);
        Assert.Contains("taken 2026-10-28", PluralRules.Source);
        Assert.Equal(["zero", "one", "two", "few", "many", "other"], PluralRules.Categories("ar"));
        Assert.Equal(["one", "two", "other"], PluralRules.Categories("he"));
        Assert.Equal(["one", "two", "few", "other"], PluralRules.Categories("en", PluralKind.Ordinal));
    }

    [Theory]
    // decimals and trailing zeros matter: "1.0" has a visible fraction, so English calls it "other" and French calls it "one"
    [InlineData("en", "1", "one")] [InlineData("en", "1.0", "other")] [InlineData("en", "1.5", "other")] [InlineData("en", "0.0", "other")]
    [InlineData("fr", "0", "one")] [InlineData("fr", "1.5", "one")] [InlineData("fr", "2", "other")] [InlineData("fr", "2.0", "other")]
    [InlineData("es", "1", "one")] [InlineData("es", "1.0", "one")] [InlineData("es", "1.5", "other")]
    [InlineData("ru", "1", "one")] [InlineData("ru", "1.0", "other")] [InlineData("ru", "21", "one")] [InlineData("ru", "2.5", "other")] [InlineData("ru", "11", "many")]
    [InlineData("pl", "1", "one")] [InlineData("pl", "1.5", "other")] [InlineData("pl", "22", "few")] [InlineData("pl", "0", "many")]
    [InlineData("ar", "0.0", "zero")] [InlineData("ar", "1.00", "one")] [InlineData("ar", "2.0", "two")] [InlineData("ar", "3.0", "few")] [InlineData("ar", "3.5", "other")] [InlineData("ar", "11.0", "many")]
    [InlineData("he", "1", "one")] [InlineData("he", "2", "two")] [InlineData("he", "3", "other")] [InlineData("he", "0.5", "one")] [InlineData("he", "2.0", "other")] [InlineData("he", "20", "other")]
    [InlineData("ja", "1.5", "other")] [InlineData("zh", "0", "other")]
    public void CardinalCategoriesOfDecimalsFollowTheVisibleDigits(string locale, string number, string expected) =>
        Assert.Equal(expected, PluralRules.Category(locale, PluralKind.Cardinal, number));

    [Theory]
    [InlineData("en", "1", "one")] [InlineData("en", "2", "two")] [InlineData("en", "3", "few")] [InlineData("en", "4", "other")] [InlineData("en", "11", "other")] [InlineData("en", "12", "other")]
    [InlineData("en", "13", "other")] [InlineData("en", "21", "one")] [InlineData("en", "22", "two")] [InlineData("en", "23", "few")] [InlineData("en", "101", "one")] [InlineData("en", "111", "other")]
    [InlineData("fr", "1", "one")] [InlineData("fr", "2", "other")] [InlineData("fr", "11", "other")]
    [InlineData("ru", "1", "other")] [InlineData("ar", "3", "other")] [InlineData("ja", "1", "other")] [InlineData("he", "1", "other")]
    public void OrdinalCategoriesFollowTheCldrOrdinalTable(string locale, string number, string expected) =>
        Assert.Equal(expected, PluralRules.Category(locale, PluralKind.Ordinal, number));

    [Theory]
    [InlineData("1.0", 1, 0, 0, 0, 1)]
    [InlineData("1.50", 2, 1, 50, 5, 1)]
    [InlineData("0.05", 2, 2, 5, 5, 0)]
    [InlineData("-12", 0, 0, 0, 0, 12)]
    [InlineData("100.000", 3, 0, 0, 0, 100)]
    public void OperandsAreReadFromTheDigitsAsWritten(string text, int v, int w, long f, long t, long i)
    {
        var o = PluralOperands.Parse(text);
        Assert.Equal((v, w, f, t, i), (o.V, o.W, o.F, o.T, o.I));
        Assert.Equal(Math.Abs(decimal.Parse(text, CultureInfo.InvariantCulture)), o.N);
    }

    [Theory]
    [InlineData("")] [InlineData("-")] [InlineData("1.")] [InlineData(".5")] [InlineData("1e3")] [InlineData("1c6")] [InlineData("1,5")] [InlineData(" 1")] [InlineData("1 ")] [InlineData("abc")]
    [InlineData("1234567890123456789")] [InlineData("--1")] [InlineData("+1")]
    public void ATextThatIsNotAPlainNumberIsRefused(string text) =>
        Assert.Equal(LocaleCodes.Argument, Fixture.Refusal(() => PluralOperands.Parse(text)));

    [Theory]
    [InlineData("n = ")] [InlineData("x = 1")] [InlineData("n % 0 = 1")] [InlineData("n = 3..1")] [InlineData("n = 1 xor v = 0")] [InlineData("n == 1")] [InlineData("= 1")] [InlineData("n = 1 and")] [InlineData("n = a")]
    public void ARuleTheLanguageDoesNotAllowIsRefused(string condition) =>
        Assert.Equal(LocaleCodes.Format, Fixture.Refusal(() => PluralRule.Parse("one", condition)));

    [Fact]
    public void TheRuleLanguageEvaluatesModulusRangesListsAndTheOperatorsOfCldr()
    {
        bool Holds(string rule, string number) => PluralRule.Parse("one", rule).Matches(PluralOperands.Parse(number));
        Assert.True(Holds("n % 10 = 2..4 and n % 100 != 12..14", "22"));
        Assert.False(Holds("n % 10 = 2..4 and n % 100 != 12..14", "12"));
        Assert.False(Holds("n % 10 = 2..4", "2.5"));                       // a range holds whole numbers only
        Assert.True(Holds("n % 10 = 1,3 and n % 100 != 11", "23"));
        Assert.False(Holds("n % 10 = 1,3 and n % 100 != 11", "11"));
        Assert.True(Holds("i = 0 or n = 1", "0.7"));                       // or
        Assert.True(Holds("i = 0 or n = 1", "1"));
        Assert.False(Holds("i = 0 or n = 1", "2"));
        Assert.True(Holds("v = 0 and i != 1", "5"));
        Assert.False(Holds("v = 0 and i != 1", "5.0"));
        Assert.True(Holds("f = 50", "1.50"));
        Assert.True(Holds("t = 5", "1.50"));
        Assert.True(Holds("w = 1", "1.50"));
        Assert.False(PluralRule.Parse("other", "").Matches(PluralOperands.Parse("1")));   // the fallback matches nothing by itself
    }
}

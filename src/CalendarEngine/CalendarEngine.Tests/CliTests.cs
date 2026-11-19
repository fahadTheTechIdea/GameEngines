using System.Text.Json.Nodes;
using Xunit;

namespace CalendarEngine.Tests;

/// <summary>The authoring tool: every command, its output, and the exit code of every mistake.</summary>
public sealed class CliTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "cal-cli-" + Guid.NewGuid().ToString("N"));

    public CliTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, true);

    private string Write(string name, string text)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "samples", name);

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = CalendarCli.Run(args, output, error);
        return (code, output.ToString().Replace("\r\n", "\n"), error.ToString().Replace("\r\n", "\n"));
    }

    [Fact]
    public void ACheckOfAGoodDocumentSaysSoWithWhatIsInIt()
    {
        var (code, output, error) = Run("check", Sample("gregorian.calendar.json"));
        Assert.Equal((0, ""), (code, error));
        var lines = output.TrimEnd('\n').Split('\n');
        Assert.Equal($"ok gregorian-town digest {Fixture.Gregorian.Digest} months 12 weekdays 7 common-year-days 365 leap-extra-days 1 day 24h x 60min x 1 ticks seasons 4 day-parts 7 moons 1 holidays 6 schedules 2 interrupts 2 host-facts 2 text-keys 64", lines[0]);
        Assert.Equal("schedule shopkeeper routines 6 (holiday 20, harvest 18, winter-rain 15, sunday 10, saturday-market 5, base 0)", lines[1]);
        Assert.Equal("schedule guard routines 1 (base 0)", lines[2]);
    }

    [Fact]
    public void ACheckOfABadDocumentNamesTheCodeAndTheFindingAndExitsWithOne()
    {
        var path = Write("bad.json", Fixture.MutateGregorian(r => r["leap"] = new JsonArray()));
        var (code, output, _) = Run("check", path);
        Assert.Equal(1, code);
        Assert.StartsWith("refused CAL_LEAP: ", output);
    }

    [Fact]
    public void ExplainPrintsTheSameTextTheEngineGives()
    {
        var instant = Fixture.At(Fixture.Gregorian, 2022, "december", 25, 9);
        var (code, output, _) = Run("explain", Sample("gregorian.calendar.json"), "shopkeeper", "2022-december-25", "9:00", "--interrupt", "invitation", "--fact", "weather.raining=false");
        Assert.Equal(0, code);
        Assert.Equal(CalendarExplain.WhereIs(Fixture.Gregorian, instant, "shopkeeper", "invitation", Fixture.Dry) + "\n", output);
    }

    [Fact]
    public void ExplainReadsTrueAndNumbersAndTextAsFacts()
    {
        var path = Sample("gregorian.calendar.json");
        var (code, output, _) = Run("explain", path, "shopkeeper", "2024-january-10", "10:00", "--fact", "weather.raining=true", "--fact", "story.plague=2");
        Assert.Equal(0, code);
        Assert.Contains("routine winter-rain (priority 15): applies, block stay-in covers 08:00 to 18:00", output);
        Assert.EndsWith("from routine winter-rain block stay-in\n", output);
        var dry = Run("explain", path, "shopkeeper", "2024-january-10", "10:00", "--fact", "weather.raining=false");
        Assert.EndsWith("from routine base block open-morning\n", dry.Output);
        Assert.Equal(1, Run("explain", path, "shopkeeper", "2024-january-10", "10:00", "--fact", "weather.raining=maybe").Code);
        Assert.Equal(1, Run("explain", path, "shopkeeper", "2024-january-10", "10:00", "--fact", "story.plague=9", "--fact", "weather.raining=true").Code);
    }

    [Fact]
    public void ExplainRefusesWhatTheEngineRefuses()
    {
        var (code, output, _) = Run("explain", Sample("gregorian.calendar.json"), "shopkeeper", "2024-january-10", "10:00");
        Assert.Equal(1, code);
        Assert.StartsWith("refused CAL_FACT_MISSING: ", output);
        Assert.Equal(1, Run("explain", Sample("gregorian.calendar.json"), "nobody", "2024-january-10", "10:00").Code);
        Assert.Equal(1, Run("explain", Sample("gregorian.calendar.json"), "guard", "2023-february-29", "10:00").Code);
        Assert.Equal(1, Run("explain", Sample("gregorian.calendar.json"), "guard", "2023-nomonth-2", "10:00").Code);
        Assert.Equal(1, Run("explain", Sample("gregorian.calendar.json"), "guard", "garbage", "10:00").Code);
        Assert.Equal(1, Run("explain", Sample("gregorian.calendar.json"), "guard", "2023-march-2", "10-00").Code);
    }

    [Fact]
    public void ConvertGoesBothWays()
    {
        var path = Sample("gregorian.calendar.json");
        Assert.Equal("instant 0 = 1970-january-1 00:00 weekday thursday day-of-year 1/365 season winter day-part night era ce year-of-era 1970\n", Run("convert", path, "instant", "0").Output);
        var instant = Fixture.At(Fixture.Gregorian, 2000, "february", 29, 12, 30);
        Assert.Equal($"2000-february-29 12:30 = instant {instant}\n", Run("convert", path, "date", "2000-february-29", "12:30").Output);
        Assert.Equal($"2000-february-29 0:00 = instant {instant - 750}\n", Run("convert", path, "date", "2000-february-29").Output);
        Assert.StartsWith("instant -1 = 1969-december-31 23:59", Run("convert", path, "instant", "-1").Output);
        Assert.Contains("era bce year-of-era 45", Run("convert", path, "instant", Fixture.At(Fixture.Gregorian, -44, "march", 15).ToString()).Output);
        Assert.Equal(0, Run("convert", path, "date", "-44-march-15").Code);
        Assert.Equal(1, Run("convert", path, "instant", "999999999999999999").Code);
        Assert.Equal(2, Run("convert", path, "instant", "abc").Code);
        Assert.Equal(1, Run("convert", path, "date", "2023-february-29").Code);
    }

    [Fact]
    public void AMonthViewLaysTheDaysUnderTheirWeekdaysAndMarksHolidays()
    {
        var (code, output, _) = Run("month-view", Sample("gregorian.calendar.json"), "2024", "february");
        Assert.Equal(0, code);
        var lines = output.TrimEnd('\n').Split('\n');
        Assert.Equal("february 2024 (gregorian-town)", lines[0]);
        Assert.Equal(" mon tue wed thu fri sat sun", lines[1]);
        Assert.Equal("               1   2   3   4", lines[2]);
        Assert.Equal("   5   6   7   8   9  10  11", lines[3]);
        Assert.Equal("  26  27  28 29*", lines[6]);
        Assert.Equal("* 29 leap-day", lines[7]);
        Assert.DoesNotContain("29", Run("month-view", Sample("gregorian.calendar.json"), "2023", "february").Output.Replace("2023", ""));
    }

    [Fact]
    public void AMonthViewOfFestivalDaysAndOfAMonthThatDoesNotExistThatYear()
    {
        var realms = Sample("realms.calendar.json");
        Assert.Equal("midwinter 1491 (realms-reckoning)\ndays outside the weeks: 1*\n* 1 midwinter-feast\n", Run("month-view", realms, "1491", "midwinter").Output);
        Assert.Equal("shieldmeet 1 has no days in this year (realms-reckoning)\n", Run("month-view", realms, "1", "shieldmeet").Output);
        Assert.StartsWith("shieldmeet 4 (realms-reckoning)\ndays outside the weeks: 1*", Run("month-view", realms, "4", "shieldmeet").Output);
        var hammer = Run("month-view", realms, "1491", "hammer").Output.Split('\n');
        Assert.Equal("  d1  d2  d3  d4  d5  d6  d7  d8  d9 d10", hammer[1]);
        Assert.Equal("   1   2   3   4   5   6   7   8   9 10*", hammer[2]);
    }

    [Fact]
    public void AMonthViewCanUseTheNamesOfALocaleTable()
    {
        var (code, output, _) = Run("month-view", Sample("valley.calendar.json"), "2", "fall", "--table", Sample("valley.en.json"));
        Assert.Equal(0, code);
        var lines = output.Split('\n');
        Assert.Equal("Fall 2 (valley-year)", lines[0]);
        Assert.Equal(" Mon Tue Wed Thu Fri Sat Sun", lines[1]);
        Assert.Equal("   1   2   3   4   5   6   7", lines[2]);
        Assert.Contains("* 16 fair", output);
    }

    [Fact]
    public void CoverageSaysHowMuchEachTableCoversAndExitsWithOneWhenOneIsIncomplete()
    {
        var path = Sample("gregorian.calendar.json");
        var (code, output, _) = Run("coverage", path, Sample("gregorian.en.json"));
        Assert.Equal((0, "en: 64/64 (100%) missing 0 unknown 0 problems 0\n"), (code, output));
        var thin = Write("fr.json", """{ "format": 1, "locale": "fr", "messages": { "cal.month.april": "avril", "cal.junk": "x", "cal.season.spring": "printemps {year}" } }""");
        var (code2, output2, _) = Run("coverage", path, Sample("gregorian.en.json"), thin);
        Assert.Equal(1, code2);
        Assert.Contains("fr: 2/64 (3%) missing 62 unknown 1 problems 1", output2);
        Assert.Contains("  missing cal.month.january", output2);
        Assert.Contains("  unknown cal.junk", output2);
        Assert.Contains("  problem cal.season.spring: uses '{year}', which a name does not give", output2);
        Assert.Equal(1, Run("coverage", path, Write("bad.json", """{ "format": 7 }""")).Code);
    }

    [Fact]
    public void AWrongCommandLineOrAMissingFileExitsWithTwo()
    {
        Assert.Equal(2, Run().Code);
        Assert.Equal(2, Run("check").Code);
        Assert.Equal(2, Run("check", "a", "b").Code);
        Assert.Equal(2, Run("bogus", "x").Code);
        Assert.Equal(2, Run("explain", Sample("gregorian.calendar.json"), "guard", "2024-march-1", "9:00", "--fact").Code);
        Assert.Equal(2, Run("explain", Sample("gregorian.calendar.json"), "guard", "2024-march-1", "9:00", "--fact", "nofield").Code);
        Assert.Equal(2, Run("explain", Sample("gregorian.calendar.json"), "guard", "2024-march-1", "9:00", "--wat", "x").Code);
        Assert.Equal(2, Run("month-view", Sample("gregorian.calendar.json"), "2024", "nomonth").Code);
        Assert.Equal(2, Run("month-view", Sample("gregorian.calendar.json"), "x", "march").Code);
        Assert.Equal(2, Run("month-view", Sample("gregorian.calendar.json"), "2024", "march", "--tabel", "x").Code);
        var (code, _, error) = Run("check", Path.Combine(_folder, "missing.json"));
        Assert.Equal(2, code);
        Assert.StartsWith("file problem: ", error);
        Assert.StartsWith("usage: CalendarEngine.Cli", Run("bogus", "x").Error);
    }
}

using EngineCommon.TestSupport;
using Xunit;

namespace EngineCommon.Tests;

/// <summary>
/// One owner for each shared capability, guarded: the locale, plural and message-format machinery lives in EngineCommon.Core/Locale only, and the channel registry in GameRuntime.Channels only. The
/// guards read every engine and game-layer project (not the test projects, whose synthetic sources would trip them) and run on synthetic sources that break each rule, so each is shown to fail.
/// </summary>
public sealed class DuplicationGuardTests
{
    private static readonly string[] Areas = ["EngineCommon", "AIEngine", "DialogueEngine", "CalendarEngine", "CombatEngine", "RelationshipEngine", "EconomyEngine", "TutorialEngine", "WorldEventsEngine", "DiplomacyEngine", "GameRuntime"];

    /// <summary>Every shipped project of the engines and the game layer, as a folder under src (a test, test-support or sample-less project is not one).</summary>
    internal static List<string> ShippedProjects()
    {
        var src = Path.Combine(SyntaxGuards.Root(), "src");
        var projects = new List<string>();
        foreach (var area in Areas)
        {
            var folder = Path.Combine(src, area);
            if (!Directory.Exists(folder)) continue;
            foreach (var project in Directory.GetDirectories(folder))
            {
                var name = Path.GetFileName(project);
                if (name.EndsWith(".Tests", StringComparison.Ordinal) || name.EndsWith(".TestSupport", StringComparison.Ordinal)) continue;
                if (Directory.GetFiles(project, "*.csproj").Length == 0) continue;
                projects.Add(area + "/" + name);
            }
        }
        return projects;
    }

    private static void Clean(IEnumerable<string> findings)
    {
        var list = findings.ToList();
        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }

    private static readonly string LocaleFolder = Path.Combine("src", "EngineCommon", "EngineCommon.Core", "Locale") + Path.DirectorySeparatorChar;

    [Fact]
    public void TheScanSeesTheEnginesAndTheGameLayer()
    {
        var projects = ShippedProjects();
        foreach (var expected in new[] { "EngineCommon/EngineCommon.Core", "EngineCommon/EngineCommon.Graph", "DialogueEngine/DialogueEngine.Core", "CalendarEngine/CalendarEngine.Core", "AIEngine/AIEngine.Core", "CombatEngine/CombatEngine.Core", "GameRuntime/GameRuntime.Channels", "GameRuntime/GameRuntime.Dialogue", "GameRuntime/GameRuntime.Calendar", "RelationshipEngine/RelationshipEngine.Core", "GameRuntime/GameRuntime.Relationship", "EconomyEngine/EconomyEngine.Core", "GameRuntime/GameRuntime.Economy", "TutorialEngine/TutorialEngine.Core", "GameRuntime/GameRuntime.Tutorial", "GameRuntime/GameRuntime.Tutorial.Bridges", "WorldEventsEngine/WorldEventsEngine.Core", "GameRuntime/GameRuntime.WorldEvents", "GameRuntime/GameRuntime.WorldEvents.Bridges", "DiplomacyEngine/DiplomacyEngine.Core", "GameRuntime/GameRuntime.Diplomacy", "GameRuntime/GameRuntime.Diplomacy.Bridges" })
            Assert.Contains(expected, projects);
        Assert.DoesNotContain(projects, p => p.EndsWith(".Tests", StringComparison.Ordinal));
        Assert.True(SyntaxGuards.Sources(projects).Count > 200, "the scan found few sources; it may be looking in the wrong place");
    }

    [Fact]
    public void OnlyTheCommonLocaleFolderImplementsLocalesPluralsAndMessageFormats()
    {
        var sources = SyntaxGuards.Sources(ShippedProjects()).Where(s => !s.File.StartsWith(LocaleFolder, StringComparison.Ordinal)).ToList();
        Assert.True(sources.Count > 200, "the scan found few sources; it may be looking in the wrong place");
        Clean(sources.SelectMany(s => SyntaxGuards.SecondLocaleImplementation(s.File, s.Text)));
        // The owner itself is what the rule is about: it must be found when the folder is included.
        var owner = SyntaxGuards.Sources(["EngineCommon/EngineCommon.Core"]).Where(s => s.File.StartsWith(LocaleFolder, StringComparison.Ordinal)).ToList();
        Assert.True(owner.SelectMany(s => SyntaxGuards.SecondLocaleImplementation(s.File, s.Text)).Count() >= 8, "the rule does not recognise the owner, so it would not recognise a copy");
    }

    [Fact]
    public void OnlyGameRuntimeChannelsImplementsTheChannelRegistry()
    {
        var sources = SyntaxGuards.Sources(ShippedProjects().Where(p => p != "GameRuntime/GameRuntime.Channels")).ToList();
        Assert.True(sources.Count > 200, "the scan found few sources; it may be looking in the wrong place");
        Clean(sources.SelectMany(s => SyntaxGuards.SecondChannelRegistry(s.File, s.Text)));
        var subclasses = sources.Count(s => s.Text.Contains(": ChannelRegistry", StringComparison.Ordinal));
        Assert.True(subclasses >= 7, "the engines' registries (dialogue, calendar, relationship, economy, tutorial, world events, diplomacy) should derive from ChannelRegistry; found " + subclasses);
        var owner = SyntaxGuards.Sources(["GameRuntime/GameRuntime.Channels"]);
        Assert.NotEmpty(owner.SelectMany(s => SyntaxGuards.SecondChannelRegistry(s.File, s.Text)));   // the owner is recognised
    }

    // ---- each rule can fail ----------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("class PluralRules { }", 1)]
    [InlineData("class LocaleTable { }", 1)]
    [InlineData("class LocaleChain { }", 1)]
    [InlineData("struct LocaleRenderer { }", 1)]
    [InlineData("enum MessageSelect { A }", 1)]
    [InlineData("record MessageArg(string Name);", 1)]
    [InlineData("static class TextDirection { }", 1)]
    [InlineData("static class CoverageRules { }", 1)]
    [InlineData("class C { string PluralFor(long n) => \"\"; }", 1)]
    [InlineData("class C { string PluralForm => \"\"; }", 1)]
    [InlineData("class C { string Path = \"cldr-plurals.json\"; }", 1)]
    [InlineData("class C { string[] Names = [\"one\", \"two\", \"few\", \"other\"]; }", 1)]
    [InlineData("class C { string[] Names = [\"one\", \"two\", \"other\"]; }", 0)]
    [InlineData("class C { // class PluralRules { } and \"cldr-plurals.json\"\n }", 0)]
    [InlineData("class LocaleCoverage { }", 0)]
    [InlineData("class LocaleSelection { }", 0)]
    [InlineData("class C { void M(MessageArg a, LocaleChain c) { c.Render(\"k\", [a]); } }", 0)]
    public void TheLocaleRuleItselfCanFail(string source, int findings) => Assert.Equal(findings, SyntaxGuards.SecondLocaleImplementation("t.cs", source).Count);

    [Theory]
    [InlineData("class ThingChannels { }", 1)]
    [InlineData("class ThingChannelRegistry : IGameComponent { }", 1)]
    [InlineData("class ThingChannels : ChannelRegistry { }", 1)]
    [InlineData("sealed class ThingChannels : ChannelRegistry { void Extra() { } }", 1)]
    [InlineData("sealed class ThingChannels : ChannelRegistry { int _state; }", 1)]
    [InlineData("sealed class ThingChannels : ChannelRegistry { const string A = \"X\"; static readonly int P = 1; public ThingChannels() : base(null!) { } }", 0)]
    [InlineData("class Hand : IGameComponent { public void Serve(string c) { } }", 1)]
    [InlineData("class Hand { readonly SortedDictionary<string, string> _served = new(); }", 1)]
    [InlineData("class Hand { readonly List<int> _unserved = new(); }", 1)]
    [InlineData("class Hand { const string U = \"THING_CHANNEL_UNKNOWN\"; }", 1)]
    [InlineData("class Hand { const string U = \"THING_CHANNEL_DUPLICATE\"; }", 1)]
    [InlineData("class Hand : IGameComponent { public void Update() { } }", 0)]
    [InlineData("class Hand { public void Serve(string c) { } }", 0)]
    [InlineData("class Hand { const string U = \"THING_CHANNEL_OTHER\"; }", 0)]
    [InlineData("class Hand { // class XChannels { } and _served\n }", 0)]
    public void TheChannelRuleItselfCanFail(string source, int findings) => Assert.Equal(findings, SyntaxGuards.SecondChannelRegistry("t.cs", source).Count);
}

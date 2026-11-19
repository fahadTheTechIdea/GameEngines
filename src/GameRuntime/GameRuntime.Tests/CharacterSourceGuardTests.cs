using Xunit;

namespace GameRuntime.Tests;

/// <summary>Layer rules for the character factory kept as tests: one place makes characters, and the engine module holds no catalog numbers.</summary>
public sealed class CharacterSourceGuardTests
{
    private static string RepositorySource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "SimEngine", "src", "Beep.SimEngine", "Standard", "Characters"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "src");
    }

    private static Dictionary<string, string> Sources(string folder) =>
        Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToDictionary(f => Path.GetRelativePath(folder, f), File.ReadAllText);

    [Fact]
    public void OnlyTheFactoryWritesTheStateOfACharacterOrASquad()
    {
        var engine = Sources(Path.Combine(RepositorySource(), "SimEngine", "src", "Beep.SimEngine"));
        // Nothing outside the module even names the two components, so nothing else can reach their state.
        var outside = engine.Where(s => !s.Key.Contains("Characters" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && (s.Value.Contains("CharacterComponent", StringComparison.Ordinal) || s.Value.Contains("SquadComponent", StringComparison.Ordinal))).Select(s => s.Key).ToArray();
        Assert.Empty(outside);
        // Inside the module only the factory calls their writers.
        var writers = engine.Where(s => s.Key.Contains("Characters" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && new[] { ".Assign(", ".Raise(", ".MarkLost(", ".Release(", ".AdvanceCursor(" }.Any(w => s.Value.Contains(w, StringComparison.Ordinal))).Select(s => Path.GetFileName(s.Key)).Order().ToArray();
        Assert.NotEmpty(writers);
        Assert.All(writers, w => Assert.StartsWith("CharacterFactoryService", w));
    }

    [Fact]
    public void NothingOutsideTheFactoryCreatesAnEntityOfACharacterOrSquadKind()
    {
        // The engine refuses to save a character or a squad the factory did not make (checkpoint validation); this guard keeps the module's own
        // code, and the game layer's director effects, from calling the world's creation path for them directly.
        var root = RepositorySource();
        foreach (var folder in new[] { Path.Combine(root, "GameRuntime", "GameRuntime.Characters"), Path.Combine(root, "GameRuntime", "GameRuntime.Field.Headless") })
            foreach (var (file, text) in Sources(folder))
            {
                Assert.DoesNotContain("PrepareCreation", text, StringComparison.Ordinal);
                Assert.DoesNotContain("CommitCreation", text, StringComparison.Ordinal);
                Assert.DoesNotContain(".Spawn(", text, StringComparison.Ordinal);
                _ = file;
            }
        var module = Sources(Path.Combine(root, "SimEngine", "src", "Beep.SimEngine", "Standard", "Characters"));
        var creators = module.Where(s => s.Value.Contains("PrepareCreation", StringComparison.Ordinal) || s.Value.Contains("CommitCreation", StringComparison.Ordinal)).Select(s => Path.GetFileName(s.Key)).Order().ToArray();
        Assert.Equal(new[] { "CharacterFactoryService.cs" }, creators);
    }

    [Fact]
    public void TheEngineModuleHoldsNoCatalogItemNoStatNumberAndNoCombatWord()
    {
        foreach (var (file, text) in Sources(Path.Combine(RepositorySource(), "SimEngine", "src", "Beep.SimEngine", "Standard", "Characters")))
        {
            foreach (var forbidden in new[] { "arms-", "gear.", "GameContent", "CombatEngine", "rifle", "sword", "musket", "armor" })
                Assert.False(text.Contains(forbidden, StringComparison.OrdinalIgnoreCase), file + " mentions " + forbidden);
        }
    }

    [Fact]
    public void TheGeneratorUsesNoClockNoCultureNoUnseededRandomnessAndNoFloatingPoint()
    {
        var module = Sources(Path.Combine(RepositorySource(), "SimEngine", "src", "Beep.SimEngine", "Standard", "Characters"));
        foreach (var (file, text) in module)
        {
            foreach (var forbidden in new[] { "DateTime", "Stopwatch", "new Random", "Random.Shared", "CurrentCulture", "ToString(\"", "double ", "float ", "decimal ", "Math.Sqrt", "Guid.NewGuid", "Environment.TickCount" })
                Assert.False(text.Contains(forbidden, StringComparison.Ordinal), file + " uses " + forbidden.Trim());
        }
    }

    [Fact]
    public void TheCatalogBridgeHoldsNoStatBeyondTheTemplateAndTheCombatEngineKnowsNoCharacter()
    {
        var root = RepositorySource();
        foreach (var (file, text) in Sources(Path.Combine(root, "CombatEngine", "CombatEngine.Core")))
        {
            Assert.DoesNotContain("Character", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Squad", text, StringComparison.Ordinal);
            _ = file;
        }
        var bridge = Sources(Path.Combine(root, "GameContent", "GameContent.Catalog.Characters"));
        Assert.DoesNotContain(bridge, s => s.Value.Contains("gear.", StringComparison.Ordinal));
    }
}

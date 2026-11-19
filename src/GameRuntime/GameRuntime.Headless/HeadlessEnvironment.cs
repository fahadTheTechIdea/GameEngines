using ScenarioEngine.Content;
using ScenarioEngine.Contracts;
using ScenarioEngine.Core;

namespace GameRuntime.Headless;

/// <summary>The mission content was refused; the diagnostics were already reported through the engine reporter.</summary>
public sealed class ContentRefusedException(string message) : Exception(message);

/// <summary>What every session of this game shares: the mission layer registry, its validated scenario and the reporter.</summary>
public sealed class HeadlessEnvironment
{
    public static string MissionPackPath => Path.Combine(Path.GetDirectoryName(typeof(HeadlessEnvironment).Assembly.Location)!, "Mission", "tycoon-mission.json");

    private HeadlessEnvironment(IGameLayerRegistry registry, IContractValidator validator, IEngineReporter reporter, ValidatedScenario scenario)
    {
        Registry = registry;
        Validator = validator;
        Reporter = reporter;
        Scenario = scenario;
    }

    public IGameLayerRegistry Registry { get; }
    public IContractValidator Validator { get; }
    public IEngineReporter Reporter { get; }
    public ValidatedScenario Scenario { get; }

    public ScenarioSessionFactory Sessions => new(Registry, Validator, Reporter);

    /// <summary>The profit the mission's objective asks for, read from the mission content so the player never keeps its own copy.</summary>
    public long GoalProfit => Scenario.Definition.Objectives.Single().CompleteWhen is CompareCondition { Value.Integer: var goal }
        ? goal
        : throw new ContentRefusedException("the tycoon objective must complete on a profit comparison");

    public static async Task<HeadlessEnvironment> LoadAsync(IEngineReporter reporter)
    {
        var registry = new GameLayerRegistry([new TycoonMissionLayer()]);
        var validator = new JsonSchemaContractValidator();
        var catalog = new ScenarioContentService(registry, validator, reporter);
        var load = await catalog.LoadAsync(new JsonFilePackSource(MissionPackPath), CancellationToken.None);
        if (!load.IsReady)
            throw new ContentRefusedException("mission content refused: " + string.Join("; ", load.Report.Diagnostics.Select(d => d.Code + " " + d.Message)));
        return new HeadlessEnvironment(registry, validator, reporter, load.Scenarios[0]);
    }
}

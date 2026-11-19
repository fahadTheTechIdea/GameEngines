using System.Text.Json.Nodes;

namespace RelationshipEngine.Tests;

/// <summary>Villages for the tests of communities, standing and balance pressure: opinions do not drift (so a test can say exactly what an opinion is on the day of the pass), no actor has a trait unless it says so.</summary>
internal static class SocialFixture
{
    public const int DayStep = 24;

    public static JsonObject Social(JsonObject root) => root["social"]!.AsObject();

    public static JsonObject Block(JsonObject root, string name) => Social(root)[name]!.AsObject();

    public static RelationshipSet Set(Action<JsonObject>? edit = null) => RelationshipLoader.Load(Edit.Doc(root =>
    {
        foreach (var axis in root["axes"]!.AsArray()) axis!["driftPerDay"] = 0;
        edit?.Invoke(root);
    }));

    /// <summary>A runtime of actors without traits, ids in the order given.</summary>
    public static RelationshipRuntime Village(RelationshipSet set, params string[] actors)
    {
        var runtime = RelationshipRuntime.Create(set);
        foreach (var actor in actors) runtime.AddActor(actor, []);
        return runtime;
    }

    public static void Think(RelationshipRuntime runtime, string holder, string other, long affinity) => runtime.AdjustAxis(holder, other, "affinity", affinity);

    public static void Trust(RelationshipRuntime runtime, string holder, string other, long trust) => runtime.AdjustAxis(holder, other, "trust", trust);

    /// <summary>Lives to the end of the next day boundary (the first step of the day is where the passes run) and returns what changed.</summary>
    public static RelationshipChanges NextDay(RelationshipRuntime runtime) => runtime.Advance((runtime.Step / DayStep + 1) * DayStep, Fixture.Spring);

    /// <summary>Opinions with the pressure memory taken away: what the actor itself thinks.</summary>
    public static long Own(RelationshipRuntime runtime, string holder, string other, string axis) =>
        runtime.OpinionWithout(runtime.Edge(holder, other)!, runtime.Set.AxisById[axis].Index, runtime.Set.MemoryById["social-pressure"].Index);
}

using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
internal static class WorldValidationReplayTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception("Validation replay: "+message);}
    private static void Reject(Action action,string prefix)
    {try{action();}catch(ArgumentException error)when(error.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}

    // Registry substitution leaves authenticated catalog content intact while testing
    // the live policy's existing reproducibility contract independently of geometry.
    private class ForwardPolicy(ISettlementCatalogPolicy source):ISettlementCatalogPolicy
    {
        public string Id=>source.Id;
        public BuildingDefinition Resolve(BuildingDefinition definition,string technology)=>source.Resolve(definition,technology);
        public IReadOnlySet<string> RequiredCapabilities(BuildingDefinition definition,string technology)=>source.RequiredCapabilities(definition,technology);
        public IReadOnlySet<string> RequiredCapabilities(MockupWorldDesign design)=>source.RequiredCapabilities(design);
        public IReadOnlyList<CatalogSupplyIssue> ValidateSupply(BuildingDefinition definition,StructureResourceRequirement requirement,CatalogSupplySource supply,CatalogSupplyContext context,CancellationToken token=default)
            =>source.ValidateSupply(definition,requirement,supply,context,token);
        public bool RequiresNeighborPolicy(IReadOnlySet<string> first,IReadOnlySet<string> second)=>source.RequiresNeighborPolicy(first,second);
    }
    private sealed class VersionedPolicy(ISettlementCatalogPolicy source):ForwardPolicy(source),IWorldGenerationDependency
    {
        public int Version {get;set;}
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(new{Version});
    }
    private sealed class PolicyRegistry(ISettlementCatalogRegistry source,ISettlementCatalogPolicy policy):ISettlementCatalogRegistry
    {
        public IReadOnlyList<SettlementCatalogDescriptor> Catalogs=>source.Catalogs;
        public RegisteredSettlementCatalog Resolve(SettlementCatalogSelection selection)=>source.Resolve(selection) with{Policy=policy};
    }
    public static Task RunAsync()
    {
        var e=WorldAreaAdaptationTests.Example();var generator=new WorldMetadataGenerator();
        var world=generator.Generate(e.Recipe,e.Catalog);
        MetadataPlacementReplay Fresh(WorldMetadataDocument document)=>new MetadataLocalLayoutPlanner().ValidateWithPlacementReplay(document,CancellationToken.None)
            ??throw new Exception("Missing qualified actual adaptation replay");
        WorldMetadataGenerator.ValidateDocument(world);
        new MetadataLocalLayoutPlanner().ValidateDocument(world);
        new MetadataPlacementPlanner().ValidateDocument(world);
        var replay=Fresh(world);new MetadataPlacementPlanner().ValidateWithReplay(world,replay,CancellationToken.None);
        Reject(()=>new MetadataPlacementPlanner().ValidateWithReplay(world,replay,CancellationToken.None),"METADATA_PLACEMENT_REPLAY_UNAVAILABLE");
        Reject(()=>Fresh(world).Consume(world with{},null,CancellationToken.None),"METADATA_PLACEMENT_REPLAY_OWNER_MISMATCH");
        Reject(()=>Fresh(world).Consume(world,TestCatalogs.Registry,CancellationToken.None),"METADATA_PLACEMENT_REPLAY_OWNER_MISMATCH");
        var mutableNodes=world.Nodes.ToList();var mutable=world with{Nodes=mutableNodes};var stale=Fresh(mutable);
        mutableNodes[0]=mutableNodes[0] with{Purpose="Changed after replay"};
        Reject(()=>stale.Consume(mutable,null,CancellationToken.None),"METADATA_PLACEMENT_REPLAY_INPUT_CHANGED");
        var cancelled=Fresh(world);using(var cancellation=new CancellationTokenSource())
        {cancellation.Cancel();try{cancelled.Consume(world,null,cancellation.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}}
        var tampered=world with{LayoutAdaptationPlan=world.LayoutAdaptationPlan! with{Attempts=[]}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(tampered),"METADATA_LAYOUT_ADAPTATION_PLAN_MISMATCH");
        Reject(()=>new MetadataLocalLayoutPlanner().ValidateDocument(tampered),"METADATA_LAYOUT_ADAPTATION_PLAN_MISMATCH");

        // A self-consistent replay without route obligations must not authorize a
        // document claiming those obligations. This exercises the separate context gate.
        var bare=generator.Generate(e.Recipe with{RouteRules=null},e.Catalog);
        var inconsistent=bare with{RoutePlan=world.RoutePlan};
        var contextReplay=Fresh(inconsistent);
        Reject(()=>new MetadataPlacementPlanner().ValidateWithReplay(inconsistent,contextReplay,CancellationToken.None),"METADATA_PLACEMENT_FEEDBACK_MISMATCH");
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(inconsistent),"METADATA_PLACEMENT_FEEDBACK_MISMATCH");

        var registered=WorldContextTests.Example();var registeredWorld=new WorldMetadataGenerator(TestCatalogs.Registry).Generate(registered.Recipe,registered.Catalog);
        var reference=registeredWorld.StructureDefinitions.Values.First();
        var policy=TestCatalogs.Registry.Resolve(new(reference.Catalog.Id,reference.Catalog.EnabledCapabilities)).Policy;
        var versioned=new VersionedPolicy(policy);var registry=new PolicyRegistry(TestCatalogs.Registry,versioned);
        var dependencyReplay=MetadataPlacementReplay.Capture(registeredWorld,registry,CancellationToken.None)!;
        Check(dependencyReplay is not null,"versioned registered policy qualifies");
        dependencyReplay!.Complete(registeredWorld.PlacementPlan!,registeredWorld.Nodes,CancellationToken.None);
        versioned.Version++;
        Reject(()=>dependencyReplay.Consume(registeredWorld,registry,CancellationToken.None),"METADATA_PLACEMENT_REPLAY_DEPENDENCY_CHANGED");
        var unknown=new PolicyRegistry(TestCatalogs.Registry,new ForwardPolicy(policy));
        Check(MetadataPlacementReplay.Capture(registeredWorld,unknown,CancellationToken.None) is null,"unversioned provider retains full placement replay");
        new MetadataPlacementPlanner(unknown).ValidateWithReplay(registeredWorld,null,CancellationToken.None);
        Console.WriteLine("PASS: qualified replay and standalone/composite validation; single use, owner/content/dependency binding, unversioned full replay, cancellation, adaptation tamper and independent feedback-context rejection.");
        return Task.CompletedTask;
    }
    public static async Task ProfileAsync(string output)
    {
        var e=WorldConnectedAreaTests.FullExample();var generator=new WorldMetadataGenerator();var clock=Stopwatch.StartNew();
        var world=generator.Generate(e.Recipe,e.Catalog);double generate=clock.Elapsed.TotalSeconds;clock.Restart();
        WorldMetadataGenerator.ValidateDocument(world);double validate=clock.Elapsed.TotalSeconds;
        var report=new{Utc=DateTimeOffset.UtcNow,GenerateSeconds=generate,ValidateSeconds=validate,WorldSha256=MetadataExecutionCache.Hash(world),Nodes=world.Nodes.Count,Physical=world.AccessPlan!.Sites.Count,World=world};
        await File.WriteAllTextAsync(output,JsonSerializer.Serialize(report,WorldJson.CreateOptions()));
        Console.WriteLine($"Generate={generate:F3}s Validate={validate:F3}s nodes={world.Nodes.Count}; source/geometry retained in {output}.");
    }
}

using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionalJointTests
{
    internal static async Task SemanticAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");
        var input=WorldRegionalCouplingTests.Example();var alpha=input.Regions.Single(r=>r.Id=="alpha");
        var original=alpha.Catalog.Archetypes.Single(a=>a.Id=="landmark");
        var landmark=original with{Capacities=[new("function","units",1,"One discovery function","semantic:function")]};
        var alternate=landmark with{Id="special-landmark"};
        var mix=new MetadataProgramMixRule("discovery-choice","Select complete terrain-funded discoveries","semantic:choice",
            [new("landmark",1),new("special-landmark",2,MaximumCount:1)],[new("function","units",1)],"cost",256,128);
        var catalog=alpha.Catalog with{Archetypes=[..alpha.Catalog.Archetypes.Select(a=>a.Id=="landmark"?landmark:
            a.Id==alpha.Recipe.RootArchetypeId?a with{Parts=a.Parts.Select(p=>p.Id=="landmarks"?p with{ProgramMix=mix}:p).ToArray()}:a),alternate]};
        var changed=alpha with{Catalog=catalog,Recipe=alpha.Recipe with{ProgramComposition=new("discovery-choice","Choose complete discoveries","semantic:choice",MaximumAttempts:64)}};
        var originalLink=input.Connections[0];
        var options=new WorldNetworkOptions("Connect the selected discovery","semantic:network","regional",originalLink.Profile,originalLink.Search with{Objective=WorldRouteObjective.FirstFeasible},
            NearestNeighbors:1,MaximumPairChecks:1,MaximumWork:50000000);
        var network=new MetadataRegionalNetworkRule("discovery-network","special-journey",[
            new("alpha","Reach the special discovery","semantic:discovery",new("alpha","",null,new("discovery","special-landmark"))),
            new("beta","Reach the other region","semantic:beta",originalLink.To)],options);
        input=input with{Regions=input.Regions.Select(r=>r.Id=="alpha"?changed:r).ToArray(),
            ProgramComposition=new("Select a connected complete discovery program","semantic:joint",64),NetworkRules=[network],MaximumWork=20000000};
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        var definition=planner.Plan(input);var proof=definition.OwnershipPlan!.JointPlan!;
        Check(proof.Attempts.Any(a=>a.Rejection?.StartsWith("METADATA_REGIONAL_NETWORK_SELECTOR_COUNT:")==true&&a.FailureRegionId=="alpha"&&a.Work>0),"missing generated role rejects a complete terrain-coupled candidate");
        Check(proof.Attempts[^1].Accepted&&proof.EnumerationWork+proof.Attempts.Sum(a=>a.Work)==definition.OwnershipPlan.Work,"alternative converges and accounts for work");
        Check(definition.OwnershipPlan.Work<20000000,"complete joint generation stays within default work cap");
        var demands=definition.OwnershipPlan.ProgramDemands!;
        WorldSpatialWorkBudget Budget()=>new(100000,default);
        Check(MetadataRegionalOwnershipPlanner.PreflightJointNetworkRoles([network],demands,Budget(),default) is null,"unique generated role is not rejected");
        MetadataRegionalNetworkRule Selector(string kind,string? archetype=null)=>network with{Destinations=[network.Destinations[0] with{
            Endpoint=network.Destinations[0].Endpoint with{Selector=new(kind,archetype)}},network.Destinations[1]]};
        Check(MetadataRegionalOwnershipPlanner.PreflightJointNetworkRoles([Selector("absent")],demands,Budget(),default) is {Count:0},"missing role is detected");
        Check(MetadataRegionalOwnershipPlanner.PreflightJointNetworkRoles([Selector("discovery")],demands,Budget(),default) is {Count:>1},"ambiguous role is detected");
        var deferred=demands.Select(d=>d.RegionId!="alpha"?d:d with{Analysis=d.Analysis with{RegionSelectionDeferred=true}}).ToArray();
        Check(MetadataRegionalOwnershipPlanner.PreflightJointNetworkRoles([Selector("absent")],deferred,Budget(),default) is null,"deferred analysis falls back to physical generation");
        Reject(()=>MetadataRegionalOwnershipPlanner.PreflightJointNetworkRoles([network],demands,new(1,default),default),"WORLD_ROUTE_WORK_BUDGET");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{MetadataRegionalOwnershipPlanner.PreflightJointNetworkRoles([network],demands,Budget(),cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var world=engine.Generate(definition);var alphaWorld=world.Regions.Single(r=>r.RegionId=="alpha").World;
        Check(alphaWorld.Nodes.Count(n=>n.ArchetypeId=="special-landmark")==1,"one actual selected discovery supplies the role");
        foreach(var region in world.Regions){int count=region.World.TerrainDemandPlan!.Results.Single().Count;
            Check(count>=4&&region.World.Nodes.Count(n=>n.Kind=="discovery")==count,"actual terrain-funded discoveries retained");}
        Check(world.Connections.Count==3&&world.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"original journeys and selected network remain two-way");
        var limited=Reject(()=>planner.Plan(input with{ProgramComposition=input.ProgramComposition! with{MaximumAttempts=1}}),"METADATA_REGIONAL_JOINT_ATTEMPT_LIMIT");
        Check(limited.Data["RegionalJoint"] is MetadataRegionalJointPlan {Attempts.Count:1},"joint attempt limit retains its observed search history");
        Reject(()=>planner.Plan(input with{MaximumWork=definition.OwnershipPlan.Work-1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(input));
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(world),"joint source, selected role and world replay");
        Console.WriteLine("PASS: terrain-coupled program search recovers a missing generated role and retains complete physical worlds and paths.");
    }
}

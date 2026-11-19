using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalRedesignTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Physical partition redesign: "+message);}
    private static ArgumentException Reject(Action run,string prefix)
    {try{run();}catch(ArgumentException error)when(error.Message.StartsWith(prefix,StringComparison.Ordinal)){return error;}throw new Exception("Missing "+prefix);}
    private static MetadataRegionalProgramRegion Wide(MetadataRegionalProgramRegion r)=>r with{Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a.Id!="compound"?a:a with{
        PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-35,-5,70,10)},Access=a.Access! with{Ports=a.Access.Ports.Select(p=>p with{
            BoundaryPosition=new(p.BoundaryPosition.X>0?35:-35,0),ApproachPosition=new(p.ApproachPosition.X>0?36:-36,0)}).ToArray()}}).ToArray()}};
    internal static MetadataRegionalOwnershipRequest Example(string name)
    {
        var source=WorldRegionalControlTests.Example();
        source=source with{Pattern=source.Pattern with{PartitionSearch=new([0,90],32)}};
        if(name=="packing")return source with{Regions=source.Regions.Select(Wide).ToArray()};
        if(name=="multi-cut")
        {
            var ids=new[]{"arrival","grove","outpost","ruins"};var boundary=Box(0,0,200,80);
            var regions=ids.Select((id,i)=>{
                var r=source.Regions[0] with{Id=id,Purpose="Complete required "+id+" program"};
                return i<2?Wide(r):r with{PinnedAnchor=new("Retain this exact destination while revising another cut","redesign:pin",new(150,i==2?20:60))};
            }).ToArray();
            var network=source.NetworkRules![0];var destination=network.Destinations[0];
            return source with{Surface=source.Surface with{Boundary=boundary},Regions=regions,NetworkRules=[network with{
                Destinations=ids.Select(id=>destination with{Id=id,Purpose="Reach complete "+id+" program",Endpoint=destination.Endpoint with{RegionId=id}}).ToArray(),
                Options=network.Options with{MaximumPairChecks=6,RequiredConnections=[new("arrival","grove"),new("grove","outpost"),new("outpost","ruins")]}}]};
        }
        source=source with{Surface=source.Surface with{Exclusions=[new("impassable-cut","Required travel must stay on connected terrain","redesign:cut",Box(45,0,10,80))]}};
        if(name=="authored")
        {
            var network=source.NetworkRules![0];var first=network.Destinations[0].Endpoint;var second=network.Destinations[1].Endpoint;
            source=source with{NetworkRules=null,Connections=[new("required-route","regional-walk","Required complete return journey","redesign:travel",first,second,first.RegionId,
                source.Surface.Boundary!,network.Options.Profile,network.Options.Search,true)]};
        }
        return source;
    }
    internal static async Task RunAsync(string output,string? only=null)
    {
        if(only is not null&&only is not ("network" or "packing" or "authored" or "multi-cut"))throw new ArgumentException("Unknown redesign case");
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        foreach(string name in new[]{"network","packing","authored","multi-cut"}.Where(n=>only is null||n==only))
        {
            var source=Example(name);string original=Json(source);var definition=planner.Plan(source);var world=engine.Generate(definition);
            var plan=definition.OwnershipPlan!;var proof=plan.PhysicalFeedbackPlan!;
            Check(Json(source)==original,"authored programs unchanged");
            Check(proof.Contexts.Count>=2&&!proof.Attempts[0].Accepted&&proof.Attempts[^1].Accepted,"actual physical failure causes another ownership context");
            Check(proof.Contexts[0].OwnershipSha256!=proof.Contexts[^1].OwnershipSha256,"ownership geometry changes before final physical acceptance");
            Check(proof.Attempts[^1].RequestedComponents is null,"component ordinals never transfer across ownership contexts");
            Check(proof.PartitionSearchWork>0&&plan.Work==proof.PartitionSearchWork+proof.Contexts.Sum(c=>c.ComponentSearch?.Work??0)+proof.Attempts.Sum(a=>a.PlanningWork+a.QualificationWork),"every context, planning and physical/search operation is charged once");
            Check(world.Regions.Sum(r=>r.World.Nodes.Count)==source.Regions.Count*5&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==source.Regions.Count*3,"all complete original sources remain");
            Check(world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"every required regional direction succeeds");
            if(name=="multi-cut")
            {
                var first=proof.Contexts.First(c=>c.Masks is not null);var last=proof.Contexts[^1];
                Check(first.SelectedPartitions!.Single(c=>c.Members==4)==last.SelectedPartitions!.Single(c=>c.Members==4),"root cut is preserved while an internal cut changes");
                Check(first.SelectedPartitions!.Single(c=>c.FirstMember==0&&c.Members==2)!=last.SelectedPartitions!.Single(c=>c.FirstMember==0&&c.Members==2),"owning child allocation is redesigned");
                Check(proof.Attempts.Any(a=>a.PlanningRejected&&a.CandidateSha256 is null&&a.PartitionFailure is not null),"infeasible pinned partition retains actual planning qualification");
                foreach(var r in definition.Regions.Where(r=>r.Id is "outpost" or "ruins"))Check(r.Recipe.Origin!.SequenceEqual(new[]{150d,r.Id=="outpost"?20d:60d}),"protected destinations are not moved");
            }
            else
            {
                var forced=engine.Generate(planner.Plan(source with{Pattern=source.Pattern with{AxisRotationDegrees=90}}));
                Check(Json(world.Regions)==Json(forced.Regions),"search equals the unchanged known feasible alternative");
            }
            await engine.PublishAsync(world,Path.Combine(output,name));
            Check(Json(await engine.ReadWorldAsync(Path.Combine(output,name,"world-regions.json")))==Json(world),"full source and context history replay exactly");
            await File.WriteAllTextAsync(Path.Combine(output,name+"-request.json"),Json(source));
        }
        if(only is not null){Console.WriteLine("PASS: physical partition redesign "+only);return;}
        var input=Example("network");var session=await MetadataRegionalGenerationSession.StartDerivedAsync(new(),input);var before=session.Snapshot;
        var changed=input with{Surface=input.Surface with{Exclusions=input.Surface.Exclusions.Select(e=>e with{Boundary=Box(55,0,10,80)}).ToArray()}};
        var edited=await session.ApplyDerivedAndPublishAsync(new("terrain","Revise connected terrain","redesign:edit",before.World.DefinitionSha256,before.Report.WorldSha256,changed),Path.Combine(output,"session"));
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(new(),Path.Combine(output,"session/generation-session.json"));Check(Json(restored.Snapshot)==Json(edited),"saved source and context history restore");
        var pinned=input with{Regions=input.Regions.Select((r,i)=>r with{PinnedAnchor=new("Keep the selected disconnected location","redesign:impossible-pin",new(i==0?25:75,40))}).ToArray()};
        var failure=Reject(()=>planner.Plan(pinned),"METADATA_REGIONAL_PHYSICAL_PARTITIONS_EXHAUSTED");
        Check(failure.Data["RegionalPhysicalFeedback"] is MetadataRegionalPhysicalFeedbackPlan{Contexts.Count:2},"true finite exhaustion retains physical and planning contexts");
        try{await session.ApplyDerivedAndPublishAsync(new("impossible","Preserve accepted world","redesign:edit",edited.World.DefinitionSha256,edited.Report.WorldSha256,pinned),Path.Combine(output,"rejected"));throw new Exception("Missing failed source edit");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(edited)&&!Directory.Exists(Path.Combine(output,"rejected")),"failed redesign retains accepted snapshot and publishes nothing");
        var rollback=await session.ApplyDerivedAsync(new("rollback","Restore complete initial world","redesign:edit",edited.World.DefinitionSha256,edited.Report.WorldSha256,input));Check(Json(rollback.World)==Json(before.World),"exact rollback");
        Reject(()=>planner.Plan(input with{PhysicalFeedback=input.PhysicalFeedback! with{MaximumAttempts=1}}),"METADATA_REGIONAL_PHYSICAL_ATTEMPT_LIMIT");
        Reject(()=>planner.Plan(input with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        using var cancel=new CancellationTokenSource();cancel.Cancel();try{planner.Plan(input,cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
        await File.WriteAllTextAsync(Path.Combine(output,"exhausted-request.json"),Json(pinned));
        Console.WriteLine("PASS: required travel and complete packing drive actual root/internal partition alternatives, exact pins, fresh component contexts, full source/history/rollback and distinct failure limits.");
    }
    internal static async Task JointAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var source=WorldRegionalJointTests.TerrainExample();var boundary=Box(0,0,100,80);
        source=source with{Surface=source.Surface with{Boundary=boundary,Exclusions=[new("separation","Required full-width travel must remain connected","redesign:joint-cut",Box(45,0,10,80))],
            Patches=source.Surface.Patches.Select(p=>p with{Boundary=boundary}).ToArray()},
            Connections=source.Connections.Select(c=>c with{RoutingDomain=boundary}).ToArray(),
            Pattern=source.Pattern with{PartitionSearch=new([0,90],64)},PhysicalFeedback=new("Qualify complete coupled programs across alternative ownership","redesign:joint",16),MaximumWork=200000000};
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();var definition=planner.Plan(source);var proof=definition.OwnershipPlan!.JointPlan!;
        Check(proof.Attempts.Any(a=>a.Stable&&!a.Accepted&&a.ConnectionId is not null),"actual coupled complete program fails its required journey");
        Check(proof.Attempts.Any(a=>a.RequestedPartitions is not null)&&proof.Attempts[^1].Accepted,"coupled source/count/choice search changes ownership and fully qualifies it");
        Check(proof.Attempts[^1].RequestedComponents is null,"new partition cannot inherit old component ordinals");
        Check(proof.EnumerationWork+proof.Attempts.Sum(a=>a.Work)==definition.OwnershipPlan.Work,"joint partition enumeration is charged once");
        var world=engine.Generate(definition);var forced=engine.Generate(planner.Plan(source with{Pattern=source.Pattern with{AxisRotationDegrees=90}}));
        Check(Json(world.Regions)==Json(forced.Regions),"all actual terrain-required nested content equals the same declared feasible alternative");
        foreach(var r in world.Regions)
        {
            int count=r.World.TerrainDemandPlan!.Results.Single().Count;
            Check(count>=4&&r.World.Nodes.Count(n=>n.Kind=="marker")==count&&r.World.Nodes.Count(n=>n.Kind=="discovery")==count,"all mandatory source members follow the actual accepted habitat");
        }
        Check(world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"every required complete outward/return journey qualifies");
        await engine.PublishAsync(world,Path.Combine(output,"joint"));Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"joint/world-regions.json")))==Json(world),"coupled ownership/source/physical history replays");
        await File.WriteAllTextAsync(Path.Combine(output,"joint-request.json"),Json(source));
        Console.WriteLine("PASS: actual coupled habitat/count/complete program changes ownership after required travel failure, with exact source preservation and replay.");
    }
    internal static async Task EnumerationAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var properties=new Dictionary<string,JsonElement>();
        var single=new MetadataProgramMixRule("single","Exactly one complete original child","redesign:single",[new("leaf",1,MaximumCount:1)],
            [new("member","units",1)],"budget",1,1);
        WorldMetadataArchetype Host(string id)=>new(id,"host","Complete host with twelve mandatory children",properties,
            Enumerable.Range(0,12).Select(i=>new MetadataPart("child-"+i,"leaf",1,"Original required child",ProgramMix:single)).ToArray(),[],
            [new("leaf",12,12)],Capacities:[new("function","units",1,"One complete function","redesign:function")]);
        var rootMix=new MetadataProgramMixRule("host","Both declared complete host alternatives","redesign:host",[new("a",1,MaximumCount:1),new("b",2,MaximumCount:1)],
            [new("function","units",1)],"budget",2,1);
        var root=new WorldMetadataArchetype("root","world","Complete choice enumeration",properties,[new("hosts","a",1,"Required complete host",ProgramMix:rootMix)],[],[new("host",1,1)]);
        var leaf=new WorldMetadataArchetype("leaf","leaf","Original complete child",properties,[],[],[],Capacities:[new("member","units",1,"One original member","redesign:member")]);
        var catalog=new WorldMetadataCatalog(1,"enumeration",[root,Host("a"),Host("b"),leaf]);
        var recipe=new WorldMetadataRecipe(1,"enumeration","framework-reference",catalog.Id,"root","world",MetadataCoverage.PartialScope,
            ["Unplaced complete-choice enumeration control; not physical or whole-world acceptance."],new Dictionary<string,int>(),new(["x","y"],"meters"),
            ProgramComposition:new("choices","Retain every declared complete alternative within the original finite budget","redesign:choices",MaximumAttempts:20));
        var generator=new WorldMetadataGenerator();var choices=generator.AnalyzeProgramChoices(recipe,catalog,new(5000000,default),default).ToArray();
        Check(choices.Length==2,"all and only the two declared host alternatives are enumerated");
        for(int i=0;i<2;i++)
        {
            Check(choices[i].Ranks.Count==13&&choices[i].Ranks[0]==i&&choices[i].Ranks.Skip(1).All(r=>r==0),"exact independent Cartesian-product rank oracle");
            Check(choices[i].Analysis.Nodes.Count==14&&choices[i].Analysis.Nodes.Count(n=>n.Kind=="leaf")==12&&choices[i].Analysis.Nodes.Single(n=>n.Kind=="host").ArchetypeId==(i==0?"a":"b"),"every required original child survives both complete alternatives");
        }
        await File.WriteAllTextAsync(Path.Combine(output,"choices.json"),Json(choices));await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(catalog));
        Console.WriteLine("PASS: both complete alternatives and all twelve original children enumerate with exact Cartesian-product ranks inside the unchanged twenty-analysis cap.");
    }
}

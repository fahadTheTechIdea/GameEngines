using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldSourceGroundAllocationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Source ground allocation: "+message);}
    private static WorldPolygon Box(double size)=>new([new(-size/2,-size/2),new(size/2,-size/2),new(size/2,size/2),new(-size/2,size/2)]);
    internal static MetadataRegionalOwnershipRequest Example(bool childChoices=false)
    {
        var input=WorldRegionalJointTests.TerrainExample();
        var result=input with{Id="source-ground-world",Regions=input.Regions.Select(r=>r with{
            Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a.Id is "landmark" or "landmark-alt"?a with{
                PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(a.Id=="landmark"?3.2:4),Reservation=Box(a.Id=="landmark"?4.2:5),
                    GroundUse=new("activity","Preserve required open activity ground","source-ground:open",.6)},
                Access=new(MetadataOccupancyKind.Reservation,[new("east","Required east approach","source-ground:east",new(a.Id=="landmark"?1.6:2,0),new(3,0),1),
                    new("west","Required west approach","source-ground:west",new(a.Id=="landmark"?-1.6:-2,0),new(-3,0),1)])}:
                a.Id=="marker"?a with{Access=new(MetadataOccupancyKind.Solid,[])}:a).ToArray()},
            Recipe=r.Recipe with{
                TerrainDemands=r.Recipe.TerrainDemands! with{Rules=r.Recipe.TerrainDemands.Rules.Select(rule=>rule with{AmountPerMember=600}).ToArray()},
                RouteRules=r.Recipe.RouteRules!.Select(rule=>rule with{FromPortId="east",ToPortId="west"}).ToArray()}
        }).ToArray(),Connections=input.Connections.Select(c=>c with{From=c.From with{PortId="east"},To=c.To with{PortId="west"}}).ToArray()};
        if(!childChoices)return result;
        return result with{Regions=result.Regions.Select(r=>{
            var marker=r.Catalog.Archetypes.Single(a=>a.Id=="marker");
            var alternative=marker with{Id="marker-alt",PhysicalFootprint=marker.PhysicalFootprint! with{Polygon=Box(2),SourceRuleId="source-ground:authored-alternative"}};
            return r with{Catalog=r.Catalog with{Archetypes=[..r.Catalog.Archetypes.Select(a=>a.Id is not ("landmark" or "landmark-alt")?a:a with{
                Parts=a.Parts.Select(p=>p with{ProgramMix=p.ProgramMix! with{Offers=[new("marker",1,MaximumCount:1),new("marker-alt",2,MaximumCount:1)],MaximumCost=2}}).ToArray()}),alternative]}};
        }).ToArray()};
    }
    internal static async Task RunAsync(string output,bool control=false)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var engine=new WorldMetadataGenerator();var e=WorldHierarchicalGroundCapacityTests.Example(.75);var analysis=engine.AnalyzeProgram(e.Recipe,e.Catalog);
        var access=e.Catalog.Archetypes.Where(a=>a.Access is not null).ToDictionary(a=>a.Id,a=>a.Access!,StringComparer.Ordinal);
        ArgumentException? refused=null;
        try{new MetadataPlacementPlanner().ValidateProgramSourceEnvelopes(e.Recipe.Placement!,analysis,"meters",access,new(1000000,default),default);}
        catch(MetadataPlacementInfeasibleException error){refused=error;}
        Check(control?refused is null:refused?.Data["PlacementSourceOnlyProof"] is true&&refused.Data["GroundUseCapacityWitness"] is MetadataGroundUseCapacityWitness,"actual source-only preallocation boundary");
        if(!control)SourceControls(engine);
        await File.WriteAllTextAsync(Path.Combine(output,"source-proof.json"),Json(new{SourceOnly=refused?.Data["PlacementSourceOnlyProof"],Witness=refused?.Data["GroundUseCapacityWitness"]}));
        var input=Example();await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(input));
        var planner=new MetadataRegionalOwnershipPlanner();var definition=planner.Plan(input);
        await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(definition));
        var world=new MetadataRegionalWorldGenerator().Generate(definition);
        foreach(var region in world.Regions)
        {
            var discoveries=region.World.Nodes.Where(n=>n.Kind=="discovery").ToArray();
            Check(discoveries.Length==region.World.TerrainDemandPlan!.Results.Single().Count&&discoveries.All(n=>n.ArchetypeId=="landmark-alt"),"all terrain-required parents select the fitting authored variant");
            Check(region.World.Nodes.Count==1+2*discoveries.Length&&region.World.AccessPlan!.Sites.Count==2*discoveries.Length,"every required parent and child retained");
        }
        Check(world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"all required regional journeys and returns complete");
        var regional=new MetadataRegionalWorldGenerator();await regional.PublishAsync(world,Path.Combine(output,"packet"));
        Check(Json(await regional.ReadWorldAsync(Path.Combine(output,"packet/world-regions.json")))==Json(world),"complete source-qualified regional reload");
        var attempts=JsonSerializer.SerializeToElement(definition.OwnershipPlan!.JointPlan!.Attempts,WorldJson.CreateOptions());
        if(!control)Check(attempts.EnumerateArray().Any(a=>a.GetProperty("InvariantChoice") is var choice&&choice.ValueKind==JsonValueKind.Object&&choice.TryGetProperty("GroundUseCapacity",out var proof)&&proof.ValueKind==JsonValueKind.Object&&a.GetProperty("CandidateSha256").ValueKind==JsonValueKind.Null),"typed early ground witness retained before allocation");
        await File.WriteAllTextAsync(Path.Combine(output,"summary.json"),Json(new{Control=control,Regions=world.Regions.Count,Nodes=world.Regions.Sum(r=>r.World.Nodes.Count),PhysicalSources=world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count),Attempts=attempts.GetArrayLength(),Work=definition.OwnershipPlan.Work}));
        Console.WriteLine("PASS: source-ground preallocation boundary and complete authored regional alternatives with every nested source, required travel and full reload.");
    }

    private static void SourceControls(WorldMetadataGenerator engine)
    {
        var e=WorldHierarchicalGroundCapacityTests.Example(.6);var analysis=engine.AnalyzeProgram(e.Recipe,e.Catalog);var planner=new MetadataPlacementPlanner();
        var access=e.Catalog.Archetypes.Where(a=>a.Access is not null).ToDictionary(a=>a.Id,a=>a.Access!,StringComparer.Ordinal);
        var measured=new WorldSpatialWorkBudget(1000000,default);
        planner.ValidateProgramSourceEnvelopes(e.Recipe.Placement!,analysis,"meters",access,measured,default);
        planner.ValidateProgramSourceEnvelopes(e.Recipe.Placement!,analysis,"meters",access,new(measured.Used,default),default);
        try{planner.ValidateProgramSourceEnvelopes(e.Recipe.Placement!,analysis,"meters",access,new(measured.Used-1,default),default);throw new Exception("Source work limit ignored");}
        catch(ArgumentException error){Check(error.Message=="WORLD_ROUTE_WORK_BUDGET","source proof keeps exact work budget");}
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        try{planner.ValidateProgramSourceEnvelopes(e.Recipe.Placement!,analysis,"meters",access,new(1000000,cancelled.Token),cancelled.Token);throw new Exception("Source cancellation ignored");}catch(OperationCanceledException){}
        var impossible=WorldHierarchicalGroundCapacityTests.Example(.75);var unknown=impossible.Recipe.Placement! with{NestedStages=impossible.Recipe.Placement!.NestedStages!.Select(s=>s.Id.StartsWith("fixtures-",StringComparison.Ordinal)?s with{Bindings=s.Bindings.Select(b=>b with{UseCatalogFootprint=false}).ToArray()}:s).ToArray()};
        // Unknown source branches are omitted only from a necessary lower bound.
        // This control does not claim the incomplete declaration generates.
        planner.ValidateProgramSourceEnvelopes(unknown,engine.AnalyzeProgram(impossible.Recipe,impossible.Catalog),"meters",access,new(1000000,default),default);
        var elongated=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id!="fixture"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{
            Polygon=new([new(-5,-1),new(5,-1),new(5,1),new(-5,1)])}}).ToArray()};
        var positioned=e.Recipe with{Placement=e.Recipe.Placement! with{
            NestedStages=e.Recipe.Placement!.NestedStages!.Select(s=>!s.Id.StartsWith("fixtures-",StringComparison.Ordinal)?s:s with{Request=s.Request with{Anchors=[new("anchor-a",new(-2,0)),new("anchor-b",new(2,0))]}}).ToArray(),
            PoseControls=e.Recipe.Placement.NestedStages!.Where(s=>s.Id.StartsWith("fixtures-",StringComparison.Ordinal)).SelectMany(s=>s.Bindings.Select((b,i)=>
                new MetadataPlacementPoseControl("turn-"+s.Id+"-"+i,b.NodeId,"Preserve authored source rotation","source-ground:pose",new(i==0?-2:2,0),MetadataPlacementPoseFrame.Stage,90))).ToArray()}};
        planner.ValidateProgramSourceEnvelopes(positioned.Placement!,engine.AnalyzeProgram(positioned,elongated),"meters",access,new(1000000,default),default);
        Check(engine.Generate(positioned,elongated).AccessPlan!.Sites.Count==7,"source preflight honors the same pose-controlled orientation as actual placement");
    }

    internal static async Task RunChoicesAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var input=Example(childChoices:true);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(input));
        var definition=new MetadataRegionalOwnershipPlanner().Plan(input);var engine=new MetadataRegionalWorldGenerator();var world=engine.Generate(definition);
        foreach(var r in world.Regions)
        {
            var discoveries=r.World.Nodes.Where(n=>n.Kind=="discovery").ToArray();var markers=r.World.Nodes.Where(n=>n.Kind=="marker").ToArray();
            Check(discoveries.Length==r.World.TerrainDemandPlan!.Results.Single().Count&&discoveries.All(n=>n.ArchetypeId=="landmark"),"preferred parent reconsidered with changed authored child geometry");
            Check(markers.Length==discoveries.Length&&markers.All(n=>n.ArchetypeId=="marker-alt"),"all required child functions use the explicitly authored fitting variant");
        }
        var trace=definition.OwnershipPlan!.JointPlan!.Attempts;
        Check(trace.Any(a=>a.InvariantChoice?.GroundUseCapacity is not null&&a.InvariantProgramSha256 is not null)&&trace[^1].Accepted,"actual source-only witness and complete final acceptance");
        var tampered=trace.Select(a=>a.InvariantChoice?.GroundUseCapacity is not {} ground?a:a with{InvariantChoice=a.InvariantChoice with{GroundUseCapacity=ground with{RequiredSolidArea=0}}}).ToArray();
        try{engine.Generate(definition with{OwnershipPlan=definition.OwnershipPlan with{JointPlan=definition.OwnershipPlan.JointPlan! with{Attempts=tampered}}});throw new Exception("Changed ground proof accepted");}
        catch(ArgumentException error){Check(error.Message=="METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH","source replay rejects changed ground witness");}
        await engine.PublishAsync(world,Path.Combine(output,"packet"));Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"packet/world-regions.json")))==Json(world),"changed source path replays exactly");
        await File.WriteAllTextAsync(Path.Combine(output,"summary.json"),Json(new{Regions=world.Regions.Count,Nodes=world.Regions.Sum(r=>r.World.Nodes.Count),PhysicalSources=world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count),Attempts=trace.Count,SourceProofs=trace.Count(a=>a.InvariantChoice?.GroundUseCapacity is not null)}));
        Console.WriteLine("PASS: invariant pruning retains a parent when its authored child alternative changes the source proof; all parts, counts, travel and reload preserved.");
    }

    internal static async Task RunLifecycleAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var engine=new WorldMetadataGenerator();var session=await MetadataRegionalGenerationSession.StartDerivedAsync(engine,Example());
        var initial=session.Snapshot;
        MetadataRegionalSourceEditRequest Edit(string id,MetadataRegionalOwnershipRequest proposed)=>new(id,"Revise complete authored alternatives","source-ground:edit",session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,proposed);
        var edited=await session.ApplyDerivedAndPublishAsync(Edit("child-choices",Example(childChoices:true)),Path.Combine(output,"edited"));
        Check(edited.World.Regions.All(r=>r.World.Nodes.Where(n=>n.Kind=="discovery").All(n=>n.ArchetypeId=="landmark")),"source edit reconsiders parent and child variants");
        string accepted=Json(edited);
        var impossible=Example() with{Regions=Example().Regions.Select(r=>r with{Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a.PhysicalFootprint?.GroundUse is not {} ground?a:a with{PhysicalFootprint=a.PhysicalFootprint with{GroundUse=ground with{MinimumOpenFraction=.9}}}).ToArray()}}).ToArray()};
        try{await session.ApplyDerivedAsync(Edit("no-fitting-program",impossible));throw new Exception("Impossible source edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==accepted,"exhausted source alternatives preserve accepted world");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        try{await session.ApplyDerivedAsync(Edit("cancel",Example()),cancelled.Token);throw new Exception("Cancelled edit accepted");}catch(OperationCanceledException){}
        Check(Json(session.Snapshot)==accepted,"cancelled edit preserves accepted world");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(engine,Path.Combine(output,"edited/generation-session.json"));
        Check(Json(restored.Snapshot)==accepted,"all accepted source choice/count/ground proofs restore exactly");session=restored;
        var rollback=await session.ApplyDerivedAndPublishAsync(Edit("rollback",Example()),Path.Combine(output,"rollback"));
        Check(Json(rollback.World)==Json(initial.World),"source rollback restores original complete world");
        await File.WriteAllTextAsync(Path.Combine(output,"summary.json"),Json(new{SourceChoiceEdit=true,FailedCapacityEditPreserved=true,CancellationPreserved=true,CheckpointRestore=true,ExactRollback=true,Nodes=rollback.World.Regions.Sum(r=>r.World.Nodes.Count)}));
        Console.WriteLine("PASS: complete authored program edit, impossible/cancelled source preservation, ground-proof checkpoint restore and exact rollback.");
    }
}

using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldNestedSourceTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Nested source: "+message);}
    private static ArgumentException Reject(Action run,string code)
    {try{run();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return e;}throw new Exception("Missing "+code);}
    internal static MetadataRegionalOwnershipRequest Example(bool population=false,bool mixed=false,bool terrain=false,double rotation=0,bool largerAllocation=false)
    {
        var basis=WorldNestedOwnershipTests.Example(terrainEdit:terrain,rotation:rotation,mixedPrograms:mixed);
        var regions=basis.Regions.Take(2).Select(r=>{
            var source=r.Recipe;
            if(largerAllocation)source=source with{RegionDemands=source.RegionDemands! with{Rules=source.RegionDemands.Rules.Select(r=>r.RegionId!="alpha"?r:r with{MaximumCoverage=r.MaximumCoverage/2}).ToArray()}};
            var recipe=source with{Regions=null,LocalLayouts=null,RegionDemands=null,LayoutAdaptation=null,
                PopulationSource=new(source,r.Catalog,new([],UsePlacementBoundary:true)),
                ProgramComposition=new("consumer-programs","Qualify complete source-owned habitat and new content","nested-source:consumer",MaximumAttempts:64)};
            var catalog=r.Catalog;
            if(population)
            {
                var token=new WorldMetadataArchetype("discovery","discovery","Every accepted-allocation discovery",new Dictionary<string,JsonElement>(),[],[],[],
                    PhysicalFootprint:new("meters","Unchanged small discovery","nested-source:body",new([new(-.2,-.2),new(.2,-.2),new(.2,.2),new(-.2,.2)])),
                    Access:new(MetadataOccupancyKind.Solid,[]),Capacities:[new("discovery","units",1,"One complete discovery","nested-source:capacity")]);
                var mix=new MetadataProgramMixRule("discoveries","Choose every required additional discovery","nested-source:mix",
                    [new("discovery",1,MaximumCount:128),new("alternate-discovery",2,MaximumCount:128)],[new("discovery","units",1)],"cost",256,128);
                catalog=catalog with{Archetypes=[..catalog.Archetypes.Select(a=>a.Id!=recipe.RootArchetypeId?a:a with{
                    Parts=[..a.Parts,new("discoveries","discovery",1,"Every allocation-funded discovery",ProgramMix:mix)]}),token,token with{Id="alternate-discovery"}]};
                recipe=recipe with{TerrainDemands=new([new("discoveries","Accepted allocation requires complete discoveries","nested-source:count",recipe.Id,"discoveries","alpha",800,AllocationId:"alpha-site")]),
                    Placement=recipe.Placement! with{Request=recipe.Placement.Request with{Demands=[..recipe.Placement.Request.Demands,
                        new("discoveries","Preserve every additional discovery","nested-source:placement",null,r.Profile,RegionId:"alpha",AllocationId:"alpha-site")]},
                        Bindings=[..recipe.Placement.Bindings,new("discoveries",recipe.Id,true,PartId:"discoveries",Population:new("Discoveries along the accepted habitat boundary","nested-source:boundary",
                            GeneratedBoundary:new(MetadataBoundarySource.Allocation,1,Offset:2,StartDistance:10,RegionId:"alpha",AllocationId:"alpha-site",LoopIndex:0,UseLongestEdge:true)))]}};
            }
            return r with{Recipe=recipe,Catalog=catalog};
        }).ToArray();
        return basis with{Id="nested-source-world",Regions=regions,Connections=basis.Connections.Take(1).ToArray(),
            ProgramComposition=basis.ProgramComposition! with{MaximumAttempts=64}};
    }
    internal static async Task RunAsync(string output,bool full=true,string? only=null)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve prior output");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var planner=new MetadataRegionalOwnershipPlanner(generator);var engine=new MetadataRegionalWorldGenerator(generator);var observations=new List<object>();
        var cases=new[]{("source-only",Example()),("population",Example(population:true)),("allocation-edit",Example(population:true,largerAllocation:true)),
            ("mixed-source",Example(mixed:true)),("terrain-rotation",Example(population:true,terrain:true,rotation:23))};
        foreach(var(name,input) in only is not null?cases.Where(c=>c.Item1==only):full?cases:cases.Take(1))
        {
            string before=Json(input);await File.WriteAllTextAsync(Path.Combine(output,name+"-request.json"),before);var definition=planner.Plan(input);var world=engine.Generate(definition);
            Check(Json(input)==before,"original source context remains unchanged");
            var joint=definition.OwnershipPlan!.JointPlan!;
            Check(joint.Attempts[0].InputChoices.All(c=>c.RegionSelectionDeferred),"initial source spatial choices explicitly unqualified");
            Check(joint.Attempts[^1].Accepted&&joint.Attempts[^1].OutputChoices!.All(c=>!c.RegionSelectionDeferred),"actual complete source qualification replaces initialization");
            foreach(var packet in world.Regions)
            {
                var w=packet.World;var source=w.PlacementPlan!.Definition.SourceWorld!;var region=definition.Regions.Single(r=>r.Id==packet.RegionId);
                var a=definition.OwnershipPlan.ProgramDemands!.Single(d=>d.RegionId==packet.RegionId).Analysis;
                Check(source.Nodes.Count==11&&source.AccessPlan!.Sites.Count==9&&source.RegionPlan!.Regions.Count==3&&source.LocalLayoutPlan!.Allocations.Count==3,"all complete original compounds/modules and nested source regions retained");
                Check(Json(source.RegionPlan!.Request.Boundary)==Json(region.Ownership),"source region boundary is computed ownership");
                Check(Json(source.RegionPlan)==Json(w.RegionPlan)&&Json(source.LocalLayoutPlan)==Json(w.LocalLayoutPlan),"source owns exact habitat and local layouts");
                Check(a.SourceProgram!.QualifiedComposition is not null&&!a.RegionSelectionDeferred&&a.SourceProgram.RegionAnalysis is not null,"accepted analysis retains actual upstream source choices/regions");
                foreach(var n in source.Nodes)Check(Json(n)==Json(w.Nodes.Single(v=>v.Id==n.Id)),"every original source node and pose exact");
                foreach(var p in source.AccessPlan!.Sites)Check(Json(p)==Json(w.AccessPlan!.Sites.Single(v=>v.NodeId==p.NodeId)),"every original body/entrance exact");
                foreach(var p in source.RoutePlan!.Routes)Check(Json(p)==Json(w.RoutePlan!.Routes.Single(v=>v.ConnectionId==p.ConnectionId)),"every original local journey exact");
                var chosen=w.ProgramCompositionPlan!.Attempts.Single();
                Check(chosen.Selections.Count==(w.TerrainDemandPlan is null?0:1)&&chosen.RegionSelection is null,"consumer selects only new programs, never the source region");
                if(w.TerrainDemandPlan is {} terrain)
                {
                    Check(terrain.Results[0].Count==(int)Math.Floor(source.LocalLayoutPlan!.Allocations.Single(p=>p.Id=="alpha-site").Shape.Area/800),"accepted source allocation drives complete downstream quantity");
                    Check(MetadataTerrainDemandPlanner.MatchesAnalysis(a.TerrainAnalysis,w),"actual habitat analysis binds all downstream quantities");
                    foreach(var trace in w.PlacementPlan.PopulationBoundaries!)
                    {
                        var pattern=trace.Boundary.Pattern;var v=pattern.Boundary.Vertices;
                        int longest=Enumerable.Range(0,v.Count).OrderByDescending(i=>v[i].DistanceTo(v[(i+1)%v.Count])).ThenBy(i=>i).First();
                        Check(pattern.StartEdgeIndex==longest,"declared longest-edge grammar follows actual source geometry");
                    }
                }
                else Check(w.ProgramCompositionPlan.Selections==0&&w.ProgramCompositionPlan.MixWork==0&&w.ProgramCompositionPlan.RegionWork==0&&w.ProgramCompositionPlan.Definition.RequiredRanks!.Count==0,"source-only consumer receipt has no fabricated selections/work/ranks");
            }
            Check(world.Connections.Single().Reverse is not null,"full forward/return world journey retained");
            await File.WriteAllTextAsync(Path.Combine(output,name+"-request.json"),Json(input));await engine.PublishAsync(world,Path.Combine(output,name));
            var restored=await engine.ReadWorldAsync(Path.Combine(output,name,"world-regions.json"));Check(Json(restored)==Json(world),"native full unbound context/source/joint replay");
            observations.Add(new{Name=name,Nodes=world.Regions.Sum(p=>p.World.Nodes.Count),Bodies=world.Regions.Sum(p=>p.World.AccessPlan!.Sites.Count),Attempts=joint.Attempts.Count});
            Console.WriteLine("PASS nested source control: "+name);
        }
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Status="PASS",Controls=observations}));
    }
}

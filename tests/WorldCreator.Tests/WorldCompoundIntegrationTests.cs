using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldCompoundIntegrationTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldLayoutAdaptationTests.Example(true);
        const string id="expedition-outposts";
        var root=e.Catalog.Archetypes.Single(a=>a.Id==e.Recipe.RootArchetypeId) with{
            Purpose="Two connected expedition outposts",Parts=[new("outposts","outpost",2,"Required expedition base")],ChildRequirements=[new("outpost",2,2)]};
        WorldMetadataArchetype Member(string name,string kind,string purpose,WorldPolygon shape,MetadataOccupancyKind occupancy,
            IReadOnlyList<MetadataPart>? parts=null,WorldPolygon? reservation=null,IReadOnlyList<MetadataAccessPort>? ports=null)=>
            new(name,kind,purpose,new Dictionary<string,JsonElement>(),parts??[],[],[],LayerId:"intent",
                PhysicalFootprint:new("meters",purpose,"expedition:"+name,shape,reservation),Access:new(occupancy,ports??[]));
        var outpost=Member("outpost","outpost","Shelter and outdoor supply yard",Box(-7,-3,14,6),MetadataOccupancyKind.Reservation,
            [new("shelters","shelter",1,"Required weather shelter"),new("yards","yard",1,"Required outdoor work area")],Box(-8,-4,16,8));
        var shelter=Member("shelter","structure","Weather shelter",Box(-2,-1,4,2),MetadataOccupancyKind.Solid,
            reservation:Box(-2.5,-1.5,5,3),ports:[new("north","Shelter entrance","expedition:entrance",new(0,-1),new(0,-2),.8)]);
        var yard=Member("yard","ground-area","Supply handling and work area",Box(-2,-1.5,4,3),MetadataOccupancyKind.Reservation,
            [new("stores","store",1,"Required supply cache")],Box(-2.5,-2,5,4),
            [new("north","Yard approach","expedition:yard-access",new(0,-1.5),new(0,-2.5),.8)]);
        var store=Member("store","equipment","Solid supply cache",Box(-.5,-.5,1,1),MetadataOccupancyKind.Solid);
        var catalog=e.Catalog with{Id="expedition-content",Archetypes=[root,outpost,shelter,yard,store]};
        var request=e.Recipe.Placement!.Request;var profile=request.Demands[0].Profile;
        var nested=new List<MetadataNestedPlacement>();var links=new List<MetadataExplicitConnection>();
        for(int i=0;i<2;i++)
        {
            string parent=id+"/outposts-"+i;
            var demands=new[]{new WorldSiteDemand("shelter","Required shelter","expedition:shelter",null,profile),
                new WorldSiteDemand("yard","Required yard","expedition:yard",null,profile)};
            nested.Add(new("outpost-"+i,parent,new("contents","Complete outpost contents","expedition:contents",null,demands,
                [new("shelter-anchor",new(-3,0)),new("yard-anchor",new(3,0))],
                [new("shelter-pin","shelter","shelter-anchor","Shelter beside work area","expedition:layout",0,0,0,1),
                 new("yard-pin","yard","yard-anchor","Work area beside shelter","expedition:layout",0,0,0,1)],MinimumSeparation:.5),
                [new("shelter",parent+"/shelters-0",true),new("yard",parent+"/yards-0",true)]));
            nested.Add(new("cache-"+i,parent+"/yards-0",new("cache","Store supplies inside yard","expedition:cache",null,
                [new("store","Required supply cache","expedition:cache",null,profile)],[new("center",new(0,0))],
                [new("pin","store","center","Keep cache in work area","expedition:cache",0,0,0,1)]),
                [new("store",parent+"/yards-0/stores-0",true)]));
            links.Add(new("local-"+i,"outpost-path",parent+"/shelters-0",parent+"/yards-0"));
        }
        links.Add(new("trail","expedition-trail",id+"/outposts-0/yards-0",id+"/outposts-1/yards-0"));
        var route=e.Recipe.RouteRules![0] with{Id="trail",ConnectionKind="expedition-trail",FromPortId="north",ToPortId="north",
            Search=e.Recipe.RouteRules[0].Search with{MaximumWork=5000000},Interfaces=[new(id+"/explicit/trail","west-link")]};
        return(e.Recipe with{Id=id,GameProfile="expedition-survival",CatalogId=catalog.Id,
            Placement=e.Recipe.Placement with{Bindings=[new(request.Demands[0].Id,id+"/outposts-0",true),new(request.Demands[1].Id,id+"/outposts-1",true)],NestedStages=nested},
            AdditionalConnections=links,RouteRules=[route,route with{Id="outpost-access",ConnectionKind="outpost-path",Interfaces=null}],
            RemainingScopes=["Two complete declared physical outposts and their paths. Whole-world diversity, decorative population, approved artwork and native Godot runtime remain open."]},catalog);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==9&&world.Nodes.All(n=>n.Position is {Count:2}),"all required compound members positioned");
        var placement=world.PlacementPlan!;
        Check(placement.NestedStages.Count==4&&placement.RelativePositions.Count==0,"every constituent has a physical allocation");
        Check(world.AccessPlan!.Sites.Count==8&&world.AccessPlan.Sites.Count(s=>s.Occupancy==MetadataOccupancyKind.Solid)==4,"two shelters and two caches are real obstacles");
        var sources=placement.Footprints.Concat(placement.NestedStages.SelectMany(s=>s.Footprints)).ToDictionary(s=>s.NodeId);
        foreach(var site in world.AccessPlan.Sites)
        {
            var node=world.Nodes.Single(n=>n.Id==site.NodeId);var source=e.Catalog.Archetypes.Single(a=>a.Id==node.ArchetypeId).PhysicalFootprint!;
            Check(Json(sources[node.Id].Polygon)==Json(source.Polygon),"exact catalog physical source retained");
            if(node.ParentId!=world.Id)
                Check(world.AccessPlan.Sites.Single(s=>s.NodeId==node.ParentId).Footprint.ContainsPolygon(site.Footprint),"full child containment");
        }
        var trail=world.RoutePlan!.Routes.Single(r=>r.Interface is not null);
        Check(trail.Interface!.FromDomainId=="west-a"&&trail.Interface.ToDomainId=="west-b","nested endpoints inherit owning allocations");
        Check(world.RoutePlan.Routes.Count==3&&world.RoutePlan.Routes.All(r=>r.Forward.Path is not null&&r.Reverse?.Path is not null),"both internal paths and regional trail work bidirectionally");
        Check(world.LayoutAdaptationPlan!.Attempts.Count==2&&!world.LayoutAdaptationPlan.Attempts[0].Accepted,"full compound survives bounded repartition");
        var query=new MetadataPhysicalAccessPlanner().RouteSurface(world.Surface!,world.AccessPlan);
        foreach(var route in world.RoutePlan.Routes)
            Check(query.ContainsCorridor(route.Forward.Path!,.3)&&query.ContainsCorridor(route.Reverse!.Path!,.3),"all paths avoid every solid with full passage width");
        var reordered=generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{NestedStages=e.Recipe.Placement.NestedStages!.Reverse().ToArray()}},e.Catalog);
        Check(Json(world.Nodes)==Json(reordered.Nodes)&&Json(world.RoutePlan)==Json(reordered.RoutePlan),"nested stage declaration order does not affect inheritance or routes");
        MetadataRoutePlan Replan(MetadataPlacementPlan changedPlacement)=>new MetadataRoutePlanner().Plan(world.RoutePlan.Rules,world.Surface!,world.Space,
            world.Nodes,world.Connections,accessPlan:world.AccessPlan,regions:world.RegionPlan,localLayouts:world.LocalLayoutPlan,placement:changedPlacement,environment:world.EnvironmentPlan!.Definition);
        MetadataPlacementPlan Override(string allocation)=>placement with{Definition=placement.Definition with{
            NestedStages=placement.Definition.NestedStages!.Select(s=>s.Id!="outpost-0"?s:s with{Request=s.Request with{
                Demands=s.Request.Demands.Select(d=>d.Id!="yard"?d:d with{AllocationId=allocation}).ToArray()}}).ToArray()}};
        Check(Json(Replan(Override("west-a")))==Json(world.RoutePlan),"explicit matching child allocation is authoritative");
        Reject(()=>Replan(Override("west-b")),"METADATA_INTERFACE_DOMAIN_MISMATCH");
        Reject(()=>Replan(placement with{Definition=placement.Definition with{NestedStages=placement.Definition.NestedStages!.Select(s=>s.Id!="outpost-0"?s:
            s with{ParentNodeId=world.Id+"/outposts-0/yards-0"}).ToArray()}}),"METADATA_INTERFACE_DOMAIN_HIERARCHY_INVALID");
        // Route endpoints can be equipment three levels deep, not just immediate children.
        var cacheCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id!="store"?a:a with{Access=a.Access! with{
            Ports=[new("north","Supply access","expedition:cache-access",new(0,-.5),new(0,-1.5),.8)]}}).ToArray()};
        var cacheWorld=generator.Generate(e.Recipe with{AdditionalConnections=e.Recipe.AdditionalConnections!.Select(c=>c.Id!="trail"?c:
            c with{FromId=c.FromId+"/stores-0",ToId=c.ToId+"/stores-0"}).ToArray()},cacheCatalog);
        Check(cacheWorld.RoutePlan!.Routes.Single(r=>r.Interface is not null).Interface!.FromDomainId=="west-a","three-level physical endpoint inherits allocation");
        var oversized=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id!="shelter"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{
            Polygon=Box(-4,-1,8,2),Reservation=Box(-4.5,-1.5,9,3)}}).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,oversized),"METADATA_LAYOUT_ADAPTATION_UNRESOLVED");
        new MetadataLocalLayoutPlanner().ValidateDocument(world);new MetadataRoutePlanner().ValidateDocument(world);
        // Missing required allocation is an input failure; neither marker fallback nor omission is allowed.
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{NestedStages=e.Recipe.Placement.NestedStages!.Where(s=>s.Id!="cache-1").ToArray()}},e.Catalog),"METADATA_PHYSICAL_ALLOCATION_REQUIRED");
        var changed=world with{RoutePlan=world.RoutePlan with{Routes=world.RoutePlan.Routes.Select(r=>r.Interface is null?r:r with{Interface=r.Interface with{FromDomainId="wrong"}}).ToArray()}};
        Reject(()=>new MetadataRoutePlanner().ValidateDocument(changed),"METADATA_ROUTE_PLAN_MISMATCH");
        string temp=Path.Combine(Path.GetTempPath(),"worldcreator-compound-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(temp);
        await generator.GenerateFilesAsync(Path.Combine(temp,"recipe.json"),Path.Combine(temp,"catalog.json"),Path.Combine(temp,"output"));
        var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(temp,"output/world-metadata.json"));Check(Json(saved)==Json(world),"complete nested compound survives export and replay");
        Console.WriteLine("PASS: complete physical outposts, nested source-sized shelters/yards/caches, inherited interface ownership, full-width internal/external access, adaptive partition, stage ordering, missing-content refusal and replay/tamper.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool value,string message){if(!value)throw new Exception("Compound: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}

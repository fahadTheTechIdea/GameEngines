using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldEntranceTravelTests
{
    private const string Id="generated-interior";
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(double rotation=0,bool network=false)
    {
        var e=WorldGeneratedInteriorTests.Example(rotation:rotation);double angle=rotation*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);
        double[] Rotate(double x,double y)=>[x*c-y*s,x*s+y*c];
        var root=e.Catalog.Archetypes[0] with{Parts=e.Catalog.Archetypes[0].Parts.Append(new MetadataPart("trail","trail",2,"Required regional trail destination",
            Layout:MetadataLineLayout.Parameters(Rotate(-8,0),Rotate(36,0)))).ToArray()};
        var room=e.Catalog.Archetypes.Single(a=>a.Id=="room");
        room=room with{Access=room.Access! with{Ports=[new("west","Source exterior entrance","entrance:west",new(-3,0),new(-4,0),1.4)]}};
        var catalog=e.Catalog with{Archetypes=[root,room,e.Catalog.Archetypes.Single(a=>a.Id=="fixture"),
            new("trail","trail-marker","Purposeful outside destination",new Dictionary<string,JsonElement>(),[],[],[])]};
        var interior=e.Recipe.Interiors!;var rule=interior.Rules[0] with{Entrances=[new(Id+"/explicit/entry","west")]};
        var search=rule.Search with{MaximumCandidates=1024,MaximumWork=100000000};
        var links=new List<MetadataExplicitConnection>{new("entry","entry",Id+"/trail-0",Id+"/rooms-0")};
        if(!network)links.Add(new("regional","regional",Id+"/trail-0",Id+"/trail-1"));
        return(e.Recipe with{Interiors=interior with{Rules=[rule]},AdditionalConnections=links,
            RouteRules=network?null:[new("regional","regional",rule.Profile,search)],
            NetworkRules=network?[new("regional-network",Id,"trail-marker",2,2,"regional",new("Connect outside destinations","entrance:regional","region",rule.Profile,search,NearestNeighbors:1,MaximumWork:150000000))]:null,
            Corridors=new([new("regional-corridor","regional","entrance:corridor",0,"connect-at-grade")],MaximumWork:100000000),
            ProgressionRules=[new("outside-inside","Reach final room through the entrance","entrance:journey",Id,Id+"/trail-1",[Id+"/rooms-5"],["regional","entry","door"],[Id+"/trail-0",Id+"/rooms-0"])],
            RemainingScopes=["Generated exterior/interior physical journeys and shared walls; multiple floors, wide terrain/content composition and world-scale performance remain open. Godot deferred."]},catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(var item in new[]{("regional-route",0d,false),("rotated-route",23d,false),("regional-network",0d,true)})
        {
            var e=Example(item.Item2,item.Item3);string folder=Path.Combine(directory,item.Item1);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),JsonSerializer.Serialize(e.Recipe,WorldJson.CreateOptions(true)));
            await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),JsonSerializer.Serialize(e.Catalog,WorldJson.CreateOptions(true)));
        }
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();
        foreach(var item in new[]{(0d,false),(23d,false),(0d,true)})
        {
            var e=Example(item.Item1,item.Item2);var world=generator.Generate(e.Recipe,e.Catalog);worlds.Add(world);
            Check(world.Nodes.Count==15&&world.AccessPlan!.Sites.Count==12,"all six source rooms, six fixtures and two outside markers retained");
            var assembly=world.InteriorPlan!.Results.Single();var entry=assembly.Openings.Single(o=>o.Entrance is not null);
            Check(assembly.Openings.Count==6&&assembly.Routes.Count==6,"five room doors plus one catalog-owned exterior entrance");
            var sourcePort=world.AccessPlan!.Sites.Single(s=>s.NodeId==Id+"/rooms-0").Ports.Single();
            var midpoint=new WorldPoint((entry.From.X+entry.To.X)/2,(entry.From.Y+entry.To.Y)/2);
            Check(midpoint.DistanceTo(sourcePort.BoundaryPosition)<1e-8&&entry.FromApproach==sourcePort.ApproachPosition,"opening and outer crossing endpoint preserve exact catalog source port");
            Independent(world);
            WorldMetadataGenerator.ValidateDocument(world);
            Console.WriteLine($"Qualified outside/inside {item.Item1} degrees, network={item.Item2}: {assembly.Walls.Count} wall pieces, {assembly.Openings.Count} openings, interior work {world.InteriorPlan.Work}, regional work {world.RoutePlan?.Work??world.NetworkPlan!.Networks[0].Plan.Work}.");
        }
        var original=Example();var world0=worlds[0];var definition=original.Recipe.Interiors!;var rule=definition.Rules[0];
        var missing=definition with{Rules=[rule with{Entrances=[new(Id+"/explicit/entry","missing")]}]};
        Reject(()=>new MetadataInteriorPlanner().Plan(missing,world0.Surface!,world0.Nodes,world0.Connections,world0.AccessPlan),"METADATA_INTERIOR_ENTRANCE_PORT_REQUIRED");
        var narrow=definition with{Rules=[rule with{OpeningWidth=1.5}]};
        Reject(()=>new MetadataInteriorPlanner().Plan(narrow,world0.Surface!,world0.Nodes,world0.Connections,world0.AccessPlan),"METADATA_INTERIOR_ENTRANCE_PORT_TOO_NARROW");
        // Direct-owner facts exercise named external port endpoints, including a
        // zero-length exterior leg. These facts are not published as catalog output.
        foreach(double extent in new[]{1d,3d})
        {
            var outsidePort=new MetadataCatalogAccess(MetadataOccupancyKind.Reservation,[new("east","Outside source entrance","test:outside-port",new(extent,0),new(extent+1,0),1.4)]);
            var definitions=world0.AccessPlan!.Definitions.ToDictionary(p=>p.Key,p=>p.Value);definitions.Add("trail",outsidePort);
            var extra=world0.Nodes.Where(n=>n.ArchetypeId=="trail").Select(n=>new MetadataPhysicalSite(n.Id,MetadataOccupancyKind.Reservation,"test:outside-source",
                Box(n.Position![0]-1,n.Position[1]-1,extent+1,2),outsidePort.Ports.Select(p=>p with{BoundaryPosition=p.BoundaryPosition.Offset(n.Position[0],n.Position[1]),ApproachPosition=p.ApproachPosition.Offset(n.Position[0],n.Position[1])}).ToArray()));
            var access=world0.AccessPlan with{Definitions=definitions,Sites=world0.AccessPlan.Sites.Concat(extra).ToArray()};
            var portDefinition=definition with{Rules=[rule with{Entrances=[new(Id+"/explicit/entry","west","east")]}]};
            var portPlan=new MetadataInteriorPlanner().Plan(portDefinition,world0.Surface!,world0.Nodes,world0.Connections,access);
            var result=portPlan.Results[0].Routes.Single(r=>r.ConnectionId==Id+"/explicit/entry");
            Check(result.Forward.Path!.Points[0]==new WorldPoint(-7+extent,0)&&result.Reverse!.Path!.Points[^1]==result.Forward.Path.Points[0],"named outside port remains exact; coincident outside approach needs no fabricated segment");
        }
        var collision=world0.AccessPlan! with{Sites=world0.AccessPlan!.Sites.Select(s=>s.NodeId==Id+"/rooms-0/fixture-0"?s with{Footprint=Box(-3,.75,.8,.5)}:s).ToArray()};
        Reject(()=>new MetadataInteriorPlanner().Plan(definition,world0.Surface!,world0.Nodes,world0.Connections,collision),"METADATA_INTERIOR_CONTENT_WALL_COLLISION");
        var noEntrance=definition with{Rules=[rule with{Entrances=null}]};
        var closed=new MetadataInteriorPlanner().Plan(noEntrance,world0.Surface!,world0.Nodes,world0.Connections,world0.AccessPlan);
        var entryRoute=world0.InteriorPlan!.Results[0].Routes.Single(r=>r.ConnectionId==Id+"/explicit/entry").Forward;
        var closedQuery=new MetadataPhysicalAccessPlanner().RouteSurface(MetadataInteriorPlanner.WithWalls(world0.Surface!,closed),world0.AccessPlan);
        Check(!closedQuery.ContainsCorridor(entryRoute.Path!,.2),"removing the declared entrance closes the same physical journey");
        var duplicate=original.Recipe with{RouteRules=[..original.Recipe.RouteRules!,new("duplicate","entry",rule.Profile,rule.Search)]};
        Reject(()=>generator.Generate(duplicate,original.Catalog),"METADATA_INTERIOR_ROUTE_OWNERSHIP_CONFLICT");
        var blocked=world0.Surface! with{Exclusions=[new("gate","Close the entrance approach","test:gate",Box(-4.2,-1,.4,2))]};
        Reject(()=>new MetadataInteriorPlanner().Plan(definition,blocked,world0.Nodes,world0.Connections,world0.AccessPlan),"METADATA_INTERIOR_EXTERIOR_PASSAGE_REJECTED");
        var chunks=new MetadataChunkPlanner().Plan(new(1,"journey-chunks","Index combined exterior/interior travel","test:journey-chunks",new(-8,-4),12,8),world0);
        Check(chunks.Members.Count(m=>m.Kind==MetadataSpatialMemberKind.Path)==14,"combined chunk index retains every exterior/interior direction with one identity");
        var session=new MetadataGenerationSession(generator,original.Recipe,original.Catalog);var before=session.Snapshot;
        var changed=original.Recipe with{Interiors=definition with{Rules=[rule with{OpeningWidth=1.3}]}};
        var after=session.Apply(new("entrance-width","Recompute shared entrance and exterior travel","test:entrance",before.World.RecipeSha256,before.World.CatalogSha256,changed,original.Catalog));
        Check(Json(after.World)==Json(generator.Generate(changed,original.Catalog)),"shared-wall entrance edit equals clean generation");
        Check(new[]{"allocation","interiors","networks","routes","corridors"}.All(id=>!after.Report.Stages.Single(s=>s.Id==id).Reused)&&after.Report.Stages.Single(s=>s.Id=="topology").Reused,"wall policy invalidates every physical consumer without rebuilding topology");
        string file=Path.Combine(Path.GetTempPath(),"world-entrance-"+Guid.NewGuid().ToString("N")+".json");
        await File.WriteAllTextAsync(file,Json(world0));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(file))==Json(world0),"saved combined exterior/interior document replays all shared physical owners");
        await new WorldMetadataSchematicExporter().CreateAsync(world0);
        Console.WriteLine("PASS: explicit exterior entrances, shared wall-aware regional routes/networks/corridors, complete outside-inside progression, retained source parts/ports, rotated input, closed/missing/narrow/blocked/duplicate-owner refusal, replay and edit equivalence. Wider world generation remains open.");
    }
    private static void Independent(WorldMetadataDocument world)
    {
        var routes=world.InteriorPlan!.Results.SelectMany(r=>r.Routes).Concat(world.RoutePlan?.Routes??[]).ToDictionary(r=>r.ConnectionId);
        foreach(var network in world.NetworkPlan?.Networks??[])foreach(var edge in network.Plan.Edges)routes.Add(edge.Link.Id,new(edge.Link.Id,network.RuleId,edge.Forward,edge.Reverse));
        var witness=world.ProgressionPlan!.Results[0].Paths[0];string current=Id+"/trail-1";WorldPoint? endpoint=null;
        var query=new MetadataPhysicalAccessPlanner().RouteSurface(MetadataInteriorPlanner.WithWalls(world.Surface!,world.InteriorPlan),world.AccessPlan);
        foreach(string id in witness.ConnectionIds)
        {
            var link=world.Connections.Single(c=>c.Id==id);bool forward=link.FromId==current;var result=forward?routes[id].Forward:routes[id].Reverse!;
            Check(result.Status==WorldRouteStatus.Found&&query.ContainsCorridor(result.Path!,.2),"every complete journey leg fits full shared-wall/source-solid clearance");
            if(endpoint is {} prior)Check(prior.DistanceTo(result.Path!.Points[0])<1e-8,"outside and inside legs share exact endpoints");
            endpoint=result.Path!.Points[^1];current=forward?link.ToId:link.FromId;
        }
        Check(current==Id+"/rooms-5"&&witness.ConnectionIds.Contains(Id+"/explicit/entry"),"complete physical witness reaches final room through declared exterior opening");
        var regional=world.RoutePlan?.Routes[0].Forward??world.NetworkPlan!.Networks[0].Plan.Edges[0].Forward;
        Check(regional.Path!.Points.Count>2,"regional path bends around closed room walls");
        Check(regional.Cost!.Distance<=54,"regional journey stays within the fixture's 1.5x direct-distance quality bound instead of detouring to the domain boundary");
        foreach(var wall in world.InteriorPlan.Results.SelectMany(r=>r.Walls))for(int i=1;i<regional.Path.Points.Count;i++)
        {
            var a=regional.Path.Points[i-1];var b=regional.Path.Points[i];
            Check(!wall.Footprint.CrossesInterior(a,b)&&wall.Footprint.Vertices.All(p=>Distance(p,a,b)>=.2-1e-7),"independent regional wall clearance");
        }
    }
    private static double Distance(WorldPoint p,WorldPoint a,WorldPoint b){double x=b.X-a.X,y=b.Y-a.Y,t=Math.Clamp(((p.X-a.X)*x+(p.Y-a.Y)*y)/(x*x+y*y),0,1);return p.DistanceTo(new(a.X+t*x,a.Y+t*y));}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected refusal: "+code);}
}

using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldCoastalMixedWorldTests
{
    const string Id="coastal-mixed-world";
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    static void Check(bool value,string reason){if(!value)throw new Exception("Coastal world: "+reason);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldNetworkPolicyTests.Example();string old=e.Recipe.Id;
        string Node(string value)=>value==old?Id:value.StartsWith(old+"/",StringComparison.Ordinal)?Id+value[old.Length..]:value;
        WorldPoint Twice(WorldPoint p)=>new(p.X*2,p.Y*2);
        var boundary=new WorldPolygon([new(0,0),new(175,0),new(200,30),new(196,100),new(165,160),new(0,160)]);
        var shore=new WorldPolygon([new(108,126),new(142,106),new(182,116),new(165,160),new(100,160)]);
        var land=new WorldTerrainPatch("lowland","ground","Lowland district foundation","coastal:lowland",boundary,0,new(0,0),0,.01,0);
        var hill=land with{Id="hillside",Purpose="Continuous hillside grade",SourceRuleId="coastal:hillside",Boundary=Box(100,0,100,160),
            Priority=1,Origin=new(100,0),Elevation=1,GradientX=.08};
        var surface=e.Recipe.Surface! with{Id="coastal-terrain",Boundary=boundary,
            Exclusions=[new("shore-reserve","Undeveloped coastal shore; no construction or routes","coastal:shore",shore)],Patches=[land,hill]};
        var locals=e.Recipe.LocalLayouts! with{Layouts=e.Recipe.LocalLayouts.Layouts.Select(l=>l with{
            Programs=l.Programs.Select(p=>p with{Anchor=Twice(p.Anchor),MinimumArea=p.MinimumArea*4,Influence=p.Influence*4}).ToArray(),
            Grid=l.Grid is {} g?g with{Origin=Twice(g.Origin),CellWidth=g.CellWidth*2,CellHeight=g.CellHeight*2}:null}).ToArray(),
            Interfaces=e.Recipe.LocalLayouts.Interfaces.Select(i=>i with{MinimumSpan=i.MinimumSpan*2}).ToArray()};
        var region=e.Recipe.Regions! with{Boundary=boundary,Profile=Regional(e.Recipe.Regions.Profile),
            Seeds=e.Recipe.Regions.Seeds.Select(s=>s with{Position=Twice(s.Position),MinimumArea=s.MinimumArea*4,Profile=s.Profile is {} p?Regional(p):null}).ToArray()};
        WorldSuitabilityProfile Regional(WorldSuitabilityProfile p)=>p with{Requirements=p.Requirements.Select(r=>r.FieldId=="region-band"?r with{Minimum=r.Minimum*2,Maximum=r.Maximum*2}:r).ToArray()};
        var profile=e.Recipe.Placement!.Request.Demands[0].Profile;
        var points=new Dictionary<string,WorldPoint>{{"west-a",new(30,40)},{"west-b",new(80,40)},{"west-c",new(30,120)},{"west-d",new(80,120)},
            {"east-a",new(130,30)},{"east-b",new(173,32)},{"east-c",new(140,80)}};
        var placement=e.Recipe.Placement.Request with{Boundary=boundary,
            Anchors=points.Select(p=>new WorldPlacementAnchor("hold-"+p.Key,p.Value)).ToArray(),
            Relations=points.Select(p=>new WorldPlacementRelation("hold-"+p.Key,p.Key,"hold-"+p.Key,"Declared compound origin","coastal:anchor",0,0,0,1)).ToArray()};
        var properties=new Dictionary<string,JsonElement>();
        var positions=new[]{new WorldPoint(-7,-6),new WorldPoint(7,-6),new WorldPoint(-7,6),new WorldPoint(7,6)};
        var parts=Enumerable.Range(0,4).Select(i=>new MetadataPart("buildings-"+i,"building",1,"Required building and equipment")).ToArray();
        var compound=e.Catalog.Archetypes.Single(a=>a.Id=="site") with{
            Purpose="Source-sized compound with four buildings and shared access ground",Parts=parts,ChildRequirements=[new("building",4,4)],
            Connections=parts.Select((p,i)=>new MetadataConnectionRule("entry-"+i,"compound-access",MetadataConnectionPattern.Hub,"self",p.Id)).ToArray(),
            PhysicalFootprint=new("meters","Exact compound boundary and grounds","coastal:compound",Box(-14,-12,28,24),Box(-15,-13,30,26)),
            Access=new(MetadataOccupancyKind.Reservation,[new("north","Compound north entry","coastal:door",new(0,-12),new(0,-14),1.6),
                new("south","Compound south entry","coastal:door",new(0,12),new(0,14),1.6),
                new("east","Compound east entry","coastal:door",new(14,0),new(16,0),1.6),
                new("west","Compound west entry","coastal:door",new(-14,0),new(-16,0),1.6)])};
        var building=new WorldMetadataArchetype("building","building","Required building with two equipment members",properties,
            [new("equipment","equipment",2,"Required equipment")],[],[new("equipment",2,2)],LayerId:"intent",
            PhysicalFootprint:new("meters","Exact eight by six building","coastal:building",Box(-4,-3,8,6),Box(-5,-4,10,8)),
            Access:new(MetadataOccupancyKind.Solid,[new("north","Building entrance","coastal:building-door",new(0,-3),new(0,-4),1.6)]));
        var equipment=new WorldMetadataArchetype("equipment","equipment","Required equipment member",properties,[],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Exact equipment body","coastal:equipment",Box(-.5,-.5,1,1)),Access:new(MetadataOccupancyKind.Solid,[]));
        var catalog=e.Catalog with{Id="coastal-programs",Archetypes=[..e.Catalog.Archetypes.Where(a=>a.Id is not "site" and not "station"),compound,building,equipment]};
        var bodyRequest=new WorldPlacementRequest("buildings","All four required buildings within each compound","coastal:buildings",null,
            parts.Select(p=>new WorldSiteDemand(p.Id,p.Purpose,"coastal:building",null,profile)).ToArray(),
            positions.Select((p,i)=>new WorldPlacementAnchor("building-"+i,p)).ToArray(),
            parts.Select((p,i)=>new WorldPlacementRelation("building-"+i,p.Id,"building-"+i,"Declared building row","coastal:row",0,0,0,1)).ToArray(),MaximumWork:1000000);
        var equipmentRequest=new WorldPlacementRequest("equipment","Required equipment fits every building","coastal:equipment",null,
            [new("equipment","Both equipment members","coastal:equipment",null,profile)],[],[],MaximumWork:1000000);
        var programs=new[]{new MetadataNestedProgram("compound-buildings","Expand every compound","coastal:program",Id,"site",7,7,bodyRequest,
                parts.Select(p=>new MetadataProgramBinding(p.Id,p.Id)).ToArray()),
            new("building-equipment","Expand equipment in every building","coastal:program",Id,"building",28,28,equipmentRequest,
                [new("equipment","equipment",Population:new("Equipment line","coastal:equipment-line",Path:new(new WorldPolyline([new(-2,0),new(2,0)]),4)))])};
        var route=e.Recipe.RouteRules![0];var foot=route.Profile with{Id="compound-walk",Purpose="Enter every building",SourceRuleId="coastal:walk",Guidance=null};
        var rules=e.Recipe.RouteRules.Select(r=>r with{Interfaces=r.Interfaces?.Select(i=>i with{ConnectionId=Node(i.ConnectionId)}).ToArray(),
            Profile=r.Profile with{Guidance=r.Profile.Guidance?.Select(g=>g with{Boundary=Box(0,0,100,160)}).ToArray()}}).Append(
            new("compound-entry","compound-access",foot,route.Search,"north","north")).ToArray();
        return(e.Recipe with{Id=Id,CatalogId=catalog.Id,GameProfile="framework-coastal-reference",Scale="settlement",Coverage=MetadataCoverage.CompleteDeclaredScope,
            RemainingScopes=[],Surface=surface,Regions=region,LocalLayouts=locals,
            Placement=e.Recipe.Placement with{Request=placement,Bindings=e.Recipe.Placement.Bindings.Select(b=>b with{NodeId=Node(b.NodeId)}).ToArray(),Programs=programs},
            AdditionalConnections=e.Recipe.AdditionalConnections!.Select(c=>c with{FromId=Node(c.FromId),ToId=Node(c.ToId)}).ToArray(),
            RouteRules=rules,NetworkRules=e.Recipe.NetworkRules!.Select(n=>n with{RegionNodeId=Node(n.RegionNodeId),Options=n.Options with{
                Profile=n.Options.Profile with{Guidance=n.Options.Profile.Guidance?.Select(g=>g with{Boundary=Box(0,0,100,160)}).ToArray()}}}).ToArray()},catalog);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve coastal evidence");Directory.CreateDirectory(output);
        var e=Example();var generator=new WorldMetadataGenerator();
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(e.Catalog));
        var world=generator.Generate(e.Recipe,e.Catalog);Validate(world);
        await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"baseline"));
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"baseline")))==Json(world),"complete published source replays");
        var center=world.RoutePlan!.Routes.Single(r=>r.RuleId=="regional-route").Interface!.Center;
        var water=e.Recipe.Surface!.Patches[0] with{Id="crossing-water",Kind="water",Boundary=Box(center.X-1,center.Y-2,2,4),Priority=2,
            Purpose="Block the old interface while preserving all compound anchors",SourceRuleId="coastal:water"};
        var wetRecipe=e.Recipe with{Surface=e.Recipe.Surface with{Patches=[..e.Recipe.Surface.Patches,water]}};
        await File.WriteAllTextAsync(Path.Combine(output,"water-recipe.json"),Json(wetRecipe));
        var wet=generator.Generate(wetRecipe,e.Catalog);Validate(wet);
        Check(wet.RoutePlan!.Routes.Single(r=>r.RuleId=="regional-route").Interface!.Center!=center,"water changes the real required crossing");
        Check(Json(world.Nodes)==Json(wet.Nodes)&&Json(world.AccessPlan!.Sites)==Json(wet.AccessPlan!.Sites),"all source geometry and declared poses survive terrain edit");
        await generator.GenerateFilesAsync(Path.Combine(output,"water-recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"water"));
        var roadlessRecipe=e.Recipe with{RouteRules=null,NetworkRules=null};
        await File.WriteAllTextAsync(Path.Combine(output,"roadless-recipe.json"),Json(roadlessRecipe));
        var roadless=generator.Generate(roadlessRecipe,e.Catalog);
        Check(roadless.Nodes.Count==94&&roadless.AccessPlan!.Sites.Count==91&&roadless.RoutePlan is null&&roadless.NetworkPlan is null&&roadless.CorridorPlan is null,"roadless input preserves all content and infers no surface passages");
        await generator.GenerateFilesAsync(Path.Combine(output,"roadless-recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"roadless"));
        Console.WriteLine("PASS: integrated irregular coastal settlement; 94 nodes / 91 physical sources, nested building/equipment programs, district-local grid and hillside allocations, protected shore, required scoped networks and building access, changed water crossing, complete replay and roadless counterfactual.");
    }
    static void Validate(WorldMetadataDocument world)
    {
        Check(world.Nodes.Count==94&&world.AccessPlan!.Sites.Count==91,"all compounds, buildings and equipment remain");
        Check(world.Nodes.Count(n=>n.Kind=="building")==28&&world.Nodes.Count(n=>n.Kind=="equipment")==56,"exact nested program counts");
        Check(world.PlacementPlan!.ProgramInstances!.Count==35,"every nested program instantiated");
        Check(world.RoutePlan!.Routes.Count(r=>r.RuleId=="compound-entry")==28&&world.RoutePlan.Routes.All(r=>r.Forward.Status==WorldRouteStatus.Found&&r.Reverse?.Status==WorldRouteStatus.Found),"every required building/region route works both ways");
        Check(world.NetworkPlan!.Networks.Count==2&&world.NetworkPlan.Networks.All(n=>n.Plan.Analysis.Components.Count==1),"both scoped destination networks connected");
        Check(world.LocalLayoutPlan!.Allocations.Count==7&&world.LocalLayoutPlan.Definition.Layouts.Count(l=>l.Strategy==WorldLocalLayoutStrategy.OrientedGrid)==1,"one local grid with separate hillside strategy");
        var query=new WorldSurfaceQuery(world.Surface!);
        foreach(var result in world.RoutePlan.Routes.SelectMany(r=>new[]{r.Forward,r.Reverse!}).Concat(world.NetworkPlan.Networks.SelectMany(n=>n.Plan.Edges.SelectMany(e=>new[]{e.Forward,e.Reverse!}))))
            Check(result.Path!.Points.All(p=>query.Sample(p).Status==WorldSurfaceStatus.Sampled),"routes retain the declared world domain");
    }
}

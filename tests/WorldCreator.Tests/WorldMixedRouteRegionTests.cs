using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldMixedRouteRegionTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static string Hash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Json(value))));
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldInterfaceTests.Example(true);var recipe=e.Recipe;var surface=recipe.Surface!;
        var land=surface.Patches[0] with{GradientX=.025,GradientY=.01,Purpose="Continuous sloping ground joining different local layouts",SourceRuleId="mixed-regions:ground"};
        var regionProfile=recipe.Regions!.Profile with{AllowedKinds=["ground","water"]};
        WorldSuitabilityProfile Regional(string id,double min,double max)=>regionProfile with{Id=id,Requirements=[..regionProfile.Requirements,
            new("regional-band","region-band","Explicit neighboring program domains","mixed-regions:domain",min,max)]};
        var regions=recipe.Regions with{Profile=Regional("planned-domain",0,50),Seeds=recipe.Regions.Seeds.Select(s=>s.Id=="east"?s with{Profile=Regional("irregular-domain",50,100)}:s).ToArray()};
        var rule=recipe.RouteRules![0];
        var guidance=new WorldRouteGuidance("planned-region","Local path alignment ends at the regional interface","mixed-regions:alignment",Box(0,0,50,80),[new(1,0),new(0,1)],2);
        var profile=rule.Profile with{Guidance=[guidance]};
        var station=e.Catalog.Archetypes.Single(a=>a.Id=="station") with{
            PhysicalFootprint=new("meters","Unscaled required service station","mixed-regions:station",Box(-.5,-.5,1,1)),
            Access=new(MetadataOccupancyKind.Solid,[])};
        var site=e.Catalog.Archetypes.Single(a=>a.Id=="site");
        site=site with{Access=site.Access! with{Occupancy=MetadataOccupancyKind.Reservation},
            Parts=site.Parts.Select(p=>p with{Layout=null}).ToArray()};
        var contentsProfile=recipe.Placement!.Request.Demands[0].Profile;
        var request=new WorldPlacementRequest("stations","Fit every required station inside its source-sized parent","mixed-regions:stations",null,
            [new("station","Both required physical stations","mixed-regions:station",null,contentsProfile)],[],[],MaximumWork:1000000);
        var program=new MetadataNestedProgram("required-stations","Every local program retains both stations","mixed-regions:program",recipe.Id,"site",7,7,request,
            [new("station","stations",Population:new("Two source-spaced stations","mixed-regions:station-spacing",Path:new(new WorldPolyline([new(-1,0),new(1,0)]),2)))]);
        return(recipe with{Surface=surface with{Patches=[land],Exclusions=surface.Exclusions.Where(x=>x.Id!="river").ToArray()},Regions=regions,
            Environment=recipe.Environment! with{Fields=[..recipe.Environment.Fields,new("region-band","Explicit regional program boundary coordinate","mixed-regions:domain","meters",0,[new(WorldFieldInput.X,1)])]},
            Placement=recipe.Placement with{Programs=[program]},
            LocalLayouts=recipe.LocalLayouts! with{Interfaces=[..recipe.LocalLayouts.Interfaces,new("regional-link","west-b","east-a","Join planned and irregular local programs","mixed-regions:interface",10)]},
            AdditionalConnections=[..recipe.AdditionalConnections!,new("regional","regional-travel",recipe.Id+"/sites-1",recipe.Id+"/sites-4",true)],
            RouteRules=[..recipe.RouteRules.Select(r=>r with{Profile=profile}),rule with{Id="regional-route",ConnectionKind="regional-travel",Profile=profile,
                Interfaces=[new(recipe.Id+"/explicit/regional","regional-link")]}],
            RemainingScopes=["Complete declared seven-program / fourteen-station physical reference across mixed local layouts. Regional intent and catalog are game-authored. This is not an entire settlement or a design-approved game world. Artwork and Godot are deferred."]},
            e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="station"?station:a.Id=="site"?site:a).ToArray()});
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Mixed region evidence exists");Directory.CreateDirectory(output);
        var e=Example();var generator=new WorldMetadataGenerator();
        var seedWorld=generator.Generate(e.Recipe,e.Catalog);
        var placed=seedWorld.PlacementPlan!.Placement.Sites;
        var pins=placed.Select(s=>new WorldPlacementAnchor("hold-"+s.DemandId,s.Position)).ToArray();
        var relations=placed.Select(s=>new WorldPlacementRelation("hold-"+s.DemandId,s.DemandId,"hold-"+s.DemandId,"Preserve every source program pose for routing counterfactuals","mixed-regions:fixed-demand",0,0,0,1)).ToArray();
        var fixedRecipe=e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Anchors=[..e.Recipe.Placement.Request.Anchors,..pins],Relations=[..e.Recipe.Placement.Request.Relations,..relations]}}};
        var guided=generator.Generate(fixedRecipe,e.Catalog);Validate(guided);
        var regional=guided.RoutePlan!.Routes.Single(r=>r.RuleId=="regional-route");var crossing=regional.Interface!;
        Check(crossing.Attempts.Count==1&&crossing.Attempts[0].Accepted,"unblocked regional crossing");
        var water=fixedRecipe.Surface!.Patches[0] with{Id="crossing-water",Kind="water",Priority=1,Boundary=Box(crossing.Center.X-.5,crossing.Center.Y-1,1,2),
            Purpose="Block the prior crossing without changing regional eligibility",SourceRuleId="mixed-regions:barrier"};
        var wetRecipe=fixedRecipe with{Surface=fixedRecipe.Surface with{Patches=[..fixedRecipe.Surface.Patches,water]}};
        string wetInput=Path.Combine(output,"water","input");Directory.CreateDirectory(wetInput);
        await File.WriteAllTextAsync(Path.Combine(wetInput,"recipe.json"),Json(wetRecipe));await File.WriteAllTextAsync(Path.Combine(wetInput,"catalog.json"),Json(e.Catalog));
        var wet=generator.Generate(wetRecipe,e.Catalog);Validate(wet);
        var wetCrossing=wet.RoutePlan!.Routes.Single(r=>r.RuleId=="regional-route").Interface!;
        Check(wetCrossing.Attempts.Count>1&&!wetCrossing.Attempts[0].Accepted&&wetCrossing.Attempts[^1].Accepted,"barrier preserves failed crossing and selects another");
        Check(wetCrossing.Center!=crossing.Center,"terrain changes actual shared crossing");
        try
        {
            generator.Generate(wetRecipe with{RouteRules=wetRecipe.RouteRules!.Select(r=>r.Id=="regional-route"?r with{Search=r.Search with{MaximumWork=50000}}:r).ToArray()},e.Catalog);
            throw new Exception("Required route work budget ignored");
        }
        catch(ArgumentException error)when(error.Message=="WORLD_ROUTE_WORK_BUDGET")
        {
            Check(error.Data["RouteRequestId"] is string id&&id.StartsWith(wetRecipe.Id+"/explicit/regional/",StringComparison.Ordinal),"exhausted approach names its actual route");
            Check((string?)error.Data["InterfaceId"]=="regional-link","exhausted approach names its required interface");
        }
        var freeRecipe=fixedRecipe with{RouteRules=fixedRecipe.RouteRules!.Select(r=>r with{Profile=r.Profile with{Guidance=null}}).ToArray()};
        var free=generator.Generate(freeRecipe,e.Catalog);Validate(free);
        foreach(var world in new[]{wet,free})
        {
            Check(Json(world.Nodes)==Json(guided.Nodes),"all node identities, poses and source facts fixed");
            Check(RegionGeometry(world)==RegionGeometry(guided),"terrain eligibility and mixed allocation boundaries fixed");
            Check(Json(world.LocalLayoutPlan!.Definition)==Json(guided.LocalLayoutPlan!.Definition)&&Json(world.LocalLayoutPlan.Areas)==Json(guided.LocalLayoutPlan.Areas),"authored local strategies and complete area accounting fixed");
            Check(Json(world.AccessPlan!.Sites)==Json(guided.AccessPlan!.Sites)&&Json(world.AccessPlan.Definitions)==Json(guided.AccessPlan.Definitions),"every physical source fixed");
        }
        Check(Json(guided.RoutePlan.Routes.Select(r=>r.Forward.Path))!=Json(free.RoutePlan!.Routes.Select(r=>r.Forward.Path)),"regional guidance changes paths at fixed demand");
        var session=new MetadataGenerationSession(generator,fixedRecipe,e.Catalog);
        var snapshot=session.Snapshot;
        var edited=session.Apply(new("crossing-water","Recompute affected routes","mixed-regions:edit",Hash(snapshot.Recipe),Hash(snapshot.Catalog),wetRecipe,e.Catalog));
        Check(Json(edited.World)==Json(wet),"incremental terrain edit equals clean generation");
        var invalid=wetRecipe with{RouteRules=wetRecipe.RouteRules!.Select(r=>r with{Profile=r.Profile with{Guidance=[]}}).ToArray()};
        try{session.Apply(new("invalid-policy","Reject invalid edit","mixed-regions:invalid",Hash(edited.Recipe),Hash(edited.Catalog),invalid,e.Catalog));throw new Exception("Invalid policy accepted");}
        catch(ArgumentException error)when(error.Message.Contains("WORLD_ROUTE_GUIDANCE",StringComparison.Ordinal)){}
        Check(Json(session.Snapshot)==Json(edited),"invalid edit preserves accepted world and report");
        foreach(var item in new[]{(Id:"guided",Recipe:fixedRecipe,World:guided),(Id:"free",Recipe:freeRecipe,World:free),(Id:"water",Recipe:wetRecipe,World:wet)})
        {
            string input=Path.Combine(output,item.Id,"input");Directory.CreateDirectory(input);
            await File.WriteAllTextAsync(Path.Combine(input,"recipe.json"),Json(item.Recipe));await File.WriteAllTextAsync(Path.Combine(input,"catalog.json"),Json(e.Catalog));
            await generator.GenerateFilesAsync(Path.Combine(input,"recipe.json"),Path.Combine(input,"catalog.json"),Path.Combine(output,item.Id,"world"));
            var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,item.Id,"world","world-metadata.json"));
            Check(Json(saved)==Json(item.World),"exact file replay "+item.Id);
        }
        await session.PublishAsync(Path.Combine(output,"session"));
        Console.WriteLine("PASS: irregular whole boundary, local grid beside irregular allocation, explicit physical regional crossing, all seven parents/fourteen physical children, guided/free/water fixed-pose counterfactuals, failed crossing retention, source-preserving replay, session edit and rollback.");
    }
    private static void Validate(WorldMetadataDocument world)
    {
        Check(world.Nodes.Count==22&&world.AccessPlan!.Sites.Count==21,"complete source physical membership");
        Check(world.PlacementPlan!.ProgramInstances!.Count==7,"every nested program instantiated");
        Check(world.LocalLayoutPlan!.Definition.Layouts.Select(l=>l.Strategy).ToHashSet().SetEquals([WorldLocalLayoutStrategy.OrientedGrid,WorldLocalLayoutStrategy.PowerDiagram]),"mixed allocation strategies");
        Check(world.RoutePlan!.Routes.Count==3&&world.RoutePlan.Routes.All(r=>r.Interface is not null&&r.Reverse is not null),"every required interface realized both ways");
        foreach(var route in world.RoutePlan.Routes)
        foreach(var pair in new[]{(Route:route.Forward,Journey:route.Interface!.Forward),(Route:route.Reverse!,Journey:route.Interface!.Reverse!)})
        {
            var points=pair.Journey.FromLeg.Path!.Points.Concat(pair.Journey.Crossing.Path!.Points.Skip(1)).Concat(pair.Journey.ToLeg.Path!.Points.Skip(1));
            Check(pair.Route.Path!.Points.SequenceEqual(points),"source-vertex exact route join");
            Check(Math.Abs(pair.Route.Cost!.Total-pair.Journey.FromLeg.Cost!.Total-pair.Journey.Crossing.Cost!.Total-pair.Journey.ToLeg.Cost!.Total)<1e-7,"complete joined cost");
        }
        new MetadataRoutePlanner().ValidateDocument(world);new MetadataPlacementPlanner().ValidateDocument(world);
        var accounting=new MetadataPhysicalAccountingPlanner();accounting.Validate(accounting.Measure(world),world);
    }
    private static string RegionGeometry(WorldMetadataDocument world)
    {
        // Terrain patch events may subdivide an unchanged geometric boundary.
        // Compare complete merged oriented-line intervals, never selected sample points.
        object Shape(WorldRegion region)
        {
            var lines=region.BoundaryEdges.Select(edge=>{
                double dx=edge.To.X-edge.From.X,dy=edge.To.Y-edge.From.Y,length=Math.Sqrt(dx*dx+dy*dy);dx/=length;dy/=length;
                if(dx<0||dx==0&&dy<0){dx=-dx;dy=-dy;}
                double a=edge.From.X*dx+edge.From.Y*dy,b=edge.To.X*dx+edge.To.Y*dy;
                return new{Key=$"{Math.Round(dx,8):R}/{Math.Round(dy,8):R}/{Math.Round(edge.From.X*dy-edge.From.Y*dx,8):R}",Min=Math.Min(a,b),Max=Math.Max(a,b)};
            }).GroupBy(e=>e.Key).OrderBy(g=>g.Key,StringComparer.Ordinal).Select(group=>{
                var intervals=new List<double[]>();foreach(var edge in group.OrderBy(e=>e.Min))
                {
                    if(intervals.Count>0&&edge.Min<=intervals[^1][1]+1e-7)intervals[^1][1]=Math.Max(intervals[^1][1],edge.Max);
                    else intervals.Add([edge.Min,edge.Max]);
                }
                return new{group.Key,Intervals=intervals.Select(v=>v.Select(x=>Math.Round(x,7)).ToArray()).ToArray()};
            }).ToArray();
            return new{region.Id,region.Seed,Area=Math.Round(region.Area,7),Lines=lines};
        }
        return Json(new{Regions=world.RegionPlan!.Regions.Select(Shape),Allocations=world.LocalLayoutPlan!.Allocations.Select(a=>new{a.Id,a.LayoutId,a.RegionId,a.Program,Shape=Shape(a.Shape)})});
    }
    private static void Check(bool value,string message){if(!value)throw new Exception("Mixed regions: "+message);}
}

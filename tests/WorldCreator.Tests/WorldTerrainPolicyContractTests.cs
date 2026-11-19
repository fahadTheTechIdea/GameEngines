using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldTerrainPolicyContractTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Terrain contract: "+message);}
    private static void Near(double actual,double expected,string message)=>Check(Math.Abs(actual-expected)<1e-8,message+": "+actual+" vs "+expected);
    private static WorldEnvironmentDefinition Fields()=>new(1,"terrain-fields",[
        new("height","Ground elevation","terrain:height","meters",0,[new(WorldFieldInput.Elevation,1)]),
        new("slope","Ground steepness","terrain:slope","rise-per-run",0,[new(WorldFieldInput.Slope,1)]),
        new("depth","Depth below authored level","terrain:depth","meters",2,[new(WorldFieldInput.Field,-1,"height")],Minimum:0),
        new("reward","Perfect soft preference","terrain:reward","score",10,[])]);
    private static WorldSuitabilityProfile Profile()=>new("usable","Dry gentle gameplay region","terrain:usable",["land","water"],
        [new("dry","depth","No standing water","terrain:dry",0,0),new("gentle","slope","Maximum source slope","terrain:gentle",0,.3)],
        [new("perfect","reward","A preference never buys hard eligibility","terrain:perfect",10,1,1000000)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool water,bool steep)
    {
        var boundary=Box(0,0,20,12);var ground=new WorldTerrainPatch("ground","land","Authored level ground","terrain:ground",boundary,0,new(0,0),3,0,0);
        var patches=new List<WorldTerrainPatch>{ground};
        if(water)patches.Add(new("wet","water","Authored water counterfactual","terrain:water",Box(0,0,6,12),1,new(0,0),0,0,0));
        if(steep)patches.Add(new("steep","land","Authored slope counterfactual","terrain:steep",Box(12,0,8,12),1,new(12,0),3,.6,0));
        var surface=new WorldSurfaceDefinition(1,"policy-land","meters",boundary,[],patches);
        var profile=Profile();var properties=new Dictionary<string,JsonElement>();
        var catalog=new WorldMetadataCatalog(1,"terrain-policy-catalog",[
            new("root","world","Complete terrain policy declaration",properties,[new("sites","site",1,"Required gameplay site")],[],[new("site",1,1)],LayerId:"intent"),
            new("site","site","Source-sized solid destination",properties,[],[],[],LayerId:"intent",
                PhysicalFootprint:new("meters","Unchanged two-metre source","terrain:site",Box(-1,-1,2,2)),
                Access:new(MetadataOccupancyKind.Solid,[new("south","Required entrance","terrain:entrance",new(0,-1),new(0,-2),.8)]))]);
        var request=new WorldPlacementRequest("site","Place source at generated region anchor","terrain:placement",boundary,
            [new("site","Required source","terrain:site",null,profile,RegionId:"play")],[],[new("pin","site","region-anchor","Bind generated intent","terrain:pin",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed);
        var regions=new WorldRegionRequest("regions","Generate dry gentle region","terrain:regions",boundary,profile,[],
            Intents:[new("play","Required source region","terrain:play",4,2)]);
        var walk=new WorldTraversalProfile("walk","Required source access","terrain:walk",.2,.3,new Dictionary<string,double>{{"land",1}},1,.5);
        return(new(1,"terrain-policy-world","framework-reference",catalog.Id,"root","world",MetadataCoverage.CompleteDeclaredScope,[],new Dictionary<string,int>(),new(["x","y"],"meters"),[9,2],true,
            AdditionalConnections:[new("access","travel","terrain-policy-world","terrain-policy-world/sites-0",true)],
            Layers:[new("intent","program","Complete declared source and travel","terrain:intent",[],MetadataCoverage.CompleteDeclaredScope,[])],Surface:surface,
            RouteRules:[new("access","travel",walk,new(.1,[]),ToPortId:"south")],Environment:Fields(),
            SuitabilityRules:[new("site-policy","terrain-policy-world","site",1,1,profile)],Regions:regions,
            Placement:new(request,[new("site","terrain-policy-world/sites-0",true)],RegionAnchors:[new("region-anchor","play","Use actual generated region","terrain:anchor")])),catalog);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var statusFacts=new List<object>();var domain=Box(-10,-10,20,20);var patch=new WorldTerrainPatch("zero","land","Genuine zero datum","terrain:zero",domain,0,new(0,0),0,0,0);
        var basic=new WorldSurfaceDefinition(1,"status-surface","meters",domain,[],[patch]);
        foreach(var (name,surface,point,status) in new[]{
            ("zero",basic,new WorldPoint(-3,-4),WorldSurfaceStatus.Sampled),
            ("outside",basic,new WorldPoint(11,0),WorldSurfaceStatus.OutsideDomain),
            ("excluded",basic with{Exclusions=[new("hole","Protected hole","terrain:hole",Box(-1,-1,2,2))]},new WorldPoint(0,0),WorldSurfaceStatus.Excluded),
            ("uncovered",basic with{Patches=[patch with{Boundary=Box(-10,-10,10,20)}]},new WorldPoint(5,5),WorldSurfaceStatus.Uncovered),
            ("ambiguous",basic with{Patches=[patch,patch with{Id="same-priority"}]},new WorldPoint(5,5),WorldSurfaceStatus.Ambiguous)})
        {
            var query=new WorldSurfaceQuery(surface);var sample=query.Sample(point);Check(sample.Status==status,"exact status "+name);
            var environment=new WorldEnvironmentQuery(query,Fields());var loose=Profile() with{Requirements=[],AllowedKinds=["land"]};var result=new WorldPointSuitability(environment,loose).Evaluate(point);
            if(status==WorldSurfaceStatus.Sampled){Near(sample.Elevation!.Value,0,"zero remains available");Check(result.Eligible&&result.Score==1,"sampled datum retains perfect preference");}
            else Check(sample.Elevation is null&&sample.GradientX is null&&sample.GradientY is null&&result.Observation.Values.Count==0&&!result.Eligible&&result.Score is null&&result.Decisions.All(d=>d.Value is null),"unavailable never becomes zero/buildable");
            statusFacts.Add(new{Name=name,Surface=surface,Point=point,Sample=sample,Result=result});
        }
        var analytic=new WorldSurfaceQuery(basic with{Patches=[patch with{Origin=new(-3,-5),Elevation=2,GradientX=.2,GradientY=-.1}]}).Sample(new(1,-1));
        Near(analytic.Elevation!.Value,2.4,"negative-origin affine height");Near(analytic.Slope!.Value,Math.Sqrt(.05),"affine slope");
        var field=new WorldHeightField(1,"cell","land","Non-square sampled cell","terrain:cell","meters",new(-4,-6),2,3,2,2,[0,2,3,8]);
        var fieldQuery=new WorldSurfaceQuery(basic with{HeightField=field,Patches=[]});
        var lower=fieldQuery.Sample(new(-3,-5.25));var upper=fieldQuery.Sample(new(-3.5,-3.75));
        Near(lower.Elevation!.Value,2.5,"lower triangle interpolation");Near(upper.Elevation!.Value,3.5,"upper triangle interpolation");
        Near(lower.GradientX!.Value,1,"lower dx");Near(lower.GradientY!.Value,2,"lower dy");Near(upper.GradientX!.Value,2.5,"upper dx");Near(upper.GradientY!.Value,1,"upper dy");
        Check(fieldQuery.Sample(new(0,0)).Status==WorldSurfaceStatus.Uncovered,"outside sampled support remains uncovered inside domain");
        try{_ =new WorldSurfaceQuery(basic with{Patches=[],HeightField=field with{Unit="feet"}});throw new Exception("Mixed surface/field units accepted");}
        catch(ArgumentException error)when(error.Message=="WORLD_HEIGHTFIELD_SURFACE_MISMATCH"){}
        await File.WriteAllTextAsync(Path.Combine(output,"sampling.json"),Json(new{Statuses=statusFacts,Affine=analytic,Field=field,Lower=lower,Upper=upper,UnitMismatchRefused=true}));
        var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();var observations=new List<object>();
        foreach(var (name,water,steep,area) in new[]{("open",false,false,240d),("water",true,false,168d),("slope",false,true,144d),("both",true,true,72d)})
        {
            var e=Example(water,steep);var world=generator.Generate(e.Recipe,e.Catalog);worlds.Add(world);
            Near(world.RegionPlan!.SuitableArea,area,"actual eligible area "+name);Near(world.RegionPlan.Regions.Single().Area,area,"complete selected region "+name);
            Check(world.Nodes.Count==2&&world.AccessPlan!.Sites.Count==1,"complete fixed content "+name);
            var site=world.AccessPlan!.Sites[0];Near(site.Footprint.Vertices.Max(p=>p.X)-site.Footprint.Vertices.Min(p=>p.X),2,"unchanged width");Near(site.Footprint.Vertices.Max(p=>p.Y)-site.Footprint.Vertices.Min(p=>p.Y),2,"unchanged height");
            Check(world.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&world.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"both required physical directions "+name);
            var env=new WorldEnvironmentQuery(new(e.Recipe.Surface!),Fields());var policy=new WorldPointSuitability(env,Profile());
            foreach(var point in new[]{new WorldPoint(3,6),new WorldPoint(16,6)})
            {
                var result=policy.Evaluate(point);bool expected=!(water&&point.X==3||steep&&point.X==16);
                Check(result.Eligible==expected&&result.Decisions.Single(d=>d.RuleId=="perfect").Score==1,"perfect preference cannot override hard field");
                if(!expected)Check(result.Score is null,"hard rejection has no accepted score");observations.Add(new{Case=name,Point=point,Result=result});
            }
            string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            Check(Json(await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"world")))==Json(world),"public file generation equals SDK "+name);
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"world/world-metadata.json")))==Json(world),"full persisted source replay "+name);
        }
        Check(worlds.Select(w=>Json(w.Nodes.Single(n=>n.ParentId is not null).Position)).Distinct().Count()==4,"water and slope actually move generated destinations");
        await File.WriteAllTextAsync(Path.Combine(output,"policy-observations.json"),Json(observations));
        var start=Example(false,false);var session=new MetadataGenerationSession(generator,start.Recipe,start.Catalog);var before=session.Snapshot;var target=Example(true,true);
        var edited=session.Apply(new("terrain-edit","Water/slope change affects generated region and destination","terrain:edit",before.World.RecipeSha256,before.World.CatalogSha256,target.Recipe,target.Catalog));
        Check(Json(edited.World)==Json(worlds[^1]),"source edit equals clean physical generation");
        var flooded=target.Recipe with{Surface=target.Recipe.Surface! with{Patches=[new("flooded","water","No dry region remains","terrain:flooded",Box(0,0,20,12),0,new(0,0),0,0,0)]}};
        try{session.Apply(new("flooded","Required dry source must refuse","terrain:flooded",edited.World.RecipeSha256,edited.World.CatalogSha256,flooded,target.Catalog));throw new Exception("Flooded edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(edited),"impossible terrain edit preserves whole accepted state");
        await session.PublishAsync(Path.Combine(output,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"session/generation-session.json"));Check(Json(restored.Snapshot)==Json(edited),"terrain history fully replays");
        await File.WriteAllTextAsync(Path.Combine(output,"flooded-recipe.json"),Json(flooded));
        Console.WriteLine("PASS: five explicit sample statuses, zero/negative-origin/triangle equations, four physical terrain counterfactuals with source size and required bidirectional travel, hard/soft provenance, actual anchor changes, source edit/replay and refusal preservation.");
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldAdaptiveLayoutGateTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    static void Check(bool value,string reason){if(!value)throw new Exception("Adaptive layout gate: "+reason);}
    static double Area(WorldPolygon polygon)=>Math.Abs(polygon.Vertices.Select((p,i)=>p.X*polygon.Vertices[(i+1)%polygon.Vertices.Count].Y-p.Y*polygon.Vertices[(i+1)%polygon.Vertices.Count].X).Sum())/2;
    static void Near(double actual,double expected,string reason)=>Check(Math.Abs(actual-expected)<1e-7,reason+": "+actual+" / "+expected);
    static void PresentationBoundary<T>(T input,string name)
    {
        var raw=JsonSerializer.SerializeToNode(input,WorldJson.CreateOptions())!.AsObject();raw[name]=16;
        try{JsonSerializer.Deserialize<T>(raw.ToJsonString(),WorldJson.CreateOptions());}
        catch(JsonException error)when(error.Message.Contains(name,StringComparison.Ordinal)){return;}
        throw new Exception("Unsupported presentation field was silently accepted: "+name);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve layout gate evidence");Directory.CreateDirectory(output);
        var source=WorldCoastalMixedWorldTests.Example();
        PresentationBoundary(source.Recipe,"RenderTilePixels");PresentationBoundary(source.Catalog,"RenderTilePixels");
        var generator=new WorldMetadataGenerator();var observations=new List<object>();WorldMetadataDocument? baseline=null;
        var cliff=new WorldPolygon([new(150,50),new(166,58),new(151,64)]);
        var coast=new WorldPolygon([new(158,88),new(180,82),new(184,99),new(160,105)]);
        var hole=Box(48,72,8,8);var terrain=source.Recipe.Surface!;
        var variants=new (string Id,WorldMetadataRecipe Recipe,double LostArea)[]{
            ("baseline",source.Recipe,0),
            ("protected-hole",source.Recipe with{Surface=terrain with{Exclusions=[..terrain.Exclusions,new("ravine","Protected landscape void","layout-gate:ravine",hole)]}},Area(hole)),
            ("steep-cliff",source.Recipe with{Surface=terrain with{Patches=[..terrain.Patches,terrain.Patches[0] with{Id="cliff",Purpose="High protected cliff excludes local construction",SourceRuleId="layout-gate:cliff",Boundary=cliff,Priority=4,Elevation=100,GradientX=0,GradientY=0}]}},Area(cliff)),
            ("lagoon-coast",source.Recipe with{Surface=terrain with{Exclusions=[..terrain.Exclusions,new("lagoon","Protected lagoon edge changes the eastern region","layout-gate:lagoon",coast)]}},Area(coast))
        };
        foreach(var variant in variants)
        {
            var world=await Publish(variant.Id,variant.Recipe,source.Catalog);Validate(world);
            if(baseline is null)baseline=world;
            else
            {
                Check(Json(world.Nodes)==Json(baseline.Nodes)&&Json(world.AccessPlan!.Sites)==Json(baseline.AccessPlan!.Sites),variant.Id+" keeps every declared source pose and size");
                Check(Json(world.LocalLayoutPlan!.Allocations)!=Json(baseline.LocalLayoutPlan!.Allocations),variant.Id+" changes the real allocation geometry");
            }
            Near(world.RegionPlan!.SuitableArea,baseline.RegionPlan!.SuitableArea-variant.LostArea,variant.Id+" exact lost suitable area");
            Near(world.LocalLayoutPlan!.Areas.Sum(a=>a.RegionArea),world.RegionPlan.Regions.Sum(r=>r.Area),variant.Id+" original domain denominator");
            foreach(var a in world.LocalLayoutPlan.Areas)Near(a.RegionArea,a.AllocatedArea+a.ResidualArea,variant.Id+" allocation plus honest residual");
            observations.Add(new{Case=variant.Id,Nodes=world.Nodes.Count,Sources=world.AccessPlan!.Sites.Count,LostArea=variant.LostArea,
                world.RegionPlan.SuitableArea,AllocationAreas=world.LocalLayoutPlan.Areas,WorldSha256=MetadataExecutionCache.Hash(world)});
            await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(observations));
        }
        var names=new Dictionary<string,string>{{"site","resource-field"},{"building","resource-unit"},{"equipment","interaction-point"}};
        string Kind(string value)=>names.GetValueOrDefault(value,value);
        var resources=source.Catalog with{Archetypes=source.Catalog.Archetypes.Select(a=>a with{Kind=Kind(a.Kind),
            ChildRequirements=a.ChildRequirements.Select(r=>r with{Kind=Kind(r.Kind)}).ToArray()}).ToArray()};
        var resourceRecipe=source.Recipe with{GameProfile="framework-resource-program",RouteRules=null,NetworkRules=null,Corridors=null};
        var resourceWorld=await Publish("roadless-resources",resourceRecipe,resources);
        Check(resourceWorld.Nodes.Count==94&&resourceWorld.Nodes.Count(n=>n.Kind=="resource-unit")==28&&resourceWorld.Nodes.Count(n=>n.Kind=="interaction-point")==56,"non-building program keeps all required resource/interaction members");
        Check(resourceWorld.RoutePlan is null&&resourceWorld.NetworkPlan is null&&resourceWorld.CorridorPlan is null,"roadless allocation emits no roads");
        Check(Json(resourceWorld.AccessPlan!.Sites)==Json(baseline!.AccessPlan!.Sites)&&Json(resourceWorld.LocalLayoutPlan)==Json(baseline.LocalLayoutPlan),"semantic game content and omitted routes do not shrink allocation denominators or source geometry");
        await OwningRepair(generator,output);
        var before=Json(source);Check(before==Json(WorldCoastalMixedWorldTests.Example()),"source declarations stay unchanged");
        Console.WriteLine("PASS: original layout gate matrix; four exact-demand terrain controls retain 94 nodes / 91 sources / full journeys, exact area loss and residuals; roadless resource/interaction composition; one owning repair with unrelated geometry preserved; input presentation-field refusal, publication and source replay.");
        async Task<WorldMetadataDocument> Publish(string id,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog)
        {
            string folder=Path.Combine(output,id);Directory.CreateDirectory(folder);string recipeFile=Path.Combine(folder,"recipe.json"),catalogFile=Path.Combine(folder,"catalog.json");
            await File.WriteAllTextAsync(recipeFile,Json(recipe));await File.WriteAllTextAsync(catalogFile,Json(catalog));
            var world=await generator.GenerateFilesAsync(recipeFile,catalogFile,Path.Combine(folder,"world"));
            Check(Json(world)==Json(await generator.ReadPublishedAsync(Path.Combine(folder,"world"))),id+" complete saved source replay");return world;
        }
    }
    static void Validate(WorldMetadataDocument world)
    {
        Check(world.Nodes.Count==94&&world.AccessPlan!.Sites.Count==91&&world.PlacementPlan!.ProgramInstances!.Count==35,"complete nested source program");
        Check(world.Nodes.Count(n=>n.Kind=="building")==28&&world.Nodes.Count(n=>n.Kind=="equipment")==56,"unchanged nested demand");
        Check(world.LocalLayoutPlan!.Allocations.Count==7&&world.LocalLayoutPlan.Definition.Layouts.Count(l=>l.Strategy==WorldLocalLayoutStrategy.OrientedGrid)==1,"one scoped grid and separate irregular allocation");
        Check(world.RoutePlan!.Routes.Count==31&&world.NetworkPlan!.Networks.Sum(n=>n.Plan.Edges.Count)==6,"all required connections retained");
        Check(world.RoutePlan.Routes.All(r=>r.Forward.Status==WorldRouteStatus.Found&&r.Reverse?.Status==WorldRouteStatus.Found),"all authored journeys qualified both ways");
        foreach(var a in world.LocalLayoutPlan.Allocations)Near(a.Shape.Cells.Sum(Area),a.Shape.Area,"raw cell area equals allocation area");
    }
    static async Task OwningRepair(WorldMetadataGenerator generator,string output)
    {
        var e=WorldJointLayoutTests.Example();var layouts=e.Recipe.LocalLayouts!;
        var fixedEast=layouts.Layouts[1] with{Grid=layouts.Layouts[1].Grid! with{CellWidth=22}};
        var recipe=e.Recipe with{LocalLayouts=layouts with{Layouts=[layouts.Layouts[0],fixedEast]},LayoutAdaptation=e.Recipe.LayoutAdaptation! with{AdditionalScopes=null}};
        var world=generator.Generate(recipe,e.Catalog);
        Check(world.LayoutAdaptationPlan!.Attempts.Count==2&&!world.LayoutAdaptationPlan.Attempts[0].Accepted&&world.LayoutAdaptationPlan.Attempts[1].Accepted,"only owning west scope needs the declared repair");
        Check(Json(world.LocalLayoutPlan!.Definition.Layouts.Single(l=>l.Id==fixedEast.Id))==Json(fixedEast),"unrelated eastern strategy definition is untouched");
        var preRepair=new MetadataLocalLayoutPlanner().Plan(recipe.LocalLayouts,world.RegionPlan!,recipe.Environment!,world.Surface!,recipe.Space);
        Check(Json(preRepair.Allocations.Where(a=>a.RegionId=="east"))==Json(world.LocalLayoutPlan.Allocations.Where(a=>a.RegionId=="east")),"unrelated eastern geometry is untouched");
        var protect=new MetadataPositionProtection("east-origin","local-layouts/sites-2","Keep an unrelated authored origin","layout-gate:pin");
        var session=new MetadataGenerationSession(generator,recipe,e.Catalog,protectedPositions:[protect]);var accepted=session.Snapshot;
        var moved=recipe with{Placement=recipe.Placement! with{Request=recipe.Placement.Request with{
            Anchors=recipe.Placement.Request.Anchors.Select(a=>a.Id=="c"?a with{Position=a.Position.Offset(-1,0)}:a).ToArray()}}};
        Check(Json(generator.Generate(moved,e.Catalog).Nodes.Single(n=>n.Id==protect.NodeId).Position)!=Json(accepted.World.Nodes.Single(n=>n.Id==protect.NodeId).Position),"unprotected anchor edit really changes the physical source");
        try{session.Apply(new("move-protected","Refuse moving a protected layout origin","layout-gate:conflict",accepted.World.RecipeSha256,accepted.World.CatalogSha256,moved,e.Catalog));throw new Exception("Protected origin moved");}
        catch(MetadataRegenerationException error)when(error.ReasonCode.StartsWith("METADATA_POSITION_PROTECTION_CONFLICT:east-origin:",StringComparison.Ordinal)){}
        Check(Json(session.Snapshot)==Json(accepted),"protected edit refusal leaves accepted world unchanged");
        string folder=Path.Combine(output,"owning-repair");Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        await session.PublishAsync(Path.Combine(folder,"world"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"world/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(session.Snapshot),"owning repair and protected positions replay exactly");
    }
}

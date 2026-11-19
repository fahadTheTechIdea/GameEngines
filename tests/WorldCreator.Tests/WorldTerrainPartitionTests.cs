using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;
using System.Text.Json;
internal static class WorldTerrainPartitionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Interior anchors: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6,message);
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Missing "+code);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool weighted=false)
    {
        var e=WorldProgramSpaceTests.Example(weighted);
        return(e.Recipe with{LocalLayouts=e.Recipe.LocalLayouts! with{Layouts=e.Recipe.LocalLayouts!.Layouts.Select(l=>l with{AreaPattern=l.AreaPattern! with{Interior=weighted?new(.05,MinimumClearance:8):null}}).ToArray()},Placement=e.Recipe.Placement! with{AllocationAnchors=e.Recipe.Placement!.AllocationAnchors!.Select(a=>a with{Interior=new(.05,MinimumClearance:8)}).ToArray()}},e.Catalog);
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) SteepSource()
    {
        var e=WorldLayeredLandformTests.Example("ridge");var recipe=e.Recipe.PopulationSource!.Recipe;
        var policy=new MetadataLayoutAdaptation("districts","Reconsider complete required spaces around protected terrain","interior:orientation",
            [new("orientation",MetadataLayoutDialKind.AreaRotation,30,-1,1)],27,200000000,AdditionalScopes:recipe.LocalLayouts!.Layouts.Where(l=>l.Id!="districts").Select(l=>
                new MetadataLayoutAdaptationScope(l.Id,"Orient required activity spaces around protected terrain","interior:orientation",[new("orientation",MetadataLayoutDialKind.AreaRotation,30,-1,1)])).ToArray());
        return(recipe with{Surface=recipe.Surface! with{Terrain=recipe.Surface!.Terrain! with{Operations=recipe.Surface.Terrain.Operations.Select(o=>o with{Height=1.8}).ToArray()}},
            LayoutAdaptation=policy,Placement=recipe.Placement! with{AllocationAnchors=recipe.Placement!.AllocationAnchors!.Select(a=>a with{Interior=new(.1)}).ToArray()}},e.Recipe.PopulationSource.Catalog);
    }
    private static double Clearance(WorldPoint p,IReadOnlyList<WorldRegionEdge> edges)
    {
        return edges.Min(e=>{double dx=e.To.X-e.From.X,dy=e.To.Y-e.From.Y,t=Math.Clamp(((p.X-e.From.X)*dx+(p.Y-e.From.Y)*dy)/(dx*dx+dy*dy),0,1);
            return p.DistanceTo(new(e.From.X+t*dx,e.From.Y+t*dy));});
    }
    private static WorldAreaPopulationPlan AreaGeometry()
    {
        var boundary=Box(0,0,12,10);var hole=Box(4,3,4,4);
        var surface=new WorldSurfaceQuery(new(1,"land","meters",boundary,[new("void","Protected void","interior:void",hole)],
            [new("ground","ground","Purposeful support","interior:ground",boundary,0,new(0,0),0,0,0)]));
        var environment=new WorldEnvironmentQuery(surface,new(1,"fields",[new("density","Uniform declared demand","interior:density","relative",1,[])]));
        var profile=new WorldSuitabilityProfile("usable","Required support","interior:support",["ground"],[],[]);
        var regions=new WorldRegionPlanner().Plan(environment,new("support","Required support","interior:support",boundary,profile,[new("region","Required support","interior:region",new(1,1),1)]));
        var domain=new WorldAreaPopulationDomain(regions,"region");var pattern=new WorldAreaPopulationPattern(Interior:new(.01,MinimumClearance:2));
        Reject(()=>WorldPopulationGeometry.Area(domain,environment,new(),1),"WORLD_AREA_CENTROID_OUTSIDE:");
        var plan=WorldPopulationGeometry.Area(domain,environment,pattern,1);var stratum=plan.Strata.Single();double optimum=7-Math.Sqrt(24);
        Near(plan.Area,104,"whole holed support area");Near(plan.Mass,104,"whole support mass");Near(stratum.MassCentroid!.Value.X,6,"independent mass centroid X");Near(stratum.MassCentroid!.Value.Y,5,"independent mass centroid Y");
        Check(hole.ContainsPoint(stratum.MassCentroid!.Value)&&!hole.ContainsPoint(stratum.Position),"preserve true mass centroid separately from valid representative");
        Near(Clearance(stratum.Position,stratum.BoundaryEdges),stratum.Interior!.Clearance,"independent segment-distance clearance");
        Check(stratum.Interior.Clearance<=optimum+1e-8&&optimum-stratum.Interior.Clearance<=.01&&stratum.Interior.UpperBound>=optimum-1e-8,"analytic largest inscribed-circle bound");
        Check(stratum.Interior.UpperBound-stratum.Interior.Clearance<=.01,"declared precision");
        Reject(()=>WorldPopulationGeometry.Area(domain,environment,pattern with{Interior=new(.01,MinimumClearance:2.2)},1),"WORLD_AREA_INTERIOR_CLEARANCE_UNSATISFIED:");
        foreach(var bad in new[]{new WorldRegionInteriorSearch(0),new(.01,0),new(.01,MinimumClearance:-1)})
            Reject(()=>WorldPopulationGeometry.Area(domain,environment,pattern with{Interior=bad},1),"WORLD_REGION_INTERIOR_SEARCH_INVALID");
        Reject(()=>WorldPopulationGeometry.Area(domain,environment,pattern with{Interior=new(.01,1)},1),"WORLD_REGION_ANCHOR_CELL_BUDGET");
        Reject(()=>WorldPopulationGeometry.Area(domain,environment,pattern,1,1),"WORLD_ROUTE_WORK_BUDGET");
        using(var c=new CancellationTokenSource()){c.Cancel();try{WorldPopulationGeometry.Area(domain,environment,pattern,1,token:c.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}}
        Console.WriteLine($"Qualified holed support: area/mass104, true centroid(6,5), optimal clearance={optimum:R}, observed={stratum.Interior.Clearance:R}, upper={stratum.Interior.UpperBound:R}.");
        return plan;
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(string name in new[]{"uniform","weighted","steep-source"})
        {
            var e=name=="steep-source"?SteepSource():Example(name=="weighted");string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        }
        await File.WriteAllTextAsync(Path.Combine(directory,"holed-area-plan.json"),Json(AreaGeometry()));
    }
    public static async Task RunAsync(bool steep=false)
    {
        AreaGeometry();var generator=new WorldMetadataGenerator();
        foreach(bool weighted in new[]{false,true})
        {
            var e=Example(weighted);var world=generator.Generate(e.Recipe,e.Catalog);var placement=world.PlacementPlan!;
            Check(world.Nodes.Count==7&&world.AccessPlan!.Sites.Count==2&&world.RoutePlan!.Routes.Count==1,"all source content and route retained");
            Check(placement.AllocationAnchorResults!.Count==2,"all declared interior witnesses retained");
            foreach(var result in placement.AllocationAnchorResults)
            {
                var allocation=world.LocalLayoutPlan!.Allocations.Single(a=>a.Id==result.AllocationId);
                Near(result.Interior.Clearance,Clearance(result.Interior.Position,allocation.Shape.BoundaryEdges),"independent allocation clearance");
                Check(result.Interior.Clearance>=8&&result.Interior.UpperBound-result.Interior.Clearance<=.05,"bounded source clearance");
                var anchor=placement.Definition.Request.Anchors.Single(a=>a.Id==result.AnchorId);Check(anchor.Position==result.Interior.Position,"effective anchor equals declared proof");
            }
            foreach(var site in placement.Placement.Sites){Near(Math.Abs(site.Footprint.Footprint.SignedArea()),84,"exact source footprint");Near(Math.Abs(site.Reservation!.Footprint.SignedArea()),192,"exact source reservation");}
            WorldMetadataGenerator.ValidateDocument(world);
            var tampered=world with{PlacementPlan=placement with{AllocationAnchorResults=placement.AllocationAnchorResults.Select((a,i)=>i==0?a with{Interior=a.Interior with{Clearance=a.Interior.Clearance+1}}:a).ToArray()}};
            Reject(()=>WorldMetadataGenerator.ValidateDocument(tampered),"METADATA_PLACEMENT_PLAN_MISMATCH");
        }
        var input=Example();var policy=input.Recipe.Placement!;
        WorldMetadataRecipe With(WorldRegionInteriorSearch search)=>input.Recipe with{Placement=policy with{AllocationAnchors=policy.AllocationAnchors!.Select(a=>a with{Interior=search}).ToArray()}};
        Reject(()=>generator.Generate(With(new(.05,MinimumClearance:100)),input.Catalog),"METADATA_ALLOCATION_ANCHOR_CLEARANCE_UNSATISFIED:");
        Reject(()=>generator.Generate(With(new(0)),input.Catalog),"WORLD_REGION_INTERIOR_SEARCH_INVALID");
        Reject(()=>generator.Generate(With(new(.001,1)),input.Catalog),"WORLD_REGION_ANCHOR_CELL_BUDGET");
        var exhausted=WorldAreaAdaptationTests.Example();var failedPolicy=exhausted.Recipe.LayoutAdaptation!;
        try{generator.Generate(exhausted.Recipe with{LayoutAdaptation=failedPolicy with{Dials=[failedPolicy.Dials[0] with{Step=1}]}},exhausted.Catalog);throw new Exception("Missing layout refusal");}
        catch(ArgumentException error)when(error.Message.StartsWith("METADATA_LAYOUT_ADAPTATION_UNRESOLVED",StringComparison.Ordinal))
        {
            Check(error.Data["LayoutAttempts"] is IReadOnlyList<MetadataLayoutAdaptationAttempt> attempts&&attempts.Count==2&&attempts.All(a=>!a.Accepted&&a.Rejection is not null),"unresolved failure exposes every bounded rejected setting");
            Check(error.Data["LayoutWork"] is long used&&used>0,"unresolved failure preserves consumed work");
        }
        var session=new MetadataGenerationSession(generator,input.Recipe,input.Catalog);var before=session.Snapshot;var changed=Example(true);
        var accepted=session.Apply(new("density","Change required-space density with declared interior anchors","interior:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed.Recipe,changed.Catalog));
        Check(Json(accepted.World)==Json(generator.Generate(changed.Recipe,changed.Catalog)),"edit equals clean generation");
        try{session.Apply(new("impossible","Retain source geometry and exact clearance","interior:rollback",accepted.World.RecipeSha256,accepted.World.CatalogSha256,With(new(.05,MinimumClearance:100)),input.Catalog));throw new Exception("Missing refusal");}catch(MetadataRegenerationException){}
        Check(Json(accepted)==Json(session.Snapshot),"failed edit preserves accepted world");
        string folder=Path.Combine(Path.GetTempPath(),"interior-anchor-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(folder);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"generation-session.json"));Check(Json(accepted)==Json(restored.Snapshot),"full checkpoint source history replay");
        if(steep)
        {
            var e=SteepSource();var world=generator.Generate(e.Recipe,e.Catalog);
            Check(world.Nodes.Count==43&&world.AccessPlan!.Sites.Count==42&&world.RoutePlan!.Routes.Count==35&&world.NetworkPlan!.Networks[0].Plan.Edges.Count==6,"complete steep source program");
            Check(world.PlacementPlan!.AllocationAnchorResults!.Count==7&&world.LayoutAdaptationPlan!.Attempts.Any(a=>!a.Accepted),"all generated physical anchors and rejected history");
            Near(world.RegionPlan!.SuitableArea,25650,"independent suitable terrain area");WorldMetadataGenerator.ValidateDocument(world);
            Console.WriteLine($"Qualified steep source:43nodes/42physical/82directed paths; attempts={world.LayoutAdaptationPlan!.Attempts.Count}, layout work={world.LayoutAdaptationPlan!.Work}.");
        }
        Console.WriteLine("PASS: bounded allocation and area interior anchors preserve true mass centroids, complete source geometry, clearance proofs, finite refusals, replay/tamper and session edit/rollback/checkpoint.");
    }
}

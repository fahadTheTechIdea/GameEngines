using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldSourceDomainTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool v,string m){if(!v)throw new Exception("Source domain: "+m);}
    private static WorldPolygon Box(double x,double y)=>new([new(-x,-y),new(x,-y),new(x,y),new(-x,y)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(int sites=2,int plants=4,double rotation=30,bool alignSource=true)
    {
        var e=WorldGenericSurroundingsTests.Example();var root=e.Catalog.Archetypes.Single(a=>a.Id==e.Recipe.RootArchetypeId);
        var sourceCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id==root.Id?a with{
            Parts=[a.Parts[0] with{Count=1,CountParameter="site-count"}],ChildRequirements=[new("site",1,4)],
            Connections=[new("access","source-travel",MetadataConnectionPattern.Chain,"sites")],RequireWeaklyConnectedChildren=true}:a).ToArray()};
        var profile=e.Recipe.Placement!.Request.Demands[0].Profile;
        var cover=WorldGroundCoverTests.Definition();cover=cover with{Materials=[..cover.Materials,new("work-floor","work-floor","Required source work area","domain:work")],Rules=cover.Rules.Select(r=>r.Id=="work"?r with{MaterialId="work-floor"}:r).ToArray()};
        var walk=new WorldTraversalProfile("walk","Required source journeys","domain:journey",.2,.5,new Dictionary<string,double>{{"ground",1}},1,1,1);
        double angle=(alignSource?rotation:0)*Math.PI/180;
        var sourceSequence=new WorldPolyline([new(50-20*Math.Cos(angle),50-20*Math.Sin(angle)),new(50+20*Math.Cos(angle),50+20*Math.Sin(angle))]);
        var sourceRecipe=e.Recipe with{Parameters=new Dictionary<string,int>{{"site-count",sites}},GroundCover=cover,
            Placement=e.Recipe.Placement with{Request=e.Recipe.Placement.Request with{Anchors=[],Relations=[]},Bindings=[new("a",e.Recipe.Id,true,rotation,PartId:"sites",
                SiteLayout:e.Recipe.Placement.Bindings[0].SiteLayout,Population:new("Complete modules aligned along their source frontage","domain:source",Path:new(sourceSequence,sites==1?40:40d/(sites-1))))]},
            RouteRules=[new("source-walk","source-travel",walk,new(.1,[]),"front","front")]};
        var leaves=new[]{new WorldMetadataArchetype("sprout","detail","Required source-buffer content",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Unchanged detail source","domain:sprout",Box(.15,.15)),Access:new(MetadataOccupancyKind.Solid,[])),
            new WorldMetadataArchetype("gear","equipment","Required work-area equipment",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent",
                PhysicalFootprint:new("meters","Unchanged equipment source","domain:gear",Box(.4,.3)),Access:new(MetadataOccupancyKind.Solid,[]))};
        var catalog=sourceCatalog with{Archetypes=[..sourceCatalog.Archetypes.Select(a=>a.Id=="site"?a with{Parts=[new("plants","sprout",0,"All required buffer members","plants"),new("equipment","gear",2,"All required work equipment")],ChildRequirements=[new("detail",1,16),new("equipment",2,2)]}:a),..leaves]};
        var band=new MetadataPopulationDomainPattern(MetadataPopulationDomainSource.SiteFeature,"GreenBuffer",Bands:new(0,[new("row",plants,0,1.2,.6,.6)]),DirectionEdgeIndex:1);
        var area=new MetadataPopulationDomainPattern(MetadataPopulationDomainSource.GroundMaterial,"work-floor",Area:new(Interior:new(.05,65536,.7)),Profile:profile);
        var request=new WorldPlacementRequest("contents","Every required source-domain member","domain:contents",null,
            [new("plants","Required buffer detail","domain:plants",null,profile),new("equipment","Required work equipment","domain:equipment",null,profile)],[],[],MinimumSeparation:.1,
            Execution:WorldPlacementExecution.Prescribed);
        var program=new MetadataNestedProgram("site-contents","Complete source surroundings program","domain:program",e.Recipe.Id,"site",1,4,request,
            [new("plants","plants",Population:new("Follow the actual buffer","domain:buffer",Facing:MetadataPopulationFacing.Along,Domain:band)),
             new("equipment","equipment",Population:new("Use actual work-floor material","domain:work",Domain:area))],ParentBoundary:MetadataParentBoundary.Reservation);
        return(sourceRecipe with{Parameters=new Dictionary<string,int>{{"site-count",sites},{"plants",plants}},PopulationSource=new(sourceRecipe,sourceCatalog),
            Placement=sourceRecipe.Placement with{Programs=[program]}},catalog);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();WorldMetadataDocument? baseline=null;
        foreach(var (name,sites,plants,rotation) in new[]{("base",2,4,30d),("rotated",2,4,57d),("more",3,6,30d)})
        {
            var e=Example(sites,plants,rotation);
            var prepared=generator.Generate(e.Recipe.PopulationSource!.Recipe,e.Recipe.PopulationSource.Catalog);
            await File.WriteAllTextAsync(Path.Combine(output,name+"-source.json"),Json(prepared));
            var domainProofs=prepared.Nodes.Where(n=>n.ArchetypeId=="site").SelectMany(n=>e.Recipe.Placement!.Programs![0].Bindings.Select(b=>new{n.Id,b.DemandId,
                Domain=MetadataPopulationSources.Domain(b.Population!.Domain!,b.PartId=="plants"?plants:2,prepared,n.Id,new(20000000,default),default)})).ToArray();
            await File.WriteAllTextAsync(Path.Combine(output,name+"-domains.json"),Json(domainProofs));
            WorldMetadataDocument world;
            try{world=generator.Generate(e.Recipe,e.Catalog);}catch(Exception error){Dump(error);throw;}
            var source=world.PlacementPlan!.Definition.SourceWorld!;
            Check(world.Nodes.Count==1+sites*(plants+3)&&world.AccessPlan!.Sites.Count==sites*(plants+3),"all actual sources and required members retained");
            Check(world.PlacementPlan.SiteFeatures!.Count==sites*4&&world.PlacementPlan.ProgramInstances!.Count==sites,"complete surroundings and repeated parent programs");
            Check(world.PlacementPlan.PopulationDomains!.Count==sites*2&&world.PlacementPlan.PopulationDomains.Sum(t=>t.NodeIds.Count)==sites*(plants+2),"every content member has domain provenance");
            Check(Json(source.RoutePlan!.Routes)==Json(world.RoutePlan!.Routes)&&source.Nodes.All(n=>Json(n)==Json(world.Nodes.Single(w=>w.Id==n.Id))),"source nodes and every source journey remain exact");
            foreach(var trace in world.PlacementPlan.PopulationDomains)
            foreach(string id in trace.NodeIds)
            {
                var body=world.AccessPlan!.Sites.Single(s=>s.NodeId==id).Footprint;
                Check(WorldRegionGeometry.Contains(trace.Domain.Shape,body,new(1000000,default)),"every whole body fits selected domain");
            }
            WorldMetadataGenerator.ValidateDocument(world);string dir=Path.Combine(output,name);await generator.PublishAsync(e.Recipe,e.Catalog,world,dir,null,default);
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"world-metadata.json")))==Json(world),"saved source/placement/domain replay");
            if(name=="base")baseline=world;
        }
        await ParentBoundaries(output,generator);
        await SourceEdit(output,generator);
        Refusals(generator,baseline!);
        await Lifecycle(output,generator,baseline!);
        Console.WriteLine("PASS: complete repeated site/material-domain content, source-edge rotation, whole-body containment, exact source journeys, parent-reservation scope, hard open-ground accounting and saved lifecycle.");
    }
    private static async Task SourceEdit(string output,WorldMetadataGenerator generator)
    {
        var e=Example();
        WorldPolygon Rect(double x0,double y0,double x1,double y1)=>new([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]);
        WorldMetadataCatalog Larger(WorldMetadataCatalog catalog)=>catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id!="site"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{
            Polygon=Rect(-3,-7,5,4),Reservation=Rect(-5,-11,6,5)},Access=a.Access! with{Ports=[a.Access.Ports[0] with{BoundaryPosition=new(1,-7),ApproachPosition=new(1,-11)}]}}).ToArray()};
        var source=e.Recipe.PopulationSource!;var recipe=e.Recipe with{PopulationSource=source with{Catalog=Larger(source.Catalog)}};var catalog=Larger(e.Catalog);
        var world=generator.Generate(recipe,catalog);
        Check(world.Nodes.Count==15&&world.PlacementPlan!.SiteFeatures!.Count==8&&world.PlacementPlan.PopulationDomains!.Count==4,"larger source retains every required member and surroundings domain");
        Check(Math.Abs(world.GroundCoverPlan!.Cells.Where(c=>c.MaterialId=="deck").Sum(c=>c.Area)-176)<1e-7,"both actual source bodies grow 80 to 88 square metres");
        await generator.PublishAsync(recipe,catalog,world,Path.Combine(output,"source-edit"),null,default);
    }
    private static void Refusals(WorldMetadataGenerator generator,WorldMetadataDocument baseline)
    {
        var source=baseline.PlacementPlan!.Definition.SourceWorld!;var e=Example();var p=e.Recipe.Placement!.Programs![0].Bindings[0].Population!.Domain!;
        string parent=source.Nodes.First(n=>n.ArchetypeId=="site").Id;
        MetadataPopulationDomainResult Sample(MetadataPopulationDomainPattern pattern,int count=4,long work=20000000,CancellationToken token=default)=>
            MetadataPopulationSources.Domain(pattern,count,source,parent,new(work,token),token);
        Check(Json(Sample(p))==Json(baseline.PlacementPlan.PopulationDomains!.Single(t=>t.Domain.SourceNodeId==parent&&t.Domain.Definition.Source==MetadataPopulationDomainSource.SiteFeature).Domain),"direct shared domain replay");
        Reject(()=>Sample(p with{Selector="MissingFeature"}),"METADATA_POPULATION_DOMAIN_FEATURE_MISSING");
        Reject(()=>Sample(p with{NodeId="missing"}),"METADATA_POPULATION_DOMAIN_OWNER_MISSING");
        Reject(()=>Sample(p with{SourceIndex=1}),"METADATA_POPULATION_DOMAIN_FEATURE_MISSING");
        Reject(()=>Sample(p with{DirectionEdgeIndex=4}),"METADATA_POPULATION_DOMAIN_EDGE_MISSING");
        foreach(var invalid in new[]{p with{Source=(MetadataPopulationDomainSource)42},p with{Selector=""},p with{SourceIndex=-1},p with{DirectionEdgeIndex=64},
            p with{Area=new()},p with{Profile=source.PlacementPlan!.Placement.Request.Demands[0].Profile},p with{Bands=p.Bands! with{DirectionDegrees=double.NaN}}})
            Reject(()=>Sample(invalid),"METADATA_POPULATION_DOMAIN_INVALID");
        var area=e.Recipe.Placement.Programs[0].Bindings[1].Population!.Domain!;
        Reject(()=>Sample(area with{Profile=null}),"METADATA_POPULATION_DOMAIN_INVALID");
        Reject(()=>Sample(area with{Selector="missing"}),"METADATA_POPULATION_DOMAIN_MATERIAL_MISSING");
        Reject(()=>Sample(area with{Selector="work-floor",NodeId=source.Id}),"METADATA_POPULATION_DOMAIN_GROUND_MISSING");
        Reject(()=>Sample(area with{SourceIndex=1}),"METADATA_POPULATION_DOMAIN_INVALID");
        var gap=new MetadataPopulationDomainPattern(MetadataPopulationDomainSource.GroundMaterial,"garden",Bands:new(30,[new("gap",4,0,1.2,.6,.6)]));
        Reject(()=>MetadataPopulationSources.Domain(gap,4,source,null,new(20000000,default),default),"WORLD_REGION_POPULATION_SPAN_MISSING");
        Reject(()=>Sample(p,count:5),"WORLD_REGION_POPULATION_COUNT_MISMATCH");
        Reject(()=>Sample(p,work:1),"WORLD_ROUTE_WORK_BUDGET");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{Sample(p,token:cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        var conflict=Example(rotation:57,alignSource:false);
        Reject(()=>generator.Generate(conflict.Recipe,conflict.Catalog),"METADATA_POPULATION_PASSAGE_BLOCKED");
        var oversized=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="sprout"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(1,1)}}:a).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,oversized),"METADATA_POPULATION_DOMAIN_FOOTPRINT_OUTSIDE");
        var oversizedReservation=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="sprout"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Reservation=Box(1,1)}}:a).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,oversizedReservation),"METADATA_POPULATION_DOMAIN_FOOTPRINT_OUTSIDE");
        var plan=baseline.PlacementPlan;var trace=plan.PopulationDomains![0];
        foreach(var changed in new[]{trace with{Domain=trace.Domain with{SourceGeometrySha256=new string('0',64)}},trace with{Domain=trace.Domain with{SourceIds=["forged"]}},
            trace with{Domain=trace.Domain with{WorldPositions=trace.Domain.WorldPositions.Skip(1).ToArray()}}})
            Reject(()=>new MetadataPlacementPlanner().ValidateDocument(baseline with{PlacementPlan=plan with{PopulationDomains=[changed,..plan.PopulationDomains.Skip(1)]}}),"METADATA_PLACEMENT_PLAN_MISMATCH");
    }
    private static async Task ParentBoundaries(string output,WorldMetadataGenerator generator)
    {
        var e=WorldGroundUseCapacityTests.Example(.999,execution:WorldPlacementExecution.Prescribed);
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="ground"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Reservation=Box(9,9)}}:a).ToArray()};
        var stage=e.Recipe.Placement!.NestedStages![0];
        var outside=stage with{ParentBoundary=MetadataParentBoundary.Reservation,Request=stage.Request with{Anchors=[new("anchor-a",new(-7,0)),new("anchor-b",new(7,0))]}};
        var recipe=e.Recipe with{Placement=e.Recipe.Placement with{NestedStages=[outside]}};
        var world=generator.Generate(recipe,catalog);var accounting=new MetadataPhysicalAccountingPlanner().Measure(world);
        var use=accounting.GroundUses!.Single();
        Check(use.MinimumSatisfied&&Math.Abs(use.OpenFraction-1)<1e-8,"reservation-scoped exterior children do not consume body ground");
        Check(world.Nodes.Count==4&&world.AccessPlan!.Sites.Count==3,"all exact ground fixtures retained");
        await generator.PublishAsync(recipe,catalog,world,Path.Combine(output,"reservation"),null,default);
        Reject(()=>generator.Generate(recipe with{Placement=recipe.Placement! with{NestedStages=[outside with{ParentBoundary=null}]}},catalog),"METADATA_PLACEMENT_UNRESOLVED");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{NestedStages=[stage with{ParentBoundary=MetadataParentBoundary.Reservation}]}},catalog),"METADATA_GROUND_USE_MINIMUM_UNSATISFIED");
        Reject(()=>generator.Generate(recipe,e.Catalog),"METADATA_PLACEMENT_PARENT_RESERVATION_REQUIRED");
        Reject(()=>generator.Generate(recipe with{Placement=recipe.Placement! with{NestedStages=[outside with{ParentBoundary=(MetadataParentBoundary)77}]}},catalog),"METADATA_PLACEMENT_PARENT_BOUNDARY_INVALID");
    }
    private static void Reject(Action action,string code)
    {
        try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}
        throw new Exception("Expected refusal: "+code);
    }
    private static void Dump(Exception error)
    {
        foreach(System.Collections.DictionaryEntry entry in error.Data)Console.Error.WriteLine(entry.Key+": "+Json(entry.Value));
        if(error is AggregateException aggregate){foreach(var inner in aggregate.InnerExceptions)Dump(inner);}
        else if(error.InnerException is {} inner)Dump(inner);
    }
    private static async Task Lifecycle(string output,WorldMetadataGenerator generator,WorldMetadataDocument baseline)
    {
        var e=Example();var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;var more=Example(plants:6);
        var after=session.Apply(new("more-content","Keep every required catalog member","domain:edit",baseline.RecipeSha256,baseline.CatalogSha256,more.Recipe,more.Catalog));
        Check(Json(after.World)==Json(generator.Generate(more.Recipe,more.Catalog)),"count edit equals clean generation");
        var program=more.Recipe.Placement!.Programs![0];var binding=program.Bindings[0];
        var bad=more.Recipe with{Placement=more.Recipe.Placement with{Programs=[program with{Bindings=[binding with{Population=binding.Population! with{
            Domain=binding.Population.Domain! with{Selector="MissingFeature"}}},..program.Bindings.Skip(1)]}]}};
        try{session.Apply(new("reject-domain","Retain accepted content when required source intent is absent","domain:refusal",after.World.RecipeSha256,after.World.CatalogSha256,bad,more.Catalog));throw new Exception("Missing rejected session edit");}
        catch(MetadataRegenerationException error){Check(error.ReasonCode.Contains("METADATA_POPULATION_DOMAIN_FEATURE_MISSING",StringComparison.Ordinal),"exact source failure retained");}
        Check(Json(session.Snapshot)==Json(after),"rejected edit preserves accepted cache, content and history");
        await session.PublishAsync(Path.Combine(output,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"session/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(after),"source-history exact replay");
    }
}

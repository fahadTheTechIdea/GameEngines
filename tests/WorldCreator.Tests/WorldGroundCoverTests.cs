using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

internal static class WorldGroundCoverTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool v,string m){if(!v)throw new Exception("Ground cover: "+m);}
    private static void Near(double a,double b,string m)=>Check(Math.Abs(a-b)<1e-6,m+": "+a+" / "+b);
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Missing "+code);}
    private static MetadataGroundMaterial Material(string id)=>new(id,id,"Game-authored "+id+" surface","ground:"+id);
    private static MetadataGroundRule Rule(string id,MetadataGroundSourceKind source,string selector,string material,int priority)=>new(id,"Purpose of "+id,"ground:"+id,source,selector,material,priority);
    internal static MetadataGroundCoverDefinition Definition()=>new(1,"ground-program","Source-driven game ground","ground:program",
        [Material("natural"),Material("yard"),Material("deck"),Material("path"),Material("garden")],
        [Rule("terrain",MetadataGroundSourceKind.Terrain,"ground","natural",0),Rule("reservation",MetadataGroundSourceKind.Reservation,"site","yard",10),
         Rule("body",MetadataGroundSourceKind.Footprint,"site","deck",30),Rule("arrival",MetadataGroundSourceKind.SiteFeature,"Forecourt","path",20),
         Rule("access",MetadataGroundSourceKind.SiteFeature,"AccessWalkway","path",20),Rule("work",MetadataGroundSourceKind.SiteFeature,"LoadingYard","yard",20),
         Rule("buffer",MetadataGroundSourceKind.SiteFeature,"GreenBuffer","garden",20)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {var e=WorldGenericSurroundingsTests.Example();return(e.Recipe with{GroundCover=Definition()},e.Catalog);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var e=Example();var baseline=generator.Generate(e.Recipe with{GroundCover=null},e.Catalog);
        var world=generator.Generate(e.Recipe,e.Catalog);var cover=world.GroundCoverPlan!;
        Check(Json(world with{GroundCoverPlan=null,GroundCoverId=null,RecipeSha256=baseline.RecipeSha256})==Json(baseline),"cover preserves complete original world, physical geometry and routes");
        Near(cover.DomainArea,10000,"full bounded domain");Near(cover.CoveredArea,10000,"complete material intent");Near(cover.UnresolvedArea,0,"no missing material");
        Near(cover.Cells.Where(c=>c.MaterialId=="deck").Sum(c=>c.Area),80,"actual rotated off-center body area");
        Near(cover.Sources.Single(s=>s.Kind==MetadataGroundSourceKind.Reservation).Boundary.SignedArea(),216,"whole source reservation");
        Check(cover.Sources.Where(s=>s.NodeId is not null).All(s=>s.ParentNodeId==world.Id),"parent ownership retained");
        Check(Json(cover)==Json(new MetadataGroundCoverPlanner().Plan(e.Recipe.GroundCover!,baseline)),"public SDK equals generation stage");
        var reordered=generator.Generate(e.Recipe with{GroundCover=e.Recipe.GroundCover! with{Materials=e.Recipe.GroundCover.Materials.Reverse().ToArray(),Rules=e.Recipe.GroundCover.Rules.Reverse().ToArray()}},e.Catalog);
        Check(Json(cover)==Json(reordered.GroundCoverPlan),"rule/material order canonical");
        WorldMetadataGenerator.ValidateDocument(world);await generator.PublishAsync(e.Recipe,e.Catalog,world,Path.Combine(output,"site"),null,default);
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"site/world-metadata.json")))==Json(world),"saved native replay");
        Reject(()=>new MetadataGroundCoverPlanner().ValidateDocument(world with{GroundCoverPlan=cover with{CoveredArea=1}}),"METADATA_GROUND_COVER_PLAN_MISMATCH");
        Reject(()=>new MetadataGroundCoverPlanner().ValidateDocument(world with{GroundCoverPlan=null}),"METADATA_GROUND_COVER_PLAN_REQUIRED");
        var altered=cover.Sources[0] with{Boundary=new(cover.Sources[0].Boundary.Vertices.Select((p,i)=>i==0?p.Offset(.1,0):p).ToArray())};
        Reject(()=>new MetadataGroundCoverPlanner().ValidateDocument(world with{GroundCoverPlan=cover with{Sources=[altered,..cover.Sources.Skip(1)]}}),"METADATA_GROUND_COVER_PLAN_MISMATCH");
        Reject(()=>generator.Generate(e.Recipe with{GroundCover=Definition() with{Rules=Definition().Rules.Append(Rule("missing",MetadataGroundSourceKind.Footprint,"undeclared","deck",40)).ToArray()}},e.Catalog),"METADATA_GROUND_SOURCE_REQUIRED:missing");
        Reject(()=>generator.Generate(e.Recipe with{GroundCover=Definition() with{Rules=Definition().Rules.Append(Rule("conflict",MetadataGroundSourceKind.Footprint,"site","garden",30)).ToArray()}},e.Catalog),"METADATA_GROUND_MATERIAL_CONFLICT");
        var partialDefinition=Definition() with{Rules=Definition().Rules.Where(r=>r.Source!=MetadataGroundSourceKind.Terrain).ToArray(),RequireComplete=false};
        var partial=generator.Generate(e.Recipe with{GroundCover=partialDefinition},e.Catalog);Near(partial.GroundCoverPlan!.CoveredArea,216,"partial declared site ground");Near(partial.GroundCoverPlan.UnresolvedArea,9784,"unresolved land is retained");
        Reject(()=>generator.Generate(e.Recipe with{GroundCover=partialDefinition with{RequireComplete=true}},e.Catalog),"METADATA_GROUND_COVER_UNRESOLVED");
        await generator.PublishAsync(e.Recipe with{GroundCover=partialDefinition},e.Catalog,partial,Path.Combine(output,"partial"),null,default);
        Reject(()=>generator.Generate(e.Recipe with{GroundCover=Definition() with{MaximumCells=1}},e.Catalog),"METADATA_GROUND_CELL_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{GroundCover=Definition() with{MaximumSources=1}},e.Catalog),"METADATA_GROUND_SOURCE_BUDGET");
        Reject(()=>new MetadataGroundCoverPlanner().PlanCore(Definition() with{MaximumWork=cover.Work-1},baseline,default),"METADATA_GROUND_COVER_WORK_BUDGET");
        Check(new MetadataGroundCoverPlanner().PlanCore(Definition() with{MaximumWork=cover.Work},baseline,default).Work==cover.Work,"exact work budget");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{new MetadataGroundCoverPlanner().PlanCore(Definition(),baseline,cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        await IrregularAsync(output,generator);await CorridorsAsync(output,generator);await AllocationsAsync(output,generator);await RegisteredAsync(output);await SourceEditsAsync(output,generator,e);await RegionalAsync(output);await SessionAsync(output,generator,e,world);
        Console.WriteLine("PASS: complete/partial ground intent, exact source/site/corridor materials, irregular domain/void accounting, nested regional programs, unchanged physical worlds, source edits/replay/refusal, bounds and cancellation.");
    }
    private static async Task IrregularAsync(string output,WorldMetadataGenerator generator)
    {
        var e=WorldMetadataTests.Example("dungeon");var boundary=new WorldPolygon([new(0,0),new(12,0),new(12,5),new(7,5),new(7,10),new(0,10)]);
        var hole=new WorldPolygon([new(2,2),new(4,2),new(4,4),new(2,4)]);
        var surface=new WorldCreator.Core.Spatial.WorldSurfaceDefinition(1,"irregular","meters",boundary,[new("void","Required void","ground:void",hole)],
            [new("plain","ground","Declared terrain","ground:source",boundary,0,new(0,0),0,0,0)]);
        var recipe=e.Recipe with{Space=new(["x","y"],"meters"),Surface=surface,GroundCover=Definition() with{Rules=[Definition().Rules[0]]}};
        var world=generator.Generate(recipe,e.Catalog);Near(world.GroundCoverPlan!.DomainArea,95,"concave domain area");Near(world.GroundCoverPlan.ExcludedArea,4,"void stays excluded");Near(world.GroundCoverPlan.CoveredArea,91,"remaining actual cover");
        await generator.PublishAsync(recipe,e.Catalog,world,Path.Combine(output,"irregular"),null,default);
        var limited=new WorldPolygon([new(0,0),new(7,0),new(7,10),new(0,10)]);
        var gapRecipe=recipe with{Surface=surface with{Patches=[surface.Patches[0] with{Boundary=limited}]},GroundCover=recipe.GroundCover! with{RequireComplete=false}};
        var gap=generator.Generate(gapRecipe,e.Catalog);Near(gap.GroundCoverPlan!.CoveredArea,66,"actual sampled terrain only");Near(gap.GroundCoverPlan.UnresolvedArea,25,"uncovered source terrain remains unresolved");
        Check(gap.GroundCoverPlan.Cells.Any(c=>c.Diagnostic=="surface-Uncovered"),"missing source terrain diagnostic retained");
        Reject(()=>generator.Generate(gapRecipe with{GroundCover=gapRecipe.GroundCover! with{RequireComplete=true}},e.Catalog),"METADATA_GROUND_COVER_UNRESOLVED");
        await generator.PublishAsync(gapRecipe,e.Catalog,gap,Path.Combine(output,"terrain-gap"),null,default);
    }
    private static async Task CorridorsAsync(string output,WorldMetadataGenerator generator)
    {
        var e=WorldCorridorTests.Example();var baseline=generator.Generate(e.Recipe,e.Catalog);string group=baseline.CorridorPlan!.Groups[0].Definition.Id;
        var terrainKinds=e.Recipe.Surface!.Patches.Select(p=>p.Kind).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var definition=Definition() with{Rules=[..terrainKinds.Select((kind,i)=>Rule("terrain-"+i,MetadataGroundSourceKind.Terrain,kind,"natural",0)),Rule("trail",MetadataGroundSourceKind.Corridor,group,"path",20)]};
        var world=generator.Generate(e.Recipe with{GroundCover=definition},e.Catalog);
        Check(world.GroundCoverPlan!.Cells.Any(c=>c.MaterialId=="path")&&world.GroundCoverPlan.Sources.Count(s=>s.Kind==MetadataGroundSourceKind.Corridor)==baseline.CorridorPlan.Groups[0].Segments.Count+baseline.CorridorPlan.Groups[0].Junctions.Count,"every whole realized segment and junction selected");
        Check(Json(world.CorridorPlan)==Json(baseline.CorridorPlan)&&Json(world.RoutePlan)==Json(baseline.RoutePlan),"ground never redesigns shared passages");
        await generator.PublishAsync(e.Recipe with{GroundCover=definition},e.Catalog,world,Path.Combine(output,"trails"),null,default);
    }
    private static async Task RegionalAsync(string output)
    {
        var source=WorldRegionalOwnershipTests.DemandExample();var ground=Definition() with{Rules=[Definition().Rules[0],
            Rule("compound",MetadataGroundSourceKind.Footprint,"compound","yard",10),Rule("module",MetadataGroundSourceKind.Footprint,"module","deck",20)]};
        source=source with{Regions=source.Regions.Select(r=>r with{Recipe=r.Recipe with{GroundCover=ground}}).ToArray()};
        var definition=new MetadataRegionalOwnershipPlanner().Plan(source);var engine=new MetadataRegionalWorldGenerator();var world=engine.Generate(definition);
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==15&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==9,"all nested physical members retained");
        Near(world.Regions.Sum(r=>r.World.GroundCoverPlan!.DomainArea),8000,"entire regional world");Near(world.Regions.Sum(r=>r.World.GroundCoverPlan!.ExcludedArea),288,"original void whole");
        Check(world.Regions.All(r=>r.World.GroundCoverPlan!.UnresolvedArea==0),"every owned region has declared material intent");
        await engine.PublishAsync(world,Path.Combine(output,"regional"));await File.WriteAllTextAsync(Path.Combine(output,"regional-request.json"),Json(source));
    }
    private static async Task AllocationsAsync(string output,WorldMetadataGenerator generator)
    {
        var e=WorldLocalLayoutTests.Example();var cover=Definition() with{Rules=[Definition().Rules[0],Rule("region",MetadataGroundSourceKind.Region,"west","garden",10),
            Rule("local",MetadataGroundSourceKind.Allocation,"west-a","yard",20)]};
        var world=generator.Generate(e.Recipe with{GroundCover=cover},e.Catalog);
        var allocation=world.LocalLayoutPlan!.Allocations.Single(a=>a.Id=="west-a");
        Near(world.GroundCoverPlan!.Cells.Where(c=>c.MaterialId=="yard").Sum(c=>c.Area),allocation.Shape.Area,"exact selected local allocation");
        Check(world.GroundCoverPlan.Sources.Any(s=>s.Kind==MetadataGroundSourceKind.Region)&&world.GroundCoverPlan.Sources.Any(s=>s.Kind==MetadataGroundSourceKind.Allocation),"semantic region and generated allocation sources retained");
        await generator.PublishAsync(e.Recipe with{GroundCover=cover},e.Catalog,world,Path.Combine(output,"allocations"),null,default);
    }
    private static async Task SessionAsync(string output,WorldMetadataGenerator generator,(WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) e,WorldMetadataDocument world)
    {
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var recipe=e.Recipe with{GroundCover=Definition() with{Rules=Definition().Rules.Select(r=>r.Id=="buffer"?r with{MaterialId="natural"}:r).ToArray()}};
        var after=session.Apply(new("material-edit","Revise explicit surface purpose","ground:edit",world.RecipeSha256,world.CatalogSha256,recipe,e.Catalog));
        Check(after.Report.Stages.Single(s=>s.Id=="allocation").Reused&&!after.Report.Stages.Single(s=>s.Id=="ground-cover").Reused,"material edit invalidates cover and preserves placement");
        Check(Json(after.World)==Json(generator.Generate(recipe,e.Catalog)),"edit equals clean generation");
        var bad=recipe with{GroundCover=Definition() with{Rules=Definition().Rules.Where(r=>r.Id!="terrain").ToArray()}};
        try{session.Apply(new("bad-cover","Refuse unresolved material intent","ground:refusal",after.World.RecipeSha256,after.World.CatalogSha256,bad,e.Catalog));throw new Exception("Missing refused edit");}
        catch(MetadataRegenerationException error)when(error.InnerException is ArgumentException cause&&cause.Message=="METADATA_GROUND_COVER_UNRESOLVED"){}
        Check(Json(session.Snapshot)==Json(after),"failed edit preserves accepted snapshot");await session.PublishAsync(Path.Combine(output,"session"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"session/generation-session.json"));Check(Json(restored.Snapshot)==Json(after),"saved history exact replay");
    }
    private static async Task SourceEditsAsync(string output,WorldMetadataGenerator generator,(WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) e)
    {
        WorldPolygon Box(double x0,double y0,double x1,double y1)=>new([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]);
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id!="site"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{
            Polygon=Box(-3,-7,5,4),Reservation=Box(-5,-11,6,5)},Access=a.Access! with{Ports=[a.Access.Ports[0] with{BoundaryPosition=new(1,-7),ApproachPosition=new(1,-11)}]}}).ToArray()};
        var larger=generator.Generate(e.Recipe,catalog);Near(larger.GroundCoverPlan!.Cells.Where(c=>c.MaterialId=="deck").Sum(c=>c.Area),88,"larger actual source changes material geometry");
        Check(larger.Nodes.Count==2&&larger.PlacementPlan!.SiteFeatures!.Count==4,"source edit retains all required members and site features");
        await generator.PublishAsync(e.Recipe,catalog,larger,Path.Combine(output,"source-edit"),null,default);
        var recipe=e.Recipe with{Surface=e.Recipe.Surface! with{Exclusions=[new("protected-void","Keep this source terrain unavailable","ground:void-edit",Box(70,70,80,74))]}};
        var terrain=generator.Generate(recipe,e.Catalog);Near(terrain.GroundCoverPlan!.ExcludedArea,40,"terrain edit retains exact void");Near(terrain.GroundCoverPlan.CoveredArea,9960,"coverage responds to actual terrain");
        Check(terrain.Nodes.Count==2&&terrain.PlacementPlan!.SiteFeatures!.Count==4,"terrain edit preserves complete declared site");
        await generator.PublishAsync(recipe,e.Catalog,terrain,Path.Combine(output,"terrain-edit"),null,default);
    }
    private static async Task RegisteredAsync(string output)
    {
        var e=WorldSurroundingsTests.Example();var definition=Definition() with{Rules=[Definition().Rules[0],Definition().Rules[1],Definition().Rules[2],Definition().Rules[4],
            Rule("registered-garden",MetadataGroundSourceKind.SiteFeature,"Garden","garden",20)]};
        var generator=new WorldMetadataGenerator(TestCatalogs.Registry);var recipe=e.Recipe with{GroundCover=definition};var world=generator.Generate(recipe,e.Catalog);
        Check(world.StructureDefinitions.Count==1&&Json(new MetadataGroundCoverPlanner(TestCatalogs.Registry).Plan(definition,world))==Json(world.GroundCoverPlan),"registered catalog and public source replay use installed registry");
        Near(world.GroundCoverPlan!.Cells.Where(c=>c.MaterialId=="deck").Sum(c=>c.Area),80,"registered cottage source unchanged");
        await generator.PublishAsync(recipe,e.Catalog,world,Path.Combine(output,"registered"),null,default);
    }
}
